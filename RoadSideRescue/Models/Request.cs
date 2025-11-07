using System;

namespace RoadSideRescue.Models
{
    public enum RequestStatus
    {
        Created,
        Broadcasted,
        Assigned,
        EnRoute,
        OnSite,
        Completed,
        Closed
    }

    public class Request
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid OwnerId { get; set; }
        public RequestStatus Status { get; set; } = RequestStatus.Created;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string? Address { get; set; }
        public string? Description { get; set; }
        public string? VehicleType { get; set; }
        public int? Severity { get; set; }
        public Guid? AssignedAgentId { get; set; }
        public int? EtaMinutes { get; set; }
        public List<string> Photos { get; set; } = new();
    }
}
