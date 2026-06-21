namespace Backend.DTO;

public class FeedbackSpamRequest
{
    public bool IsSpam { get; set; }
    public string StatusAfter { get; set; } = "open";
}
