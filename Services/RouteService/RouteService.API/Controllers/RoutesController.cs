using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RouteService.API.Dtos.Routes;
using RouteService.API.Services.Interfaces;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

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
            var ownerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(ownerIdClaim) || !Guid.TryParse(ownerIdClaim, out var ownerId))
            {
                _logger.LogWarning("Owner ID claim is missing or invalid.");
                return Guid.Empty; // Or throw, but controller actions will return Unauthorized/Forbid
            }
            return ownerId;
        }

        [HttpPost]
        [Authorize(Policy = "TruckOwner")]
        public async Task<IActionResult> CreateRoute([FromBody] CreateRouteRequest request)
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

            try
            {
                _logger.LogInformation("Attempting to create route for Owner: {OwnerId}", ownerId);
                var routeDto = await _routeService.CreateRouteAsync(request, ownerId);
                _logger.LogInformation("Route {RouteId} created successfully for Owner: {OwnerId}", routeDto.Id, ownerId);
                return CreatedAtRoute(nameof(GetRouteById), new { id = routeDto.Id }, routeDto);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "ArgumentException while creating route for Owner: {OwnerId}.", ownerId);
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "UnauthorizedAccessException while creating route for Owner: {OwnerId}.", ownerId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while creating route for Owner: {OwnerId}.", ownerId);
                return StatusCode(500, "An unexpected error occurred while creating the route.");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetRoutes([FromQuery] RouteFilterRequest filter)
        {
            try
            {
                _logger.LogInformation("Fetching routes with filter: {@RouteFilter}", filter);
                var routes = await _routeService.GetRoutesAsync(filter);
                return Ok(routes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching routes with filter: {@RouteFilter}", filter);
                return StatusCode(500, "An error occurred while fetching routes.");
            }
        }

        [HttpGet("{id}", Name = "GetRouteById")]
        public async Task<IActionResult> GetRouteById(Guid id)
        {
            try
            {
                _logger.LogInformation("Fetching route by Id: {RouteId}", id);
                var routeDto = await _routeService.GetRouteByIdAsync(id);
                if (routeDto == null)
                {
                    _logger.LogWarning("Route with Id: {RouteId} not found.", id);
                    return NotFound();
                }
                return Ok(routeDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching route by Id: {RouteId}", id);
                return StatusCode(500, "An error occurred while fetching the route.");
            }
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "TruckOwner")]
        public async Task<IActionResult> UpdateRoute(Guid id, [FromBody] UpdateRouteRequest request)
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

            try
            {
                _logger.LogInformation("Attempting to update route {RouteId} by Owner: {OwnerId}", id, ownerId);
                var updatedRouteDto = await _routeService.UpdateRouteAsync(id, request, ownerId);
                if (updatedRouteDto == null)
                {
                    _logger.LogWarning("Route {RouteId} not found for update by Owner: {OwnerId}", id, ownerId);
                    return NotFound();
                }
                _logger.LogInformation("Route {RouteId} updated successfully by Owner: {OwnerId}", id, ownerId);
                return Ok(updatedRouteDto);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "ArgumentException while updating route {RouteId} by Owner: {OwnerId}.", id, ownerId);
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "UnauthorizedAccessException while updating route {RouteId} by Owner: {OwnerId}.", id, ownerId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while updating route {RouteId} by Owner: {OwnerId}.", id, ownerId);
                return StatusCode(500, "An unexpected error occurred while updating the route.");
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "TruckOwner")]
        public async Task<IActionResult> CancelRoute(Guid id)
        {
            var ownerId = GetOwnerIdFromClaims();
            if (ownerId == Guid.Empty)
            {
                return Unauthorized("Owner ID claim is missing or invalid.");
            }

            try
            {
                _logger.LogInformation("Attempting to cancel route {RouteId} by Owner: {OwnerId}", id, ownerId);
                var success = await _routeService.CancelRouteAsync(id, ownerId);
                if (!success)
                {
                    _logger.LogWarning("Route {RouteId} not found or could not be cancelled by Owner: {OwnerId}", id, ownerId);
                    return NotFound(); 
                }
                _logger.LogInformation("Route {RouteId} cancelled successfully by Owner: {OwnerId}", id, ownerId);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                 _logger.LogWarning(ex, "UnauthorizedAccessException while cancelling route {RouteId} by Owner: {OwnerId}.", id, ownerId);
                return Forbid();
            }
            catch (InvalidOperationException ex) // Could be thrown by service if route is in non-cancellable state
            {
                _logger.LogWarning(ex, "InvalidOperationException while cancelling route {RouteId} by Owner: {OwnerId}.", id, ownerId);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while cancelling route {RouteId} by Owner: {OwnerId}.", id, ownerId);
                return StatusCode(500, "An unexpected error occurred while cancelling the route.");
            }
        }

        [HttpPut("{id}/capacity")]
        [Authorize(Policy = "Admin")] // Placeholder policy as discussed
        public async Task<IActionResult> UpdateRouteCapacity(Guid id, [FromBody] UpdateRouteCapacityRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                _logger.LogInformation("Attempting to update capacity for route {RouteId}", id);
                var updatedRouteDto = await _routeService.UpdateRouteCapacityAsync(id, request);
                if (updatedRouteDto == null)
                {
                    _logger.LogWarning("Route {RouteId} not found for capacity update.", id);
                    return NotFound();
                }
                _logger.LogInformation("Capacity updated for Route {RouteId}.", id);
                return Ok(updatedRouteDto);
            }
            catch (ArgumentOutOfRangeException ex) // For invalid capacity values if service throws it
            {
                _logger.LogWarning(ex, "ArgumentOutOfRangeException while updating capacity for route {RouteId}.", id);
                return BadRequest(ex.Message);
            }
            catch (ArgumentException ex) // Generic argument issues
            {
                _logger.LogWarning(ex, "ArgumentException while updating capacity for route {RouteId}.", id);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while updating capacity for route {RouteId}.", id);
                return StatusCode(500, "An unexpected error occurred while updating route capacity.");
            }
        }
    }
}
