using System.Text.Json.Serialization;

namespace Backend.Models
{
    public class TrafficViolation
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("vehicle_types")]
        public List<string> VehicleTypes { get; set; } = new();

        [JsonPropertyName("subject_text")]
        public string SubjectText { get; set; } = string.Empty;

        [JsonPropertyName("penalty_text")]
        public string PenaltyText { get; set; } = string.Empty;

        [JsonPropertyName("penalty_legal_basis")]
        public string PenaltyLegalBasis { get; set; } = string.Empty;

        [JsonPropertyName("additional_penalty_text")]
        public string AdditionalPenaltyText { get; set; } = string.Empty;

        [JsonPropertyName("additional_penalty_legal_basis")]
        public string AdditionalPenaltyLegalBasis { get; set; } = string.Empty;

        [JsonPropertyName("fine_min")]
        public int FineMin { get; set; }

        [JsonPropertyName("fine_max")]
        public int FineMax { get; set; }

        [JsonPropertyName("aliases")]
        public List<string> Aliases { get; set; } = new();

        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; set; } = new();

        [JsonPropertyName("search_text")]
        public string SearchText { get; set; } = string.Empty;

        [JsonPropertyName("related_violation_ids")]
        public List<string> RelatedViolationIds { get; set; } = new();

        [JsonPropertyName("status")]
        public string Status { get; set; } = "active";
    }
}
