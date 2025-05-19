using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using UserService.API.Data;
using UserService.API.Models;

namespace UserService.API.Events
{
    // Define event classes from other services that UserService will consume
    // These should match the event contracts from those services
    
    public class TruckVerifiedEvent
    {
        public Guid TruckId { get; set; }
        public Guid OwnerId { get; set; }
        public bool IsVerified { get; set; }
        public string? VerificationNotes { get; set; }
        public DateTimeOffset VerifiedAt { get; set; }
    }
    
    public class BookingCompletedEvent
    {
        public Guid BookingId { get; set; }
        public Guid ShipmentId { get; set; }
        public Guid RouteId { get; set; }
        public Guid ShipperId { get; set; }
        public Guid TruckOwnerId { get; set; }
        public DateTimeOffset CompletionDate { get; set; }
        public int? ShipperRatingGiven { get; set; }
        public int? TruckOwnerRatingGiven { get; set; }
        public decimal AgreedPrice { get; set; }
    }
    
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
                // Update ratings for both shipper and truck owner if provided
                if (message.ShipperRatingGiven.HasValue)
                {
                    await UpdateUserRatingAsync(message.ShipperId, message.ShipperRatingGiven.Value);
                }
                
                if (message.TruckOwnerRatingGiven.HasValue)
                {
                    await UpdateUserRatingAsync(message.TruckOwnerId, message.TruckOwnerRatingGiven.Value);
                }
                
                // Record transaction history in the user's profile
                await RecordTransactionAsync(message);
                
                _logger.LogInformation("Successfully processed BookingCompleted event for BookingId: {BookingId}", 
                    message.BookingId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing BookingCompleted event for BookingId: {BookingId}", 
                    message.BookingId);
            }
        }
        
        private async Task UpdateUserRatingAsync(Guid userId, int rating)
        {
            // Get the user
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found for rating update", userId);
                return;
            }
            
            // Update the user's rating (this is a simple average; could be more sophisticated)
            user.RatingCount += 1;
            user.RatingTotal += rating;
            user.Rating = (decimal)user.RatingTotal / user.RatingCount;
            
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Updated rating for user {UserId} to {Rating}", userId, user.Rating);
        }
        
        private async Task RecordTransactionAsync(BookingCompletedEvent message)
        {
            // This method would record transaction history or update user metrics
            // For example, tracking total revenue for truck owners or total spend for shippers
            
            // Implementation depends on how you want to store transaction history
            // This is just a placeholder for the actual implementation
            _logger.LogInformation("Transaction recorded for BookingId: {BookingId}", message.BookingId);
            
            await Task.CompletedTask; // Placeholder
        }
    }
} 