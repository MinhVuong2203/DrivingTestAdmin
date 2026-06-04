using Backend.DTO;
using Backend.Filters;
using Backend.Service.Interface;
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
        public async Task<IActionResult> GetAll([FromQuery] UserPageRequest request)
        {
            return Ok(await _service.GetPage(request));
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
            if (IsAdminRole(user.role) && !CurrentAdminIsImportant())
            {
                return ForbiddenAdminAction();
            }

            await _service.Create(user);
            return Ok();
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, User user)
        {
            if (!await CanManageTargetAdmin(id) || (IsAdminRole(user.role) && !CurrentAdminIsImportant()))
            {
                return ForbiddenAdminAction();
            }

            await _service.Update(id, user);
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            if (!await CanManageTargetAdmin(id))
            {
                return ForbiddenAdminAction();
            }

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

            if (!await CanManageTargetAdmin(id))
            {
                return ForbiddenAdminAction();
            }

            await _service.UpdateStatus(id, status, request.LockDays);
            return Ok();
        }

        [HttpPatch("{id}/role")]
        public async Task<IActionResult> UpdateRole(string id, [FromBody] UpdateUserRoleRequest request)
        {
            if (!CurrentAdminIsImportant())
            {
                return ForbiddenAdminAction();
            }

            if (string.IsNullOrWhiteSpace(request.Role))
            {
                return BadRequest("Role is required.");
            }

            var role = request.Role.Trim().ToLowerInvariant();
            if (role is not ("admin" or "user"))
            {
                return BadRequest("Role must be admin or user.");
            }

            var target = await _service.GetById(id);
            if (target == null)
            {
                return NotFound();
            }

            if (target.uid == CurrentAdminUid() && target.isImportant && role != "admin")
            {
                return BadRequest("Root admin cannot demote itself.");
            }

            await _service.UpdateRole(id, role);
            return Ok();
        }

        private async Task<bool> CanManageTargetAdmin(string id)
        {
            if (CurrentAdminIsImportant())
            {
                return true;
            }

            var target = await _service.GetById(id);
            return target == null || !IsAdminRole(target.role);
        }

        private bool CurrentAdminIsImportant()
        {
            return HttpContext.Items["AdminIsImportant"] is bool isImportant && isImportant;
        }

        private string? CurrentAdminUid()
        {
            return HttpContext.Items["AdminUid"]?.ToString();
        }

        private static bool IsAdminRole(string? role)
        {
            return string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase);
        }

        private ObjectResult ForbiddenAdminAction()
        {
            return new ObjectResult(new
            {
                message = "Chi admin goc moi duoc thao tac tren admin khac."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
