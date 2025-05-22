namespace RouteService.API.Services.Interfaces
{
    /// <summary>
    /// Interface for Truck Service HTTP client
    /// </summary>
    public interface ITruckServiceClient
    {
        /// <summary>
        /// Verifies that a truck exists and belongs to the specified owner
        /// </summary>
        /// <param name="truckId">ID of the truck</param>
        /// <param name="ownerId">ID of the owner</param>
        /// <returns>True if truck exists and belongs to owner, otherwise false</returns>
        Task<bool> VerifyTruckOwnershipAsync(Guid truckId, Guid ownerId);
        
        /// <summary>
        /// Gets truck capacity information
        /// </summary>
        /// <param name="truckId">ID of the truck</param>
        /// <returns>Tuple with (weight capacity in kg, volume capacity in cubic meters)</returns>
        Task<(decimal CapacityKg, decimal? CapacityM3)> GetTruckCapacityAsync(Guid truckId);
    }
}
