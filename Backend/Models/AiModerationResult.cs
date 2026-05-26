namespace Backend.Models
{
    public class AiModerationResult
    {
        public bool violated { get; set; }

        public string reason { get; set; } = "";

        public string source { get; set; } = "ai";

        public string rawResponse { get; set; } = "";
    }
}