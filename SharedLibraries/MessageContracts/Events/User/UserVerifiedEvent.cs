namespace MessageContracts.Events.User
{
    /// <summary>
    /// Event published when a user's verification status changes
    /// </summary>
    public class UserVerifiedEvent : UserEventBase
    {
        /// <summary>
        /// Whether the user's email has been confirmed.
        /// </summary>
        public bool IsEmailConfirmed { get; set; }

        /// <summary>
        /// Whether the user's phone has been confirmed.
        /// </summary>
        public bool IsPhoneConfirmed { get; set; }
        
        /// <summary>
        /// When the verification status was changed/confirmed.
        /// </summary>
        public DateTimeOffset VerifiedAt { get; set; } = DateTimeOffset.UtcNow;
    }
} 