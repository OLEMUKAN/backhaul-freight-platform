using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RouteService.API.Models.DTOs
{
    /// <summary>
    /// Data Transfer Object for creating a new route
    /// </summary>
    public class CreateRouteRequest
    {
        /// <summary>
        /// The truck assigned to this route
        /// </summary>
        [Required]
        public Guid TruckId { get; set; }
        
        /// <summary>
        /// Indicates if this is a return leg of a journey
        /// </summary>
        [Required]
        public bool IsReturnLeg { get; set; }
        
        /// <summary>
        /// The human-readable address of the origin location
        /// </summary>
        [Required]
        [StringLength(255)]
        public string OriginAddress { get; set; } = null!;
        
        /// <summary>
        /// The origin location coordinates [longitude, latitude]
        /// </summary>
        [Required]
        public double[] OriginCoordinates { get; set; } = null!;
        
        /// <summary>
        /// The human-readable address of the destination location
        /// </summary>
        [Required]
        [StringLength(255)]
        public string DestinationAddress { get; set; } = null!;
        
        /// <summary>
        /// The destination location coordinates [longitude, latitude]
        /// </summary>
        [Required]
        public double[] DestinationCoordinates { get; set; } = null!;
        
        /// <summary>
        /// Intermediate points on the route (array of [longitude, latitude] pairs)
        /// </summary>
        public IEnumerable<double[]>? ViaPoints { get; set; }
        
        /// <summary>
        /// Planned departure time
        /// </summary>
        [Required]
        public DateTimeOffset DepartureTime { get; set; }
        
        /// <summary>
        /// Planned arrival time
        /// </summary>
        [Required]
        public DateTimeOffset ArrivalTime { get; set; }
        
        /// <summary>
        /// Start of time window when cargo can be loaded
        /// </summary>
        [Required]
        public DateTimeOffset AvailableFrom { get; set; }
        
        /// <summary>
        /// End of time window when cargo must be delivered
        /// </summary>
        [Required]
        public DateTimeOffset AvailableTo { get; set; }
        
        /// <summary>
        /// Additional notes for the route
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Alias for DepartureTime
        /// </summary>
        [JsonIgnore]
        public DateTimeOffset ScheduledDeparture
        {
            get => DepartureTime;
            set => DepartureTime = value;
        }

        /// <summary>
        /// Alias for ArrivalTime
        /// </summary>
        [JsonIgnore]
        public DateTimeOffset ScheduledArrival
        {
            get => ArrivalTime;
            set => ArrivalTime = value;
        }
        
        /// <summary>
        /// Validate that the times are consistent
        /// </summary>
        public bool AreTimesValid()
        {
            return DepartureTime < ArrivalTime &&
                   AvailableFrom <= DepartureTime &&
                   ArrivalTime <= AvailableTo;
        }
        
        /// <summary>
        /// Validate that the coordinates are valid
        /// </summary>
        public bool AreCoordinatesValid()
        {
            if (OriginCoordinates == null || OriginCoordinates.Length != 2 ||
                DestinationCoordinates == null || DestinationCoordinates.Length != 2)
            {
                return false;
            }
            
            if (ViaPoints != null)
            {
                foreach (var point in ViaPoints)
                {
                    if (point == null || point.Length != 2)
                    {
                        return false;
                    }
                }
            }
            
            return true;
        }
    }
}