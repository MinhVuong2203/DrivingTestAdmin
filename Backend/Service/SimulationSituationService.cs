using Backend.Models;
using Backend.Repository;
using Backend.Service.Interface;
using System.Text;
using System.Text.Json;

namespace Backend.Service
{
    public class SimulationSituationService : ISimulationSituationService
    {
        private readonly SimulationSituationRepository _repository;
        private readonly IWebHostEnvironment _environment;

        public SimulationSituationService(
            SimulationSituationRepository repository,
            IWebHostEnvironment environment)
        {
            _repository = repository;
            _environment = environment;
        }

        public async Task<int> ImportFromJsonAsync()
        {
            var filePath = Path.Combine(
                _environment.ContentRootPath,
                "Data",
                "simulation_situations.json");

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Khong tim thay file Data/simulation_situations.json.", filePath);
            }

            var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            var situations = JsonSerializer.Deserialize<List<SimulationSituation>>(json)
                ?? new List<SimulationSituation>();

            var validSituations = situations
                .Where(IsValidSituation)
                .Select(NormalizeSituation)
                .ToList();

            return await _repository.ImportAsync(validSituations);
        }

        public async Task<List<SimulationSituation>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<SimulationSituation?> GetByIdAsync(string docId)
        {
            if (string.IsNullOrWhiteSpace(docId))
                return null;

            return await _repository.GetByIdAsync(docId);
        }

        private static bool IsValidSituation(SimulationSituation situation)
        {
            return !string.IsNullOrWhiteSpace(situation.DocId)
                && situation.Id > 0
                && !string.IsNullOrWhiteSpace(situation.VideoUrl)
                && situation.ScoreWindows.Count == 5;
        }

        private static SimulationSituation NormalizeSituation(SimulationSituation situation)
        {
            situation.DocId = situation.DocId.Trim();
            situation.Title = string.IsNullOrWhiteSpace(situation.Title)
                ? $"Tinh huong {situation.Id}"
                : situation.Title.Trim();
            situation.VideoUrl = situation.VideoUrl.Trim();
            situation.ScoreWindows = situation.ScoreWindows
                .OrderByDescending(window => window.Score)
                .ToList();

            return situation;
        }
    }
}
