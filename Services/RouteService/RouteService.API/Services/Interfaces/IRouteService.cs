using RouteService.API.Models;
using RouteService.API.Models.DTOs;

namespace RouteService.API.Services.Interfaces
{
    /// <summary>
    /// Interface for Route Service operations
    /// </summary>
    public interface IRouteService
    {
        /// <summary>
        /// Creates a new route
        /// </summary>
        /// <param name="request">Route creation request</param>
        /// <param name="ownerId">ID of the truck owner creating the route</param>
        /// <returns>Created route DTO</returns>
        Task<RouteDto> CreateRouteAsync(CreateRouteRequest request, Guid ownerId);
        
        /// <summary>
        /// Gets a route by its ID
        /// </summary>
        /// <param name="id">The route ID</param>
        /// <returns>Route DTO if found, null otherwise</returns>
        Task<RouteDto?> GetRouteByIdAsync(Guid id);
        
        /// <summary>
        /// Gets routes with optional filtering
        /// </summary>
        /// <param name="filter">Filter criteria</param>
        /// <returns>List of routes matching the filter</returns>
        Task<IEnumerable<RouteDto>> GetRoutesAsync(RouteFilterRequest? filter = null);
        
        /// <summary>
        /// Updates an existing route
        /// </summary>
        /// <param name="id">Route ID</param>
        /// <param name="request">Route update request</param>
        /// <param name="ownerId">ID of the truck owner updating the route</param>
        /// <returns>Updated route DTO</returns>
        Task<RouteDto?> UpdateRouteAsync(Guid id, UpdateRouteRequest request, Guid ownerId);
        
        /// <summary>
        /// Updates route capacity after booking
        /// </summary>
        /// <param name="id">Route ID</param>
        /// <param name="request">Capacity update request</param>
        /// <returns>Updated route DTO</returns>
        Task<RouteDto?> UpdateRouteCapacityAsync(Guid id, UpdateRouteCapacityRequest request);
        
        /// <summary>
        /// Cancels a route
        /// </summary>
        /// <param name="id">Route ID</param>
        /// <param name="ownerId">ID of the truck owner cancelling the route</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> CancelRouteAsync(Guid id, Guid ownerId);
    }
}
