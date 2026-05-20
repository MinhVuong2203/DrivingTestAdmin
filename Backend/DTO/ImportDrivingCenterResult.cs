namespace Backend.DTO
{
    public class ImportDrivingCenterResult
    {    
        public string message { get; set; } = string.Empty;
        public int total_from_api { get; set; }
        public int saved_count { get; set; }
    }
}
