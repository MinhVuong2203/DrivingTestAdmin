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
            [FromBody] FeedbackSubmitRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(new
                {
                    message = "Content is required."
                });
            }

            var uid = HttpContext.Items["UserUid"]?.ToString();
            if (string.IsNullOrWhiteSpace(uid))
            {
                return Unauthorized(new
                {
                    message = "Không có quyền"
                });
            }

            var content = request.Content.Trim();
            var feedbackRef = _firestoreDb.Collection("feedbacks").Document();

            await feedbackRef.SetAsync(
                new Dictionary<string, object?>
                {
                    { "userId", uid },
                    { "email", request.Email ?? "" },
                    { "displayName", request.DisplayName ?? "" },
                    { "content", content },
                    { "timestamp", FieldValue.ServerTimestamp },
                    { "platform", string.IsNullOrWhiteSpace(request.Platform) ? "Android" : request.Platform },
                    { "status", "open" },
                    { "replyText", "" },
                    { "replySource", "" },
                    { "spamRisk", false },
                    { "replies", new List<object>() }
                }
            );

            var aiResult = await _feedbackAiService.GenerateReplyAsync(
                new FeedbackAiReplyRequest
                {
                    Content = content,
                    DisplayName = request.DisplayName ?? "",
                    Email = request.Email ?? "",
                    Platform = request.Platform ?? ""
                }
            );

            var now = Timestamp.GetCurrentTimestamp();
            var replyEntry = new Dictionary<string, object>
            {
                { "id", Guid.NewGuid().ToString("N") },
                { "content", aiResult.ReplyText },
                { "authorName", "Trợ lý AI" },
                { "source", "auto_ai" },
                { "createdAt", now }
            };

            await feedbackRef.UpdateAsync(
                new Dictionary<string, object>
                {
                    { "status", aiResult.SpamRisk ? "spam" : "replied" },
                    { "replyText", aiResult.ReplyText },
                    { "replyAuthor", "Trợ lý AI" },
                    { "replySource", "auto_ai" },
                    { "repliedAt", now },
                    { "autoRepliedAt", now },
                    { "spamRisk", aiResult.SpamRisk },
                    { "spamReason", aiResult.SpamReason ?? "" },
                    { "replies", FieldValue.ArrayUnion(replyEntry) },
                    { "updatedAt", FieldValue.ServerTimestamp }
                }
            );

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
            var uid = HttpContext.Items["UserUid"]?.ToString();
            if (string.IsNullOrWhiteSpace(uid))
            {
                return Unauthorized(new
                {
                    message = "Không có quyền"
                });
            }

            var snapshot = await _firestoreDb
                .Collection("feedbacks")
                .WhereEqualTo("userId", uid)
                .OrderByDescending("timestamp")
                .GetSnapshotAsync();

            var result = snapshot.Documents
                .Select(MapFeedbackItem)
                .ToList();

            return Ok(result);
        }

        [AdminAuthorize]
        [HttpPost("ai-reply")]
        public async Task<IActionResult> GenerateAiReply(
            [FromBody] FeedbackAiReplyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(new
                {
                    message = "Content is required."
                });
            }

            var result = await _feedbackAiService.GenerateReplyAsync(request);
            return Ok(result);
        }

        [UserAuthorize]
        [HttpPost("{feedbackId}/auto-reply")]
        public async Task<IActionResult> GenerateAutomaticReply(
            string feedbackId,
            [FromBody] FeedbackAiReplyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(new
                {
                    message = "Content is required."
                });
            }

            var feedbackRef = FeedbackDocument(feedbackId);
            var snapshot = await feedbackRef.GetSnapshotAsync();

            if (!snapshot.Exists)
            {
                return NotFound(new
                {
                    message = "Feedback not found."
                });
            }

            var currentUid = HttpContext.Items["UserUid"]?.ToString();
            var data = snapshot.ToDictionary();
            var feedbackUserId = data.TryGetValue("userId", out var userIdValue)
                ? userIdValue?.ToString()
                : null;

            if (!string.IsNullOrWhiteSpace(feedbackUserId)
                && !string.Equals(feedbackUserId, currentUid, StringComparison.Ordinal))
            {
                return Forbid();
            }

            var result = await _feedbackAiService.GenerateReplyAsync(request);
            var now = Timestamp.GetCurrentTimestamp();
            var replyEntry = new Dictionary<string, object>
            {
                { "id", Guid.NewGuid().ToString("N") },
                { "content", result.ReplyText },
                { "authorName", "Trợ lý AI" },
                { "source", "auto_ai" },
                { "createdAt", now }
            };

            await feedbackRef.UpdateAsync(
                new Dictionary<string, object>
                {
                    { "status", result.SpamRisk ? "spam" : "replied" },
                    { "replyText", result.ReplyText },
                    { "replyAuthor", "Trợ lý AI" },
                    { "replySource", "auto_ai" },
                    { "repliedAt", now },
                    { "autoRepliedAt", now },
                    { "spamRisk", result.SpamRisk },
                    { "spamReason", result.SpamReason ?? "" },
                    { "replies", FieldValue.ArrayUnion(replyEntry) },
                    { "updatedAt", FieldValue.ServerTimestamp }
                }
            );

            return Ok(result);
        }

        [AdminAuthorize]
        [HttpPut("{feedbackId}/ai-suggestion")]
        public async Task<IActionResult> SaveAiSuggestion(
            string feedbackId,
            [FromBody] FeedbackAiReplyResponse request)
        {
            await FeedbackDocument(feedbackId).UpdateAsync(
                new Dictionary<string, object>
                {
                    { "aiSuggestedReply", request.ReplyText ?? "" },
                    { "aiSpamRisk", request.SpamRisk },
                    { "aiSpamReason", request.SpamReason ?? "" },
                    { "spamRisk", request.SpamRisk },
                    { "spamReason", request.SpamReason ?? "" },
                    { "aiGeneratedAt", FieldValue.ServerTimestamp },
                    { "updatedAt", FieldValue.ServerTimestamp }
                }
            );

            return Ok(new
            {
                message = "Đã lưu gợi ý AI."
            });
        }

        [AdminAuthorize]
        [HttpPut("{feedbackId}/reply")]
        public async Task<IActionResult> SaveReply(
            string feedbackId,
            [FromBody] FeedbackReplyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ReplyText))
            {
                return BadRequest(new
                {
                    message = "ReplyText is required."
                });
            }

            var now = Timestamp.GetCurrentTimestamp();
            var replyEntry = new Dictionary<string, object>
            {
                { "id", Guid.NewGuid().ToString("N") },
                { "content", request.ReplyText.Trim() },
                { "authorName", request.ReplyAuthor },
                { "source", string.IsNullOrWhiteSpace(request.Source) ? "manual" : request.Source },
                { "createdAt", now }
            };

            await FeedbackDocument(feedbackId).UpdateAsync(
                new Dictionary<string, object>
                {
                    { "status", "replied" },
                    { "replyText", request.ReplyText.Trim() },
                    { "replyAuthor", request.ReplyAuthor },
                    { "replySource", string.IsNullOrWhiteSpace(request.Source) ? "manual" : request.Source },
                    { "repliedAt", now },
                    { "replies", FieldValue.ArrayUnion(replyEntry) },
                    { "updatedAt", FieldValue.ServerTimestamp }
                }
            );

            return Ok(new
            {
                message = "Đã lưu câu trả lời."
            });
        }

        [AdminAuthorize]
        [HttpPut("{feedbackId}/spam")]
        public async Task<IActionResult> MarkSpam(
            string feedbackId,
            [FromBody] FeedbackSpamRequest request)
        {
            await FeedbackDocument(feedbackId).UpdateAsync(
                new Dictionary<string, object>
                {
                    { "status", request.IsSpam ? "spam" : request.StatusAfter },
                    { "spamRisk", request.IsSpam },
                    { "spamReason", request.IsSpam ? "Admin đánh dấu nội dung có dấu hiệu spam." : "" },
                    { "updatedAt", FieldValue.ServerTimestamp }
                }
            );

            return Ok(new
            {
                message = request.IsSpam
                    ? "Đã đánh dấu spam."
                    : "Đã bỏ đánh dấu spam."
            });
        }

        private DocumentReference FeedbackDocument(string feedbackId)
        {
            return _firestoreDb.Collection("feedbacks").Document(feedbackId);
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
                    .ToList();
            }

            if (replies.Count == 0
                && data.TryGetValue("replyText", out var replyTextValue)
                && !string.IsNullOrWhiteSpace(replyTextValue?.ToString()))
            {
                replies.Add(new FeedbackReplyDto
                {
                    Id = "legacy",
                    Content = replyTextValue?.ToString() ?? "",
                    AuthorName = ReadString(data, "replyAuthor"),
                    Source = ReadString(data, "replySource"),
                    CreatedAt = ReadTimestamp(data, "repliedAt")
                });
            }

            return new FeedbackItemDto
            {
                FeedbackId = document.Id,
                Content = ReadString(data, "content"),
                Platform = ReadString(data, "platform"),
                Status = ReadString(data, "status") == "" ? "open" : ReadString(data, "status"),
                Timestamp = ReadTimestamp(data, "timestamp"),
                Replies = replies
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

        private static DateTime? ReadTimestamp(
            IReadOnlyDictionary<string, object> data,
            string key)
        {
            return data.TryGetValue(key, out var value) && value is Timestamp timestamp
                ? timestamp.ToDateTime()
                : null;
        }
    }
}
