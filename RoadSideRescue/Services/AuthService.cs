using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;
using RoadSideRescue.Models;
using RoadSideRescue.Api.Services;
using RoadSideRescue.Services.Interfaces;
using RoadSideRescue.Dto;

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

        public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.Email))
                throw new ArgumentException("Email is required.", nameof(request.Email));

            if (string.IsNullOrWhiteSpace(request.Password))
                throw new ArgumentException("Password is required.", nameof(request.Password));

            var existingUser = await _userRepo.GetByEmailAsync(request.Email);
            if (existingUser != null)
                throw new InvalidOperationException("A user with this email already exists.");

            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Email = request.Email,
                Phone = request.Phone,
                Role = request.Role,
                RatingAverage = 0,
                CreatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            await _userRepo.AddAsync(user);

            var token = GenerateJwtToken(user);

            return new AuthResponse
            {
                UserId = user.Id,
                Token = token
            };
        }

        private string GenerateJwtToken(User user)
        {
            var jwtKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is missing");
            var jwtIssuer = _configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer is missing");
            var jwtAudience = _configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience is missing");
            var expiryHours = int.TryParse(_configuration["Jwt:ExpiryInHours"], out var h) ? h : 24;

            // Try base64 decode first (matches Program.AddJwtAuthentication behavior)
            byte[] keyBytes;
            if (TryBase64Decode(jwtKey, out var decoded) && decoded != null && decoded.Length >= 1)
            {
                keyBytes = decoded;
            }
            else
            {
                keyBytes = Encoding.UTF8.GetBytes(jwtKey);
            }

            if (keyBytes.Length < 16)
            {
                throw new InvalidOperationException(
                    $"JWT Key must be at least 128 bits (16 bytes). Configured key length (bytes): {keyBytes.Length}. " +
                    "If you intended to use a base64 key, provide a base64-encoded value that decodes to >=16 bytes.");
            }

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("role", user.Role.ToString())
            };

            var signingKey = new SymmetricSecurityKey(keyBytes);
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(expiryHours),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static bool TryBase64Decode(string s, out byte[] bytes)
        {
            try
            {
                bytes = Convert.FromBase64String(s);
                return true;
            }
            catch
            {
                bytes = Array.Empty<byte>();
                return false;
            }
        }
    }
}



