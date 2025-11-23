using Microsoft.EntityFrameworkCore;
using RoadSideRescue.Data;
using RoadSideRescue.Models;
using RoadSideRescue.Services;
using Xunit;

namespace RoadSideRescue.Api.Tests.Services
{
    public class RequestServiceTests
    {
        private static ApplicationDbContext CreateInMemoryContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task AssignAgentAsync_AssignsAgent_WhenRequestAvailableAsync()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateInMemoryContext(dbName);

            var requestId = Guid.NewGuid();
            var request = new Request
            {
                Id = requestId,
                OwnerId = Guid.NewGuid(),
                Status = RequestStatus.Created,
                CreatedAt = DateTime.UtcNow
            };
            context.Requests.Add(request);

            var agentId = Guid.NewGuid();
            var agent = new Agent
            {
                Id = agentId,
                UserId = Guid.NewGuid(),
                Status = AgentStatus.Online
            };
            context.Agents.Add(agent);

            await context.SaveChangesAsync();

            var service = new RequestService(context);

            var assigned = await service.AssignAgentAsync(requestId, agentId);

            Assert.NotNull(assigned);
            Assert.Equal(agentId, assigned.AssignedAgentId);
            Assert.Equal(RequestStatus.Assigned, assigned.Status);
        }

        [Fact]
        public async Task AssignAgentAsync_Throws_WhenAlreadyAssignedAsync()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateInMemoryContext(dbName);

            var existingAgentId = Guid.NewGuid();
            var request = new Request
            {
                Id = Guid.NewGuid(),
                OwnerId = Guid.NewGuid(),
                Status = RequestStatus.Assigned,
                AssignedAgentId = existingAgentId,
                CreatedAt = DateTime.UtcNow
            };
            context.Requests.Add(request);
            await context.SaveChangesAsync();

            var service = new RequestService(context);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAgentAsync(request.Id, Guid.NewGuid()));
        }

        [Fact]
        public async Task AddMessageAsync_PersistsMessageAsync()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateInMemoryContext(dbName);

            var requestId = Guid.NewGuid();
            var request = new Request
            {
                Id = requestId,
                OwnerId = Guid.NewGuid(),
                Status = RequestStatus.Created,
                CreatedAt = DateTime.UtcNow
            };
            context.Requests.Add(request);
            await context.SaveChangesAsync();

            var service = new RequestService(context);

            var fromUserId = Guid.NewGuid();
            var text = "Help needed";
            var msg = await service.AddMessageAsync(requestId, fromUserId, text);

            Assert.NotNull(msg);
            Assert.Equal(requestId, msg.RequestId);
            Assert.Equal(fromUserId, msg.FromUserId);
            Assert.Equal(text, msg.Text);

            var persisted = await context.Messages.FirstOrDefaultAsync(m => m.Id == msg.Id);
            Assert.NotNull(persisted);
        }

        [Fact]
        public async Task UpdateAgentLocationAsync_UpdatesCoordinatesAsync()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateInMemoryContext(dbName);

            var agentId = Guid.NewGuid();
            var agent = new Agent
            {
                Id = agentId,
                UserId = Guid.NewGuid(),
                Status = AgentStatus.Online
            };
            context.Agents.Add(agent);
            await context.SaveChangesAsync();

            var service = new RequestService(context);

            var newLat = -33.9;
            var newLng = 18.4;
            var updated = await service.UpdateAgentLocationAsync(agentId, newLat, newLng);

            Assert.NotNull(updated);
            Assert.Equal(newLat, updated.CurrentLat);
            Assert.Equal(newLng, updated.CurrentLng);
            Assert.NotNull(updated.LastSeenAt);
        }
    }
}
