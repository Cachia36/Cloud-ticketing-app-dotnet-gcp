using System;
using System.IO;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using StackExchange.Redis;

using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Google.Cloud.SecretManager.V1;
using Google.Cloud.Storage.V1;

using cloud_ticket_app.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Networking: listen on Cloud Run's PORT (defaults to 8080) ---

var isCloudRun = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("K_SERVICE"));
if (isCloudRun)
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// (Optional) appsettings.json is already loaded by CreateBuilder; fine to keep:
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// ---------- Google auth (local file in dev; ADC on Cloud Run) ----------
var projectId = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT")
              ?? builder.Configuration["Gcp:ProjectId"]   // set for local dev if you want
              ?? "pftc-459412";

GoogleCredential credential;

if (!isCloudRun && File.Exists("gcp-service-account.json"))
{
    credential = GoogleCredential.FromFile("gcp-service-account.json")
                                 .CreateScoped("https://www.googleapis.com/auth/cloud-platform");
}
else
{
    credential = GoogleCredential.GetApplicationDefault()
                                 .CreateScoped("https://www.googleapis.com/auth/cloud-platform");
}

// ---------- Google Cloud clients (Secret Manager, Storage, Firestore) ----------
var secretClient = new SecretManagerServiceClientBuilder { Credential = credential }.Build();
var storage = StorageClient.Create(credential);
var db = new FirestoreDbBuilder { ProjectId = projectId, Credential = credential }.Build();

builder.Services.AddSingleton(secretClient);
builder.Services.AddSingleton(credential);
builder.Services.AddSingleton(storage);
builder.Services.AddSingleton(db);

// ---------- MVC + Session ----------
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();

// ---------- Redis (host/port/password from Secret Manager) ----------
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    // These secrets must exist (give your Cloud Run SA 'roles/secretmanager.secretAccessor')
    var host = secretClient.AccessSecretVersion(
        new SecretVersionName(projectId, "redis-url", "latest"))
        .Payload.Data.ToStringUtf8().Trim();

    var portStr = secretClient.AccessSecretVersion(
        new SecretVersionName(projectId, "redis-port", "latest"))
        .Payload.Data.ToStringUtf8().Trim();

    var password = secretClient.AccessSecretVersion(
        new SecretVersionName(projectId, "redis-password", "latest"))
        .Payload.Data.ToStringUtf8().Trim();

    if (!int.TryParse(portStr, out var redisPort)) redisPort = 12154; // fallback if secret missing

    var cfg = new ConfigurationOptions
    {
        AbortOnConnectFail = false,
        ConnectRetry = 5,
        ConnectTimeout = 10000,
        SyncTimeout = 15000,
        KeepAlive = 30,
        AllowAdmin = true,
        Ssl = false, // set true only if your Redis requires TLS
        User = "default",
        Password = password
    };
    cfg.EndPoints.Add(host, redisPort);

    var mux = ConnectionMultiplexer.Connect(cfg);

    // Simple diagnostics
    Console.WriteLine($"[Redis] Connected: {mux.IsConnected}");
    try { Console.WriteLine($"[Redis] Ping: {mux.GetDatabase().Ping().TotalMilliseconds} ms"); }
    catch (Exception ex) { Console.WriteLine($"[Redis] Ping failed: {ex.Message}"); }

    return mux;
});

// ---------- Your app services ----------
builder.Services.AddScoped<RedisTicketService>();
builder.Services.AddScoped<FirestoreTicketService>();

// ---------- Authentication (Cookies + Google). ClientSecret from Secret Manager ----------
var oauthSecretName = new SecretVersionName(projectId, "google-oauth-client-secret", "latest");
var clientSecret = secretClient.AccessSecretVersion(oauthSecretName).Payload.Data.ToStringUtf8();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie()
.AddGoogle(options =>
{
    // ClientId from appsettings (local), ClientSecret from Secret Manager
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
    options.ClientSecret = clientSecret;
    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Cloud Run terminates TLS at the proxy; HTTP inside the container is fine.
// Keeping this is okay; it may log a benign warning about HTTPS port.
app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();

// Session must be enabled BEFORE anything that reads context.Session
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// Middleware that checks Session 'Role' after auth
app.Use(async (context, next) =>
{
    var path = context.Request.Path;

    // Ignore Google OAuth plumbing and login endpoints
    if (path.StartsWithSegments("/signin-google") ||
        path.StartsWithSegments("/Authenticator") ||
        path.StartsWithSegments("/Account/Login") ||
        path.StartsWithSegments("/Home/Error"))
    {
        await next();
        return;
    }

    if (context.User.Identity?.IsAuthenticated == true &&
        string.IsNullOrEmpty(context.Session.GetString("Role")))
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        context.Response.Redirect("/Authenticator/Login");
        return;
    }

    await next();
});

// Helpful startup log (shows bound URLs, e.g., http://0.0.0.0:8080 on Cloud Run)
app.Logger.LogInformation("Starting on: {Urls}", string.Join(", ", app.Urls));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();