using cloud_ticket_app.Models;
using Google.Cloud.Firestore;

namespace cloud_ticket_app.Services
{
    public class FirestoreTicketService
    {
        private readonly FirestoreDb _firestoreDb;

        // ✅ DI constructor: FirestoreDb is built in Program.cs with your service account
        public FirestoreTicketService(FirestoreDb firestoreDb)
        {
            _firestoreDb = firestoreDb;
        }

        public async Task<List<Ticket>> GetTicketsByEmailAsync(string userEmail, CancellationToken ct = default)
        {
            var ticketCollection = _firestoreDb.Collection("ticketsArchive");
            var query = ticketCollection.WhereEqualTo("email", userEmail);
            var snapshot = await query.GetSnapshotAsync(ct);

            var tickets = new List<Ticket>(snapshot.Count);
            foreach (var doc in snapshot.Documents)
            {
                if (!doc.Exists) continue;
                var ticket = doc.ConvertTo<Ticket>();
                ticket.Id = doc.Id;
                tickets.Add(ticket);
            }
            return tickets;
        }

        public async Task<Ticket?> GetTicketByIdAsync(string ticketId, CancellationToken ct = default)
        {
            var docRef = _firestoreDb.Collection("ticketsArchive").Document(ticketId);
            var snapshot = await docRef.GetSnapshotAsync(ct);
            if (!snapshot.Exists) return null;

            var ticket = snapshot.ConvertTo<Ticket>();
            ticket.Id = snapshot.Id;
            return ticket;
        }
    }
}