using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RoadSideRescue.Data;
using RoadSideRescue.Dto;
using RoadSideRescue.Hubs;
using RoadSideRescue.Models;

namespace RoadSideRescue.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RequestsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<RequestsHub> _hub;

        public RequestsController(ApplicationDbContext db, IHubContext<RequestsHub> hub)
        {
            _db = db;
            _hub = hub;
        }

        // POST api/requests
        // Requires authentication so OwnerId is taken from the JWT 'sub' claim.
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateAsync([FromBody] CreateRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var ownerId))
                return Unauthorized(new { message = "Unable to determine authenticated user id." });

            var request = new Request
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                Status = RequestStatus.Created,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Lat = dto.Lat,
                Lng = dto.Lng,
                Address = dto.Address,
                Description = dto.Description,
                VehicleType = dto.VehicleType,
                Photos = dto.Photos ?? new List<string>()
            };

            _db.Requests.Add(request);
            await _db.SaveChangesAsync();

            // Broadcast minimal request info to SignalR clients
            await _hub.Clients.All.SendAsync("RequestCreated", new
            {
                Id = request.Id,
                Status = request.Status.ToString(),
                Lat = request.Lat,
                Lng = request.Lng
            });

            return CreatedAtAction(nameof(GetByIdAsync), new { id = request.Id }, new { id = request.Id, status = request.Status.ToString() });
        }

        // GET api/requests/{id}
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty)
                return BadRequest(new { message = "Invalid id" });

            var req = await _db.Requests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
            if (req == null)
                return NotFound();

            var dto = new RequestSummaryDto
            {
                Id = req.Id,
                Status = req.Status.ToString(),
                Lat = req.Lat,
                Lng = req.Lng,
                Address = req.Address,
                Description = req.Description,
                VehicleType = req.VehicleType,
                Photos = req.Photos ?? new List<string>()
            };

            return Ok(dto);
        }
    }
}