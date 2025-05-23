using MessageContracts.Enums; // Required for UserRole if it's used in UserEventBase or directly

namespace MessageContracts.Events.User
{
    /// <summary>
    /// Event published when a phone verification code is generated for a user.
    /// </summary>
    public class PhoneVerificationCodeGeneratedEvent : UserEventBase
    {
        /// <summary>
        /// The phone number for which the verification code was generated.
        /// </summary>
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>
        /// The generated phone verification code.
        /// </summary>
        public string VerificationCode { get; set; } = string.Empty;
    }
}
