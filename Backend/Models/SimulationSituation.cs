using System.Text.Json.Serialization;

namespace Backend.Models
{
    public class SimulationSituation
    {
        [JsonPropertyName("docId")]
        public string DocId { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("chapter")]
        public int Chapter { get; set; }

        [JsonPropertyName("duration")]
        public int Duration { get; set; }

        [JsonPropertyName("videoUrl")]
        public string VideoUrl { get; set; } = string.Empty;

        [JsonPropertyName("scoreWindows")]
        public List<SimulationScoreWindow> ScoreWindows { get; set; } = new();

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;
    }

    public class SimulationScoreWindow
    {
        [JsonPropertyName("from")]
        public double From { get; set; }

        [JsonPropertyName("to")]
        public double To { get; set; }

        [JsonPropertyName("score")]
        public int Score { get; set; }
    }
}
