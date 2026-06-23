namespace Backend.Models
{
    public class OtpCacheEntry
    {
        public string OtpHash { get; set; } =
            string.Empty;

        public DateTime ExpiresAtUtc { get; set; }

        public int FailedAttempts { get; set; }
    }
}