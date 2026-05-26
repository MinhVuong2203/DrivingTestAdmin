using Backend.Models;

namespace Backend.DTO
{
    public class DrivingCenterSearchResult
    {
        public string message { get; set; } = string.Empty;
        public int total { get; set; }
        public int page { get; set; }
        public int page_size { get; set; }
        public bool has_more { get; set; }
        public List<DrivingCenter> data { get; set; } = new();
    }
}
