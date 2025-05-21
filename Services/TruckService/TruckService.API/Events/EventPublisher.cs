using MassTransit;
using MessageContracts.Events.Truck;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace TruckService.API.Events
{
    public interface IEventPublisher
    {
        Task PublishTruckRegisteredAsync(TruckRegisteredEvent eventData);
        Task PublishTruckUpdatedAsync(TruckUpdatedEvent eventData);
        Task PublishTruckStatusUpdatedAsync(TruckStatusUpdatedEvent eventData);
        Task PublishTruckVerifiedAsync(TruckVerifiedEvent eventData);
        Task PublishTruckDeletedAsync(TruckDeletedEvent eventData);
        Task PublishTruckDocumentUploadedAsync(TruckDocumentUploadedEvent eventData);
    }

    public class EventPublisher : IEventPublisher
    {
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<EventPublisher> _logger;

        public EventPublisher(
            IPublishEndpoint publishEndpoint,
            ILogger<EventPublisher> logger)
        {
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task PublishTruckRegisteredAsync(TruckRegisteredEvent eventData)
        {
            _logger.LogInformation("Publishing TruckRegistered event for truck {TruckId}", eventData.TruckId);
            await _publishEndpoint.Publish(eventData);
        }

        public async Task PublishTruckUpdatedAsync(TruckUpdatedEvent eventData)
        {
            _logger.LogInformation("Publishing TruckUpdated event for truck {TruckId}", eventData.TruckId);
            await _publishEndpoint.Publish(eventData);
        }

        public async Task PublishTruckStatusUpdatedAsync(TruckStatusUpdatedEvent eventData)
        {
            _logger.LogInformation("Publishing TruckStatusUpdated event for truck {TruckId} - Status changed from {PreviousStatus} to {NewStatus}", 
                eventData.TruckId, eventData.PreviousStatus, eventData.NewStatus);
            await _publishEndpoint.Publish(eventData);
        }

        public async Task PublishTruckVerifiedAsync(TruckVerifiedEvent eventData)
        {
            _logger.LogInformation("Publishing TruckVerified event for truck {TruckId} - Verified: {IsVerified}", 
                eventData.TruckId, eventData.IsVerified);
            await _publishEndpoint.Publish(eventData);
        }

        public async Task PublishTruckDeletedAsync(TruckDeletedEvent eventData)
        {
            _logger.LogInformation("Publishing TruckDeleted event for truck {TruckId}", eventData.TruckId);
            await _publishEndpoint.Publish(eventData);
        }

        public async Task PublishTruckDocumentUploadedAsync(TruckDocumentUploadedEvent eventData)
        {
            _logger.LogInformation("Publishing TruckDocumentUploaded event for truck {TruckId} - DocumentType: {DocumentType}", 
                eventData.TruckId, eventData.DocumentType);
            await _publishEndpoint.Publish(eventData);
        }
    }
} 