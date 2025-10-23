using Google.Cloud.Firestore;
using Google.Cloud.PubSub.V1;
using Google.Cloud.Storage.V1;
using Google.Apis.Auth.OAuth2;
using cloud_ticket_app.Models;
using cloud_ticket_app.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Encoding = System.Text.Encoding;

public class TicketsController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly RedisTicketService _redisService;
    private readonly FirestoreTicketService _firestoreService;
    private readonly StorageClient _storage;
    private readonly FirestoreDb _db;
    private readonly GoogleCredential _cred;

    public TicketsController(
        IConfiguration configuration,
        RedisTicketService redisService,
        FirestoreTicketService firestoreService,
        StorageClient storage,
        FirestoreDb db,
        GoogleCredential cred)
    {
        _configuration = configuration;
        _redisService = redisService;
        _firestoreService = firestoreService;
        _storage = storage;
        _db = db;
        _cred = cred;
    }
    private static readonly Uri CloseTicketFunction =
        new("https://europe-west1-pftc-459412.cloudfunctions.net/closeTicket");

    [HttpGet]
    [Authorize] 
    public IActionResult SubmitTicket() => View();

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ViewTicket(string id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest("Ticket ID is required.");

        // Try Redis first
        var ticket = await _redisService.GetTicketByIdAsync(id);
        if (ticket == null)
        {
            // Fallback to Firestore
            ticket = await _firestoreService.GetTicketByIdAsync(id);
            if (ticket == null) return NotFound();
        }

        // AuthZ: technician or ticket owner only
        var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        var isTech = string.Equals(userRole, "Technician", StringComparison.OrdinalIgnoreCase);
        var isOwner = string.Equals(userEmail, ticket.Email, StringComparison.OrdinalIgnoreCase);

        if (!isTech && !isOwner)
            return Forbid();

        return View(ticket); // requires Views/Tickets/ViewTicket.cshtml
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> SubmitTicket(IFormCollection form, CancellationToken ct)
    {
        try
        {
            Console.WriteLine("---- SubmitTicket Called ----");

            var projectId = "pftc-459412";
            var bucketName = "ticket-screenshots-kc1";

            var title = form["title"];
            var description = form["description"];
            var priority = form["priority"];
            var files = Request.Form.Files.GetFiles("upload");

            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var uploadedFileUrls = new List<string>();
            foreach (var file in files)
            {
                if (file.Length <= 0) continue;

                var objectName = $"{userId}/{Guid.NewGuid()}_{file.FileName}";
                using var stream = file.OpenReadStream();

                await _storage.UploadObjectAsync(
                    bucket: bucketName,
                    objectName: objectName,
                    contentType: file.ContentType,
                    source: stream,
                    cancellationToken: ct);

                var publicUrl = $"https://storage.cloud.google.com/{bucketName}/{objectName}";
                uploadedFileUrls.Add(publicUrl);
            }

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

            // Pub/Sub publisher with injected credentials
            var topicName = TopicName.FromProjectTopic(projectId, "tickets-topic");
            var publisher = await new PublisherClientBuilder
            {
                TopicName = topicName,
                Credential = _cred
            }.BuildAsync(ct);

            var payload = Newtonsoft.Json.JsonConvert.SerializeObject(ticket);
            await publisher.PublishAsync(new PubsubMessage
            {
                Data = Google.Protobuf.ByteString.CopyFromUtf8(payload),
                Attributes = { { "priority", priority } }
            });

            Console.WriteLine("Ticket submitted and published successfully.");
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine("===== ERROR in SubmitTicket =====");
            Console.WriteLine(ex);
            return StatusCode(500, "Internal Server Error: " + ex.Message);
        }
    }

    [HttpPost]
    [Route("Tickets/CloseTicket")]
    [IgnoreAntiforgeryToken] // keep or replace with [ValidateAntiForgeryToken] if you add an anti-forgery header in fetch
    public async Task<IActionResult> CloseTicket([FromForm] string ticketId)
    {

        if (string.IsNullOrWhiteSpace(ticketId))
            return BadRequest("ticketId is required.");

        // Decide who closed it (pull from your auth; fallback if unavailable)
        var resolvedBy =
            User?.Identity?.IsAuthenticated == true
                ? (User.Identity?.Name ?? "Unknown User")
                : "Technician";

        // Build the JSON the Cloud Function expects
        var payloadObj = new
        {
            ticket_id = ticketId,
            resolved_by = resolvedBy
        };
        var json = JsonSerializer.Serialize(payloadObj);
        using var http = new HttpClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync(CloseTicketFunction, content);
        }
        catch (Exception)
        {
            return StatusCode(502, "Failed to reach closeTicket service.");
        }

        var body = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
            return Ok(body); // your JS only checks response.ok

        // Bubble up function error/status for troubleshooting
        return StatusCode((int)response.StatusCode, body);
    }

    [HttpPost("/Tickets/RefreshTicketFunction")]
    [Authorize]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> RefreshTicketFunction(CancellationToken ct)
    {
        const string functionUrl = "https://europe-west1-pftc-459412.cloudfunctions.net/ticketManager";

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
            using var req = new HttpRequestMessage(HttpMethod.Post, functionUrl)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            };

            try
            {
                var oidc = await _cred.GetOidcTokenAsync(
                    OidcTokenOptions.FromTargetAudience(functionUrl), ct);
                var idToken = await oidc.GetAccessTokenAsync(ct); // returns the ID token string
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
            }
            catch (Exception authEx)
            {
                Console.WriteLine($"[RefreshTicketFunction] OIDC token error: {authEx}");
            }

            var resp = await http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                var msg = $"Function error ({(int)resp.StatusCode} {resp.StatusCode}): {body}";
                Console.WriteLine("[RefreshTicketFunction] " + msg);
                return StatusCode((int)resp.StatusCode, msg);
            }

            return Ok(new { refreshed = true, functionResponse = body });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, "Timed out calling ticketManager (25s).");
        }
        catch (HttpRequestException hex)
        {
            return StatusCode(502, $"Network error calling ticketManager: {hex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RefreshTicketFunction] Unexpected: {ex}");
            return StatusCode(500, ex.ToString());
        }
    }

    public async Task<List<string>> GetTechnicianEmailsAsync(CancellationToken ct = default)
    {
        var usersRef = _db.Collection("users");
        var snapshot = await usersRef.WhereEqualTo("role", "Technician").GetSnapshotAsync(ct);

        var emails = new List<string>(snapshot.Count);
        foreach (var doc in snapshot.Documents)
        {
            if (doc.Exists && doc.ContainsField("email"))
                emails.Add(doc.GetValue<string>("email"));
        }
        return emails;
    }
}