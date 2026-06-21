using Backend.DTO;

namespace Backend.Service.Interface
{
    public interface IFeedbackAiService
    {
        Task<FeedbackAiReplyResponse> GenerateReplyAsync(
            FeedbackAiReplyRequest request,
            CancellationToken cancellationToken = default);
            FeedbackAiReplyRequest request);
    }
}
