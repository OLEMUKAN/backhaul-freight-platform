using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using TruckService.API.Models.Dtos;
using TruckService.API.Services;

namespace TruckService.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TrucksController : ControllerBase
    {
        private readonly ITruckService _truckService;
        private readonly ILogger<TrucksController> _logger;

        public TrucksController(
            ITruckService truckService,
            ILogger<TrucksController> logger)
        {
            _truckService = truckService ?? throw new ArgumentNullException(nameof(truckService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: api/trucks
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<TruckResponse>>> GetTrucks([FromQuery] int? status = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                var userRole = GetCurrentUserRole();
                
                // If user is admin, allow them to query all trucks
                Guid? ownerFilter = userRole == "Admin" ? null : userId;
                
                _logger.LogInformation("User {UserId} with role {Role} requesting trucks list", userId, userRole);
                
                var trucks = await _truckService.GetTrucksAsync(ownerFilter, status);
                return Ok(trucks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting trucks");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving trucks");
            }
        }

        // GET: api/trucks/{id}
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<TruckResponse>> GetTruck(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var userRole = GetCurrentUserRole();
                
                _logger.LogInformation("User {UserId} with role {Role} requesting truck {TruckId}", userId, userRole, id);
                
                var truck = await _truckService.GetTruckByIdAsync(id);
                
                // If the truck doesn't belong to the user and the user is not an admin, forbid access
                if (truck.OwnerId != userId && userRole != "Admin")
                {
                    _logger.LogWarning("User {UserId} attempted to access truck {TruckId} owned by {OwnerId}", 
                        userId, id, truck.OwnerId);
                    return Forbid();
                }
                
                return Ok(truck);
            }
            catch (KeyNotFoundException)
            {
                _logger.LogWarning("Truck not found with ID {TruckId}", id);
                return NotFound($"No truck found with ID {id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting truck {TruckId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving truck");
            }
        }

        // POST: api/trucks
        [HttpPost]
        [Authorize(Roles = "TruckOwner")]
        public async Task<ActionResult<TruckResponse>> CreateTruck([FromBody] CreateTruckRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                
                _logger.LogInformation("User {UserId} creating new truck with registration {Registration}", 
                    userId, request.RegistrationNumber);
                
                var truck = await _truckService.CreateTruckAsync(userId, request);
                
                return CreatedAtAction(nameof(GetTruck), new { id = truck.Id }, truck);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex.Message);
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating truck");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error creating truck");
            }
        }

        // PUT: api/trucks/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "TruckOwner")]
        public async Task<ActionResult<TruckResponse>> UpdateTruck(Guid id, [FromBody] UpdateTruckRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                
                _logger.LogInformation("User {UserId} updating truck {TruckId}", userId, id);
                
                var truck = await _truckService.UpdateTruckAsync(id, userId, request);
                
                return Ok(truck);
            }
            catch (KeyNotFoundException)
            {
                _logger.LogWarning("Truck not found with ID {TruckId}", id);
                return NotFound($"No truck found with ID {id}");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("User {UserId} attempted unauthorized update of truck {TruckId}", GetCurrentUserId(), id);
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating truck {TruckId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error updating truck");
            }
        }

        // DELETE: api/trucks/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "TruckOwner")]
        public async Task<ActionResult> DeleteTruck(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                
                _logger.LogInformation("User {UserId} deleting truck {TruckId}", userId, id);
                
                var result = await _truckService.DeleteTruckAsync(id, userId);
                
                if (!result)
                {
                    return NotFound($"No truck found with ID {id}");
                }
                
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("User {UserId} attempted unauthorized deletion of truck {TruckId}", GetCurrentUserId(), id);
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting truck {TruckId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error deleting truck");
            }
        }

        // POST: api/trucks/{id}/verify
        [HttpPost("{id}/verify")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<TruckResponse>> VerifyTruck(Guid id, [FromBody] VerifyTruckRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                
                _logger.LogInformation("Admin {UserId} verifying truck {TruckId}", userId, id);
                
                var truckVerificationRequest = new TruckVerificationRequest
                {
                    IsVerified = request.IsVerified,
                    VerificationNotes = request.VerificationNotes
                };
                
                var truck = await _truckService.VerifyTruckAsync(id, truckVerificationRequest);
                
                return Ok(truck);
            }
            catch (KeyNotFoundException)
            {
                _logger.LogWarning("Truck not found with ID {TruckId}", id);
                return NotFound($"No truck found with ID {id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying truck {TruckId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error verifying truck");
            }
        }

        // POST: api/trucks/{id}/documents
        [HttpPost("{id}/documents")]
        [Authorize(Roles = "TruckOwner")]
        public async Task<ActionResult<string>> UploadTruckDocument(Guid id, [FromForm] IFormFile file, [FromQuery] string documentType)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest("No file uploaded");
                }
                
                if (string.IsNullOrWhiteSpace(documentType))
                {
                    return BadRequest("Document type is required");
                }
                
                var userId = GetCurrentUserId();
                
                _logger.LogInformation("User {UserId} uploading {DocumentType} document for truck {TruckId}", 
                    userId, documentType, id);
                
                var documentUrl = await _truckService.UploadTruckDocumentAsync(id, userId, file, documentType);
                
                return Ok(new { url = documentUrl });
            }
            catch (KeyNotFoundException)
            {
                _logger.LogWarning("Truck not found with ID {TruckId}", id);
                return NotFound($"No truck found with ID {id}");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("User {UserId} attempted unauthorized document upload for truck {TruckId}", 
                    GetCurrentUserId(), id);
                return Forbid(ex.Message);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document for truck {TruckId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error uploading document");
            }
        }
          // Helper methods for getting user identity information
        private Guid GetCurrentUserId()
        {
            // Try multiple claim types
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                             User.FindFirst("sub")?.Value ??
                             User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                _logger.LogWarning("Invalid user ID claim in token. Available claims: {Claims}", 
                    string.Join(", ", User.Claims.Select(c => $"{c.Type}:{c.Value}")));
                throw new InvalidOperationException("User ID claim is missing or invalid");
            }
            
            return userId;
        }
        
        private string GetCurrentUserRole()
        {
            // Try multiple role claim types
            var role = User.FindFirst(ClaimTypes.Role)?.Value ??
                      User.FindFirst("role")?.Value ??
                      User.FindFirst("roleName")?.Value;
            
            return role ?? "Unknown";
        }
    }
}