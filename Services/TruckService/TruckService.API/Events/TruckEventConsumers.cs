using MassTransit;
using MessageContracts.Events.User; // This already exists
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using TruckService.API.Data;
using Microsoft.EntityFrameworkCore;
using Common.Messaging; // Added for IEventPublisher
using MessageContracts.Events.Truck; // Added for TruckStatusUpdatedEvent
using MessageContracts.Enums; // Added for UserStatus enum

namespace TruckService.API.Events
{
    // Consumer classes for external events

    public class UserStatusChangedConsumer : IConsumer<MessageContracts.Events.User.UserStatusChangedEvent> // Changed to use MessageContracts event
    {
        private readonly TruckDbContext _dbContext;
        private readonly IEventPublisher _eventPublisher; // This will be Common.Messaging.IEventPublisher via DI
        private readonly ILogger<UserStatusChangedConsumer> _logger;

        public UserStatusChangedConsumer(
            TruckDbContext dbContext,
            IEventPublisher eventPublisher, // This will be Common.Messaging.IEventPublisher via DI
            ILogger<UserStatusChangedConsumer> logger)
        {
            _dbContext = dbContext;
            _eventPublisher = eventPublisher;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<MessageContracts.Events.User.UserStatusChangedEvent> context) // Changed to use MessageContracts event
        {
            var message = context.Message;
            _logger.LogInformation("Consuming UserStatusChanged event for UserId: {UserId}, New Status: {NewStatus}", 
                message.UserId, message.NewStatus);

            try
            {
                // If user is suspended/inactive (Status 2 or 3), mark their trucks as inactive
                if (message.NewStatus == UserStatus.Inactive || message.NewStatus == UserStatus.Suspended) // Changed to use UserStatus enum
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
                            await _eventPublisher.PublishAsync(new MessageContracts.Events.Truck.TruckStatusUpdatedEvent // Changed to PublishAsync
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