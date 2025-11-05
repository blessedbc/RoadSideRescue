using Microsoft.AspNetCore.Mvc;
using RoadSideRescue.Dto;
using System.Threading.Tasks;

namespace RoadSideRescue.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        // In a real app inject IAuthService
        public AuthController() { }

        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequest req)
        {
            // TODO: validate, hash password, create User and optionally Agent row, return token
            return CreatedAtAction(nameof(Register), new AuthResponse { UserId = System.Guid.NewGuid(), Token = "stub-token" });
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest req)
        {
            // TODO: validate credentials and return JWT
            return Ok(new AuthResponse { UserId = System.Guid.NewGuid(), Token = "stub-token" });
        }
    }
}
