using Backend.Models;
using Google.Cloud.Firestore;

namespace Backend.Repository
{
    public class TrafficViolationRepository
    {
        private const string CollectionName = "traffic_violations";
        private readonly FirestoreDb _db;

        public TrafficViolationRepository(FirestoreDb db)
        {
            _db = db;
        }

        public async Task<int> ImportAsync(List<TrafficViolation> violations)
        {
            if (violations.Count == 0)
                return 0;

            var savedCount = 0;

            foreach (var chunk in violations.Chunk(450))
            {
                var batch = _db.StartBatch();

                foreach (var violation in chunk)
                {
                    if (string.IsNullOrWhiteSpace(violation.Id))
                        continue;

                    var docRef = _db.Collection(CollectionName).Document(violation.Id);
                    batch.Set(docRef, ToFirestoreData(violation), SetOptions.Overwrite);
                    savedCount++;
                }

                await batch.CommitAsync();
            }

            return savedCount;
        }

        public async Task<List<TrafficViolation>> GetAllActiveAsync()
        {
            var snapshot = await _db.Collection(CollectionName)
                .WhereEqualTo("status", "active")
                .GetSnapshotAsync();

            return snapshot.Documents
                .Select(MapDocument)
                .OrderBy(violation => violation.FineMin)
                .ThenBy(violation => violation.Title)
                .ToList();
        }

        public async Task<TrafficViolation?> GetByIdAsync(string id)
        {
            var doc = await _db.Collection(CollectionName)
                .Document(id)
                .GetSnapshotAsync();

            if (!doc.Exists)
                return null;

            return MapDocument(doc);
        }

        private static Dictionary<string, object> ToFirestoreData(TrafficViolation violation)
        {
            return new Dictionary<string, object>
            {
                { "id", violation.Id },
                { "title", violation.Title },
                { "vehicle_types", violation.VehicleTypes },
                { "subject_text", violation.SubjectText },
                { "penalty_text", violation.PenaltyText },
                { "penalty_legal_basis", violation.PenaltyLegalBasis },
                { "additional_penalty_text", violation.AdditionalPenaltyText },
                { "additional_penalty_legal_basis", violation.AdditionalPenaltyLegalBasis },
                { "fine_min", violation.FineMin },
                { "fine_max", violation.FineMax },
                { "aliases", violation.Aliases },
                { "keywords", violation.Keywords },
                { "search_text", violation.SearchText },
                { "related_violation_ids", violation.RelatedViolationIds },
                { "status", string.IsNullOrWhiteSpace(violation.Status) ? "active" : violation.Status }
            };
        }

        private static TrafficViolation MapDocument(DocumentSnapshot doc)
        {
            var data = doc.ToDictionary();

            return new TrafficViolation
            {
                Id = GetString(data, "id", doc.Id),
                Title = GetString(data, "title"),
                VehicleTypes = GetStringList(data, "vehicle_types"),
                SubjectText = GetString(data, "subject_text"),
                PenaltyText = GetString(data, "penalty_text"),
                PenaltyLegalBasis = GetString(data, "penalty_legal_basis"),
                AdditionalPenaltyText = GetString(data, "additional_penalty_text"),
                AdditionalPenaltyLegalBasis = GetString(data, "additional_penalty_legal_basis"),
                FineMin = GetInt(data, "fine_min"),
                FineMax = GetInt(data, "fine_max"),
                Aliases = GetStringList(data, "aliases"),
                Keywords = GetStringList(data, "keywords"),
                SearchText = GetString(data, "search_text"),
                RelatedViolationIds = GetStringList(data, "related_violation_ids"),
                Status = GetString(data, "status", "active")
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

        private static List<string> GetStringList(Dictionary<string, object> data, string key)
        {
            if (!data.TryGetValue(key, out var value) || value == null)
                return new List<string>();

            if (value is IEnumerable<object> items)
            {
                return items.Select(item => item?.ToString() ?? string.Empty)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToList();
            }

            var legacyValue = value.ToString();
            return string.IsNullOrWhiteSpace(legacyValue) ? new List<string>() : new List<string> { legacyValue };
        }
    }
}
