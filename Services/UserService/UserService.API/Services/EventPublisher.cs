using MassTransit;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace UserService.API.Services
{
    public class EventPublisher : IEventPublisher
    {
        private readonly ILogger<EventPublisher> _logger;
        private readonly IPublishEndpoint _publishEndpoint;

        public EventPublisher(ILogger<EventPublisher> logger, IPublishEndpoint publishEndpoint)
        {
            _logger = logger;
            _publishEndpoint = publishEndpoint;
        }

        public async Task PublishAsync<T>(T eventMessage) where T : class
        {
            try
            {
                _logger.LogInformation("Publishing event: {EventType}", typeof(T).Name);
                
                // Use MassTransit to publish the event to the message broker
                await _publishEndpoint.Publish(eventMessage);
                
                _logger.LogInformation("Successfully published event: {EventType}", typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing event {EventType}: {ErrorMessage}", 
                    typeof(T).Name, ex.Message);
                throw; // Rethrow to let the calling code handle the error
            }
        }
    }
} 