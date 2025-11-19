using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace RoadSideRescue.Dto
{
    public class AgentAvailabilityRequest
    {
        [Required]
        public bool Online { get; set; }

        [Required]
        [Range(-90, 90)]
        public double Lat { get; set; }

        [Required]
        [Range(-180, 180)]
        public double Lng { get; set; }

        [Range(0, int.MaxValue)]
        public int? ServiceRadiusMeters { get; set; }

        public List<string>? Skills { get; set; }
    }
}
