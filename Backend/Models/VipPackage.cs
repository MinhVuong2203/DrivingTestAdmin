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
        public double VipPrice { get; set; }

        [FirestoreProperty("price_inline")]
        public double? PriceInline { get; set; }

        [FirestoreProperty("isPeriod")]
        public bool IsPeriod { get; set; }

        [FirestoreProperty("vip_time")]
        public int? VipTime { get; set; } // Thời gian hiệu lực (ngày), chỉ dùng khi IsPeriod = false

        [FirestoreProperty("descript")]
        public List<string> Descript { get; set; } = new List<string>();

        [FirestoreProperty("is_active")]
        public bool IsActive { get; set; } = true;

        [FirestoreProperty("color_theme")]
        public string ColorTheme { get; set; } = "blue"; // blue, purple, gold, platinum

        [FirestoreProperty("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [FirestoreProperty("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
