using Backend.DTO;
using Backend.Models;

namespace Backend.Service.Interface
{
    public interface IDrivingCenterService
    {
        Task<List<DrivingCenter>> GetAll();
        Task<List<DrivingCenter>> Search(string? keyword);
        Task<DrivingCenterSearchResult> SearchPaged(string? keyword, int page, int pageSize);
        Task<DrivingCenter?> GetById(string id);
    }
}
