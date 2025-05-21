namespace TruckService.API.Models.Enums
{
    /// <summary>
    /// Represents the status of a truck in the system
    /// </summary>
    public enum TruckStatus
    {
        /// <summary>
        /// Truck is active and available
        /// </summary>
        Active = 1,
        
        /// <summary>
        /// Truck is inactive
        /// </summary>
        Inactive = 2,
        
        /// <summary>
        /// Truck is undergoing maintenance
        /// </summary>
        UnderMaintenance = 3,
        
        /// <summary>
        /// Truck is pending verification
        /// </summary>
        PendingVerification = 4,
        
        /// <summary>
        /// Truck verification was rejected
        /// </summary>
        VerificationRejected = 5
    }
}
