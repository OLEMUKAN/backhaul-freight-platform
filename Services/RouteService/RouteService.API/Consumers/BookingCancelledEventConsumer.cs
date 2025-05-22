using MassTransit;
using Microsoft.Extensions.Logging;
using RouteService.API.Services.Interfaces;
using MessageContracts.Events.Booking; // Assuming this is the correct namespace
using System.Threading.Tasks;
using RouteService.API.Dtos.Routes; // For UpdateRouteCapacityRequest
using System;

namespace RouteService.API.Consumers
{
    public class BookingCancelledEventConsumer : IConsumer<BookingCancelledEvent>
    {
        private readonly IRouteService _routeService;
        private readonly ILogger<BookingCancelledEventConsumer> _logger;

        public BookingCancelledEventConsumer(IRouteService routeService, ILogger<BookingCancelledEventConsumer> logger)
        {
            _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Consume(ConsumeContext<BookingCancelledEvent> context)
        {
            var message = context.Message;
            _logger.LogInformation("Received BookingCancelledEvent for BookingId: {BookingId}, RouteId: {RouteId}. Restoring capacity.", 
                message.BookingId, message.RouteId);

            var updateCapacityRequest = new UpdateRouteCapacityRequest
            {
                BookingId = message.BookingId,
                CapacityChangeKg = message.BookedWeightKg, // Positive: capacity restored
                CapacityChangeM3 = message.BookedVolumeM3, // Positive
                Reason = $"Booking {message.BookingId} cancelled."
            };

            try
            {
                var updatedRoute = await _routeService.UpdateRouteCapacityAsync(message.RouteId, updateCapacityRequest);
                if (updatedRoute == null)
                {
                    _logger.LogWarning("Route {RouteId} not found or update failed when processing BookingCancelledEvent {BookingId}.", 
                        message.RouteId, message.BookingId);
                    // Depending on requirements, could throw to NACK and retry/dead-letter
                }
                else
                {
                    _logger.LogInformation("Successfully updated capacity for RouteId: {RouteId} due to BookingCancelledEvent {BookingId}.", 
                        message.RouteId, message.BookingId);
                }
            }
            catch (ArgumentException ex)
            {
                 _logger.LogWarning(ex, "ArgumentException while updating capacity for RouteId {RouteId} from BookingCancelledEvent {BookingId}.", message.RouteId, message.BookingId);
                // Consider NACK for retries if it's a transient issue
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while processing BookingCancelledEvent for BookingId: {BookingId}, RouteId: {RouteId}.",
                    message.BookingId, message.RouteId);
                // Throwing the exception will cause MassTransit to NACK the message and retry or move to error queue
                throw;
            }
        }
    }
}
