using Backend.Models;

namespace Backend.Service.Interface
{
    public interface IDrivingCenterService
    {
        Task<List<DrivingCenter>> GetAll();
        Task<List<DrivingCenter>> Search(string? keyword);
        Task<DrivingCenter?> GetById(string id);
    }
}
