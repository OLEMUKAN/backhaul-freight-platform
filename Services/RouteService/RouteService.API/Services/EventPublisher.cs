using MassTransit;
using MessageContracts.Events.Route;
using Microsoft.Extensions.Logging;
using RouteService.API.Services.Interfaces;

namespace RouteService.API.Services
{
    /// <summary>
    /// Implements event publishing for route-related events
    /// </summary>
    public class EventPublisher : IEventPublisher
    {
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<EventPublisher> _logger;
        
        public EventPublisher(IPublishEndpoint publishEndpoint, ILogger<EventPublisher> logger)
        {
            _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <inheritdoc />
        public async Task PublishRouteCreatedEventAsync(Guid routeId)
        {
            try
            {
                _logger.LogInformation("Publishing RouteCreatedEvent for Route {RouteId}", routeId);
                
                var @event = new RouteCreatedEvent
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTimeOffset.UtcNow,
                    RouteId = routeId
                };
                
                await _publishEndpoint.Publish(@event);
                
                _logger.LogInformation("Successfully published RouteCreatedEvent {EventId} for Route {RouteId}", @event.Id, routeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish RouteCreatedEvent for Route {RouteId}", routeId);
                throw;
            }
        }
        
        /// <inheritdoc />
        public async Task PublishRouteUpdatedEventAsync(Guid routeId)
        {
            try
            {
                _logger.LogInformation("Publishing RouteUpdatedEvent for Route {RouteId}", routeId);
                
                var @event = new RouteUpdatedEvent
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTimeOffset.UtcNow,
                    RouteId = routeId
                };
                
                await _publishEndpoint.Publish(@event);
                
                _logger.LogInformation("Successfully published RouteUpdatedEvent {EventId} for Route {RouteId}", @event.Id, routeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish RouteUpdatedEvent for Route {RouteId}", routeId);
                throw;
            }
        }
        
        /// <inheritdoc />
        public async Task PublishRouteStatusUpdatedEventAsync(Guid routeId, MessageContracts.Enums.RouteStatus previousStatus, MessageContracts.Enums.RouteStatus newStatus)
        {
            try
            {
                _logger.LogInformation("Publishing RouteStatusUpdatedEvent for Route {RouteId} - Status change from {PreviousStatus} to {NewStatus}", 
                    routeId, previousStatus, newStatus);
                
                var @event = new RouteStatusUpdatedEvent
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTimeOffset.UtcNow,
                    RouteId = routeId,
                    PreviousStatus = previousStatus,
                    NewStatus = newStatus
                };
                
                await _publishEndpoint.Publish(@event);
                
                _logger.LogInformation("Successfully published RouteStatusUpdatedEvent {EventId} for Route {RouteId}", 
                    @event.Id, routeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish RouteStatusUpdatedEvent for Route {RouteId}", routeId);
                throw;
            }
        }
        
        /// <inheritdoc />
        public async Task PublishRouteCapacityChangedEventAsync(Guid routeId, decimal previousAvailableKg, decimal newAvailableKg, 
            decimal? previousAvailableM3, decimal? newAvailableM3)
        {
            try
            {
                _logger.LogInformation("Publishing RouteCapacityChangedEvent for Route {RouteId} - " +
                    "Weight capacity change from {PreviousKg}kg to {NewKg}kg, Volume capacity change from {PreviousM3}m³ to {NewM3}m³", 
                    routeId, previousAvailableKg, newAvailableKg, previousAvailableM3, newAvailableM3);
                
                var @event = new RouteCapacityChangedEvent
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTimeOffset.UtcNow,
                    RouteId = routeId,
                    PreviousCapacityAvailableKg = previousAvailableKg,
                    NewCapacityAvailableKg = newAvailableKg,
                    PreviousCapacityAvailableM3 = previousAvailableM3,
                    NewCapacityAvailableM3 = newAvailableM3
                };
                
                await _publishEndpoint.Publish(@event);
                
                _logger.LogInformation("Successfully published RouteCapacityChangedEvent {EventId} for Route {RouteId}", 
                    @event.Id, routeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish RouteCapacityChangedEvent for Route {RouteId}", routeId);
                throw;
            }
        }
    }
}
