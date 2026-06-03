using Backend.Service.Interface;
using FirebaseAdmin.Messaging;
using Google.Cloud.Firestore;

namespace Backend.Service
{
    public class NotificationPushService : INotificationPushService
    {
        private readonly FirestoreDb _db;

        public NotificationPushService(FirestoreDb db)
        {
            _db = db;
        }

        public async Task SendPushToUser(
            string userId,
            string title,
            string body,
            string postId,
            string type)
        {
            var userSnap = await _db.Collection("users")
                .Document(userId)
                .GetSnapshotAsync();

            if (!userSnap.Exists)
                return;

            List<string> tokens = new();

            if (userSnap.ContainsField("fcmToken"))
            {
                var token = userSnap.GetValue<string>("fcmToken");
                if (!string.IsNullOrWhiteSpace(token))
                    tokens.Add(token);
            }

            if (userSnap.ContainsField("fcm_tokens"))
            {
                var tokenList = userSnap.GetValue<List<string>>("fcm_tokens");
                tokens.AddRange(tokenList.Where(t => !string.IsNullOrWhiteSpace(t)));
            }

            tokens = tokens.Distinct().ToList();

            if (tokens.Count == 0)
                return;

            foreach (var token in tokens)
            {
                var message = new Message
                {
                    Token = token,
                    Notification = new Notification
                    {
                        Title = title,
                        Body = body
                    },
                    Data = new Dictionary<string, string>
                    {
                        { "type", type },
                        { "postId", postId },
                        { "userId", userId }
                    }
                };

                try
                {
                    await FirebaseMessaging.DefaultInstance.SendAsync(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"FCM send error: {ex.Message}");
                }
            }
        }
    }
}