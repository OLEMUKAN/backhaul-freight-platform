using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using UserService.API.Models.Dtos;
using UserService.API.Services;
using Microsoft.AspNetCore.Identity;
using UserService.API.Models;
using UserService.API.Models.Enums;

namespace UserService.API.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IAuthService _authService;
        private readonly ILogger<UsersController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public UsersController(
            IUserService userService, 
            IAuthService authService, 
            ILogger<UsersController> logger,
            UserManager<ApplicationUser> userManager)
        {
            _userService = userService;
            _authService = authService;
            _logger = logger;
            _userManager = userManager;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var result = await _userService.RegisterAsync(request);
                _logger.LogInformation("User registered successfully: {Email}", request.Email);
                return Ok(result);
            }
            catch (ApplicationException ex)
            {
                _logger.LogWarning("Registration failed: {Message}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            try
            {
                var user = await _userService.GetCurrentUserAsync();
                return Ok(user);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Unauthorized access: {Message}", ex.Message);
                return Unauthorized(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning("User not found: {Message}", ex.Message);
                return NotFound(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request)
        {
            try 
            {
                var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? string.Empty);
                var result = await _userService.UpdateUserAsync(userId, request);
                _logger.LogInformation("User profile updated: {UserId}", userId);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Unauthorized access: {Message}", ex.Message);
                return Unauthorized(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning("User not found: {Message}", ex.Message);
                return NotFound(new { message = ex.Message });
            }
            catch (ApplicationException ex)
            {
                _logger.LogWarning("Profile update failed: {Message}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById([FromRoute] Guid id)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                return Ok(user);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning("User not found: {Message}", ex.Message);
                return NotFound(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("verify/email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            try
            {
                var userId = User.FindFirst("sub")?.Value ?? string.Empty;
                var result = await _authService.VerifyEmailAsync(userId, request.Token);
                
                if (result)
                {
                    _logger.LogInformation("Email verified for user: {UserId}", userId);
                    return Ok(new { message = "Email verified successfully." });
                }
                
                return BadRequest(new { message = "Email verification failed." });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Email verification failed: {Message}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("verify/phone")]
        public async Task<IActionResult> VerifyPhone([FromBody] VerifyPhoneRequest request)
        {
            try
            {
                var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? string.Empty);
                var result = await _userService.VerifyPhoneAsync(userId, request.Code);
                
                if (result)
                {
                    _logger.LogInformation("Phone verified for user: {UserId}", userId);
                    return Ok(new { message = "Phone verified successfully." });
                }
                
                return BadRequest(new { message = "Phone verification failed." });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Phone verification failed: {Message}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("verify/phone/generate")]
        public async Task<IActionResult> GeneratePhoneVerificationCode()
        {
            try
            {
                var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? string.Empty);
                await _userService.GeneratePhoneVerificationCodeAsync(userId);
                
                _logger.LogInformation("Phone verification code generated for user: {UserId}", userId);
                return Ok(new { message = "Verification code sent to your phone." });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Phone verification code generation failed: {Message}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("validate/{id}")]
        public async Task<IActionResult> ValidateUserExists(Guid id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id.ToString());
                if (user == null)
                {
                    _logger.LogWarning("User validation failed: User {UserId} not found", id);
                    return NotFound();
                }
                
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating user {UserId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error validating user");
            }
        }

        [HttpGet("validate/{id}/role/{role}")]
        public async Task<IActionResult> ValidateUserRole(Guid id, string role)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id.ToString());
                if (user == null)
                {
                    _logger.LogWarning("User role validation failed: User {UserId} not found", id);
                    return NotFound("User not found");
                }
                  // Check if user has the required role
                var isInRole = await _userManager.IsInRoleAsync(user, role);
                
                // Also check if the user is active
                var isActive = user.Status == UserStatus.Active;
                
                if (!isInRole || !isActive)
                {
                    _logger.LogWarning("User role validation failed: User {UserId} is not an active {Role}", id, role);
                    return BadRequest($"User is not an active {role}");
                }
                
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating user {UserId} role {Role}", id, role);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error validating user role");
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateUserStatus(Guid id, [FromBody] UpdateUserStatusRequest request)
        {
            try
            {
                var result = await _userService.UpdateUserStatusAsync(id, request.Status);
                
                if (result)
                {
                    _logger.LogInformation("Admin updated status for user {UserId} to {Status}", id, request.Status);
                    return Ok(new { message = "User status updated successfully." });
                }
                
                return BadRequest(new { message = "Failed to update user status." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for user {UserId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error updating user status");
            }
        }
    }
}
