using Google.Cloud.Firestore;

namespace Backend.Models
{
    [FirestoreData]
    public class ModerationKeyword
    {
        [FirestoreDocumentId]
        public string keywordId { get; set; } = "";

        [FirestoreProperty]
        public string keyword { get; set; } = "";

        [FirestoreProperty]
        public bool isActive { get; set; } = true;

        [FirestoreProperty]
        public DateTime createdAt { get; set; }

        [FirestoreProperty]
        public DateTime updatedAt { get; set; }
    }
}