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
    public class BookingConfirmedEventConsumer : IConsumer<BookingConfirmedEvent>
    {
        private readonly IRouteService _routeService;
        private readonly ILogger<BookingConfirmedEventConsumer> _logger;
        private readonly RouteDbContext _dbContext;

        public BookingConfirmedEventConsumer(IRouteService routeService, ILogger<BookingConfirmedEventConsumer> logger, RouteDbContext dbContext)
        {
            _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task Consume(ConsumeContext<BookingConfirmedEvent> context)
        {
            var message = context.Message;
            var bookingId = message.BookingId; // EventId for ProcessedEvent table

            _logger.LogInformation("Received BookingConfirmedEvent for BookingId: {BookingId}, RouteId: {RouteId}.", 
                bookingId, message.RouteId);

            // Idempotency Check
            if (await _dbContext.ProcessedEvents.AnyAsync(pe => pe.EventId == bookingId, context.CancellationToken))
            {
                _logger.LogInformation("BookingConfirmedEvent for BookingId: {BookingId} already processed. Skipping.", bookingId);
                return;
            }

            _logger.LogInformation("Processing BookingConfirmedEvent for BookingId: {BookingId}. Reducing capacity for RouteId: {RouteId}.",
                bookingId, message.RouteId);

            var updateCapacityRequest = new UpdateRouteCapacityRequest
            {
                BookingId = bookingId,
                CapacityChangeKg = -message.BookedWeightKg, // Negative: capacity consumed
                CapacityChangeM3 = message.BookedVolumeM3.HasValue ? -message.BookedVolumeM3.Value : (decimal?)null, // Negative
                Reason = $"Booking {bookingId} confirmed."
            };

            try
            {
                var updatedRoute = await _routeService.UpdateRouteCapacityAsync(message.RouteId, updateCapacityRequest, context.CancellationToken);
                
                // Mark event as processed
                _dbContext.ProcessedEvents.Add(new ProcessedEvent { EventId = bookingId, ProcessedAt = DateTimeOffset.UtcNow });
                await _dbContext.SaveChangesAsync(context.CancellationToken);

                if (updatedRoute == null)
                {
                    _logger.LogWarning("Route {RouteId} not found or update failed when processing BookingConfirmedEvent {BookingId}. Event marked as processed.", 
                        message.RouteId, bookingId);
                    // Depending on requirements, could throw to NACK and retry/dead-letter, but after marking processed, this might be complex.
                    // For now, we log and acknowledge. If the route service call fails before this, it will be retried by MassTransit.
                }
                else
                {
                    _logger.LogInformation("Successfully updated capacity for RouteId: {RouteId} due to BookingConfirmedEvent {BookingId}. Event marked as processed.", 
                        message.RouteId, bookingId);
                }
            }
            catch (ArgumentException ex)
            {
                 _logger.LogWarning(ex, "ArgumentException while updating capacity for RouteId {RouteId} from BookingConfirmedEvent {BookingId}.", bookingId, message.BookingId);
                // Consider NACK for retries if it's a transient issue
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while processing BookingConfirmedEvent for BookingId: {BookingId}, RouteId: {RouteId}.",
                    message.BookingId, message.RouteId);
                // Throwing the exception will cause MassTransit to NACK the message and retry or move to error queue
                throw; 
            }
        }
    }
}
