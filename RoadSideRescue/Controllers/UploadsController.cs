namespace RoadSideRescue.Controllers
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using System;
    using System.Threading.Tasks;

    [ApiController]
    [Route("api/[controller]")]
    public class UploadsController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("No file");

            // TODO: store file to blob storage and return public (or signed) URL
            var fakeUrl = $"https://example.blob/{Guid.NewGuid()}/{file.FileName}";
            await Task.CompletedTask;
            return Created(string.Empty, new { url = fakeUrl });
        }
    }
}
