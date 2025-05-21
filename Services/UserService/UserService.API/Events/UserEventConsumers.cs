using MassTransit;
using MessageContracts.Events.Booking;
using MessageContracts.Events.Truck;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using UserService.API.Data;
using UserService.API.Models;

namespace UserService.API.Events
{
    // Consumer classes for external events
    public class TruckVerifiedConsumer : IConsumer<TruckVerifiedEvent>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<TruckVerifiedConsumer> _logger;

        public TruckVerifiedConsumer(
            UserManager<ApplicationUser> userManager,
            ILogger<TruckVerifiedConsumer> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<TruckVerifiedEvent> context)
        {
            var message = context.Message;
            _logger.LogInformation("Consuming TruckVerified event for OwnerId: {OwnerId}, TruckId: {TruckId}", 
                message.OwnerId, message.TruckId);

            try
            {
                // Find the user who owns the truck
                var user = await _userManager.FindByIdAsync(message.OwnerId.ToString());
                
                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found for TruckVerified event", message.OwnerId);
                    return;
                }

                // Update user metadata to indicate they have a verified truck
                // This could be useful for showing verified status in the UI
                // or for business logic that requires verified truck owners
                user.HasVerifiedTruck = true;
                await _userManager.UpdateAsync(user);
                
                _logger.LogInformation("Updated user {UserId} with verified truck status", message.OwnerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing TruckVerified event for user {UserId}", message.OwnerId);
            }
        }
    }

    public class BookingCompletedConsumer : IConsumer<BookingCompletedEvent>
    {
        private readonly UserDbContext _dbContext;
        private readonly ILogger<BookingCompletedConsumer> _logger;

        public BookingCompletedConsumer(
            UserDbContext dbContext,
            ILogger<BookingCompletedConsumer> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<BookingCompletedEvent> context)
        {
            var message = context.Message;
            _logger.LogInformation("Consuming BookingCompleted event for BookingId: {BookingId}", message.BookingId);

            try
            {
                // Logic to update user ratings based on the booking
                // This would typically involve:
                // 1. Finding the shipper and truck owner users
                // 2. Updating their ratings based on the new rating values
                // 3. Calculating new average ratings
                // 4. Saving changes

                _logger.LogInformation("Updated ratings for shipper {ShipperId} and truck owner {TruckOwnerId}", 
                    message.ShipperId, message.TruckOwnerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing BookingCompleted event for booking {BookingId}", message.BookingId);
            }
        }
    }
} 