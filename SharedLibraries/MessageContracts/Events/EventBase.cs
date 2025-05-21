namespace MessageContracts.Events
{
    /// <summary>
    /// Base class for all event messages
    /// </summary>
    public abstract class EventBase
    {
        /// <summary>
        /// Event timestamp in UTC
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
        
        /// <summary>
        /// Unique identifier for this event instance
        /// </summary>
        public Guid EventId { get; set; } = Guid.NewGuid();
    }
} 