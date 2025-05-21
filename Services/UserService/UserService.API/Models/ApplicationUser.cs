using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using UserService.API.Models.Enums;

namespace UserService.API.Models
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string Name { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Shipper;
        public bool IsEmailConfirmed { get; set; }
        public bool IsPhoneConfirmed { get; set; }
        
        // Rating information
        public decimal? Rating { get; set; }
        public int RatingCount { get; set; } = 0;
        public int RatingTotal { get; set; } = 0;
        
        public DateTimeOffset RegistrationDate { get; set; }
        public DateTimeOffset? LastLoginDate { get; set; }
        public UserStatus Status { get; set; } = UserStatus.PendingVerification;
        public string? ProfilePictureUrl { get; set; }
        
        // Additional fields for enhanced tracking
        public bool HasVerifiedTruck { get; set; } = false;
        public DateTimeOffset? LastPasswordChangedDate { get; set; }
        
        // Notification preferences
        public bool EmailNotificationsEnabled { get; set; } = true;
        public bool SmsNotificationsEnabled { get; set; } = true;
        public bool PushNotificationsEnabled { get; set; } = true;
        public string PreferredLanguage { get; set; } = "en";
    }
}
