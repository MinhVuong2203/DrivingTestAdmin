using Backend.Models;

namespace Backend.Service.Interface
{
    public interface IModerationService
    {
        Task<bool> IsAutoDeleteEnabled();

        Task SetAutoDeleteEnabled(bool enabled);

        Task<List<ModerationKeyword>> GetAllKeywords();

        Task<ModerationKeyword> CreateKeyword(ModerationKeyword keyword);

        Task ToggleKeyword(string keywordId, bool isActive);

        Task<bool> IsPostViolated(string content);

        Task<bool> IsPostViolatedByAiFirst(string content);
    }
}