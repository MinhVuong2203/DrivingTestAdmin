using Backend.Models;

namespace Backend.Service.Interface
{
    public interface INotificationService
    {
        Task<Notification> Create(Notification notification);

        Task<List<Notification>> GetByUserId(string userId);

        Task MarkAsRead(string notificationId);
    }
}