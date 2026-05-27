using Backend.Models;
using Backend.Service.Interface;
using Backend.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VipPackageController : ControllerBase
    {
        private readonly IVipPackageService _service;

        public VipPackageController(IVipPackageService service)
        {
            _service = service;
        }

        // GET: api/VipPackage
        [HttpGet]
        [AdminAuthorize]
        public async Task<ActionResult<List<VipPackage>>> GetAll()
        {
            try
            {
                var packages = await _service.GetAllPackagesAsync();
                return Ok(packages);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách gói VIP", error = ex.Message });
            }
        }

        // GET: api/VipPackage/active
        [HttpGet("active")]
        public async Task<ActionResult<List<VipPackage>>> GetActivePackages()
        {
            try
            {
                var packages = await _service.GetActivePackagesAsync();
                return Ok(packages);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách gói VIP active", error = ex.Message });
            }
        }

        // GET: api/VipPackage/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<VipPackage>> GetById(string id)
        {
            try
            {
                var package = await _service.GetPackageByIdAsync(id);
                if (package == null)
                    return NotFound(new { message = "Không tìm thấy gói VIP" });

                return Ok(package);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy thông tin gói VIP", error = ex.Message });
            }
        }

        // POST: api/VipPackage
        [HttpPost]
        [AdminAuthorize]
        public async Task<ActionResult<VipPackage>> Create([FromBody] VipPackage package)
        {
            try
            {
                var created = await _service.CreatePackageAsync(package);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi tạo gói VIP", error = ex.Message });
            }
        }

        // PUT: api/VipPackage/{id}
        [HttpPut("{id}")]
        [AdminAuthorize]
        public async Task<ActionResult> Update(string id, [FromBody] VipPackage package)
        {
            try
            {
                var result = await _service.UpdatePackageAsync(id, package);
                if (!result)
                    return NotFound(new { message = "Không tìm thấy gói VIP để cập nhật" });

                return Ok(new { message = "Cập nhật gói VIP thành công" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi cập nhật gói VIP", error = ex.Message });
            }
        }

        // DELETE: api/VipPackage/{id}
        [HttpDelete("{id}")]
        [AdminAuthorize]
        public async Task<ActionResult> Delete(string id)
        {
            try
            {
                var result = await _service.DeletePackageAsync(id);
                if (!result)
                    return NotFound(new { message = "Không tìm thấy gói VIP để xóa" });

                return Ok(new { message = "Xóa gói VIP thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi xóa gói VIP", error = ex.Message });
            }
        }

        // GET: api/VipPackage/search?keyword=abc
        [HttpGet("search")]
        [AdminAuthorize]
        public async Task<ActionResult<List<VipPackage>>> Search([FromQuery] string keyword)
        {
            try
            {
                var packages = await _service.SearchPackagesAsync(keyword);
                return Ok(packages);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi tìm kiếm gói VIP", error = ex.Message });
            }
        }

        // PATCH: api/VipPackage/{id}/toggle-active
        [HttpPatch("{id}/toggle-active")]
        [AdminAuthorize]
        public async Task<ActionResult> ToggleActive(string id)
        {
            try
            {
                var result = await _service.ToggleActiveStatusAsync(id);
                if (!result)
                    return NotFound(new { message = "Không tìm thấy gói VIP" });

                return Ok(new { message = "Cập nhật trạng thái thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi cập nhật trạng thái", error = ex.Message });
            }
        }
    }
}
