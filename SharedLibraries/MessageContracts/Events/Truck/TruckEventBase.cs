using MessageContracts.Events;

namespace MessageContracts.Events.Truck
{
    /// <summary>
    /// Base class for all truck-related events
    /// </summary>
    public abstract class TruckEventBase : EventBase
    {
        /// <summary>
        /// Unique identifier of the truck
        /// </summary>
        public Guid TruckId { get; set; }
        
        /// <summary>
        /// Unique identifier of the truck owner
        /// </summary>
        public Guid OwnerId { get; set; }
    }
} 