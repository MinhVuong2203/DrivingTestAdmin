using Backend.DTO;
using Backend.Filters;
using Backend.Service.Interface;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FeedbackController : ControllerBase
    {
        private static readonly TimeSpan SpamWindow = TimeSpan.FromMinutes(30);
        private const int MaxFeedbacksInSpamWindow = 3;

        private readonly IFeedbackAiService _feedbackAiService;
        private readonly FirestoreDb _firestoreDb;

        public FeedbackController(
            IFeedbackAiService feedbackAiService,
            FirestoreDb firestoreDb)
        {
            _feedbackAiService = feedbackAiService;
            _firestoreDb = firestoreDb;
        }

        [UserAuthorize]
        [HttpPost("submit")]
        public async Task<IActionResult> SubmitFeedback(
            [FromBody] FeedbackSubmitRequest request,
            CancellationToken cancellationToken)
        {
            var uid = CurrentUserUid();
            if (string.IsNullOrWhiteSpace(uid))
            {
                return Unauthorized(new { message = "Không có quyền" });
            }

            var content = request.Content?.Trim() ?? "";
            if (content.Length < 10)
            {
                return BadRequest(new
                {
                    message = "Nội dung phản hồi cần tối thiểu 10 ký tự."
                });
            }

            if (content.Length > 800)
            {
                return BadRequest(new
                {
                    message = "Nội dung phản hồi không được vượt quá 800 ký tự."
                });
            }

            var userFeedbacks = await LoadUserFeedbacksAsync(uid);
            var spamWarning = BuildSpamWarning(userFeedbacks, content);
            if (!string.IsNullOrWhiteSpace(spamWarning))
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new
                {
                    message = spamWarning
                });
            }

            var aiResult = await _feedbackAiService.GenerateReplyAsync(
                new FeedbackAiReplyRequest
                {
                    Content = content,
                    DisplayName = request.DisplayName ?? "",
                    Email = request.Email ?? "",
                    Platform = request.Platform ?? ""
                },
                cancellationToken);

            var now = Timestamp.GetCurrentTimestamp();
            var feedbackRef = _firestoreDb.Collection("feedbacks").Document();
            var replyEntry = BuildReplyEntry(
                aiResult.ReplyText,
                "Trợ lý AI",
                aiResult.Source == "fallback" ? "auto_fallback" : "auto_ai",
                now);

            await feedbackRef.SetAsync(new Dictionary<string, object>
            {
                ["userId"] = uid,
                ["email"] = request.Email ?? "",
                ["displayName"] = request.DisplayName ?? "",
                ["content"] = content,
                ["timestamp"] = FieldValue.ServerTimestamp,
                ["updatedAt"] = FieldValue.ServerTimestamp,
                ["platform"] = string.IsNullOrWhiteSpace(request.Platform) ? "Android" : request.Platform,
                ["status"] = aiResult.SpamRisk ? "spam" : "replied",
                ["replyText"] = aiResult.ReplyText,
                ["replyAuthor"] = "Trợ lý AI",
                ["replySource"] = replyEntry["source"],
                ["repliedAt"] = now,
                ["autoRepliedAt"] = now,
                ["spamRisk"] = aiResult.SpamRisk,
                ["spamReason"] = aiResult.SpamReason ?? "",
                ["replies"] = new[] { replyEntry }
            }, cancellationToken: cancellationToken);

            return Ok(new
            {
                feedbackId = feedbackRef.Id,
                reply = aiResult
            });
        }

        [UserAuthorize]
        [HttpGet("my")]
        public async Task<IActionResult> GetMyFeedbacks()
        {
            var uid = CurrentUserUid();
            if (string.IsNullOrWhiteSpace(uid))
            {
                return Unauthorized(new { message = "Không có quyền" });
            }

            var feedbacks = await LoadUserFeedbacksAsync(uid);
            return Ok(feedbacks);
        }

        [AdminAuthorize]
        [HttpPost("ai-reply")]
        public async Task<IActionResult> GenerateAiReply(
            [FromBody] FeedbackAiReplyRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(new { message = "Content is required." });
            }

            var result = await _feedbackAiService.GenerateReplyAsync(request, cancellationToken);
            return Ok(result);
        }

        [UserAuthorize]
        [HttpPost("{feedbackId}/auto-reply")]
        public async Task<IActionResult> GenerateAutomaticReply(
            string feedbackId,
            [FromBody] FeedbackAiReplyRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(new { message = "Content is required." });
            }

            var feedbackRef = FeedbackDocument(feedbackId);
            var snapshot = await feedbackRef.GetSnapshotAsync(cancellationToken);
            if (!snapshot.Exists)
            {
                return NotFound(new { message = "Feedback not found." });
            }

            var data = snapshot.ToDictionary();
            var currentUid = CurrentUserUid();
            var feedbackUserId = ReadString(data, "userId");
            if (!string.IsNullOrWhiteSpace(feedbackUserId)
                && !string.Equals(feedbackUserId, currentUid, StringComparison.Ordinal))
            {
                return Forbid();
            }

            var result = await _feedbackAiService.GenerateReplyAsync(request, cancellationToken);
            var now = Timestamp.GetCurrentTimestamp();
            var replyEntry = BuildReplyEntry(
                result.ReplyText,
                "Trợ lý AI",
                result.Source == "fallback" ? "auto_fallback" : "auto_ai",
                now);

            await feedbackRef.UpdateAsync(new Dictionary<string, object>
            {
                ["status"] = result.SpamRisk ? "spam" : "replied",
                ["replyText"] = result.ReplyText,
                ["replyAuthor"] = "Trợ lý AI",
                ["replySource"] = replyEntry["source"],
                ["repliedAt"] = now,
                ["autoRepliedAt"] = now,
                ["spamRisk"] = result.SpamRisk,
                ["spamReason"] = result.SpamReason ?? "",
                ["replies"] = FieldValue.ArrayUnion(replyEntry),
                ["updatedAt"] = FieldValue.ServerTimestamp
            }, cancellationToken: cancellationToken);

            return Ok(result);
        }

        [AdminAuthorize]
        [HttpPut("{feedbackId}/ai-suggestion")]
        public async Task<IActionResult> SaveAiSuggestion(
            string feedbackId,
            [FromBody] FeedbackAiReplyRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(new { message = "Content is required." });
            }

            var feedbackRef = FeedbackDocument(feedbackId);
            var snapshot = await feedbackRef.GetSnapshotAsync(cancellationToken);
            if (!snapshot.Exists)
            {
                return NotFound(new { message = "Feedback not found." });
            }

            var result = await _feedbackAiService.GenerateReplyAsync(request, cancellationToken);
            await feedbackRef.UpdateAsync(new Dictionary<string, object>
            {
                ["aiSuggestedReply"] = result.ReplyText,
                ["aiGeneratedAt"] = FieldValue.ServerTimestamp,
                ["spamRisk"] = result.SpamRisk,
                ["spamReason"] = result.SpamReason ?? "",
                ["updatedAt"] = FieldValue.ServerTimestamp
            }, cancellationToken: cancellationToken);

            return Ok(result);
        }

        [AdminAuthorize]
        [HttpPut("{feedbackId}/reply")]
        public async Task<IActionResult> ReplyToFeedback(
            string feedbackId,
            [FromBody] FeedbackReplyRequest request,
            CancellationToken cancellationToken)
        {
            var replyText = request.ReplyText?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(replyText))
            {
                return BadRequest(new { message = "Reply text is required." });
            }

            var feedbackRef = FeedbackDocument(feedbackId);
            var snapshot = await feedbackRef.GetSnapshotAsync(cancellationToken);
            if (!snapshot.Exists)
            {
                return NotFound(new { message = "Feedback not found." });
            }

            var now = Timestamp.GetCurrentTimestamp();
            var author = string.IsNullOrWhiteSpace(request.ReplyAuthor)
                ? "Quản trị viên"
                : request.ReplyAuthor.Trim();
            var source = string.IsNullOrWhiteSpace(request.Source)
                ? "manual"
                : request.Source.Trim();
            var replyEntry = BuildReplyEntry(replyText, author, source, now);

            await feedbackRef.UpdateAsync(new Dictionary<string, object>
            {
                ["status"] = "replied",
                ["replyText"] = replyText,
                ["replyAuthor"] = author,
                ["replySource"] = source,
                ["repliedAt"] = now,
                ["replies"] = FieldValue.ArrayUnion(replyEntry),
                ["updatedAt"] = FieldValue.ServerTimestamp
            }, cancellationToken: cancellationToken);

            return Ok(new
            {
                message = "Đã lưu phản hồi.",
                reply = replyEntry
            });
        }

        [AdminAuthorize]
        [HttpPut("{feedbackId}/spam")]
        public async Task<IActionResult> MarkSpam(
            string feedbackId,
            [FromBody] FeedbackSpamRequest request,
            CancellationToken cancellationToken)
        {
            var feedbackRef = FeedbackDocument(feedbackId);
            var snapshot = await feedbackRef.GetSnapshotAsync(cancellationToken);
            if (!snapshot.Exists)
            {
                return NotFound(new { message = "Feedback not found." });
            }

            var statusAfter = string.IsNullOrWhiteSpace(request.StatusAfter)
                ? "open"
                : request.StatusAfter.Trim();

            await feedbackRef.UpdateAsync(new Dictionary<string, object>
            {
                ["status"] = request.IsSpam ? "spam" : statusAfter,
                ["spamRisk"] = request.IsSpam,
                ["spamReviewedAt"] = FieldValue.ServerTimestamp,
                ["updatedAt"] = FieldValue.ServerTimestamp
            }, cancellationToken: cancellationToken);

            return Ok(new
            {
                message = request.IsSpam
                    ? "Đã đánh dấu spam."
                    : "Đã bỏ đánh dấu spam."
            });
        }

        private async Task<List<FeedbackItemDto>> LoadUserFeedbacksAsync(string uid)
        {
            var snapshot = await _firestoreDb
                .Collection("feedbacks")
                .WhereEqualTo("userId", uid)
                .GetSnapshotAsync();

            return snapshot.Documents
                .Select(MapFeedbackItem)
                .OrderByDescending(item => item.Timestamp ?? DateTime.MinValue)
                .ToList();
        }

        private string? CurrentUserUid()
        {
            return HttpContext.Items["UserUid"]?.ToString();
        }

        private DocumentReference FeedbackDocument(string feedbackId)
        {
            return _firestoreDb.Collection("feedbacks").Document(feedbackId);
        }

        private static string BuildSpamWarning(
            IEnumerable<FeedbackItemDto> feedbacks,
            string content)
        {
            var normalizedContent = Normalize(content);
            var now = DateTime.UtcNow;
            var recentCount = 0;

            foreach (var item in feedbacks)
            {
                var createdAt = item.Timestamp;
                if (createdAt.HasValue && now - createdAt.Value.ToUniversalTime() <= SpamWindow)
                {
                    recentCount++;
                }

                if (Normalize(item.Content) == normalizedContent)
                {
                    return "Bạn đã gửi nội dung này rồi. Hãy bổ sung thêm chi tiết nếu cần.";
                }
            }

            return recentCount >= MaxFeedbacksInSpamWindow
                ? "Bạn đã gửi nhiều phản hồi trong 30 phút gần đây. Vui lòng thử lại sau."
                : "";
        }

        private static Dictionary<string, object> BuildReplyEntry(
            string content,
            string authorName,
            string source,
            Timestamp createdAt)
        {
            return new Dictionary<string, object>
            {
                ["id"] = Guid.NewGuid().ToString("N"),
                ["content"] = content,
                ["authorName"] = authorName,
                ["source"] = source,
                ["createdAt"] = createdAt
            };
        }

        private static FeedbackItemDto MapFeedbackItem(DocumentSnapshot document)
        {
            var data = document.ToDictionary();
            var replies = new List<FeedbackReplyDto>();

            if (data.TryGetValue("replies", out var repliesValue)
                && repliesValue is IEnumerable<object> rawReplies)
            {
                replies = rawReplies
                    .OfType<Dictionary<string, object>>()
                    .Select(MapReply)
                    .Where(reply => !string.IsNullOrWhiteSpace(reply.Content))
                    .OrderBy(reply => reply.CreatedAt ?? DateTime.MinValue)
                    .ToList();
            }

            if (replies.Count == 0
                && !string.IsNullOrWhiteSpace(ReadString(data, "replyText")))
            {
                replies.Add(new FeedbackReplyDto
                {
                    Id = "legacy",
                    Content = ReadString(data, "replyText"),
                    AuthorName = ReadString(data, "replyAuthor"),
                    Source = ReadString(data, "replySource"),
                    CreatedAt = ReadTimestamp(data, "repliedAt")
                });
            }

            var latestReply = replies.LastOrDefault();

            return new FeedbackItemDto
            {
                FeedbackId = document.Id,
                UserId = ReadString(data, "userId"),
                Email = ReadString(data, "email"),
                DisplayName = ReadString(data, "displayName"),
                Content = ReadString(data, "content"),
                Platform = string.IsNullOrWhiteSpace(ReadString(data, "platform"))
                    ? "Khác"
                    : ReadString(data, "platform"),
                Status = string.IsNullOrWhiteSpace(ReadString(data, "status"))
                    ? latestReply is null ? "open" : "replied"
                    : ReadString(data, "status"),
                Timestamp = ReadTimestamp(data, "timestamp"),
                UpdatedAt = ReadTimestamp(data, "updatedAt"),
                Replies = replies,
                ReplyText = latestReply?.Content ?? ReadString(data, "replyText"),
                ReplyAuthor = latestReply?.AuthorName ?? ReadString(data, "replyAuthor"),
                ReplySource = latestReply?.Source ?? ReadString(data, "replySource"),
                RepliedAt = latestReply?.CreatedAt ?? ReadTimestamp(data, "repliedAt"),
                AiSuggestedReply = ReadString(data, "aiSuggestedReply"),
                AiGeneratedAt = ReadTimestamp(data, "aiGeneratedAt"),
                SpamRisk = ReadBool(data, "spamRisk"),
                SpamReason = ReadString(data, "spamReason")
            };
        }

        private static FeedbackReplyDto MapReply(Dictionary<string, object> data)
        {
            return new FeedbackReplyDto
            {
                Id = ReadString(data, "id"),
                Content = ReadString(data, "content"),
                AuthorName = ReadString(data, "authorName"),
                Source = ReadString(data, "source"),
                CreatedAt = ReadTimestamp(data, "createdAt")
            };
        }

        private static string ReadString(
            IReadOnlyDictionary<string, object> data,
            string key)
        {
            return data.TryGetValue(key, out var value)
                ? value?.ToString() ?? ""
                : "";
        }

        private static bool ReadBool(
            IReadOnlyDictionary<string, object> data,
            string key)
        {
            return data.TryGetValue(key, out var value)
                && value is bool boolValue
                && boolValue;
        }

        private static DateTime? ReadTimestamp(
            IReadOnlyDictionary<string, object> data,
            string key)
        {
            if (!data.TryGetValue(key, out var value))
            {
                return null;
            }

            return value switch
            {
                Timestamp timestamp => timestamp.ToDateTime(),
                DateTime dateTime => dateTime,
                string text when DateTime.TryParse(text, out var parsed) => parsed,
                _ => null
            };
        }

        private static string Normalize(string value)
        {
            return string.Join(
                " ",
                value.Trim().ToLowerInvariant().Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
