using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;
using RoadSideRescue.Models;
using RoadSideRescue.Api.Services;

namespace RoadSideRescue.Services
{
    public class AuthService : IAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly IUserRepository _userRepo;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AuthService(IConfiguration configuration, IUserRepository userRepo, IPasswordHasher<User> passwordHasher)
        {
            _configuration = configuration;
            _userRepo = userRepo;
            _passwordHasher = passwordHasher;
        }

        /// <summary>
        /// Logs in a user by verifying email and password, returns JWT if successful.
        /// </summary>
        public async Task<string?> LoginAsync(string email, string password)
        {
            var user = await _userRepo.GetByEmailAsync(email);
            if (user == null)
                return null;

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (result == PasswordVerificationResult.Failed)
                return null;

            return GenerateJwtToken(user);
        }

        /// <summary>
        /// Registers a new user with hashed password.
        /// </summary>
        public async Task RegisterAsync(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email is required.", nameof(email));

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password is required.", nameof(password));

            // Check if user already exists
            var existingUser = await _userRepo.GetByEmailAsync(email);
            if (existingUser != null)
                throw new InvalidOperationException("A user with this email already exists.");

            // Create new user
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Role = 0, // default role
                CreatedAt = DateTime.UtcNow
            };

            // Hash password
            user.PasswordHash = _passwordHasher.HashPassword(user, password);

            // Save user to database
            await _userRepo.AddAsync(user);
        }

        /// <summary>
        /// Generates a JWT token for the given user.
        /// </summary>
        private string GenerateJwtToken(User user)
        {
            var jwtKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is missing");
            var jwtIssuer = _configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer is missing");
            var jwtAudience = _configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience is missing");
            var expiryHours = int.TryParse(_configuration["Jwt:ExpiryInHours"], out var h) ? h : 24;

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("role", user.Role.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(expiryHours),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}



