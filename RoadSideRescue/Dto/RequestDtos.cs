using System;
using System.Collections.Generic;

namespace RoadSideRescue.Dto
{
    public class CreateRequestDto
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string? Address { get; set; }
        public string? Description { get; set; }
        public List<string>? Photos { get; set; } 
        public string? VehicleType { get; set; }
    }

    public class RequestSummaryDto
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = "";
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string? Address { get; set; } 
        public string? Description { get; set; }
        public string? VehicleType { get; set; }
        public List<string> Photos { get; set; } = new();
    }
}