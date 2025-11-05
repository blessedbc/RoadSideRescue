using Microsoft.AspNetCore.Mvc;
using RoadSideRescue.Dto;
using System;

namespace RoadSideRescue.Controllers
{
    [ApiController]
    [Route("api/agent")]
    public class AgentsController : ControllerBase
    {
        public AgentsController() { }

        [HttpPost("availability")]
        public IActionResult SetAvailability([FromBody] object payload)
        {
            // Expected payload: { online: true, lat: ..., lng: ... }
            // TODO: update agent presence store
            return Ok(new { success = true });
        }

        [HttpPost("{requestId:guid}/accept")]
        public IActionResult Accept(Guid requestId)
        {
            // TODO: attempt DB transaction to assign request; return 409 if race
            return Ok(new { assigned = true });
        }
    }
}
