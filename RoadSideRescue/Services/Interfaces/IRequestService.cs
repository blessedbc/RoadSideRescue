using RoadSideRescue.Models;

namespace RoadSideRescue.Services.Interfaces
{
    public interface IRequestService
    {
        Task<Request?> AssignAgentAsync(Guid requestId, Guid agentId);
        Task<Message> AddMessageAsync(Guid requestId, Guid fromUserId, string text);
        Task<Agent?> UpdateAgentLocationAsync(Guid agentId, double lat, double lng);
        Task<Request?> GetRequestByIdAsync(Guid requestId);
    }
}
