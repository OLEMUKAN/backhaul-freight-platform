using MassTransit;
using Microsoft.Extensions.Logging;
using RouteService.API.Services.Interfaces;
using MessageContracts.Events.Booking; // Assuming this is the correct namespace
using System.Threading.Tasks;
using RouteService.API.Dtos.Routes; // For UpdateRouteCapacityRequest
using System;
using RouteService.API.Data; // For RouteDbContext
using Microsoft.EntityFrameworkCore; // For AnyAsync
using RouteService.API.Models; // For ProcessedEvent

namespace RouteService.API.Consumers
{
    public class BookingCancelledEventConsumer : IConsumer<BookingCancelledEvent>
    {
        private readonly IRouteService _routeService;
        private readonly ILogger<BookingCancelledEventConsumer> _logger;
        private readonly RouteDbContext _dbContext;

        public BookingCancelledEventConsumer(IRouteService routeService, ILogger<BookingCancelledEventConsumer> logger, RouteDbContext dbContext)
        {
            _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task Consume(ConsumeContext<BookingCancelledEvent> context)
        {
            var message = context.Message;
            var bookingId = message.BookingId; // EventId for ProcessedEvent table

            _logger.LogInformation("Received BookingCancelledEvent for BookingId: {BookingId}, RouteId: {RouteId}.", 
                bookingId, message.RouteId);

            // Idempotency Check
            if (await _dbContext.ProcessedEvents.AnyAsync(pe => pe.EventId == bookingId, context.CancellationToken))
            {
                _logger.LogInformation("BookingCancelledEvent for BookingId: {BookingId} already processed. Skipping.", bookingId);
                return;
            }
            
            _logger.LogInformation("Processing BookingCancelledEvent for BookingId: {BookingId}. Restoring capacity for RouteId: {RouteId}.",
                bookingId, message.RouteId);

            var updateCapacityRequest = new UpdateRouteCapacityRequest
            {
                BookingId = bookingId,
                CapacityChangeKg = message.BookedWeightKg, // Positive: capacity restored
                CapacityChangeM3 = message.BookedVolumeM3, // Positive
                Reason = $"Booking {bookingId} cancelled."
            };

            try
            {
                var updatedRoute = await _routeService.UpdateRouteCapacityAsync(message.RouteId, updateCapacityRequest, context.CancellationToken);

                // Mark event as processed
                _dbContext.ProcessedEvents.Add(new ProcessedEvent { EventId = bookingId, ProcessedAt = DateTimeOffset.UtcNow });
                await _dbContext.SaveChangesAsync(context.CancellationToken);

                if (updatedRoute == null)
                {
                    _logger.LogWarning("Route {RouteId} not found or update failed when processing BookingCancelledEvent {BookingId}. Event marked as processed.", 
                        message.RouteId, bookingId);
                    // Depending on requirements, could throw to NACK and retry/dead-letter
                }
                else
                {
                    _logger.LogInformation("Successfully updated capacity for RouteId: {RouteId} due to BookingCancelledEvent {BookingId}. Event marked as processed.", 
                        message.RouteId, bookingId);
                }
            }
            catch (ArgumentException ex)
            {
                 _logger.LogWarning(ex, "ArgumentException while updating capacity for RouteId {RouteId} from BookingCancelledEvent {BookingId}.", bookingId, message.BookingId);
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
