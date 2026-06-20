using Backend.DTO;
using Backend.Models;

namespace Backend.Service.Interface
{
    public interface IDrivingCenterImportService
    {
        Task<ImportDrivingCenterResult> ImportFromLocalBusinessData(string query);
        Task<DrivingCenterSearchResult> SearchLocalBusinessDataPaged(string query, int page, int pageSize);
        Task<List<DrivingCenter>> Search(string? keyword);
        Task<DrivingCenter?> GetById(string id);
    }
}
