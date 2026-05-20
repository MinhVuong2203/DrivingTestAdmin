using Google.Cloud.Firestore;

namespace Backend.Models
{
    [FirestoreData]
    public class DrivingCenter
    {
        [FirestoreDocumentId]
        public string? id { get; set; }

        [FirestoreProperty]
        public string name { get; set; } = string.Empty;

        [FirestoreProperty]
        public string phone_number { get; set; } = string.Empty;

        [FirestoreProperty]
        public string photo_url { get; set; } = string.Empty;

        [FirestoreProperty]
        public string website { get; set; } = string.Empty;

        [FirestoreProperty]
        public double rating { get; set; }

        [FirestoreProperty]
        public int review_count { get; set; }

        [FirestoreProperty]
        public string business_status { get; set; } = string.Empty;

        [FirestoreProperty]
        public string address { get; set; } = string.Empty;

        [FirestoreProperty]
        public string district { get; set; } = string.Empty;

        [FirestoreProperty]
        public string city { get; set; } = string.Empty;

        [FirestoreProperty]
        public string opening_status { get; set; } = string.Empty;

        [FirestoreProperty]
        public string search_query { get; set; } = string.Empty;

        [FirestoreProperty]
        public DateTime created_at { get; set; } = DateTime.UtcNow;

        [FirestoreProperty]
        public DateTime updated_at { get; set; } = DateTime.UtcNow;
    }
}
