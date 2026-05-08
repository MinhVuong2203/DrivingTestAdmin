namespace Frontend.Models
{
    public class VipPackage
    {
        public string? Id { get; set; }
        public string VipName { get; set; } = string.Empty;
        public double VipPrice { get; set; }
        public int VipTime { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<string> Features { get; set; } = new List<string>();
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;
        public string ColorTheme { get; set; } = "blue";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
