using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RoadSideRescue.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // require authenticated users to upload
    public class UploadsController : ControllerBase
    {
        private readonly ILogger<UploadsController> _logger;

        public UploadsController(ILogger<UploadsController> logger)
        {
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> UploadAsync([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded" });

            try
            {
                // Replace this with real blob storage upload logic in production.
                var fileUrl = $"https://example.blob/{Guid.NewGuid()}/{file.FileName}";

                await Task.CompletedTask;

                return Created(fileUrl, new { url = fileUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File upload failed for user {User}", User?.Identity?.Name ?? "anonymous");
                return StatusCode(500, new { message = "An internal server error occurred." });
            }
        }
    }
}

