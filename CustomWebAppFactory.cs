csharp RoadSideRescue\tests\RoadSideRescue.Tests\Integration\AgentIntegrationTests.cs
using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;
using RoadSideRescue.Data;
using RoadSideRescue.Models;
using System.Collections.Generic;

namespace RoadSideRescue.Api.Tests.Integration
{
    public class CustomWebAppFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Replace ApplicationDbContext with InMemory for tests
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb");
                });

                // Build service provider and ensure DB created
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Database.EnsureDeleted();
                db.Database.EnsureCreated();
            });
        }
    }

    public class AgentIntegrationTests : IClassFixture<CustomWebAppFactory>
    {
        private readonly CustomWebAppFactory _factory;
        private readonly HttpClient _client;
        private const string JwtKey = "4fdwlCftUMFhq8dX+JC93fyYEdd5vl0jClahZB45vl0=";
        private const string Issuer = "RoadSideRescue";
        private const string Audience = "RoadSideRescueAudience";

        public AgentIntegrationTests(CustomWebAppFactory factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        private string GenerateJwt(Guid userId, string role)
        {
            var keyBytes = Encoding.UTF8.GetBytes(JwtKey);
            var creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim("role", role)
            };
            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
            );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [Fact]
        public async Task SetAvailability_CreatesAgent_ForAgentUser()
        {
            // Arrange - seed a user with Agent role
            var userId = Guid.NewGuid();
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Users.Add(new User { Id = userId, Email = "agent1@example.com", Name = "Agent One", Role = UserRole.Agent });
                await db.SaveChangesAsync();
            }

            var token = GenerateJwt(userId, "Agent");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var payload = new
            {
                online = true,
                lat = -33.9,
                lng = 18.4,
                serviceRadiusMeters = 3000,
                skills = new[] { "Towing" }
            };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            // Act
            var res = await _client.PostAsync("/api/agent/availability", content);

            // Assert
            res.EnsureSuccessStatusCode();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var agent = await db.Agents.FirstOrDefaultAsync(a => a.UserId == userId);
                Assert.NotNull(agent);
                Assert.Equal(AgentStatus.Online, agent.Status);
                Assert.Equal(3000, agent.ServiceRadiusMeters);
            }
        }

        [Fact]
        public async Task Accept_AssignsAgent_ToRequest()
        {
            // Arrange - seed owner, request, agent and agent user
            var ownerId = Guid.NewGuid();
            var agentUserId = Guid.NewGuid();
            Guid requestId;

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Users.Add(new User { Id = ownerId, Email = "owner@example.com", Name = "Owner", Role = UserRole.Owner });
                db.Users.Add(new User { Id = agentUserId, Email = "agent2@example.com", Name = "Agent Two", Role = UserRole.Agent });

                var agent = new Agent { Id = Guid.NewGuid(), UserId = agentUserId, Status = AgentStatus.Online };
                db.Agents.Add(agent);

                var request = new Request { Id = Guid.NewGuid(), OwnerId = ownerId, Lat = -33.9, Lng = 18.4, Status = RequestStatus.Created, CreatedAt = DateTime.UtcNow };
                requestId = request.Id;
                db.Requests.Add(request);

                await db.SaveChangesAsync();
            }

            var token = GenerateJwt(agentUserId, "Agent");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            var res = await _client.PostAsync($"/api/agent/{requestId}/accept", null);

            // Assert
            res.EnsureSuccessStatusCode();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var request = await db.Requests.FirstOrDefaultAsync(r => r.Id == requestId);
                Assert.NotNull(request);
                Assert.NotNull(request.AssignedAgentId);
                var agent = await db.Agents.FirstOrDefaultAsync(a => a.Id == request.AssignedAgentId);
                Assert.NotNull(agent);
                Assert.Equal(agent.UserId, agentUserId);
                Assert.Equal(RequestStatus.Assigned, request.Status);
            }
        }
    }
}