using System;

namespace UserService.API.Models.Enums
{
    /// <summary>
    /// Represents user roles in the system
    /// </summary>
    public enum UserRole
    {
        /// <summary>
        /// Shipper role - users who need to ship cargo
        /// </summary>
        Shipper = 1,
        
        /// <summary>
        /// Truck owner role - users who own and operate trucks
        /// </summary>
        TruckOwner = 2,
        
        /// <summary>
        /// Administrator role - users with full system access
        /// </summary>
        Admin = 3
    }

    /// <summary>
    /// Represents user status in the system
    /// </summary>
    public enum UserStatus
    {
        /// <summary>
        /// User account is active
        /// </summary>
        Active = 1,
        
        /// <summary>
        /// User account is inactive
        /// </summary>
        Inactive = 2,
        
        /// <summary>
        /// User account is suspended
        /// </summary>
        Suspended = 3,
        
        /// <summary>
        /// User account is pending verification
        /// </summary>
        PendingVerification = 4
    }
    
    /// <summary>
    /// Extension methods for user enums
    /// </summary>
    public static class UserEnumExtensions
    {        /// <summary>
        /// Converts UserRole enum to its string representation
        /// </summary>
        public static string ToRoleName(this UserRole role) => role.ToString();
        
        /// <summary>
        /// Converts string to UserRole enum
        /// </summary>
        public static UserRole ToUserRole(this string roleName)
        {
            // Handle numeric string scenarios
            if (int.TryParse(roleName, out var roleId) && 
                Enum.IsDefined(typeof(UserRole), roleId))
            {
                return (UserRole)roleId;
            }
            
            // Handle string name scenarios
            if (Enum.TryParse<UserRole>(roleName, true, out var role))
                return role;
            
            throw new ArgumentException($"Invalid role name or ID: {roleName}");
        }
        
        /// <summary>
        /// Safely converts string to UserRole enum with a default fallback
        /// </summary>
        public static UserRole ToUserRoleSafe(this string? roleName, UserRole defaultRole = UserRole.Shipper)
        {
            if (string.IsNullOrEmpty(roleName))
                return defaultRole;
                
            try
            {
                return ToUserRole(roleName);
            }
            catch
            {
                return defaultRole;
            }
        }
        
        /// <summary>
        /// Converts UserStatus enum to its string representation
        /// </summary>
        public static string ToStatusName(this UserStatus status) => status.ToString();
    }
}
