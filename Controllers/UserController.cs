using cloud_ticket_app.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using StackExchange.Redis;
using Google.Cloud.Firestore;
using cloud_ticket_app.Services;

namespace cloud_ticket_app.Controllers
{
    [Authorize(Roles = "User")]
    public class UserController : Controller
    {
        private readonly RedisTicketService _redisService;
        private readonly FirestoreTicketService _firestoreService;

        public UserController(RedisTicketService redisService, FirestoreTicketService firestoreService)
        {
            _redisService = redisService;
            _firestoreService = firestoreService;
        }

        public async Task<IActionResult> UserDashboard()
        {
            string email = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;

            var redisTickets = await _redisService.GetTicketsByEmailAsync(email);
            var firestoreTickets = await _firestoreService.GetTicketsByEmailAsync(email);

            foreach (var ticket in firestoreTickets)
            {
                Console.WriteLine($"Ticket Title: {ticket.Title}, CreatedAt: {ticket.CreatedAt}, Status: {ticket.Status}");
            }

            var allTickets = redisTickets.Concat(firestoreTickets)
                .OrderBy(t => t.Status == "Closed" ? 1 : 0)
                .ThenBy(t => GetPriorityOrder(t.Priority))
                .ThenByDescending(t => t.CreatedAt)
                .ToList();

            ViewBag.Tickets = allTickets;
            return View();
        }

        private int GetPriorityOrder(string priority)
        {
            return priority switch
            {
                "High" => 0,
                "Medium" => 1,
                "Low" => 2,
                _ => 3
            };
        }
    }
}
