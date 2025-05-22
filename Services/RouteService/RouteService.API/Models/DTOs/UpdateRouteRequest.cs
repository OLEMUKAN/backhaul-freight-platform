using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RouteService.API.Models.DTOs
{
    /// <summary>
    /// Data Transfer Object for updating an existing route
    /// </summary>
    public class UpdateRouteRequest
    {
        /// <summary>
        /// Indicates if this is a return leg of a journey
        /// </summary>
        public bool? IsReturnLeg { get; set; }
        
        /// <summary>
        /// The human-readable address of the origin location
        /// </summary>
        [StringLength(255)]
        public string? OriginAddress { get; set; }
        
        /// <summary>
        /// The origin location coordinates [longitude, latitude]
        /// </summary>
        public double[]? OriginCoordinates { get; set; }
        
        /// <summary>
        /// The human-readable address of the destination location
        /// </summary>
        [StringLength(255)]
        public string? DestinationAddress { get; set; }
        
        /// <summary>
        /// The destination location coordinates [longitude, latitude]
        /// </summary>
        public double[]? DestinationCoordinates { get; set; }
        
        /// <summary>
        /// Intermediate points on the route (array of [longitude, latitude] pairs)
        /// </summary>
        public IEnumerable<double[]>? ViaPoints { get; set; }
        
        /// <summary>
        /// Planned departure time
        /// </summary>
        public DateTimeOffset? DepartureTime { get; set; }
        
        /// <summary>
        /// Planned arrival time
        /// </summary>
        public DateTimeOffset? ArrivalTime { get; set; }
        
        /// <summary>
        /// Start of time window when cargo can be loaded
        /// </summary>
        public DateTimeOffset? AvailableFrom { get; set; }
        
        /// <summary>
        /// End of time window when cargo must be delivered
        /// </summary>
        public DateTimeOffset? AvailableTo { get; set; }
        
        /// <summary>
        /// Current status of the route
        /// </summary>
        public int? Status { get; set; }
        
        /// <summary>
        /// Validate that the request has at least one field to update
        /// </summary>
        public bool HasChanges()
        {
            return IsReturnLeg != null || 
                   OriginAddress != null || 
                   OriginCoordinates != null || 
                   DestinationAddress != null || 
                   DestinationCoordinates != null || 
                   ViaPoints != null || 
                   DepartureTime != null || 
                   ArrivalTime != null || 
                   AvailableFrom != null || 
                   AvailableTo != null || 
                   Status != null;
        }
        
        /// <summary>
        /// Validate that the coordinates are valid
        /// </summary>
        public bool AreCoordinatesValid()
        {
            if (OriginCoordinates != null && OriginCoordinates.Length != 2)
            {
                return false;
            }
            
            if (DestinationCoordinates != null && DestinationCoordinates.Length != 2)
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