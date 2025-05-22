using System;

namespace MessageContracts.Events.User
{
    /// <summary>
    /// Event published when a new user is created/registered.
    /// Inherits UserId, Email, Role, and Timestamp from UserEventBase.
    /// </summary>
    public class UserCreatedEvent : UserEventBase
    {
        /// <summary>
        /// Name of the user.
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }
}
