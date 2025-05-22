using MessageContracts.Events;

namespace MessageContracts.Events.Route
{
    /// <summary>
    /// Event published when a route's available capacity changes
    /// </summary>
    public class RouteCapacityChangedEvent : EventBase
    {
        /// <summary>
        /// Unique identifier of the route
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
        /// Previous available weight capacity in kilograms
        /// </summary>
        public decimal PreviousCapacityAvailableKg { get; set; }
        
        /// <summary>
        /// New available weight capacity in kilograms
        /// </summary>
        public decimal NewCapacityAvailableKg { get; set; }
        
        /// <summary>
        /// Previous available volume capacity in cubic meters
        /// </summary>
        public decimal? PreviousCapacityAvailableM3 { get; set; }
        
        /// <summary>
        /// New available volume capacity in cubic meters
        /// </summary>
        public decimal? NewCapacityAvailableM3 { get; set; }
        
        /// <summary>
        /// Booking ID that caused the capacity change, if applicable
        /// </summary>
        public Guid? BookingId { get; set; }
    }
} 