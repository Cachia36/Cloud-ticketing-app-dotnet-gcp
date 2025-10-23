using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using cloud_ticket_app.Models;
using StackExchange.Redis;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;

namespace cloud_ticket_app.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IConnectionMultiplexer _redis;
    private readonly FirestoreDb _db; 

    public HomeController(
        ILogger<HomeController> logger,
        IConfiguration configuration,
        IConnectionMultiplexer redis,
        FirestoreDb db)
    {
        _logger = logger;
        _configuration = configuration;
        _redis = redis;
        _db = db;
    }
    public async Task<IActionResult> Index()
    {
        return View();
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignTechnicianRole()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrEmpty(userId)) return Forbid();

        // Optional: short-circuit if already Technician
        if (User.IsInRole("Technician"))
        {
            TempData["Message"] = "You already have the Technician role.";
            return RedirectToAction("Index");
        }

        // Update Firestore
        var userRef = _db.Collection("users").Document(userId);
        await userRef.SetAsync(new { role = "Technician", email }, SetOptions.MergeAll);

        // Update session
        HttpContext.Session.SetString("Role", "Technician");

        // Reissue cookie with updated Role claim
        var claims = User.Claims.Where(c => c.Type != ClaimTypes.Role).ToList();
        claims.Add(new Claim(ClaimTypes.Role, "Technician"));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); // ✅ await

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            });

        TempData["Message"] = "Role updated to Technician.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignUserRole()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrEmpty(userId)) return Forbid();

        // Optional: short-circuit if already Technician
        if (User.IsInRole("User"))
        {
            TempData["Message"] = "You already have the User role.";
            return RedirectToAction("Index");
        }

        // Update Firestore
        var userRef = _db.Collection("users").Document(userId);
        await userRef.SetAsync(new { role = "User", email }, SetOptions.MergeAll);

        // Update session
        HttpContext.Session.SetString("Role", "User");

        // Reissue cookie with updated Role claim
        var claims = User.Claims.Where(c => c.Type != ClaimTypes.Role).ToList();
        claims.Add(new Claim(ClaimTypes.Role, "User"));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            });

        TempData["Message"] = "Role updated to User.";
        return RedirectToAction("Index");
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}