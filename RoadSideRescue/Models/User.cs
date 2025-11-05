using System;

namespace RoadSideRescue.Models
{
    public enum UserRole { Owner, Agent, Admin }

    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public UserRole Role { get; set; } = UserRole.Owner;
        public double RatingAverage { get; set; } = 0.0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}