namespace MessageContracts.Events.User
{
    /// <summary>
    /// Event published when a user's verification status changes
    /// </summary>
    public class UserVerifiedEvent : UserEventBase
    {
        /// <summary>
        /// Whether the user has been verified
        /// </summary>
        public bool IsVerified { get; set; }
        
        /// <summary>
        /// Optional notes about the verification
        /// </summary>
        public string? VerificationNotes { get; set; }
        
        /// <summary>
        /// When the verification status was changed
        /// </summary>
        public DateTimeOffset VerifiedAt { get; set; } = DateTimeOffset.UtcNow;
    }
} 