using Microsoft.AspNetCore.SignalR;
using RoadSideRescue.Data;
using RoadSideRescue.Models;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace RoadSideRescue.Hubs
{
    public class RequestsHub : Hub
    {
        private readonly ApplicationDbContext _db;

        public RequestsHub(ApplicationDbContext db)
        {
            _db = db;
        }

        // Automatically add user to request group when connecting
        public async Task JoinRequestGroup(Guid requestId, Guid userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, requestId.ToString());

            // Optional: Track which user is in which request
        }

        // Agent accepts a request
        public async Task AcceptRequest(Guid requestId, Guid agentId)
        {
            var request = await _db.Requests.FirstOrDefaultAsync(r => r.Id == requestId);
            if (request == null) { await Clients.Caller.SendAsync("Error", "Request not found"); return; }

            request.AssignedAgentId = agentId;
            request.Status = RequestStatus.Assigned;
            request.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Add agent to the request group
            await Groups.AddToGroupAsync(Context.ConnectionId, requestId.ToString());

            // Notify only owner + assigned agent
            await Clients.Group(requestId.ToString()).SendAsync("RequestAssigned", new
            {
                request.Id,
                request.Status,
                request.AssignedAgentId
            });
        }

        // Send a message to the request group
        public async Task SendMessage(Guid requestId, Guid fromUserId, string message)
        {
            var msg = new Message
            {
                Id = Guid.NewGuid(),
                RequestId = requestId,
                FromUserId = fromUserId,
                Text = message,
                CreatedAt = DateTime.UtcNow
            };

            await _db.Messages.AddAsync(msg);
            await _db.SaveChangesAsync();

            await Clients.Group(requestId.ToString()).SendAsync("NewMessage", new
            {
                msg.Id,
                msg.RequestId,
                msg.FromUserId,
                msg.Text,
                msg.CreatedAt
            });
        }

        // Agent updates location
        public async Task UpdateLocation(Guid agentId, double lat, double lng)
        {
            var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId);
            if (agent == null) return;

            agent.CurrentLat = lat;
            agent.CurrentLng = lng;
            agent.LastSeenAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await Clients.All.SendAsync("AgentLocationUpdate", new
            {
                agent.Id,
                agent.CurrentLat,
                agent.CurrentLng
            });
        }
    }
}