namespace Backend.DTO;

public class FeedbackReplyDto
{
    public string Id { get; set; } = "";
    public string Content { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string Source { get; set; } = "";
    public DateTime? CreatedAt { get; set; }
}
