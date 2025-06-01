using Google.Cloud.Firestore;

namespace cloud_ticket_app.Models
{
    [FirestoreData]
    public class Ticket
    {
        [FirestoreDocumentId]
        public string Id { get; set; }

        [FirestoreProperty("title")]
        public string Title { get; set; }

        [FirestoreProperty("description")]
        public string Description { get; set; }

        [FirestoreProperty("priority")]
        public string Priority { get; set; }

        [FirestoreProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [FirestoreProperty("email")]
        public string Email { get; set; }

        [FirestoreProperty("status")]
        public string Status { get; set; }

        [FirestoreProperty("screenshots")]
        public List<string> Screenshots { get; set; }

        [FirestoreProperty("resolvedBy")]
        public string ResolvedBy { get; set; }

        [FirestoreProperty("resolvedAt")]
        public DateTime ResolvedAt { get; set; }
    }
}
