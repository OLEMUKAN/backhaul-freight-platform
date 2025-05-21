using System;
using System.Collections.Generic;
using UserService.API.Models.Enums;

namespace UserService.API.Events
{
    // Base class for all user events
    public abstract class UserEventBase
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    }

    public class UserCreatedEvent : UserEventBase
    {
        public string Name { get; set; } = string.Empty;
    }

    public class UserUpdatedEvent : UserEventBase
    {
        public string? Name { get; set; }
        public string? PhoneNumber { get; set; }
        public bool? IsEmailConfirmed { get; set; }
        public bool? IsPhoneConfirmed { get; set; }
        public int? Status { get; set; }
    }

    public class UserVerifiedEvent : UserEventBase
    {
        public bool IsEmailVerified { get; set; }
        public bool IsPhoneVerified { get; set; }
    }    public class UserStatusChangedEvent : UserEventBase
    {
        public UserStatus PreviousStatus { get; set; }
        public UserStatus NewStatus { get; set; }
    }

    public class UserLoginEvent : UserEventBase
    {
        public string IpAddress { get; set; } = string.Empty;
        public string DeviceInfo { get; set; } = string.Empty;
        public string? Location { get; set; }
    }

    // New authentication events
    public class UserLoggedOutEvent : UserEventBase
    {
        // Additional logout details can be added here
    }

    public class CredentialChangedEvent : UserEventBase
    {
        public bool IsPasswordReset { get; set; }
    }

    public class AccountLockedEvent : UserEventBase
    {
        public string Reason { get; set; } = string.Empty; // "FailedAttempts", "AdminAction", etc.
        public DateTimeOffset? UnlockTime { get; set; } // When automatic unlock will occur
    }

    // Profile management events
    public class UserProfileUpdatedEvent : UserEventBase
    {
        public Dictionary<string, object> ChangedProperties { get; set; } = new Dictionary<string, object>();
    }

    public class UserContactInfoChangedEvent : UserEventBase
    {
        public string OldEmail { get; set; } = string.Empty;
        public string NewEmail { get; set; } = string.Empty;
        public string OldPhone { get; set; } = string.Empty;
        public string NewPhone { get; set; } = string.Empty;
        public bool RequiresVerification { get; set; }
    }

    public class UserRoleChangedEvent : UserEventBase
    {
        public int PreviousRole { get; set; }
        public int NewRole { get; set; }
    }

    // Preference events
    public class UserPreferencesUpdatedEvent : UserEventBase
    {
        public bool EmailNotificationsEnabled { get; set; }
        public bool SmsNotificationsEnabled { get; set; }
        public bool PushNotificationsEnabled { get; set; }
        public Dictionary<string, bool> NotificationTypePreferences { get; set; } = new Dictionary<string, bool>();
        public string PreferredLanguage { get; set; } = "en";
    }
} 