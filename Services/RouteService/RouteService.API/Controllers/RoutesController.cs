using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RouteService.API.Models.DTOs;
using RouteService.API.Services.Interfaces;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace RouteService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RoutesController : ControllerBase
    {
        private readonly IRouteService _routeService;
        private readonly ILogger<RoutesController> _logger;

        public RoutesController(IRouteService routeService, ILogger<RoutesController> logger)
        {
            _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private Guid GetOwnerIdFromClaims()
        {
            // Try multiple claim types
            var ownerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                             User.FindFirst("sub")?.Value ??
                             User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            if (string.IsNullOrEmpty(ownerIdClaim) || !Guid.TryParse(ownerIdClaim, out var ownerId))
            {
                _logger.LogWarning("Invalid user ID claim in token. Available claims: {Claims}", 
                    string.Join(", ", User.Claims.Select(c => $"{c.Type}:{c.Value}")));
                return Guid.Empty; // Or throw, but controller actions will return Unauthorized/Forbid
            }
            return ownerId;
        }

        [HttpPost]
        [Authorize(Roles = "TruckOwner")]
        public async Task<IActionResult> CreateRoute([FromBody] CreateRouteRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var ownerId = GetOwnerIdFromClaims();
            if (ownerId == Guid.Empty)
            {
                return Unauthorized("Owner ID claim is missing or invalid.");
            }

            // Exceptions are now handled by GlobalExceptionHandlerMiddleware.
            // Specific business logic checks (like ownerId or if service returns null) remain.
            _logger.LogInformation("Attempting to create route for Owner: {OwnerId}", ownerId);
            var routeDto = await _routeService.CreateRouteAsync(request, ownerId, cancellationToken);
            _logger.LogInformation("Route {RouteId} created successfully for Owner: {OwnerId}", routeDto.Id, ownerId);
            return CreatedAtRoute(nameof(GetRouteById), new { id = routeDto.Id }, routeDto);
        }

        [HttpGet]
        public async Task<IActionResult> GetRoutes([FromQuery] RouteFilterRequest filter, CancellationToken cancellationToken)
        {
            // Exceptions are now handled by GlobalExceptionHandlerMiddleware.
            _logger.LogInformation("Fetching routes with filter: {@RouteFilter}", filter);
            var routes = await _routeService.GetRoutesAsync(filter, cancellationToken);
            return Ok(routes);
        }

        [HttpGet("{id}", Name = "GetRouteById")]
        public async Task<IActionResult> GetRouteById(Guid id, CancellationToken cancellationToken)
        {
            // Exceptions are now handled by GlobalExceptionHandlerMiddleware.
            _logger.LogInformation("Fetching route by Id: {RouteId}", id);
            var routeDto = await _routeService.GetRouteByIdAsync(id, cancellationToken);
            if (routeDto == null)
            {
                // This is a normal "not found" scenario, not an exception.
                _logger.LogWarning("Route with Id: {RouteId} not found.", id);
                return NotFound(); 
            }
            return Ok(routeDto);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "TruckOwner")]
        public async Task<IActionResult> UpdateRoute(Guid id, [FromBody] UpdateRouteRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var ownerId = GetOwnerIdFromClaims();
            if (ownerId == Guid.Empty)
            {
                return Unauthorized("Owner ID claim is missing or invalid.");
            }

            // Exceptions are now handled by GlobalExceptionHandlerMiddleware.
            _logger.LogInformation("Attempting to update route {RouteId} by Owner: {OwnerId}", id, ownerId);
            var updatedRouteDto = await _routeService.UpdateRouteAsync(id, request, ownerId, cancellationToken);
            if (updatedRouteDto == null)
            {
                // This is a normal "not found" scenario, not an exception.
                _logger.LogWarning("Route {RouteId} not found for update by Owner: {OwnerId}", id, ownerId);
                return NotFound();
            }
            _logger.LogInformation("Route {RouteId} updated successfully by Owner: {OwnerId}", id, ownerId);
            return Ok(updatedRouteDto);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "TruckOwner")]
        public async Task<IActionResult> CancelRoute(Guid id, CancellationToken cancellationToken)
        {
            var ownerId = GetOwnerIdFromClaims();
            if (ownerId == Guid.Empty)
            {
                return Unauthorized("Owner ID claim is missing or invalid.");
            }

            // Exceptions like UnauthorizedAccessException or InvalidOperationException will be caught by middleware.
            _logger.LogInformation("Attempting to cancel route {RouteId} by Owner: {OwnerId}", id, ownerId);
            var success = await _routeService.CancelRouteAsync(id, ownerId, cancellationToken);
            if (!success)
            {
                // This is a normal "not found" or "cannot cancel" scenario handled by service logic, not an exception.
                _logger.LogWarning("Route {RouteId} not found or could not be cancelled by Owner: {OwnerId}", id, ownerId);
                return NotFound(); 
            }
            _logger.LogInformation("Route {RouteId} cancelled successfully by Owner: {OwnerId}", id, ownerId);
            return NoContent();
        }

        [HttpPut("{id}/capacity")]
        [Authorize(Roles = "Admin")] // Placeholder policy as discussed
        public async Task<IActionResult> UpdateRouteCapacity(Guid id, [FromBody] UpdateRouteCapacityRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            // Exceptions like ArgumentOutOfRangeException or ArgumentException will be caught by middleware.
            _logger.LogInformation("Attempting to update capacity for route {RouteId}", id);
            var updatedRouteDto = await _routeService.UpdateRouteCapacityAsync(id, request, cancellationToken);
            if (updatedRouteDto == null)
            {
                // This is a normal "not found" scenario, not an exception.
                _logger.LogWarning("Route {RouteId} not found for capacity update.", id);
                return NotFound();
            }
            _logger.LogInformation("Capacity updated for Route {RouteId}.", id);
            return Ok(updatedRouteDto);
        }
    }
}
