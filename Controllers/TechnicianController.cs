using cloud_ticket_app.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Data;

namespace cloud_ticket_app.Controllers
{
    [Authorize(Roles = "Technician")]
    public class TechnicianController : Controller
    {
        private readonly IConnectionMultiplexer _redis;

        public TechnicianController(IConnectionMultiplexer redis) 
        {
            _redis = redis;
        }
        public async Task<IActionResult> TechnicianDashboard()
        {
            string email = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
            var tickets = new List<Ticket>();

            var db = _redis.GetDatabase();
            var server = _redis.GetServer(_redis.GetEndPoints()[0]);
            var ticketKeys = server
                .Keys(db.Database, pattern: "ticket:*", pageSize: 1000, flags: CommandFlags.PreferMaster)
                .Select(k => k.ToString())
                .ToList();

            
                foreach (var key in ticketKeys)
                {
                    var json = await db.StringGetAsync(key.ToString());

                    if (!json.IsNullOrEmpty)
                    {
                        var ticket = JsonConvert.DeserializeObject<Ticket>(json);

                        if (ticket.Status != "Closed" || (DateTime.UtcNow - ticket.CreatedAt).TotalDays <= 7)
                        {
                            tickets.Add(ticket);
                        }
                    }
                }

            tickets = tickets
                .Where(t => (DateTime.UtcNow - t.CreatedAt).TotalDays <= 7)
                .OrderBy(t => t.Status == "Closed" ? 1 : 0)
                .ThenBy(t => t.Priority == "High" ? 0 : t.Priority == "Medium" ? 1 : 2)
                .ThenByDescending(t => t.CreatedAt)
                .ToList();

            ViewBag.Tickets = tickets;
            return View();
        }

    }
}
