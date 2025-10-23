using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Google.Cloud.Firestore;
using StackExchange.Redis;
using Microsoft.AspNetCore.Authentication;
using Google.Cloud.SecretManager.V1;
using cloud_ticket_app.Services;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;

var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Load config (CreateBuilder already loads appsettings.json, but OK to keep)
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// 1) Load your local service-account JSON (no env var needed)
var credential = GoogleCredential.FromFile("gcp-service-account.json")
                                 .CreateScoped("https://www.googleapis.com/auth/cloud-platform");

// 2) Build Google Cloud clients with the SAME credential
var secretClient = new SecretManagerServiceClientBuilder {
    Credential = credential
}.Build();

var storage = StorageClient.Create(credential);

var db = new FirestoreDbBuilder {
    ProjectId = "pftc-459412",
    Credential = credential
}.Build();

builder.Services.AddSingleton(secretClient);
builder.Services.AddSingleton(credential);
builder.Services.AddSingleton(storage);
builder.Services.AddSingleton(db);

// MVC + session
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var host = secretClient.AccessSecretVersion(
        new SecretVersionName("pftc-459412", "redis-url", "latest"))
        .Payload.Data.ToStringUtf8().Trim();

    var portStr = secretClient.AccessSecretVersion(
        new SecretVersionName("pftc-459412", "redis-port", "latest")) // <- this is actually your port
        .Payload.Data.ToStringUtf8().Trim();

    var password = secretClient.AccessSecretVersion(
        new SecretVersionName("pftc-459412", "redis-password", "latest"))
        .Payload.Data.ToStringUtf8().Trim();

    if (!int.TryParse(portStr, out var port)) port = 12154; // safe default

    var cfg = new ConfigurationOptions
    {
        AbortOnConnectFail = false,
        ConnectRetry = 5,
        ConnectTimeout = 10000,
        SyncTimeout = 15000,
        KeepAlive = 30,
        AllowAdmin = true,
        Ssl = false,              // non-TLS as per your working redis-cli form
        User = "default",
        Password = password
    };
    cfg.EndPoints.Add(host, port);

    var mux = ConnectionMultiplexer.Connect(cfg);

    // quick sanity logs
    Console.WriteLine($"[Redis] Connected: {mux.IsConnected}");
    try { Console.WriteLine($"[Redis] Ping: {mux.GetDatabase().Ping().TotalMilliseconds} ms"); }
    catch (Exception ex) { Console.WriteLine($"[Redis] Ping failed: {ex.Message}"); }

    return mux;
});
;

builder.Services.AddScoped<RedisTicketService>();
builder.Services.AddScoped<FirestoreTicketService>();

var oauthSecretName = new SecretVersionName("pftc-459412", "google-oauth-client-secret", "latest");
var clientSecret = secretClient.AccessSecretVersion(oauthSecretName).Payload.Data.ToStringUtf8();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie()
.AddGoogle(options =>
{
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

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ✅ Session must be enabled BEFORE anything that reads context.Session
app.UseSession();

// (Optional) You can also put authentication before your custom middleware
app.UseAuthentication();
app.UseAuthorization();

// Your middleware that reads Session
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
