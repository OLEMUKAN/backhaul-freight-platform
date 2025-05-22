using MessageContracts.Events;

namespace MessageContracts.Events.Route
{
    /// <summary>
    /// Event published when a route's status is updated
    /// </summary>
    public class RouteStatusUpdatedEvent : EventBase
    {
        /// <summary>
        /// Unique identifier of the route
        /// </summary>
        public Guid RouteId { get; set; }
        
        /// <summary>
        /// Owner (user) identifier
        /// </summary>
        public Guid OwnerId { get; set; }
        
        /// <summary>
        /// Previous status of the route
        /// </summary>
        public int PreviousStatus { get; set; }
        
        /// <summary>
        /// New status of the route
        /// </summary>
        public int NewStatus { get; set; }
        
        /// <summary>
        /// Reason for the status change, if applicable
        /// </summary>
        public string? StatusChangeReason { get; set; }
    }
} 