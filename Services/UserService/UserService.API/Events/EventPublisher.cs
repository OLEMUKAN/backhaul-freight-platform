using MassTransit;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace UserService.API.Events
{
    public interface IEventPublisher
    {
        Task PublishUserCreatedAsync(UserCreatedEvent eventData);
        Task PublishUserVerifiedAsync(UserVerifiedEvent eventData);
        Task PublishUserStatusChangedAsync(UserStatusChangedEvent eventData);
        Task PublishUserRoleChangedAsync(UserRoleChangedEvent eventData);
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

        public async Task PublishUserCreatedAsync(UserCreatedEvent eventData)
        {
            _logger.LogInformation("Publishing UserCreated event for user {UserId}", eventData.UserId);
            await _publishEndpoint.Publish(eventData);
        }

        public async Task PublishUserVerifiedAsync(UserVerifiedEvent eventData)
        {
            _logger.LogInformation("Publishing UserVerified event for user {UserId}", eventData.UserId);
            await _publishEndpoint.Publish(eventData);
        }

        public async Task PublishUserStatusChangedAsync(UserStatusChangedEvent eventData)
        {
            _logger.LogInformation("Publishing UserStatusChanged event for user {UserId} - Status changed from {PreviousStatus} to {NewStatus}",
                eventData.UserId, eventData.PreviousStatus, eventData.NewStatus);
            await _publishEndpoint.Publish(eventData);
        }

        public async Task PublishUserRoleChangedAsync(UserRoleChangedEvent eventData)
        {
            _logger.LogInformation("Publishing UserRoleChanged event for user {UserId} - Role changed from {PreviousRole} to {NewRole}",
                eventData.UserId, eventData.PreviousRole, eventData.NewRole);
            await _publishEndpoint.Publish(eventData);
        }
    }
} 