using MassTransit;
using MessageContracts.Events.User;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using TruckService.API.Data;
using Microsoft.EntityFrameworkCore;

namespace TruckService.API.Events
{
    // Define event classes from other services that TruckService will consume
    // These should match the event contracts from those services
    
    public class UserStatusChangedEvent
    {
        public Guid UserId { get; set; }
        public int PreviousStatus { get; set; }
        public int NewStatus { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }
    
    // Consumer classes for external events

    public class UserStatusChangedConsumer : IConsumer<UserStatusChangedEvent>
    {
        private readonly TruckDbContext _dbContext;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<UserStatusChangedConsumer> _logger;

        public UserStatusChangedConsumer(
            TruckDbContext dbContext,
            IEventPublisher eventPublisher,
            ILogger<UserStatusChangedConsumer> logger)
        {
            _dbContext = dbContext;
            _eventPublisher = eventPublisher;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<UserStatusChangedEvent> context)
        {
            var message = context.Message;
            _logger.LogInformation("Consuming UserStatusChanged event for UserId: {UserId}, New Status: {NewStatus}", 
                message.UserId, message.NewStatus);

            try
            {
                // If user is suspended/inactive (Status 2 or 3), mark their trucks as inactive
                if (message.NewStatus == 2 || message.NewStatus == 3) // Inactive or Suspended
                {
                    var userTrucks = await _dbContext.Trucks
                        .Where(t => t.OwnerId == message.UserId && t.Status == 1) // Active trucks
                        .ToListAsync();
                    
                    if (userTrucks.Any())
                    {
                        _logger.LogInformation("Marking {Count} trucks as inactive due to user {UserId} status change to {Status}", 
                            userTrucks.Count, message.UserId, message.NewStatus);
                        
                        foreach (var truck in userTrucks)
                        {
                            var previousStatus = truck.Status;
                            truck.Status = 2; // Inactive
                            truck.UpdatedAt = DateTimeOffset.UtcNow;
                            
                            // Publish event for each truck status change
                            await _eventPublisher.PublishTruckStatusUpdatedAsync(new MessageContracts.Events.Truck.TruckStatusUpdatedEvent
                            {
                                TruckId = truck.Id,
                                OwnerId = truck.OwnerId,
                                PreviousStatus = previousStatus,
                                NewStatus = truck.Status
                            });
                        }
                        
                        await _dbContext.SaveChangesAsync();
                        _logger.LogInformation("Successfully updated truck statuses for user {UserId}", message.UserId);
                    }
                }
                // If user is reactivated (Status 1), don't automatically reactivate trucks
                // This requires admin verification
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing UserStatusChanged event for user {UserId}", message.UserId);
            }
        }
    }
} 