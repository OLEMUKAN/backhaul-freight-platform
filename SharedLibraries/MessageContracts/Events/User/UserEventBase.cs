using MessageContracts.Events;

namespace MessageContracts.Events.User
{
    /// <summary>
    /// Base class for all user-related events
    /// </summary>
    public abstract class UserEventBase : EventBase
    {
        /// <summary>
        /// Unique identifier of the user
        /// </summary>
        public Guid UserId { get; set; }
    }
} 