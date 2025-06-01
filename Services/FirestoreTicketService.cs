using cloud_ticket_app.Controllers;
using cloud_ticket_app.Models;
using Google.Cloud.Firestore;

namespace cloud_ticket_app.Services
{
    public class FirestoreTicketService
    {
        private readonly FirestoreDb _firestoreDb;

        public FirestoreTicketService()
        {
            _firestoreDb = FirestoreDb.Create("pftc-459412");
        }

        public async Task<List<Ticket>> GetTicketsByEmailAsync(string userEmail)
        {
            CollectionReference ticketCollection = _firestoreDb.Collection("ticketsArchive");

            //Get tickets for specific user
            Query query = ticketCollection.WhereEqualTo("email", userEmail);
            QuerySnapshot snapshot = await query.GetSnapshotAsync();

            List<Ticket> tickets = new List<Ticket>();
            foreach (DocumentSnapshot doc in snapshot.Documents)
            {
                if (doc.Exists)
                {
                    Ticket ticket = doc.ConvertTo<Ticket>();
                    ticket.Id = doc.Id;
                    tickets.Add(ticket);
                }
            }

            return tickets;
        }

        public async Task<Ticket?> GetTicketByIdAsync(string ticketId)
        {
            DocumentReference docRef = _firestoreDb.Collection("ticketsArchive").Document(ticketId);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

            if (snapshot.Exists)
            {
                var ticket = snapshot.ConvertTo<Ticket>();
                ticket.Id = snapshot.Id;
                return ticket;
            }

            return null;
        }
    }
}
