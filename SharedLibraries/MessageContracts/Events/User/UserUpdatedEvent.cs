using System;
using MessageContracts.Enums;

namespace MessageContracts.Events.User
{
    /// <summary>
    /// Event published when a user's profile information is updated.
    /// Inherits UserId, Email, Role, and Timestamp from UserEventBase.
    /// </summary>
    public class UserUpdatedEvent : UserEventBase
    {
        /// <summary>
        /// New name of the user, if changed.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// New phone number of the user, if changed.
        /// </summary>
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// Whether the user's email is confirmed, if changed.
        /// </summary>
        public bool? IsEmailConfirmed { get; set; }

        /// <summary>
        /// Whether the user's phone is confirmed, if changed.
        /// </summary>
        public bool? IsPhoneConfirmed { get; set; }

        /// <summary>
        /// New status of the user, if changed.
        /// </summary>
        public UserStatus? Status { get; set; }
    }
}
