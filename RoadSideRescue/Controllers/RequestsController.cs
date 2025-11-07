using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RoadSideRescue.Data;
using RoadSideRescue.Dto;
using RoadSideRescue.Hubs;
using RoadSideRescue.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RoadSideRescue.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RequestsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<RequestsHub> _hubContext;

        public RequestsController(ApplicationDbContext db, IHubContext<RequestsHub> hubContext)
        {
            _db = db;
            _hubContext = hubContext;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateRequestDto dto)
        {
            if (dto == null) return BadRequest("Request data is missing.");

            var ownerId = Guid.NewGuid(); // TODO: replace with authenticated user id
            var request = new Request
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                Lat = dto.Lat,
                Lng = dto.Lng,
                Address = dto.Address,
                Description = dto.Description,
                VehicleType = dto.VehicleType,
                Photos = dto.Photos ?? new List<string>(),
                CreatedAt = DateTime.UtcNow,
                Status = RequestStatus.Created
            };

            await _db.Requests.AddAsync(request);
            await _db.SaveChangesAsync();

            // Add owner to SignalR group for this request
            await _hubContext.Groups.AddToGroupAsync(ownerId.ToString(), request.Id.ToString());

            // Broadcast to all agents (or optionally to a matching service)
            await _hubContext.Clients.All.SendAsync("RequestCreated", new
            {
                request.Id,
                request.Status,
                request.Lat,
                request.Lng
            });

            return CreatedAtAction(nameof(Get), new { id = request.Id }, new { request.Id, request.Status });
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var request = await _db.Requests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
            if (request == null) return NotFound();

            return Ok(new RequestSummaryDto
            {
                Id = request.Id,
                Status = request.Status.ToString(),
                Lat = request.Lat,
                Lng = request.Lng,
                Address = request.Address,
                Description = request.Description,
                VehicleType = request.VehicleType,
                Photos = request.Photos
            });
        }
    }
}








//using Microsoft.AspNetCore.Mvc;
//using RoadSideRescue.Dto;
//using RoadSideRescue.Models;
//using System;
//using System.Threading.Tasks;

//namespace RoadSideRescue.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public class RequestsController : ControllerBase
//    {
//        // TODO: inject ApplicationDbContext, matching service, signalR hub context
//        public RequestsController() { }

//        [HttpPost]
//        public IActionResult Create([FromBody] CreateRequestDto dto)
//        {
//            // TODO: persist request, trigger matching/broadcast
//            var request = new Request
//            {
//                Id = Guid.NewGuid(),
//                OwnerId = Guid.NewGuid(), // replace with authenticated user id
//                Lat = dto.Lat,
//                Lng = dto.Lng,
//                Address = dto.Address,
//                Description = dto.Description,
//                Photos = dto.Photos ?? Array.Empty<string>(),
//                CreatedAt = DateTime.UtcNow,
//                Status = RequestStatus.Created
//            };

//            return CreatedAtAction(nameof(Get), new { id = request.Id }, new { request.Id, request.Status });
//        }

//        [HttpGet("{id:guid}")]
//        public IActionResult Get(Guid id)
//        {
//            // TODO: load from DB; return 404 if not found
//            return Ok(new { id, status = "Created" });
//        }
//    }
//}
