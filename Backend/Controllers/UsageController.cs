using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Http;
using Backend.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [UserAuthorize]
    public class UsageController : ControllerBase
    {
        private readonly FirestoreDb _db;
        public UsageController(FirestoreDb db)
        {
            _db = db;
        }

        [HttpPost("can-use-recognition/{uid}")]
        public async Task<IActionResult> CanUseRecognition(string uid)
        {
            if (!IsCurrentUser(uid))
            {
                return Forbid();
            }

            var docRef = _db.Collection("users").Document(uid);
            var doc = await docRef.GetSnapshotAsync();

            if (!doc.Exists) return NotFound();

            var user = doc.ToDictionary();

            // 1. CHECK VIP
            var vip = GetVipUser(user);
            if (vip != null)
            {
                if (!vip.ContainsKey("endDate"))
                    return Ok(true);

                var endDate = ((Timestamp)vip["endDate"]).ToDateTime();

                if (DateTime.UtcNow < endDate)
                    return Ok(true);
            }

            // 2. USER THƯỜNG
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            int count = 0;

            if (user.ContainsKey("usage"))
            {
                var usage = user["usage"] as Dictionary<string, object>;

                if (usage != null && usage.ContainsKey("trafficSignRecognition"))
                {
                    var tsr = usage["trafficSignRecognition"] as Dictionary<string, object>;

                    if (tsr != null && tsr.ContainsKey("date") && tsr["date"].ToString() == today)
                    {
                        count = Convert.ToInt32(tsr["count"]);
                    }
                }
            }

            if (count >= 3)
                return Ok(false);

            // 3. UPDATE
            await docRef.SetAsync(new
            {
                usage = new
                {
                    trafficSignRecognition = new
                    {
                        date = today,
                        count = count + 1
                    }
                }
            }, SetOptions.MergeAll);

            return Ok(true);
        }


        [HttpGet("remaining/{uid}")]
        public async Task<IActionResult> GetRemaining(string uid)
        {
            if (!IsCurrentUser(uid))
            {
                return Forbid();
            }

            var doc = await _db.Collection("users").Document(uid).GetSnapshotAsync();

            if (!doc.Exists) return NotFound();

            var user = doc.ToDictionary();

            // VIP
            var vip = GetVipUser(user);
            if (vip != null)
            {
                if (!vip.ContainsKey("endDate"))
                    return Ok(-1);

                var endDate = ((Timestamp)vip["endDate"]).ToDateTime();

                if (DateTime.UtcNow < endDate)
                    return Ok(-1);
            }

            string today = DateTime.Now.ToString("yyyy-MM-dd");

            int used = 0;

            if (user.ContainsKey("usage"))
            {
                var usage = user["usage"] as Dictionary<string, object>;

                if (usage != null && usage.ContainsKey("trafficSignRecognition"))
                {
                    var tsr = usage["trafficSignRecognition"] as Dictionary<string, object>;

                    if (tsr != null && tsr["date"].ToString() == today)
                    {
                        used = Convert.ToInt32(tsr["count"]);
                    }
                }
            }

            return Ok(Math.Max(0, 3 - used));
        }

        private static Dictionary<string, object>? GetVipUser(Dictionary<string, object> user)
        {
            if (user.TryGetValue("vipUser", out var vipUserValue) &&
                vipUserValue is Dictionary<string, object> vipUser)
            {
                return vipUser;
            }

            if (user.TryGetValue("vip", out var legacyVipValue) &&
                legacyVipValue is Dictionary<string, object> legacyVip)
            {
                return legacyVip;
            }

            return null;
        }

        private bool IsCurrentUser(string uid)
        {
            return HttpContext.Items["UserUid"]?.ToString() == uid;
        }
    }
}
