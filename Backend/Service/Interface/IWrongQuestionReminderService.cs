using Backend.DTO;

namespace Backend.Service.Interface
{
    public interface IWrongQuestionReminderService
    {
        Task<WrongQuestionReminderSendResult> SendToEligibleUsersAsync(
            CancellationToken cancellationToken = default);
    }
}
