using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace RoadSideRescue.Hubs
{
    public class RequestsHub : Hub
    {
        // Called by server to push events; clients subscribe to types such as RequestCreated, RequestAssigned, NewMessage, AgentLocationUpdate

        public override Task OnConnectedAsync()
        {
            // Optionally capture presence info from query string or after a handshake method
            return base.OnConnectedAsync();
        }

        public Task AcceptRequest(string requestId)
        {
            // Called by agent client to accept a request. Server should validate and attempt assignment.
            return Task.CompletedTask;
        }

        public Task SendMessage(string requestId, string message)
        {
            // Broadcast message to the request group (owner + assigned agent)
            return Task.CompletedTask;
        }

        public Task UpdateLocation(double lat, double lng)
        {
            // Update agent location in memory/store and optionally broadcast to interested owners
            return Task.CompletedTask;
        }
    }
}
