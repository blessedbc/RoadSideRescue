using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RoadSideRescue.Data;
using RoadSideRescue.Dto;
using RoadSideRescue.Hubs;
using RoadSideRescue.Models;
using RoadSideRescue.Services.Interfaces;
using System.Security.Claims;
using System.Text.Json;

namespace RoadSideRescue.Controllers
{
    [ApiController]
    [Route("api/agent")]
    [Authorize(Policy = "AgentOnly")] // limit access to users with role claim "Agent"
    public class AgentsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<RequestsHub> _hubContext;
        private readonly IRequestService _requestService;
        private readonly ILogger<AgentsController> _logger;

        public AgentsController(ApplicationDbContext db, IHubContext<RequestsHub> hubContext, IRequestService requestService, ILogger<AgentsController> logger)
        {
            _db = db;
            _hubContext = hubContext;
            _requestService = requestService;
            _logger = logger;
        }

        // POST api/agent/availability
        [HttpPost("availability")]
        public async Task<IActionResult> SetAvailabilityAsync([FromBody] AgentAvailabilityRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
                return Unauthorized(new { message = "Unable to determine authenticated user id." });

            try
            {
                var agent = await _db.Agents.FirstOrDefaultAsync(a => a.UserId == userId);
                if (agent == null)
                {
                    agent = new Agent
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId
                    };
                    _db.Agents.Add(agent);
                }

                agent.Status = request.Online ? AgentStatus.Online : AgentStatus.Offline;
                agent.CurrentLat = request.Lat;
                agent.CurrentLng = request.Lng;
                agent.LastSeenAt = DateTime.UtcNow;
                if (request.ServiceRadiusMeters.HasValue)
                    agent.ServiceRadiusMeters = request.ServiceRadiusMeters.Value;
                if (request.Skills != null)
                    agent.SkillsJson = JsonSerializer.Serialize(request.Skills);

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    agent = new
                    {
                        agent.Id,
                        agent.UserId,
                        Status = agent.Status.ToString(),
                        agent.CurrentLat,
                        agent.CurrentLng,
                        agent.ServiceRadiusMeters
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update availability for user {UserId}", sub);
                return StatusCode(500, new { message = "An internal server error occurred." });
            }
        }

        // POST api/agent/{requestId}/accept
        [HttpPost("{requestId:guid}/accept")]
        public async Task<IActionResult> AcceptAsync(Guid requestId)
        {
            if (requestId == Guid.Empty)
                return BadRequest(new { message = "Invalid request ID" });

            var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
                return Unauthorized(new { message = "Unable to determine authenticated user id." });

            try
            {
                // find agent profile for authenticated user
                var agent = await _db.Agents.FirstOrDefaultAsync(a => a.UserId == userId);
                if (agent == null)
                    return BadRequest(new { message = "Agent profile not found for authenticated user." });

                // delegate assignment to the service
                var request = await _requestService.AssignAgentAsync(requestId, agent.Id);
                if (request == null)
                    return NotFound(new { message = "Request not found." });

                // Notify the request group (owner + assigned agent should be in the group client-side)
                await _hubContext.Clients.Group(requestId.ToString()).SendAsync("RequestAssigned", new
                {
                    request.Id,
                    request.Status,
                    request.AssignedAgentId
                });

                return Ok(new { assigned = true, assignedAgentId = agent.Id });
            }
            catch (InvalidOperationException ex)
            {
                // domain error - return 409 as before
                _logger.LogInformation(ex, "Conflict while assigning request {RequestId} by agent {AgentId}", requestId, sub);
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to accept request {RequestId} by user {UserId}", requestId, sub);
                return StatusCode(500, new { message = "An internal server error occurred." });
            }
        }
    }
}