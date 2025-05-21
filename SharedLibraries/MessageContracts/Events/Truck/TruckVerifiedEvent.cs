namespace MessageContracts.Events.Truck
{
    /// <summary>
    /// Event published when a truck's verification status changes
    /// </summary>
    public class TruckVerifiedEvent : TruckEventBase
    {
        /// <summary>
        /// Whether the truck has been verified
        /// </summary>
        public bool IsVerified { get; set; }
        
        /// <summary>
        /// Optional notes from the admin/verifier
        /// </summary>
        public string? VerificationNotes { get; set; }
        
        /// <summary>
        /// When the verification status was changed
        /// </summary>
        public DateTimeOffset VerifiedAt { get; set; }
    }
} 