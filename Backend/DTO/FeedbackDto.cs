namespace Backend.DTO;

public class FeedbackSubmitRequest
{
    public string Content { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? Platform { get; set; }
}

public class FeedbackAiReplyRequest
{
    public string Content { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? Platform { get; set; }
}

public class FeedbackAiReplyResponse
{
    public string ReplyText { get; set; } = "";
    public bool SpamRisk { get; set; }
    public string SpamReason { get; set; } = "";
    public string Source { get; set; } = "ai";
}

public class FeedbackReplyRequest
{
    public string ReplyText { get; set; } = "";
    public string? ReplyAuthor { get; set; }
    public string? Source { get; set; }
}

public class FeedbackSpamRequest
{
    public bool IsSpam { get; set; }
    public string? StatusAfter { get; set; }
}

public class FeedbackReplyDto
{
    public string Id { get; set; } = "";
    public string Content { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string Source { get; set; } = "";
    public DateTime? CreatedAt { get; set; }
}

public class FeedbackItemDto
{
    public string FeedbackId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Content { get; set; } = "";
    public string Platform { get; set; } = "";
    public string Status { get; set; } = "open";
    public DateTime? Timestamp { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<FeedbackReplyDto> Replies { get; set; } = [];
    public string ReplyText { get; set; } = "";
    public string ReplyAuthor { get; set; } = "";
    public string ReplySource { get; set; } = "";
    public DateTime? RepliedAt { get; set; }
    public string AiSuggestedReply { get; set; } = "";
    public DateTime? AiGeneratedAt { get; set; }
    public bool SpamRisk { get; set; }
    public string SpamReason { get; set; } = "";
}
