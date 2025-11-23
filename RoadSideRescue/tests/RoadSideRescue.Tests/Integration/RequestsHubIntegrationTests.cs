using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualStudio.Threading;
using Xunit;
using RoadSideRescue.Data;
using RoadSideRescue.Models;

namespace RoadSideRescue.Api.Tests.Integration
{
    public class RequestsHubIntegrationTests : IClassFixture<CustomWebAppFactory>
    {
        private readonly CustomWebAppFactory _factory;
        private readonly HttpClient _client;
        private const string JwtKey = "4fdwlCftUMFhq8dX+JC93fyYEdd5vl0jClahZB45vl0=";
        private const string Issuer = "RoadSideRescue";
        private const string Audience = "RoadSideRescueAudience";

        public RequestsHubIntegrationTests(CustomWebAppFactory factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        private string GenerateJwt(Guid userId, string role)
        {
            var keyBytes = Convert.FromBase64String(JwtKey);
            var creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
            var claims = new[]
            {
                new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, userId.ToString()),
                new System.Security.Claims.Claim("role", role)
            };
            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
            );
            return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
        }

        [Fact]
        public async Task AcceptRequest_ViaHub_AssignsAgentAndBroadcastsAsync()
        {
            // Arrange - seed owner, agent user, agent and request
            var ownerId = Guid.NewGuid();
            var agentUserId = Guid.NewGuid();
            Guid requestId;
            Guid agentId;

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Users.Add(new User { Id = ownerId, Email = "owner-hub@example.com", Name = "OwnerHub", Role = UserRole.Owner });
                db.Users.Add(new User { Id = agentUserId, Email = "agent-hub@example.com", Name = "AgentHub", Role = UserRole.Agent });

                var agent = new Agent { Id = Guid.NewGuid(), UserId = agentUserId, Status = AgentStatus.Online };
                db.Agents.Add(agent);
                agentId = agent.Id;

                var request = new Request { Id = Guid.NewGuid(), OwnerId = ownerId, Lat = -33.9, Lng = 18.4, Status = RequestStatus.Created, CreatedAt = DateTime.UtcNow };
                requestId = request.Id;
                db.Requests.Add(request);

                await db.SaveChangesAsync();
            }

            var token = GenerateJwt(agentUserId, "Agent");

            // Build HubConnection to test server
            var hubUrl = new Uri(_client.BaseAddress!, $"/hubs/requests?access_token={token}");
            var connection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    // Route requests to test server
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                })
                .Build();

            var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

            connection.On<object>("RequestAssigned", payload =>
            {
                var json = JsonSerializer.SerializeToElement(payload);
                tcs.TrySetResult(json);
            });

            await connection.StartAsync();

            // Act - call hub method
            await connection.InvokeAsync("AcceptRequest", requestId, agentId);

            // Assert - wait for broadcast
            var received = await tcs.Task.TimeoutAfterAsync(TimeSpan.FromSeconds(5));
            Assert.True(received.TryGetProperty("id", out var idProp));
            Assert.Equal(requestId.ToString(), idProp.GetString());

            // Verify DB state
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var req = await db.Requests.FirstOrDefaultAsync(r => r.Id == requestId);
                Assert.NotNull(req);
                Assert.Equal(RequestStatus.Assigned, req.Status);
                Assert.Equal(agentId, req.AssignedAgentId);
            }

            await connection.StopAsync();
            await connection.DisposeAsync();
        }

        [Fact]
        public async Task SendMessage_ViaHub_PersistsAndBroadcastsAsync()
        {
            // Arrange - seed owner and request
            var ownerId = Guid.NewGuid();
            Guid requestId;

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Users.Add(new User { Id = ownerId, Email = "owner-msg@example.com", Name = "OwnerMsg", Role = UserRole.Owner });

                var request = new Request { Id = Guid.NewGuid(), OwnerId = ownerId, Lat = -33.9, Lng = 18.4, Status = RequestStatus.Created, CreatedAt = DateTime.UtcNow };
                requestId = request.Id;
                db.Requests.Add(request);

                await db.SaveChangesAsync();
            }

            var token = GenerateJwt(ownerId, "Owner");

            var hubUrl = new Uri(_client.BaseAddress!, $"/hubs/requests?access_token={token}");
            var connection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                })
                .Build();

            var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

            connection.On<object>("NewMessage", payload =>
            {
                var json = JsonSerializer.SerializeToElement(payload);
                tcs.TrySetResult(json);
            });

            await connection.StartAsync();

            var text = "Help from hub";

            // Act
            await connection.InvokeAsync("SendMessage", requestId, ownerId, text);

            // Assert - wait for broadcast
            var received = await tcs.Task.TimeoutAfterAsync(TimeSpan.FromSeconds(5));
            Assert.True(received.TryGetProperty("text", out var textProp));
            Assert.Equal(text, textProp.GetString());

            // Verify DB state
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var msg = await db.Messages.FirstOrDefaultAsync(m => m.RequestId == requestId && m.Text == text);
                Assert.NotNull(msg);
                Assert.Equal(ownerId, msg.FromUserId);
            }

            await connection.StopAsync();
            await connection.DisposeAsync();
        }
    }

    // small helper to add timeout support to TaskCompletionSource awaits
    #pragma warning disable VSTHRD003
    internal static class TaskExtensions
    {
        public static async Task<T> TimeoutAfterAsync<T>(this Task<T> task, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            var tcs = new TaskCompletionSource<bool>();
            using (cts.Token.Register(s => ((TaskCompletionSource<bool>)s!).TrySetResult(true), tcs))
            {
                if (task.IsCompleted)
                    return await task.ConfigureAwait(false); // already completed

                var completedTask = await Task.WhenAny(task, tcs.Task).ConfigureAwait(false);
                if (completedTask == task)
                    return await task.ConfigureAwait(false);

                throw new TimeoutException("The operation has timed out.");
    #pragma warning restore VSTHRD003
            }
        }
    }
}
