using Microsoft.AspNetCore.SignalR;
using RoadSideRescue.Services.Interfaces;

namespace RoadSideRescue.Hubs
{
    public class RequestsHub : Hub
    {
        private readonly IRequestService _service;

        public RequestsHub(IRequestService service)
        {
            _service = service;
        }

        // Automatically add user to request group when connecting
        public async Task JoinRequestGroupAsync(Guid requestId, Guid userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, requestId.ToString());
        }

        // Agent accepts a request
        public async Task AcceptRequestAsync(Guid requestId, Guid agentId)
        {
            try
            {
                var request = await _service.AssignAgentAsync(requestId, agentId);
                if (request == null)
                {
                    await Clients.Caller.SendAsync("Error", "Request not found");
                    return;
                }

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
            catch (InvalidOperationException ex)
            {
                await Clients.Caller.SendAsync("Error", ex.Message);
            }
            catch (Exception)
            {
                await Clients.Caller.SendAsync("Error", "An error occurred while accepting the request");
            }
        }

        // Send a message to the request group
        public async Task SendMessageAsync(Guid requestId, Guid fromUserId, string message)
        {
            var msg = await _service.AddMessageAsync(requestId, fromUserId, message);

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
        public async Task UpdateLocationAsync(Guid agentId, double lat, double lng)
        {
            var agent = await _service.UpdateAgentLocationAsync(agentId, lat, lng);
            if (agent == null) return;

            await Clients.All.SendAsync("AgentLocationUpdate", new
            {
                agent.Id,
                agent.CurrentLat,
                agent.CurrentLng
            });
        }
    }
}