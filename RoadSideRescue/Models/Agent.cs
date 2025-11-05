using System;

namespace RoadSideRescue.Models
{
    public enum AgentStatus { Offline, Online, Busy }

    public class Agent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public AgentStatus Status { get; set; } = AgentStatus.Offline;
        public double? CurrentLat { get; set; }
        public double? CurrentLng { get; set; }
        public DateTime? LastSeenAt { get; set; }
        public int ServiceRadiusMeters { get; set; } = 5000;
        public string? SkillsJson { get; set; } // keep simple for MVP
    }
}
