using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;

namespace UserService.API.Models
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string Name { get; set; } = string.Empty;
        public int Role { get; set; } // 1=Shipper, 2=TruckOwner, 3=Admin
        public bool IsEmailConfirmed { get; set; }
        public bool IsPhoneConfirmed { get; set; }
        
        // Rating information
        public decimal? Rating { get; set; }
        public int RatingCount { get; set; } = 0;
        public int RatingTotal { get; set; } = 0;
        
        public DateTimeOffset RegistrationDate { get; set; }
        public DateTimeOffset? LastLoginDate { get; set; }
        public int Status { get; set; } // 1=Active, 2=Inactive, 3=Suspended, 4=PendingVerification
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
