using Google.Cloud.Firestore;

namespace Backend.Models
{
    [FirestoreData]
    public class Notification
    {
        [FirestoreDocumentId]
        public string notificationId { get; set; } = "";

        [FirestoreProperty]
        public string userId { get; set; } = "";

        [FirestoreProperty]
        public string title { get; set; } = "";

        [FirestoreProperty]
        public string message { get; set; } = "";

        [FirestoreProperty]
        public string type { get; set; } = "";

        [FirestoreProperty]
        public string postId { get; set; } = "";

        [FirestoreProperty]
        public bool isRead { get; set; } = false;

        [FirestoreProperty]
        public DateTime createdAt { get; set; } = DateTime.UtcNow;
    }
}