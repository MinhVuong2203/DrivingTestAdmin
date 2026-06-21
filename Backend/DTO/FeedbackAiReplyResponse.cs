namespace Backend.DTO;

public class FeedbackAiReplyResponse
{
    public string ReplyText { get; set; } = "";
    public bool SpamRisk { get; set; }
    public string SpamReason { get; set; } = "";
    public string Source { get; set; } = "ai";
}
