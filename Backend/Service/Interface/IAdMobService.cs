using Backend.Models;

namespace Backend.Service.Interface
{
    public interface IAdMobService
    {
        Task<List<AdMobReportResponse>> GetNetworkReportAsync(
            string startDate, string endDate);
    }
}