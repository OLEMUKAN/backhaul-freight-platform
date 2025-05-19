using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Threading.Tasks;
using UserService.API.Models.Dtos;
using UserService.API.Services;

namespace UserService.API.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var result = await _authService.LoginAsync(request);
                _logger.LogInformation("User login successful: {Email}", request.Email);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Login failed: {Message}", ex.Message);
                return Unauthorized(new { message = ex.Message });
            }
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var result = await _authService.RefreshTokenAsync(request.RefreshToken);
                return Ok(result);
            }
            catch (Exception ex) when (ex is SecurityTokenException || ex is ApplicationException)
            {
                _logger.LogWarning("Token refresh failed: {Message}", ex.Message);
                return Unauthorized(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
        {
            var result = await _authService.RevokeTokenAsync(request.RefreshToken);
            if (result)
            {
                _logger.LogInformation("User logged out successfully");
                return Ok(new { message = "Logged out successfully" });
            }
            
            return BadRequest(new { message = "Invalid token" });
        }

        [HttpPost("password/forgot")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                var token = await _authService.GeneratePasswordResetTokenAsync(request.Email);
                // TODO: Send email with token via notification service
                _logger.LogInformation("Password reset token generated for {Email}", request.Email);
                return Ok(new { message = "Password reset instructions sent to your email" });
            }
            catch (ApplicationException ex)
            {
                _logger.LogWarning("Password reset failed: {Message}", ex.Message);
                // Don't reveal if user exists or not
                return Ok(new { message = "If your email exists in our system, you will receive password reset instructions" });
            }
        }

        [HttpPost("password/reset")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var email = request.Email; // Make sure Email property exists
            var result = await _authService.ResetPasswordAsync(email, request.Token, request.NewPassword);
            if (result)
            {
                _logger.LogInformation("Password reset successful for {Email}", email);
                return Ok(new { message = "Password reset successful" });
            }
            
            _logger.LogWarning("Password reset failed for {Email}", email);
            return BadRequest(new { message = "Invalid or expired token" });
        }
    }
}
