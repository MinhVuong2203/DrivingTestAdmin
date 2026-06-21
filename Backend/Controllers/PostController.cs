using Backend.Service;
using Backend.Service.Interface;
using Backend.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Backend.DTO;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostController : ControllerBase
    {
        private readonly IPostService _postService;
        private readonly Cloudinary _cloudinary;

        public PostController(IPostService postService, IConfiguration configuration)
        {
            _postService = postService;

            var account = new Account(
                configuration["Cloudinary:CloudName"],
                configuration["Cloudinary:ApiKey"],
                configuration["Cloudinary:ApiSecret"]
            );

            _cloudinary = new Cloudinary(account);
        }

        [HttpGet]
        [UserAuthorize]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _postService.GetAll());
        }

        [HttpGet("admin")]
        [AdminAuthorize]
        public async Task<IActionResult> GetAllForAdmin()
        {
            return Ok(await _postService.GetAll());
        }

        [HttpGet("{id}")]
        [UserAuthorize]
        public async Task<IActionResult> GetById(string id)
        {
            var post = await _postService.GetById(id);
            if (post == null)
            {
                return NotFound();
            }
            return Ok(post);
        }

        [HttpGet("author/{authorId}")]
        [UserAuthorize]
        public async Task<IActionResult> GetByAuthorId(string authorId)
        {
            var posts = await _postService.GetByAuthorID(authorId);
            if (posts == null || posts.Count == 0)
            {
                return NotFound();
            }
            return Ok(posts);
        }

        //[HttpPost]
        //public async Task<IActionResult> Create([FromBody] Post post)
        //{
        //    post.address ??= "";
        //    post.createdAt = DateTime.UtcNow;
        //    post.updatedAt = DateTime.UtcNow;
        //    //post.status = true;
        //    //post.isDeleted = false;

        //    await _postService.Create(post);

        //    return Ok(post);
        //}

        [HttpPost]
        [UserAuthorize]
        public async Task<IActionResult> Create([FromBody] Post post)
        {
            if (post == null)
            {
                return BadRequest(new
                {
                    message = "Dữ liệu bài viết không hợp lệ"
                });
            }

            post.content = post.content?.Trim() ?? "";
            post.imageUrl ??= "";
            post.videoUrl ??= "";
            post.videoPublicId ??= "";

            var hasContent = !string.IsNullOrWhiteSpace(post.content);
            var hasImage = !string.IsNullOrWhiteSpace(post.imageUrl);
            var hasVideo = !string.IsNullOrWhiteSpace(post.videoUrl);

            if (!hasContent && !hasImage && !hasVideo)
            {
                return BadRequest(new
                {
                    message = "Bài viết phải có nội dung, ảnh hoặc video"
                });
            }

            // Một bài chỉ được có ảnh hoặc video, không có đồng thời cả hai.
            if (hasImage && hasVideo)
            {
                return BadRequest(new
                {
                    message = "Mỗi bài viết chỉ được chọn ảnh hoặc video"
                });
            }

            if (hasVideo)
            {
                if (string.IsNullOrWhiteSpace(post.videoPublicId))
                {
                    return BadRequest(new
                    {
                        message = "Thiếu mã video Cloudinary"
                    });
                }

                if (post.videoDuration <= 0 || post.videoDuration > 30)
                {
                    return BadRequest(new
                    {
                        message = "Thời lượng video không hợp lệ"
                    });
                }
            }
            else
            {
                post.videoUrl = "";
                post.videoPublicId = "";
                post.videoDuration = 0;
                post.videoBytes = 0;
            }

            var currentUserId = HttpContext.Items["UserUid"]?.ToString();

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Unauthorized();
            }

            // Không cho client tạo bài bằng UID của người khác.
            post.authorId = currentUserId;

            var result = await _postService.Create(post);

            return Ok(result);
        }

        [HttpDelete("{id}")]
        [UserAuthorize]
        public async Task<IActionResult> Delete(string id)
        {
            var existingPost = await _postService.GetById(id);
            if (existingPost == null)
            {
                return NotFound();
            }
            await _postService.Delete(id);
            return NoContent();
        }

        [HttpDelete("admin/{id}")]
        [AdminAuthorize]
        public async Task<IActionResult> DeleteForAdmin(string id)
        {
            var existingPost = await _postService.GetById(id);
            if (existingPost == null)
            {
                return NotFound();
            }

            await _postService.Delete(id);
            return NoContent();
        }

        [HttpPost("{postId}/like")]
        [UserAuthorize]
        public async Task<IActionResult> LikePost(string postId, [FromQuery] string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest("userId is required");

            if (!IsCurrentUser(userId))
                return Forbid();

            var existingPost = await _postService.GetById(postId);
            if (existingPost == null)
                return NotFound("Post not found");

            await _postService.LikePost(postId, userId);
            return Ok(new { message = "Liked successfully" });
        }

        [HttpPost("{postId}/unlike")]
        [UserAuthorize]
        public async Task<IActionResult> UnlikePost(string postId, [FromQuery] string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest("userId is required");

            if (!IsCurrentUser(userId))
                return Forbid();

            var existingPost = await _postService.GetById(postId);
            if (existingPost == null)
                return NotFound("Post not found");

            await _postService.UnlikePost(postId, userId);
            return Ok(new { message = "Unliked successfully" });
        }

        [HttpGet("{postId}/liked")]
        [UserAuthorize]
        public async Task<IActionResult> IsLiked(string postId, [FromQuery] string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest("userId is required");

            if (!IsCurrentUser(userId))
                return Forbid();

            var isLiked = await _postService.IsLiked(postId, userId);
            return Ok(new { isLiked });
        }

        [HttpPost("upload-image")]
        [UserAuthorize]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("File không hợp lệ");

            await using var stream = file.OpenReadStream();

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "posts"
            };

            var result = await _cloudinary.UploadAsync(uploadParams);

            if (result.Error != null)
                return BadRequest(result.Error.Message);

            return Ok(new
            {
                imageUrl = result.SecureUrl.ToString(),
                publicId = result.PublicId
            });
        }

        [HttpPost("upload-video")]
        [UserAuthorize]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(100 * 1024 * 1024)]
        public async Task<IActionResult> UploadVideo(IFormFile file)
        {
            const long maxFileSize = 100 * 1024 * 1024;
            const double maxDurationSeconds = 30.0;

            if (file == null || file.Length == 0)
            {
                return BadRequest(new
                {
                    message = "Video không hợp lệ"
                });
            }

            if (file.Length > maxFileSize)
            {
                return BadRequest(new
                {
                    message = "Video không được vượt quá 100 MB"
                });
            }

            /*
             * Không giới hạn theo phần mở rộng như mp4, mov, mkv...
             * Chấp nhận mọi MIME type video.
             */
            if (string.IsNullOrWhiteSpace(file.ContentType) ||
                !file.ContentType.StartsWith(
                    "video/",
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = "File được chọn không phải là video"
                });
            }

            await using var stream = file.OpenReadStream();

            var uploadParams = new VideoUploadParams
            {
                File = new FileDescription(
                    file.FileName,
                    stream
                ),
                Folder = "posts/videos",
                UseFilename = false,
                UniqueFilename = true,
                Overwrite = false
            };

            var uploadResult = await _cloudinary.UploadAsync(
                uploadParams
            );

            if (uploadResult.Error != null)
            {
                return BadRequest(new
                {
                    message =
                        $"Cloudinary không hỗ trợ hoặc không thể xử lý video này: " +
                        uploadResult.Error.Message
                });
            }

            var duration = uploadResult.Duration;

            if (duration <= 0)
            {
                await DeleteCloudinaryVideo(uploadResult.PublicId);

                return BadRequest(new
                {
                    message = "Không thể xác định thời lượng video"
                });
            }

            if (duration > maxDurationSeconds)
            {
                await DeleteCloudinaryVideo(uploadResult.PublicId);

                return BadRequest(new
                {
                    message =
                        $"Video chỉ được dài tối đa 30 giây. " +
                        $"Video hiện tại dài {duration:F1} giây."
                });
            }

            /*
             * URL gốc có thể là MOV, MKV, AVI...
             * Tạo URL MP4 để Flutter dễ phát trên nhiều thiết bị hơn.
             */
            var playableVideoUrl = _cloudinary.Api.UrlVideoUp
                .Secure(true)
                .Transform(new Transformation()
                    .FetchFormat("mp4")
                    .Quality("auto"))
                .BuildUrl(uploadResult.PublicId);

            return Ok(new
            {
                videoUrl = playableVideoUrl,
                originalVideoUrl =
                    uploadResult.SecureUrl?.ToString() ?? "",
                videoPublicId =
                    uploadResult.PublicId ?? "",
                duration,
                bytes = uploadResult.Bytes,
                originalFormat =
                    uploadResult.Format ?? "",
                deliveryFormat = "mp4",
                width = uploadResult.Width,
                height = uploadResult.Height
            });
        }

        [HttpGet("paged")]
        [UserAuthorize]
        public async Task<IActionResult> GetPostsPaged(
        [FromQuery] int limit = 10,
        [FromQuery] DateTime? lastCreatedAt = null)
        {
            var posts = await _postService.GetPostsPaged(limit, lastCreatedAt);
            return Ok(posts);
        }

        private bool IsCurrentUser(string? uid)
        {
            return HttpContext.Items["UserUid"]?.ToString() == uid;
        }

        public class DeleteVideoRequest
        {
            public string PublicId { get; set; } = "";
        }

        [HttpDelete("video")]
        [UserAuthorize]
        public async Task<IActionResult> DeleteUploadedVideo(
        [FromBody] DeleteVideoRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.PublicId))
            {
                return BadRequest(new
                {
                    message = "PublicId không hợp lệ"
                });
            }

            // Chỉ cho xóa video trong thư mục bài viết.
            if (!request.PublicId.StartsWith(
                    "posts/videos/",
                    StringComparison.Ordinal))
            {
                return Forbid();
            }

            var result = await _cloudinary.DestroyAsync(
                new DeletionParams(request.PublicId)
                {
                    ResourceType = ResourceType.Video,
                    Invalidate = true
                }
            );

            return Ok(new
            {
                result = result.Result
            });
        }

        [HttpDelete("admin/permanent/{id}")]
        [AdminAuthorize]
        public async Task<IActionResult> PermanentDelete(string id)
        {
            var post = await _postService.GetById(id);

            if (post == null)
            {
                return NotFound(new
                {
                    message = "Không tìm thấy bài viết"
                });
            }

            if (!string.IsNullOrWhiteSpace(post.videoPublicId))
            {
                await _cloudinary.DestroyAsync(
                    new DeletionParams(post.videoPublicId)
                    {
                        ResourceType = ResourceType.Video,
                        Invalidate = true
                    }
                );
            }

            // Sau này bổ sung PermanentDelete vào service/repository.
            return NoContent();
        }

        private async Task DeleteCloudinaryVideo(string? publicId)
        {
            if (string.IsNullOrWhiteSpace(publicId))
            {
                return;
            }

            await _cloudinary.DestroyAsync(
                new DeletionParams(publicId)
                {
                    ResourceType = ResourceType.Video,
                    Invalidate = true
                }
            );
        }
    }
}
