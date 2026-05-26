using Backend.Models;

namespace Backend.Service.Interface
{
    public interface IAiModerationService
    {
        Task<bool> IsPostViolatedByAi(string content);

        Task<AiModerationResult> CheckPostByAi(string content);
    }
}