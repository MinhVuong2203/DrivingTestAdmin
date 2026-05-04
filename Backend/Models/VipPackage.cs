using Google.Cloud.Firestore;

namespace Backend.Models
{
    [FirestoreData]
    public class VipPackage
    {
        [FirestoreProperty("id")]
        public string? Id { get; set; }

        [FirestoreProperty("vip_name")]
        public string VipName { get; set; } = string.Empty;

        [FirestoreProperty("vip_price")]
        public decimal VipPrice { get; set; }

        [FirestoreProperty("vip_time")]
        public int VipTime { get; set; } // Thời gian hiệu lực (ngày)

        [FirestoreProperty("descript")]
        public string Description { get; set; } = string.Empty;

        [FirestoreProperty("features")]
        public List<string> Features { get; set; } = new List<string>();

        [FirestoreProperty("is_active")]
        public bool IsActive { get; set; } = true;

        [FirestoreProperty("sort_order")]
        public int SortOrder { get; set; } = 0;

        [FirestoreProperty("color_theme")]
        public string ColorTheme { get; set; } = "blue"; // blue, purple, gold, platinum

        [FirestoreProperty("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [FirestoreProperty("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}