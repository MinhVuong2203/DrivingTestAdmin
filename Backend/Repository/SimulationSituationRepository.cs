using Backend.Models;
using Google.Cloud.Firestore;

namespace Backend.Repository
{
    public class SimulationSituationRepository
    {
        private const string CollectionName = "simulation_situations";
        private readonly FirestoreDb _db;

        public SimulationSituationRepository(FirestoreDb db)
        {
            _db = db;
        }

        public async Task<int> ImportAsync(List<SimulationSituation> situations)
        {
            if (situations.Count == 0)
                return 0;

            var savedCount = 0;

            foreach (var chunk in situations.Chunk(450))
            {
                var batch = _db.StartBatch();

                foreach (var situation in chunk)
                {
                    if (string.IsNullOrWhiteSpace(situation.DocId))
                        continue;

                    var docRef = _db.Collection(CollectionName).Document(situation.DocId);
                    batch.Set(docRef, ToFirestoreData(situation), SetOptions.Overwrite);
                    savedCount++;
                }

                await batch.CommitAsync();
            }

            return savedCount;
        }

        public async Task<List<SimulationSituation>> GetAllAsync()
        {
            var snapshot = await _db.Collection(CollectionName)
                .OrderBy("id")
                .GetSnapshotAsync();

            return snapshot.Documents
                .Select(MapDocument)
                .ToList();
        }

        public async Task<SimulationSituation?> GetByIdAsync(string docId)
        {
            var doc = await _db.Collection(CollectionName)
                .Document(docId)
                .GetSnapshotAsync();

            if (!doc.Exists)
                return null;

            return MapDocument(doc);
        }

        private static Dictionary<string, object> ToFirestoreData(SimulationSituation situation)
        {
            return new Dictionary<string, object>
            {
                { "id", situation.Id },
                { "title", situation.Title },
                { "chapter", situation.Chapter },
                { "duration", situation.Duration },
                { "videoUrl", situation.VideoUrl },
                { "scoreWindows", situation.ScoreWindows.Select(window => new Dictionary<string, object>
                    {
                        { "from", window.From },
                        { "to", window.To },
                        { "score", window.Score }
                    }).ToList()
                },
                { "isActive", situation.IsActive }
            };
        }

        private static SimulationSituation MapDocument(DocumentSnapshot doc)
        {
            var data = doc.ToDictionary();

            return new SimulationSituation
            {
                DocId = doc.Id,
                Id = GetInt(data, "id"),
                Title = GetString(data, "title"),
                Chapter = GetInt(data, "chapter"),
                Duration = GetInt(data, "duration"),
                VideoUrl = GetString(data, "videoUrl"),
                ScoreWindows = GetScoreWindows(data, "scoreWindows"),
                IsActive = GetBool(data, "isActive", true)
            };
        }

        private static string GetString(Dictionary<string, object> data, string key, string defaultValue = "")
        {
            return data.TryGetValue(key, out var value) ? value?.ToString() ?? defaultValue : defaultValue;
        }

        private static int GetInt(Dictionary<string, object> data, string key)
        {
            if (!data.TryGetValue(key, out var value) || value == null)
                return 0;

            return Convert.ToInt32(value);
        }

        private static bool GetBool(Dictionary<string, object> data, string key, bool defaultValue = false)
        {
            if (!data.TryGetValue(key, out var value) || value == null)
                return defaultValue;

            return Convert.ToBoolean(value);
        }

        private static List<SimulationScoreWindow> GetScoreWindows(Dictionary<string, object> data, string key)
        {
            if (!data.TryGetValue(key, out var value) || value == null)
                return new List<SimulationScoreWindow>();

            if (value is not IEnumerable<object> items)
                return new List<SimulationScoreWindow>();

            return items
                .OfType<Dictionary<string, object>>()
                .Select(item => new SimulationScoreWindow
                {
                    From = GetDouble(item, "from"),
                    To = GetDouble(item, "to"),
                    Score = GetInt(item, "score")
                })
                .ToList();
        }

        private static double GetDouble(Dictionary<string, object> data, string key)
        {
            if (!data.TryGetValue(key, out var value) || value == null)
                return 0;

            return Convert.ToDouble(value);
        }
    }
}
