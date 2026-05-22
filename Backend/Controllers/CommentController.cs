using Backend.Models;
using Backend.Service.Interface;
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
        public async Task<IActionResult> GetByPostId(string postId)
        {
            var comments = await _commentService.GetByPostId(postId);
            return Ok(comments);
        }

        [HttpPost("{postId}")]
        public async Task<IActionResult> Create(
            string postId,
            [FromBody] Comment comment)
        {
            try
            {
                var result = await _commentService.Create(postId, comment);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{postId}/{commentId}")]
        public async Task<IActionResult> Delete(
            string postId,
            string commentId,
            [FromQuery] string currentUserId,
            [FromQuery] bool isAdmin = false)
        {
            try
            {
                var result = await _commentService.Delete(
                    postId,
                    commentId,
                    currentUserId,
                    isAdmin
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