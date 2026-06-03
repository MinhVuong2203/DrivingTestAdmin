using Backend.Models;
using Backend.Repository;
using Backend.Service.Interface;

namespace Backend.Service
{
    public class NotificationService : INotificationService
    {
        private readonly NotificationRepository _repository;

        public NotificationService(NotificationRepository repository)
        {
            _repository = repository;
        }

        public Task<Notification> Create(Notification notification)
        {
            return _repository.Create(notification);
        }

        public Task<List<Notification>> GetByUserId(string userId)
        {
            return _repository.GetByUserId(userId);
        }

        public Task MarkAsRead(string notificationId)
        {
            return _repository.MarkAsRead(notificationId);
        }
    }
}