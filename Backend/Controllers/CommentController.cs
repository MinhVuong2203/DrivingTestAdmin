using Backend.Models;
using Backend.Service.Interface;
using Backend.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommentController : ControllerBase
    {
        private readonly ICommentService _commentService;

        public CommentController(ICommentService commentService)
        {
            _commentService = commentService;
        }

        [HttpGet("{postId}")]
        [UserAuthorize]
        public async Task<IActionResult> GetByPostId(string postId)
        {
            var comments = await _commentService.GetByPostId(postId);
            return Ok(comments);
        }

        [HttpGet("admin/{postId}")]
        [AdminAuthorize]
        public async Task<IActionResult> GetByPostIdForAdmin(string postId)
        {
            var comments = await _commentService.GetByPostId(postId);
            return Ok(comments);
        }

        [HttpPost("{postId}")]
        [UserAuthorize]
        public async Task<IActionResult> Create(
            string postId,
            [FromBody] Comment comment)
        {
            try
            {
                if (HttpContext.Items["UserUid"]?.ToString() != comment.authorId)
                {
                    return Forbid();
                }

                var result = await _commentService.Create(postId, comment);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{postId}/{commentId}")]
        [UserAuthorize]
        public async Task<IActionResult> Delete(
            string postId,
            string commentId,
            [FromQuery] string currentUserId,
            [FromQuery] bool isAdmin = false)
        {
            try
            {
                if (HttpContext.Items["UserUid"]?.ToString() != currentUserId)
                {
                    return Forbid();
                }

                var result = await _commentService.Delete(
                    postId,
                    commentId,
                    currentUserId,
                    false
                );

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
