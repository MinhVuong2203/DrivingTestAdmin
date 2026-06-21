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
                return Unauthorized(new { message = "Khong co quyen" });
            }

            var content = request.Content?.Trim() ?? "";
            if (content.Length < 10)
            {
                return BadRequest(new { message = "Noi dung phan hoi can toi thieu 10 ky tu." });
            }

            if (content.Length > 800)
            {
                return BadRequest(new { message = "Noi dung phan hoi khong duoc vuot qua 800 ky tu." });
            }

            var userFeedbacks = await LoadUserFeedbacksAsync(uid, cancellationToken);
            var spamWarning = BuildSpamWarning(userFeedbacks, content);
            if (!string.IsNullOrWhiteSpace(spamWarning))
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new { message = spamWarning });
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
            var replySource = aiResult.Source == "fallback" ? "auto_fallback" : "auto_ai";
            var replyEntry = BuildReplyEntry(aiResult.ReplyText, "Tro ly AI", replySource, now);
            var feedbackRef = _firestoreDb.Collection("feedbacks").Document();

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
                ["replyAuthor"] = "Tro ly AI",
                ["replySource"] = replySource,
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
        public async Task<IActionResult> GetMyFeedbacks(CancellationToken cancellationToken)
        {
            var uid = CurrentUserUid();
            if (string.IsNullOrWhiteSpace(uid))
            {
                return Unauthorized(new { message = "Khong co quyen" });
            }

            var feedbacks = await LoadUserFeedbacksAsync(uid, cancellationToken);
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
                ? "Quan tri vien"
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
                message = "Da luu phan hoi.",
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
                message = request.IsSpam ? "Da danh dau spam." : "Da bo danh dau spam."
            });
        }

        private async Task<List<FeedbackItemDto>> LoadUserFeedbacksAsync(
            string uid,
            CancellationToken cancellationToken)
        {
            var snapshot = await _firestoreDb
                .Collection("feedbacks")
                .WhereEqualTo("userId", uid)
                .GetSnapshotAsync(cancellationToken);

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
                    return "Ban da gui noi dung nay roi. Hay bo sung them chi tiet neu can.";
                }
            }

            return recentCount >= MaxFeedbacksInSpamWindow
                ? "Ban da gui nhieu phan hoi trong 30 phut gan day. Vui long thu lai sau."
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
            var replies = ReadReplies(data);
            var latestReply = replies.LastOrDefault();

            return new FeedbackItemDto
            {
                FeedbackId = document.Id,
                UserId = ReadString(data, "userId"),
                Email = ReadString(data, "email"),
                DisplayName = ReadString(data, "displayName"),
                Content = ReadString(data, "content"),
                Platform = string.IsNullOrWhiteSpace(ReadString(data, "platform"))
                    ? "Khac"
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

        private static List<FeedbackReplyDto> ReadReplies(
            IReadOnlyDictionary<string, object> data)
        {
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

            if (replies.Count == 0 && !string.IsNullOrWhiteSpace(ReadString(data, "replyText")))
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

            return replies;
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

        private static string ReadString(IReadOnlyDictionary<string, object> data, string key)
        {
            return data.TryGetValue(key, out var value)
                ? value?.ToString() ?? ""
                : "";
        }

        private static bool ReadBool(IReadOnlyDictionary<string, object> data, string key)
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
