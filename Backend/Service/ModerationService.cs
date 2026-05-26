using Backend.Models;
using Backend.Repository;
using Backend.Service.Interface;

namespace Backend.Service
{
    public class ModerationService : IModerationService
    {
        private readonly ModerationRepository _moderationRepository;
        private readonly IAiModerationService _aiModerationService;

        public ModerationService(
        ModerationRepository moderationRepository,
        IAiModerationService aiModerationService)
        {
            _moderationRepository = moderationRepository;
            _aiModerationService = aiModerationService;
        }

        public Task<bool> IsAutoDeleteEnabled()
        {
            return _moderationRepository.IsAutoDeleteEnabled();
        }

        public Task SetAutoDeleteEnabled(bool enabled)
        {
            return _moderationRepository.SetAutoDeleteEnabled(enabled);
        }

        public Task<List<ModerationKeyword>> GetAllKeywords()
        {
            return _moderationRepository.GetAllKeywords();
        }

        public Task<ModerationKeyword> CreateKeyword(ModerationKeyword keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword.keyword))
            {
                throw new ArgumentException("Keyword không được để trống");
            }

            return _moderationRepository.CreateKeyword(keyword);
        }

        public Task ToggleKeyword(string keywordId, bool isActive)
        {
            return _moderationRepository.ToggleKeyword(keywordId, isActive);
        }

        public async Task<bool> IsPostViolated(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return false;

            var enabled = await _moderationRepository.IsAutoDeleteEnabled();
            if (!enabled) return false;

            var keywords = await _moderationRepository.GetActiveKeywords();
            var lowerContent = content.ToLower();

            return keywords.Any(k =>
                !string.IsNullOrWhiteSpace(k.keyword) &&
                lowerContent.Contains(k.keyword.ToLower())
            );
        }

        // Trả về true nếu bị vi phạm bởi AI, false nếu không bị vi phạm hoặc có lỗi xảy ra
        public async Task<bool> IsPostViolatedByAiFirst(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return false;

            var enabled = await _moderationRepository.IsAutoDeleteEnabled();
            if (!enabled)
                return false;

            var aiViolated = await _aiModerationService.IsPostViolatedByAi(content);

            if (aiViolated)
                return true;

            return await IsPostViolated(content);
        }
    }
}