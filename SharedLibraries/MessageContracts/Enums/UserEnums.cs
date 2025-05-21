namespace MessageContracts.Enums
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
}
