namespace Backend.DTO;

public class FeedbackItemDto
{
    public string FeedbackId { get; set; } = "";
    public string Content { get; set; } = "";
    public string Platform { get; set; } = "";
    public string Status { get; set; } = "open";
    public DateTime? Timestamp { get; set; }
    public List<FeedbackReplyDto> Replies { get; set; } = new();
}
