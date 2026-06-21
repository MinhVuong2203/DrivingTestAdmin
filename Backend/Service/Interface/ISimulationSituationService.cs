using Backend.Models;

namespace Backend.Service.Interface
{
    public interface ISimulationSituationService
    {
        Task<int> ImportFromJsonAsync();
        Task<List<SimulationSituation>> GetAllAsync();
        Task<SimulationSituation?> GetByIdAsync(string docId);
    }
}
