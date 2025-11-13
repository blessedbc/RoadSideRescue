using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Moq;
using RoadSideRescue.Api.Services;
using RoadSideRescue.Models;
using RoadSideRescue.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace RoadSideRescue.Api.Tests.Auth
{
    public class AuthServiceTests
    {
        private readonly AuthService _authService;
        private readonly Mock<IUserRepository> _mockUserRepo;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AuthServiceTests()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Jwt:Key", "test-secret-key-12345678901234567890123456789012" },
                    { "Jwt:Issuer", "TestIssuer" },
                    { "Jwt:Audience", "TestAudience" }
                })
                .Build();

            _mockUserRepo = new Mock<IUserRepository>();
            _passwordHasher = new PasswordHasher<User>();

            _authService = new AuthService(config, _mockUserRepo.Object, _passwordHasher);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturn_Token_WhenCredentialsValid()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                PasswordHash = _passwordHasher.HashPassword(null!, "password"),
                Role = 0
            };

            _mockUserRepo.Setup(r => r.GetByEmailAsync("test@example.com"))
                         .ReturnsAsync(user);

            // Act
            var token = await _authService.LoginAsync("test@example.com", "password");

            // Assert
            Assert.NotNull(token);
            Assert.Contains(".", token!);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturn_Null_WhenPasswordInvalid()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                PasswordHash = _passwordHasher.HashPassword(null!, "password"),
                Role = 0
            };

            _mockUserRepo.Setup(r => r.GetByEmailAsync("test@example.com"))
                         .ReturnsAsync(user);

            // Act
            var token = await _authService.LoginAsync("test@example.com", "wrongpassword");

            // Assert
            Assert.Null(token);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturn_Null_WhenUserNotFound()
        {
            // Arrange
            _mockUserRepo.Setup(r => r.GetByEmailAsync("notfound@example.com"))
                         .ReturnsAsync((User?)null);

            // Act
            var token = await _authService.LoginAsync("notfound@example.com", "password");

            // Assert
            Assert.Null(token);
        }
    }
}


