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
        public IActionResult Index()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult Login()
        {
            var properties = new AuthenticationProperties
            { 
                RedirectUri = Url.Action("LoginCallback"),
                Items =
                {
                    { "prompt", "select_account" }
                }
            };

            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [Authorize]
        public async Task<IActionResult> LoginCallback()
        {

            string email = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
            string userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Index", "Home");
            }

            var db = FirestoreDb.Create("pftc-459412");
            var userDoc = await db.Collection("users").Document(userId).GetSnapshotAsync();

            string role = "User";

            if (!userDoc.Exists)
            {
                await db.Collection("users").Document(userId).SetAsync(new { role, email });
            }
            else if (userDoc.TryGetValue("role", out string firestoreRole))
            {
                role = firestoreRole;
            }
            //Save role to session
            HttpContext.Session.SetString("Role", role);

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
