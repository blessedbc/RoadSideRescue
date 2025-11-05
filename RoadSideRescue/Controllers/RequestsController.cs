using Microsoft.AspNetCore.Mvc;
using RoadSideRescue.Dto;
using RoadSideRescue.Models;
using System;
using System.Threading.Tasks;

namespace RoadSideRescue.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RequestsController : ControllerBase
    {
        // TODO: inject ApplicationDbContext, matching service, signalR hub context
        public RequestsController() { }

        [HttpPost]
        public IActionResult Create([FromBody] CreateRequestDto dto)
        {
            // TODO: persist request, trigger matching/broadcast
            var request = new Request
            {
                Id = Guid.NewGuid(),
                OwnerId = Guid.NewGuid(), // replace with authenticated user id
                Lat = dto.Lat,
                Lng = dto.Lng,
                Address = dto.Address,
                Description = dto.Description,
                Photos = dto.Photos ?? Array.Empty<string>(),
                CreatedAt = DateTime.UtcNow,
                Status = RequestStatus.Created
            };

            return CreatedAtAction(nameof(Get), new { id = request.Id }, new { request.Id, request.Status });
        }

        [HttpGet("{id:guid}")]
        public IActionResult Get(Guid id)
        {
            // TODO: load from DB; return 404 if not found
            return Ok(new { id, status = "Created" });
        }
    }
}
