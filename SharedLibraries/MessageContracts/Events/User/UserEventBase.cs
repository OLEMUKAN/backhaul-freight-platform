using MessageContracts.Events;

namespace MessageContracts.Events.User
{
    /// <summary>
    /// Base class for all user-related events
    /// </summary>
using MessageContracts.Enums;

    public abstract class UserEventBase : EventBase
    {
        /// <summary>
        /// Unique identifier of the user
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Email of the user
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Role of the user
        /// </summary>
        public UserRole Role { get; set; }
    }
} 