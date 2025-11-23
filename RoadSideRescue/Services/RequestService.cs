using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RoadSideRescue.Data;
using RoadSideRescue.Models;
using RoadSideRescue.Services.Interfaces;

namespace RoadSideRescue.Services
{
    public class RequestService : IRequestService
    {
        private readonly ApplicationDbContext _db;

        public RequestService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<Request?> AssignAgentAsync(Guid requestId, Guid agentId)
        {
            var request = await _db.Requests.FirstOrDefaultAsync(r => r.Id == requestId);
            if (request == null) return null;

            if (request.AssignedAgentId.HasValue)
                throw new InvalidOperationException("Request already assigned.");

            request.AssignedAgentId = agentId;
            request.Status = RequestStatus.Assigned;
            request.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return request;
        }

        public async Task<Message> AddMessageAsync(Guid requestId, Guid fromUserId, string text)
        {
            var msg = new Message
            {
                Id = Guid.NewGuid(),
                RequestId = requestId,
                FromUserId = fromUserId,
                Text = text,
                CreatedAt = DateTime.UtcNow
            };

            _db.Messages.Add(msg);
            await _db.SaveChangesAsync();

            return msg;
        }

        public async Task<Agent?> UpdateAgentLocationAsync(Guid agentId, double lat, double lng)
        {
            var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId);
            if (agent == null) return null;

            agent.CurrentLat = lat;
            agent.CurrentLng = lng;
            agent.LastSeenAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return agent;
        }

        public async Task<Request?> GetRequestByIdAsync(Guid requestId)
        {
            return await _db.Requests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == requestId);
        }
    }
}
