using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Google.Cloud.Firestore;
using StackExchange.Redis;
using Microsoft.AspNetCore.Authentication;
using Google.Cloud.SecretManager.V1;
using cloud_ticket_app.Services;

var builder = WebApplication.CreateBuilder(args);
var client = SecretManagerServiceClient.Create();

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "gcp-service-account.json");
FirestoreDb db = FirestoreDb.Create("pftc-459412");

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession();

var redisSecret = new SecretVersionName("pftc-459412", "redis-url", "latest");

var redisUrlResult = client.AccessSecretVersion(redisSecret);
string redisUrl = redisUrlResult.Payload.Data.ToStringUtf8();

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var redisConnectionString = redisUrl;

    var options = ConfigurationOptions.Parse(redisConnectionString);
    options.AllowAdmin = true;
    options.AbortOnConnectFail = false;
    options.ConnectRetry = 5;

    return ConnectionMultiplexer.Connect(options);
});
builder.Services.AddScoped<RedisTicketService>();
builder.Services.AddScoped<FirestoreTicketService>();

var secretName = new SecretVersionName("pftc-459412", "google-oauth-client-secret", "latest");

var result = client.AccessSecretVersion(secretName);
string clientSecret = result.Payload.Data.ToStringUtf8();

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
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");

    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.Use(async (context, next) =>
{
    if (context.User.Identity.IsAuthenticated &&
        string.IsNullOrEmpty(context.Session.GetString("Role")))
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        context.Response.Redirect("/Authenticator/Login"); 
        return;
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();