using Backend.DTO;

namespace Backend.Service.Interface
{
    public interface IAdminStatisticsService
    {
        Task<AdminStatisticsResponse> GetStatistics(DateTime? from, DateTime? to, string? range);
    }
}
