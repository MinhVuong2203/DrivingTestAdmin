namespace Backend.DTO;

public class FeedbackReplyRequest
{
    public string ReplyText { get; set; } = "";
    public string Source { get; set; } = "manual";
    public string ReplyAuthor { get; set; } = "Quản trị viên";
}
