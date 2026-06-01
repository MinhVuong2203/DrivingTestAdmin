namespace Backend.Models
{
    public class AdMobReportResponse
    {
        public string Date { get; set; } = "";
        public double EstimatedEarnings { get; set; }
        public long Impressions { get; set; }
        public long Clicks { get; set; }
        public double Ecpm { get; set; }
    }
}