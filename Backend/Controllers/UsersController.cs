using Backend.Service;
using Backend.Service.Interface;
using Backend.Filters;
using Backend.DTO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AdminAuthorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _service;

        public UsersController(IUserService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _service.GetAll());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var user = await _service.GetById(id);
            if (user == null) return NotFound();
            return Ok(user);
        }

        [HttpPost]
        public async Task<IActionResult> Create(User user)
        {
            await _service.Create(user);
            return Ok();
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, User user)
        {
            await _service.Update(id, user);
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            await _service.Delete(id);
            return Ok();
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateUserStatusRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Status))
            {
                return BadRequest("Status is required.");
            }

            if (request.LockDays is <= 0)
            {
                return BadRequest("LockDays must be greater than 0.");
            }

            var status = request.Status.Trim().ToLowerInvariant();
            if (status is not ("active" or "locked"))
            {
                return BadRequest("Status must be active or locked.");
            }

            await _service.UpdateStatus(id, status, request.LockDays);
            return Ok();
        }


    }
}
