using Backend.Service;
using Backend.Service.Interface;
using Backend.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

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
            var result = await _postService.Create(post);
            return Ok(result);
        }

        [HttpPut("{id}")]
        [UserAuthorize]
        public async Task<IActionResult> Update(string id, Post post)
        {
            var existingPost = await _postService.GetById(id);
            if (existingPost == null)
            {
                return NotFound();
            }

            await _postService.Update(id, post);
            return NoContent();
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
    }
}
