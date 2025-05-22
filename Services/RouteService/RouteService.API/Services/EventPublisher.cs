using MassTransit;
using Microsoft.Extensions.Logging;
using RouteService.API.Services.Interfaces;
using MessageContracts.Events.Route;
using MessageContracts.Enums;
using System;
using System.Threading.Tasks;

namespace RouteService.API.Services
{
    public class EventPublisher : IEventPublisher
    {
        private readonly IBus _bus;
        private readonly ILogger<EventPublisher> _logger;

        public EventPublisher(IBus bus, ILogger<EventPublisher> logger)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task PublishRouteCreatedEventAsync(Guid routeId)
        {
            var eventMessage = new RouteCreatedEvent
            {
                RouteId = routeId,
                Timestamp = DateTime.UtcNow
            };

            await _bus.Publish(eventMessage);
            _logger.LogInformation("Published RouteCreatedEvent for RouteId: {RouteId}", routeId);
        }

        public async Task PublishRouteUpdatedEventAsync(Guid routeId)
        {
            var eventMessage = new RouteUpdatedEvent
            {
                RouteId = routeId,
                Timestamp = DateTime.UtcNow
            };

            await _bus.Publish(eventMessage);
            _logger.LogInformation("Published RouteUpdatedEvent for RouteId: {RouteId}", routeId);
        }

        public async Task PublishRouteStatusUpdatedEventAsync(Guid routeId, RouteStatus previousStatus, RouteStatus newStatus)
        {
            var eventMessage = new RouteStatusUpdatedEvent
            {
                RouteId = routeId,
                OldStatus = previousStatus,
                NewStatus = newStatus,
                Timestamp = DateTime.UtcNow
            };

            await _bus.Publish(eventMessage);
            _logger.LogInformation("Published RouteStatusUpdatedEvent for RouteId: {RouteId}. OldStatus: {OldStatus}, NewStatus: {NewStatus}", 
                routeId, previousStatus, newStatus);
        }

        public async Task PublishRouteCapacityChangedEventAsync(Guid routeId, decimal previousAvailableKg, decimal newAvailableKg, decimal? previousAvailableM3, decimal? newAvailableM3)
        {
            var eventMessage = new RouteCapacityChangedEvent
            {
                RouteId = routeId,
                PreviousAvailableCapacityKg = previousAvailableKg,
                NewAvailableCapacityKg = newAvailableKg,
                PreviousAvailableCapacityM3 = previousAvailableM3,
                NewAvailableCapacityM3 = newAvailableM3,
                Timestamp = DateTime.UtcNow
            };

            await _bus.Publish(eventMessage);
            _logger.LogInformation("Published RouteCapacityChangedEvent for RouteId: {RouteId}. PrevKg: {PrevKg}, NewKg: {NewKg}, PrevM3: {PrevM3}, NewM3: {NewM3}", 
                routeId, previousAvailableKg, newAvailableKg, previousAvailableM3, newAvailableM3);
        }
    }
}
