using Backend.Models;

namespace Backend.Service.Interface
{
    public interface ITrafficViolationService
    {
        Task<int> ImportFromJsonAsync();
        Task<List<TrafficViolation>> SearchAsync(string? keyword, string? vehicleType);
        Task<TrafficViolation?> GetByIdAsync(string id);
    }
}
