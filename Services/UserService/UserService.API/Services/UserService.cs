using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Common.Messaging; // Added for IEventPublisher
using MessageContracts.Events.User; // Added for UserXXXEvent types
// UserService.API.Events; // Removed, assuming event DTOs are from MessageContracts
// using UserService.API.Services; // No longer needed for IEventPublisher
using UserService.API.Models;
using UserService.API.Models.Dtos;
using UserService.API.Models.Enums;

namespace UserService.API.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IAuthService _authService;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<UserService> _logger;

        public UserService(
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor httpContextAccessor,
            IAuthService authService,
            IEventPublisher eventPublisher,
            ILogger<UserService> logger)
        {
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _authService = authService;
            _eventPublisher = eventPublisher;
            _logger = logger;
        }

        public async Task<UserResponse> RegisterAsync(RegisterRequest request)
        {
            // Check if email is already registered
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                throw new ApplicationException("Email is already registered.");
            }
            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                Name = request.Name,
                Role = request.Role,
                RegistrationDate = DateTimeOffset.UtcNow,
                // DEV MODE: Set all verifications to true and status to Active
                Status = UserStatus.Active,
                IsEmailConfirmed = true,
                IsPhoneConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new ApplicationException($"User registration failed: {errors}");
            }

            // Assign ASP.NET Identity role for IsInRoleAsync to work
            await _userManager.AddToRoleAsync(user, user.Role.ToString());

            // Generate email verification token (to be used for email sending)
            await _authService.GenerateEmailVerificationTokenAsync(user);

            // Publish user created event
            await _eventPublisher.PublishAsync(new UserCreatedEvent
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                Name = user.Name,
                Role = (MessageContracts.Enums.UserRole)user.Role // Casted
            });

            // TODO: Send verification email - will be implemented in notification service

            return MapToUserResponse(user);
        }

        public async Task<UserResponse> GetUserByIdAsync(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID {id} not found.");
            }

            return MapToUserResponse(user);
        }

        public async Task<UserResponse> GetCurrentUserAsync()
        {
            var userId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            return await GetUserByIdAsync(userGuid);
        }

        public async Task<UserResponse> UpdateUserAsync(Guid userId, UpdateProfileRequest request)
        {
            var currentUserId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId) || Guid.Parse(currentUserId) != userId)
            {
                throw new UnauthorizedAccessException("You can only update your own profile.");
            }

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID {userId} not found.");
            }

            var userUpdated = false;
            var updateEvent = new UserUpdatedEvent
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                Role = (MessageContracts.Enums.UserRole)user.Role // Casted
            };

            // Update user properties
            if (!string.IsNullOrEmpty(request.Name) && user.Name != request.Name)
            {
                user.Name = request.Name;
                updateEvent.Name = request.Name;
                userUpdated = true;
            }

            if (!string.IsNullOrEmpty(request.PhoneNumber))
            {
                // If phone number changed, require verification again
                if (user.PhoneNumber != request.PhoneNumber)
                {
                    user.PhoneNumber = request.PhoneNumber;
                    user.IsPhoneConfirmed = false;
                    updateEvent.PhoneNumber = request.PhoneNumber;
                    updateEvent.IsPhoneConfirmed = false;
                    userUpdated = true;
                    // TODO: Send verification SMS
                }
            }

            if (!string.IsNullOrEmpty(request.ProfilePictureUrl) && user.ProfilePictureUrl != request.ProfilePictureUrl)
            {
                user.ProfilePictureUrl = request.ProfilePictureUrl;
                userUpdated = true;
            }

            if (userUpdated)
            {
                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    throw new ApplicationException($"User update failed: {errors}");
                }

                // Publish user updated event
                await _eventPublisher.PublishAsync(updateEvent);
            }

            return MapToUserResponse(user);
        }

        public async Task<bool> DeleteUserAsync(Guid id)
        {            // Check if admin or same user
            var currentUserId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUserRole = _httpContextAccessor.HttpContext?.User.FindFirstValue("role");
            
            if (string.IsNullOrEmpty(currentUserId) || 
                (Guid.Parse(currentUserId) != id && currentUserRole != UserRole.Admin.ToString("D")))
            {
                throw new UnauthorizedAccessException("You don't have permission to delete this user.");
            }

            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                return false;
            }

            var previousStatus = user.Status;
            
            // Soft delete - mark as inactive
            user.Status = UserStatus.Inactive;
            await _userManager.UpdateAsync(user);

            // Publish user status changed event
            await _eventPublisher.PublishAsync(new UserStatusChangedEvent
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                Role = (MessageContracts.Enums.UserRole)user.Role, // Casted
                PreviousStatus = (MessageContracts.Enums.UserStatus)previousStatus, // Casted
                NewStatus = (MessageContracts.Enums.UserStatus)user.Status // Casted
            });

            return true;
        }

        public async Task<string> GeneratePhoneVerificationCodeAsync(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID {userId} not found.");
            }

            // Generate 6-digit code
            var random = new Random();
            var code = random.Next(100000, 999999).ToString();

            // In a real implementation, store this securely with expiration
            // For now, we'll use ASP.NET Identity's phone token functionality
            var token = await _userManager.GenerateChangePhoneNumberTokenAsync(user, user.PhoneNumber ?? string.Empty);

            // Event published for NotificationService to send SMS
            await _eventPublisher.PublishAsync(new PhoneVerificationCodeGeneratedEvent
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                Role = (MessageContracts.Enums.UserRole)user.Role,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                VerificationCode = token
            });

            return token;
        }

        public async Task<bool> VerifyPhoneAsync(Guid userId, string code)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return false;
            }

            // Verify code
            var result = await _userManager.VerifyChangePhoneNumberTokenAsync(user, code, user.PhoneNumber ?? string.Empty);
            if (result)
            {
                var wasAlreadyVerified = user.IsPhoneConfirmed;
                user.IsPhoneConfirmed = true;
                await _userManager.UpdateAsync(user);

                if (!wasAlreadyVerified)
                {
                    // Publish verification event
                    await _eventPublisher.PublishAsync(new UserVerifiedEvent
                    {
                        UserId = user.Id,
                        Email = user.Email ?? string.Empty,
                        Role = (MessageContracts.Enums.UserRole)user.Role, // Casted
                        IsPhoneConfirmed = true, // Corrected property name
                        IsEmailConfirmed = user.IsEmailConfirmed // Corrected property name
                    });
                }

                return true;
            }

            return false;
        }        public async Task<bool> UpdateUserStatusAsync(Guid userId, UserStatus newStatus)
        {
            _logger.LogInformation("Updating status for user {UserId} to {Status}", userId, newStatus);
            
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                _logger.LogWarning("User not found with ID {UserId}", userId);
                return false;
            }
            
            var previousStatus = user.Status;
            
            // Only update if status is actually changing
            if (previousStatus == newStatus)
            {
                _logger.LogInformation("User {UserId} already has status {Status}, no change needed", userId, newStatus);
                return true;
            }
            
            user.Status = newStatus;
            var result = await _userManager.UpdateAsync(user);
            
            if (result.Succeeded)
            {
                _logger.LogInformation("Successfully updated status for user {UserId} from {PreviousStatus} to {NewStatus}", 
                    userId, previousStatus, newStatus);
                    
                // Publish status changed event
                await _eventPublisher.PublishAsync(new UserStatusChangedEvent // Removed Events. qualifier
                {
                    UserId = userId,
                    Email = user.Email ?? string.Empty, // Added Email assignment
                    Role = (MessageContracts.Enums.UserRole)user.Role, // Added Role assignment and cast
                    PreviousStatus = (MessageContracts.Enums.UserStatus)previousStatus, // Casted
                    NewStatus = (MessageContracts.Enums.UserStatus)newStatus // Casted
                });
                
                return true;
            }
            
            _logger.LogWarning("Failed to update status for user {UserId}: {Errors}", 
                userId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return false;
        }

        private UserResponse MapToUserResponse(ApplicationUser user)
        {
            return new UserResponse
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                Name = user.Name,
                Role = user.Role,
                IsEmailConfirmed = user.IsEmailConfirmed,
                IsPhoneConfirmed = user.IsPhoneConfirmed,
                Rating = user.Rating,
                RegistrationDate = user.RegistrationDate,
                LastLoginDate = user.LastLoginDate,
                Status = user.Status,
                ProfilePictureUrl = user.ProfilePictureUrl
            };
        }
    }
}