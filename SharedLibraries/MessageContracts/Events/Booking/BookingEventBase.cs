using MessageContracts.Events;

namespace MessageContracts.Events.Booking
{
    /// <summary>
    /// Base class for all booking-related events
    /// </summary>
    public abstract class BookingEventBase : EventBase
    {
        /// <summary>
        /// Unique identifier of the booking
        /// </summary>
        public Guid BookingId { get; set; }
        
        /// <summary>
        /// Unique identifier of the shipment
        /// </summary>
        public Guid ShipmentId { get; set; }
        
        /// <summary>
        /// Unique identifier of the route
        /// </summary>
        public Guid RouteId { get; set; }
        
        /// <summary>
        /// Unique identifier of the shipper
        /// </summary>
        public Guid ShipperId { get; set; }
        
        /// <summary>
        /// Unique identifier of the truck owner
        /// </summary>
        public Guid TruckOwnerId { get; set; }
    }
} 