namespace Backend.DTO;

public class FeedbackSubmitRequest
{
    public string Content { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Platform { get; set; } = "";
}
