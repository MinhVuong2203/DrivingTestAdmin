using Backend.Service;
using Backend.Service.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostController : ControllerBase
    {
        private readonly IPostService _postService;

        public PostController(IPostService postService)
        {
            _postService = postService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _postService.GetAll());
        }

        [HttpGet("{id}")]
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
        public async Task<IActionResult> GetByAuthorId(string authorId)
        {
            var posts = await _postService.GetByAuthorID(authorId);
            if (posts == null || posts.Count == 0)
            {
                return NotFound();
            }
            return Ok(posts);
        }

        [HttpPost]
        public async Task<IActionResult> Create(Post post)
        {
            await _postService.Create(post);
            return Ok();
        }

        [HttpPut("{id}")]
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

    }
}
