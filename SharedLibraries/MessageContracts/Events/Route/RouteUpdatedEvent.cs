using MessageContracts.Events;

namespace MessageContracts.Events.Route
{
    /// <summary>
    /// Event published when a route is updated
    /// </summary>
    public class RouteUpdatedEvent : EventBase
    {
        /// <summary>
        /// Unique identifier of the updated route
        /// </summary>
        public Guid RouteId { get; set; }
        
        /// <summary>
        /// Truck identifier associated with this route
        /// </summary>
        public Guid TruckId { get; set; }
        
        /// <summary>
        /// Owner (user) identifier
        /// </summary>
        public Guid OwnerId { get; set; }
        
        /// <summary>
        /// Indicates if this is a return leg of the journey
        /// </summary>
        public bool IsReturnLeg { get; set; }
        
        /// <summary>
        /// Origin address
        /// </summary>
        public string OriginAddress { get; set; } = null!;
        
        /// <summary>
        /// Destination address
        /// </summary>
        public string DestinationAddress { get; set; } = null!;
        
        /// <summary>
        /// Planned departure time
        /// </summary>
        public DateTimeOffset DepartureTime { get; set; }
        
        /// <summary>
        /// Planned arrival time
        /// </summary>
        public DateTimeOffset ArrivalTime { get; set; }
        
        /// <summary>
        /// Available weight capacity in kilograms
        /// </summary>
        public decimal CapacityAvailableKg { get; set; }
        
        /// <summary>
        /// Available volume capacity in cubic meters
        /// </summary>
        public decimal? CapacityAvailableM3 { get; set; }
        
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
        public int Status { get; set; }
    }
} 