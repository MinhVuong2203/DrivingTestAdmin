using Backend.Models;
using Google.Cloud.Firestore;

namespace Backend.Repository
{
    public class NotificationRepository
    {
        private readonly FirestoreDb _db;

        public NotificationRepository(FirestoreDb db)
        {
            _db = db;
        }

        public async Task<Notification> Create(Notification notification)
        {
            var docRef = _db.Collection("notifications").Document();

            notification.notificationId = docRef.Id;
            notification.createdAt = DateTime.UtcNow;
            notification.isRead = false;

            await docRef.SetAsync(notification);

            return notification;
        }

        public async Task<List<Notification>> GetByUserId(string userId)
        {
            var snapshot = await _db.Collection("notifications")
                .WhereEqualTo("userId", userId)
                .OrderByDescending("createdAt")
                .GetSnapshotAsync();

            return snapshot.Documents.Select(doc =>
            {
                var item = doc.ConvertTo<Notification>();
                item.notificationId = doc.Id;
                return item;
            }).ToList();
        }

        public async Task MarkAsRead(string notificationId)
        {
            await _db.Collection("notifications")
                .Document(notificationId)
                .UpdateAsync(new Dictionary<string, object>
                {
                    { "isRead", true }
                });
        }
    }
}