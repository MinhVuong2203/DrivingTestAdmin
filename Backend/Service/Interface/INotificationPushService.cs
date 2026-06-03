namespace Backend.Service.Interface
{
    public interface INotificationPushService
    {
        Task SendPushToUser(
            string userId,
            string title,
            string body,
            string postId,
            string type
        );
    }
}