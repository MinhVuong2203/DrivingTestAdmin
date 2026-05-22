using Google.Cloud.Firestore;

namespace Backend.Models
{
    [FirestoreData]
    public class PaymentOrder
    {
        [FirestoreProperty("id")]
        public string Id { get; set; } = string.Empty;

        [FirestoreProperty("order_code")]
        public long OrderCode { get; set; }

        [FirestoreProperty("payment_link_id")]
        public string PaymentLinkId { get; set; } = string.Empty;

        [FirestoreProperty("checkout_url")]
        public string CheckoutUrl { get; set; } = string.Empty;

        [FirestoreProperty("user_id")]
        public string UserId { get; set; } = string.Empty;

        [FirestoreProperty("package_id")]
        public string PackageId { get; set; } = string.Empty;

        [FirestoreProperty("package_name")]
        public string PackageName { get; set; } = string.Empty;

        [FirestoreProperty("amount")]
        public int Amount { get; set; }

        [FirestoreProperty("status")]
        public string Status { get; set; } = "PENDING";

        [FirestoreProperty("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [FirestoreProperty("paid_at")]
        public DateTime? PaidAt { get; set; }
    }
}
