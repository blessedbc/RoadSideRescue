using System;
using RoadSideRescue.Models;

namespace RoadSideRescue.Dto
{
    public class RegisterRequest
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Password { get; set; } = "";
        public UserRole Role { get; set; } = UserRole.Owner;
    }

    public class LoginRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class AuthResponse
    {
        public Guid UserId { get; set; }
        public string Token { get; set; } = "";
    }
}
