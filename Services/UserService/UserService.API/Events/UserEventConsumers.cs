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
                user.HasVerifiedTruck = message.IsVerified; // Set based on the event
                await _userManager.UpdateAsync(user);
                
                _logger.LogInformation("Updated user {UserId} with HasVerifiedTruck = {IsVerified}", message.OwnerId, message.IsVerified);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing TruckVerified event for user {UserId}", message.OwnerId);
            }
        }
    }

    public class BookingCompletedConsumer : IConsumer<BookingCompletedEvent>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<BookingCompletedConsumer> _logger;

        public BookingCompletedConsumer(
            UserManager<ApplicationUser> userManager,
            ILogger<BookingCompletedConsumer> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<BookingCompletedEvent> context)
        {
            var message = context.Message;
            _logger.LogInformation("Consuming BookingCompleted event for BookingId: {BookingId}", message.BookingId);

            try
            {
                // Update Truck Owner's Rating
                if (message.TruckOwnerId != Guid.Empty && message.ShipperRatingGiven.HasValue && message.ShipperRatingGiven.Value >= 1 && message.ShipperRatingGiven.Value <= 5)
                {
                    var truckOwner = await _userManager.FindByIdAsync(message.TruckOwnerId.ToString());
                    if (truckOwner != null)
                    {
                        double currentTotalRating = (truckOwner.Rating ?? 0.0) * truckOwner.NumberOfRatings;
                        double newTotalRating = currentTotalRating + message.ShipperRatingGiven.Value;
                        truckOwner.NumberOfRatings++;
                        truckOwner.Rating = newTotalRating / truckOwner.NumberOfRatings;
                        await _userManager.UpdateAsync(truckOwner);
                        _logger.LogInformation("Updated rating for Truck Owner {TruckOwnerId}. New Rating: {Rating}, Total Ratings: {NumberOfRatings}",
                            truckOwner.Id, truckOwner.Rating, truckOwner.NumberOfRatings);
                    }
                    else
                    {
                        _logger.LogWarning("Truck Owner with ID {TruckOwnerId} not found.", message.TruckOwnerId);
                    }
                }

                // Update Shipper's Rating
                if (message.ShipperId != Guid.Empty && message.TruckOwnerRatingGiven.HasValue && message.TruckOwnerRatingGiven.Value >= 1 && message.TruckOwnerRatingGiven.Value <= 5)
                {
                    var shipper = await _userManager.FindByIdAsync(message.ShipperId.ToString());
                    if (shipper != null)
                    {
                        double currentTotalRating = (shipper.Rating ?? 0.0) * shipper.NumberOfRatings;
                        double newTotalRating = currentTotalRating + message.TruckOwnerRatingGiven.Value;
                        shipper.NumberOfRatings++;
                        shipper.Rating = newTotalRating / shipper.NumberOfRatings;
                        await _userManager.UpdateAsync(shipper);
                        _logger.LogInformation("Updated rating for Shipper {ShipperId}. New Rating: {Rating}, Total Ratings: {NumberOfRatings}",
                            shipper.Id, shipper.Rating, shipper.NumberOfRatings);
                    }
                    else
                    {
                        _logger.LogWarning("Shipper with ID {ShipperId} not found.", message.ShipperId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing BookingCompleted event for booking {BookingId}", message.BookingId);
            }
        }
    }
} 