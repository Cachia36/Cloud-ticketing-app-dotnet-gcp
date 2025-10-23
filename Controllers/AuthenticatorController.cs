using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Google.Cloud.Firestore;
using System.Security.Claims;

namespace cloud_ticket_app.Controllers
{
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class AuthenticatorController : Controller
    {
        private readonly FirestoreDb _db;

        // ⬅️ Inject the FirestoreDb built in Program.cs (with your service account)
        public AuthenticatorController(FirestoreDb db)
        {
            _db = db;
        }

        public IActionResult Index() => View();

        [AllowAnonymous]
        public IActionResult Login()
        {
            var props = new AuthenticationProperties
            {
                RedirectUri = Url.Action(nameof(LoginCallback)),
                Items = { { "prompt", "select_account" } }
            };
            return Challenge(props, GoogleDefaults.AuthenticationScheme);
        }

        // ⬅️ AllowAnonymous: the Google middleware sets the user on this request
        [AllowAnonymous]
        public async Task<IActionResult> LoginCallback()
        {
            // After Google returns, the user principal should be populated
            var email = User.FindFirstValue(ClaimTypes.Email);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(userId))
                return RedirectToAction("Index", "Home");

            var userRef = _db.Collection("users").Document(userId);
            var snap = await userRef.GetSnapshotAsync();

            var role = "User";
            if (!snap.Exists)
            {
                await userRef.SetAsync(new { role, email });
            }
            else if (snap.TryGetValue("role", out string firestoreRole) && !string.IsNullOrWhiteSpace(firestoreRole))
            {
                role = firestoreRole;
            }

            // Save role to session BEFORE redirecting anywhere that depends on it
            HttpContext.Session.SetString("Role", role);

            // Build app cookie with your claims (email/id/role)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }
    }
}