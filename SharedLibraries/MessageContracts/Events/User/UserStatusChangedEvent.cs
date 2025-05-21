using MessageContracts.Enums;

namespace MessageContracts.Events.User
{
    /// <summary>
    /// Event published when a user's status changes
    /// </summary>
    public class UserStatusChangedEvent : UserEventBase
    {
        /// <summary>
        /// Previous status
        /// </summary>
        public UserStatus PreviousStatus { get; set; }
        
        /// <summary>
        /// New status
        /// </summary>
        public UserStatus NewStatus { get; set; }
    }
}