using Backend.Filters;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [UserAuthorize]
    public class ProfileController : ControllerBase
    {
        private const long MaxAvatarBytes = 5 * 1024 * 1024;
        private static readonly HashSet<string> AllowedAvatarExtensions = new(
            StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp",
            ".gif",
        };

        private readonly Cloudinary _cloudinary;
        private readonly FirestoreDb _db;

        public ProfileController(IConfiguration configuration, FirestoreDb db)
        {
            _db = db;

            var account = new Account(
                configuration["Cloudinary:CloudName"],
                configuration["Cloudinary:ApiKey"],
                configuration["Cloudinary:ApiSecret"]
            );

            _cloudinary = new Cloudinary(account);
        }

        [HttpPost("avatar")]
        public async Task<IActionResult> UploadAvatar(IFormFile file)
        {
            var uid = HttpContext.Items["UserUid"]?.ToString();
            if (string.IsNullOrWhiteSpace(uid))
            {
                return Unauthorized();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest("File không hợp lệ.");
            }

            if (file.Length > MaxAvatarBytes)
            {
                return BadRequest("Ảnh đại diện không được vượt quá 5MB.");
            }

            if (!IsImageFile(file))
            {
                return BadRequest("Chỉ chấp nhận file ảnh.");
            }

            await using var stream = file.OpenReadStream();

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "avatar",
                PublicId = uid,
                Overwrite = true,
                Invalidate = true,
                UseFilename = false,
                UniqueFilename = false,
            };

            var result = await _cloudinary.UploadAsync(uploadParams);

            if (result.Error != null)
            {
                return BadRequest(result.Error.Message);
            }

            var photoUrl = result.SecureUrl?.ToString();
            if (string.IsNullOrWhiteSpace(photoUrl))
            {
                return BadRequest("Không lấy được đường dẫn ảnh từ Cloudinary.");
            }

            await _db.Collection("users").Document(uid).SetAsync(
                new Dictionary<string, object>
                {
                    ["photoURL"] = photoUrl,
                    ["updatedAt"] = FieldValue.ServerTimestamp,
                },
                SetOptions.MergeAll
            );

            await FirebaseAuth.DefaultInstance.UpdateUserAsync(new UserRecordArgs
            {
                Uid = uid,
                PhotoUrl = photoUrl,
            });

            return Ok(new
            {
                photoURL = photoUrl,
                publicId = result.PublicId,
            });
        }

        [HttpPatch("display-name")]
        public async Task<IActionResult> UpdateDisplayName(
            [FromBody] UpdateDisplayNameRequest request)
        {
            var uid = HttpContext.Items["UserUid"]?.ToString();
            if (string.IsNullOrWhiteSpace(uid))
            {
                return Unauthorized();
            }

            var displayName = request.DisplayName?.Trim();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return BadRequest("Tên hiển thị không được để trống.");
            }

            if (displayName.Length > 80)
            {
                return BadRequest("Tên hiển thị không được vượt quá 80 ký tự.");
            }

            await _db.Collection("users").Document(uid).SetAsync(
                new Dictionary<string, object>
                {
                    ["displayName"] = displayName,
                    ["updatedAt"] = FieldValue.ServerTimestamp,
                },
                SetOptions.MergeAll
            );

            await FirebaseAuth.DefaultInstance.UpdateUserAsync(new UserRecordArgs
            {
                Uid = uid,
                DisplayName = displayName,
            });

            return Ok(new
            {
                displayName,
            });
        }

        private static bool IsImageFile(IFormFile file)
        {
            var contentType = file.ContentType?.Trim();
            if (!string.IsNullOrWhiteSpace(contentType) &&
                contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var extension = Path.GetExtension(file.FileName);
            return AllowedAvatarExtensions.Contains(extension);
        }
    }

    public class UpdateDisplayNameRequest
    {
        public string? DisplayName { get; set; }
    }
}
