using MessageContracts.Enums;
using NetTopologySuite.Geometries;
using System.Text.Json.Serialization;

namespace RouteService.API.Models
{
    /// <summary>
    /// Represents a planned route for a truck
    /// </summary>
    public class Route
    {
        /// <summary>
        /// Unique identifier for the route
        /// </summary>
        public Guid Id { get; set; }
        
        /// <summary>
        /// The truck assigned to this route
        /// </summary>
        public Guid TruckId { get; set; }
        
        /// <summary>
        /// The owner (truck owner) of this route
        /// </summary>
        public Guid OwnerId { get; set; }
        
        /// <summary>
        /// Indicates if this is a return leg of a journey
        /// </summary>
        public bool IsReturnLeg { get; set; }
        
        /// <summary>
        /// The human-readable address of the origin location
        /// </summary>
        public string OriginAddress { get; set; } = null!;
        
        /// <summary>
        /// The geographic point representing the origin location
        /// </summary>
        public Point OriginPoint { get; set; } = null!;
        
        /// <summary>
        /// The human-readable address of the destination location
        /// </summary>
        public string DestinationAddress { get; set; } = null!;
        
        /// <summary>
        /// The geographic point representing the destination location
        /// </summary>
        public Point DestinationPoint { get; set; } = null!;
        
        /// <summary>
        /// Intermediate points on the route (serialized as JSON)
        /// </summary>
        public string? ViaPoints { get; set; }
        
        /// <summary>
        /// The complete geometry of the route path
        /// </summary>
        public LineString? GeometryPath { get; set; }
        
        /// <summary>
        /// Planned departure time
        /// </summary>
        public DateTimeOffset DepartureTime { get; set; }
        
        /// <summary>
        /// Planned arrival time
        /// </summary>
        public DateTimeOffset ArrivalTime { get; set; }
        
        /// <summary>
        /// Start of time window when cargo can be loaded
        /// </summary>
        public DateTimeOffset AvailableFrom { get; set; }
        
        /// <summary>
        /// End of time window when cargo must be delivered
        /// </summary>
        public DateTimeOffset AvailableTo { get; set; }
        
        /// <summary>
        /// Remaining available weight capacity in kilograms
        /// </summary>
        public decimal CapacityAvailableKg { get; set; }
        
        /// <summary>
        /// Remaining available volume capacity in cubic meters
        /// </summary>
        public decimal? CapacityAvailableM3 { get; set; }
        
        /// <summary>
        /// Total weight capacity of the truck in kilograms
        /// </summary>
        public decimal TotalCapacityKg { get; set; }
        
        /// <summary>
        /// Total volume capacity of the truck in cubic meters
        /// </summary>
        public decimal? TotalCapacityM3 { get; set; }
        
        /// <summary>
        /// Estimated distance of the route in kilometers
        /// </summary>
        public decimal EstimatedDistanceKm { get; set; }
        
        /// <summary>
        /// Estimated duration of the route in minutes
        /// </summary>
        public int EstimatedDurationMinutes { get; set; }
        
        /// <summary>
        /// Current status of the route
        /// </summary>
        public RouteStatus Status { get; set; }
        
        /// <summary>
        /// When the route was created
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }
        
        /// <summary>
        /// When the route was last updated
        /// </summary>
        public DateTimeOffset UpdatedAt { get; set; }
    }
} 