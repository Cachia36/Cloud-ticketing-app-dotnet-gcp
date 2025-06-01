using Microsoft.AspNetCore.Mvc;
using Google.Cloud.PubSub.V1;
using Google.Cloud.Storage.V1;
using Google.Protobuf;
using cloud_ticket_app.Models;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.FileSystemGlobbing.Internal.Patterns;
using System;
using Google.Apis.Storage.v1.Data;
using Google.Cloud.Firestore.V1;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using cloud_ticket_app.Services;

namespace cloud_ticket_app.Controllers
{
    public class TicketsController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly RedisTicketService _redisService;
        private readonly FirestoreTicketService _firestoreService;
        public TicketsController(IConfiguration configuration, RedisTicketService redisService, FirestoreTicketService firestoreService)
        {
            _configuration = configuration;
            _redisService = redisService;
            _firestoreService = firestoreService;
        }

        public IActionResult SubmitTicket()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SubmitTicket(IFormCollection form)
        {
            try
            {
                Console.WriteLine("---- SubmitTicket Called ----");
                string projectId = "pftc-459412";

                string title = form["title"];
                string description = form["description"];
                string priority = form["priority"];

                Console.WriteLine($"Title: {title}, Description: {description}, Priority: {priority}");

                var files = Request.Form.Files.GetFiles("upload");
                Console.WriteLine($"Files count: {files.Count}");

                string userEmail = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
                string userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                Console.WriteLine($"UserEmail: {userEmail}, UserId: {userId}");

                // Verify bucket
                string bucketName = "ticket-screenshots-kc1";
                var storageClient = await StorageClient.CreateAsync();

                var uploadedFileUrls = new List<string>();

                foreach (var file in files)
                {
                    Console.WriteLine($"Processing file: {file.FileName}, Size: {file.Length}");

                    using var stream = file.OpenReadStream();
                    string objectName = $"{userId}/{Guid.NewGuid()}_{file.FileName}";
                    Console.WriteLine($"Uploading to GCS as: {objectName}");

                    await storageClient.UploadObjectAsync(bucketName, objectName, file.ContentType, stream, options: new UploadObjectOptions { UserProject = projectId });
                    var obj = await storageClient.GetObjectAsync(bucketName, objectName);

                    var publicURL = $"https://storage.cloud.google.com/{bucketName}/{objectName}";
                    uploadedFileUrls.Add(publicURL);
                }

                Console.WriteLine("Files uploaded, preparing Pub/Sub...");

                var ticket = new Ticket
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = title,
                    Description = description,
                    Priority = priority,
                    Email = userEmail,
                    Status = "Queued",
                    Screenshots = uploadedFileUrls,
                    CreatedAt = DateTime.UtcNow
                };

                var jsonPayload = JsonConvert.SerializeObject(ticket);
                var topicName = TopicName.Parse("projects/pftc-459412/topics/tickets-topic");
                var publisher = await PublisherClient.CreateAsync(topicName);

                var message = new PubsubMessage
                {
                    Data = ByteString.CopyFromUtf8(jsonPayload),
                    Attributes = { { "priority", priority } }
                };

                Console.WriteLine("Publishing message...");
                await publisher.PublishAsync(message);

                Console.WriteLine("Ticket submitted successfully.");

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine("===== ERROR in SubmitTicket =====");
                Console.WriteLine(ex.ToString());  // This will print the exact stack trace
                return StatusCode(500, "Internal Server Error: " + ex.Message);
            }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> CloseTicket([FromForm] string ticketId)
        {
            Console.WriteLine("CloseTicket method called");

            string resolvedBy = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;

            if (string.IsNullOrEmpty(resolvedBy) || string.IsNullOrEmpty(ticketId))
            {
                return BadRequest("Missing resolvedBy or ticketId.");
            }

            var functionUrl = "https://europe-west1-pftc-459412.cloudfunctions.net/closeTicket";

            var payload = new
            {
                ticket_id = ticketId,
                resolved_by = resolvedBy
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            using var httpClient = new HttpClient();

            try
            {
                var response = await httpClient.PostAsync(functionUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseData = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("Ticket successfully closed and archived:");
                    Console.WriteLine(responseData);
                    return Ok("Ticket successfully closed.");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Failed to close ticket: {error}");
                    return StatusCode((int)response.StatusCode, error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occurred while calling function:");
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Failed to call close function.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> RefreshTicketFunction()
        {
            var functionUrl = "https://europe-west1-pftc-459412.cloudfunctions.net/ticketManager";
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.PostAsync(
                    functionUrl,
                    new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
                );

                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, $"Function error: {body}");
                }

            }

            return RedirectToAction("Index", "Home");
        }

        public async Task<List<string>> GetTechnicianEmailsAsync()
        {
            string projectId = "pftc-459412";
            FirestoreDb db = FirestoreDb.Create(projectId);
            CollectionReference usersRef = db.Collection("users");
            Query query = usersRef.WhereEqualTo("role", "Technician");
            QuerySnapshot snapshot = await query.GetSnapshotAsync();

            List<string> technicianEmails = new List<string>();

            foreach (DocumentSnapshot doc in snapshot.Documents)
            {
                if (doc.Exists && doc.ContainsField("email"))
                {
                    technicianEmails.Add(doc.GetValue<string>("email"));
                }
            }

            return technicianEmails;
        }
        [Authorize]
        public async Task<IActionResult> ViewTicket(string id)
        {
            string userEmail = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(id))
                return BadRequest("Ticket ID is required.");

            var ticket = await _redisService.GetTicketByIdAsync(id);

            if (ticket == null)
            {
                ticket = await _firestoreService.GetTicketByIdAsync(id); 
            }

            if (ticket == null)
                return NotFound();

            bool isTechnician = userRole == "Technician";
            bool isOwner = userEmail == ticket.Email;

            if (!isTechnician && !isOwner)
                return Forbid();

            return View(ticket);
        }


    }
}