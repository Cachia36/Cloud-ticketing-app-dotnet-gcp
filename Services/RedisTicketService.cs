using StackExchange.Redis;
using cloud_ticket_app.Models;
using Newtonsoft.Json;

namespace cloud_ticket_app.Services
{
    public class RedisTicketService
    {
        private readonly IConnectionMultiplexer _redis;

        public RedisTicketService(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public async Task<List<Ticket>> GetTicketsByEmailAsync(string email) 
        {
            var tickets = new List<Ticket>();
            var db = _redis.GetDatabase();
            var server = _redis.GetServer(_redis.GetEndPoints()[0]);

            var ticketKeys = server
                .Keys(db.Database, pattern: "ticket:*", pageSize: 1000, flags: CommandFlags.PreferMaster)
                .Select(k => k.ToString())
                .ToList();

            foreach (var key in ticketKeys)
            {
                var json = await db.StringGetAsync(key);

                if (!json.IsNullOrEmpty)
                {
                    var ticket = JsonConvert.DeserializeObject<Ticket>(json);

                    if (ticket.Email == email)
                    {
                        tickets.Add(ticket);
                    }
                }
            }

            return tickets;
        }

        public async Task<Ticket?> GetTicketByIdAsync(string ticketId)
        {
            var db = _redis.GetDatabase();
            var key = $"ticket:{ticketId}";
            var json = await db.StringGetAsync(key);

            if (json.IsNullOrEmpty) return null;

            return JsonConvert.DeserializeObject<Ticket>(json);
        }
    }
}
