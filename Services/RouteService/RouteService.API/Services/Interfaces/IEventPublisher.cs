using MessageContracts.Events.Route;

namespace RouteService.API.Services.Interfaces
{
    /// <summary>
    /// Interface for publishing route-related events
    /// </summary>
    public interface IEventPublisher
    {
        /// <summary>
        /// Publishes a RouteCreatedEvent
        /// </summary>
        /// <param name="routeId">ID of the created route</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task PublishRouteCreatedEventAsync(Guid routeId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Publishes a RouteUpdatedEvent
        /// </summary>
        /// <param name="routeId">ID of the updated route</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task PublishRouteUpdatedEventAsync(Guid routeId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Publishes a RouteStatusUpdatedEvent
        /// </summary>
        /// <param name="routeId">ID of the route with updated status</param>
        /// <param name="previousStatus">Previous status of the route</param>
        /// <param name="newStatus">New status of the route</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task PublishRouteStatusUpdatedEventAsync(Guid routeId, MessageContracts.Enums.RouteStatus previousStatus, MessageContracts.Enums.RouteStatus newStatus, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Publishes a RouteCapacityChangedEvent
        /// </summary>
        /// <param name="routeId">ID of the route with updated capacity</param>
        /// <param name="previousAvailableKg">Previous available weight capacity</param>
        /// <param name="newAvailableKg">New available weight capacity</param>
        /// <param name="previousAvailableM3">Previous available volume capacity</param>
        /// <param name="newAvailableM3">New available volume capacity</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task PublishRouteCapacityChangedEventAsync(Guid routeId, decimal previousAvailableKg, decimal newAvailableKg, decimal? previousAvailableM3, decimal? newAvailableM3, CancellationToken cancellationToken = default);
    }
}
