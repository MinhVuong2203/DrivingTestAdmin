using Backend.DTO;
using Backend.Service.Interface;
using FirebaseAdmin.Messaging;
using Google.Cloud.Firestore;

namespace Backend.Service
{
    public class WrongQuestionReminderService : IWrongQuestionReminderService
    {
        private const string ReminderType = "wrong_question_reminder";

        private readonly FirestoreDb _db;
        private readonly ILogger<WrongQuestionReminderService> _logger;

        public WrongQuestionReminderService(
            FirestoreDb db,
            ILogger<WrongQuestionReminderService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<WrongQuestionReminderSendResult> SendToEligibleUsersAsync(
            CancellationToken cancellationToken = default)
        {
            var result = new WrongQuestionReminderSendResult();

            var snapshot = await _db
                .Collection("users")
                .WhereEqualTo("reminder_wrong", true)
                .GetSnapshotAsync(cancellationToken);

            result.EligibleUsers = snapshot.Count;

            foreach (var document in snapshot.Documents)
            {
                var data = document.ToDictionary();
                var tokens = ExtractTokens(data);

                if (tokens.Count == 0)
                {
                    continue;
                }

                result.UsersWithTokens++;
                result.TokenCount += tokens.Count;

                foreach (var tokenBatch in tokens.Chunk(500))
                {
                    var message = new MulticastMessage
                    {
                        Tokens = tokenBatch.ToList(),
                        Notification = new Notification
                        {
                            Title = "Đến giờ ôn lại câu sai",
                            Body = "Bấm để làm nhanh một câu bạn từng trả lời sai."
                        },
                        Data = new Dictionary<string, string>
                        {
                            ["type"] = ReminderType
                        },
                        Android = new AndroidConfig
                        {
                            Priority = Priority.High,
                            Notification = new AndroidNotification
                            {
                                ClickAction = "FLUTTER_NOTIFICATION_CLICK",
                                Icon = "ic_stat_wrong_question",
                                Color = "#6366F1"
                            }
                        },
                        Apns = new ApnsConfig
                        {
                            Aps = new Aps
                            {
                                Sound = "default",
                                Badge = 1
                            }
                        }
                    };

                    try
                    {
                        var response = await FirebaseMessaging.DefaultInstance
                            .SendEachForMulticastAsync(message, cancellationToken);

                        result.SuccessCount += response.SuccessCount;
                        result.FailureCount += response.FailureCount;
                    }
                    catch (Exception ex)
                    {
                        result.FailureCount += tokenBatch.Length;
                        _logger.LogWarning(
                            ex,
                            "Failed to send wrong question reminder to user {UserId}.",
                            document.Id);
                    }
                }
            }

            return result;
        }

        private static List<string> ExtractTokens(Dictionary<string, object> data)
        {
            if (!data.TryGetValue("fcm_tokens", out var raw) || raw is null)
            {
                return [];
            }

            if (raw is IEnumerable<object> values)
            {
                return values
                    .OfType<string>()
                    .Select(token => token.Trim())
                    .Where(token => !string.IsNullOrWhiteSpace(token))
                    .Distinct()
                    .ToList();
            }

            if (raw is IEnumerable<string> strings)
            {
                return strings
                    .Select(token => token.Trim())
                    .Where(token => !string.IsNullOrWhiteSpace(token))
                    .Distinct()
                    .ToList();
            }

            return [];
        }
    }
}
