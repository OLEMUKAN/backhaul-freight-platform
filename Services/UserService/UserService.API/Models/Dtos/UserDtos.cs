using System;
using System.ComponentModel.DataAnnotations;
using UserService.API.Models.Enums;

namespace UserService.API.Models.Dtos
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{6,}$", 
            ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one digit, and one special character")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be at least 2 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Role is required")]
        public UserRole Role { get; set; } = UserRole.Shipper;
    }

    public class UpdateProfileRequest
    {
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be at least 2 characters")]
        public string? Name { get; set; }

        [Phone(ErrorMessage = "Invalid phone number format")]
        public string? PhoneNumber { get; set; }

        [Url(ErrorMessage = "Invalid URL format")]
        public string? ProfilePictureUrl { get; set; }
    }    public class UserResponse
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public bool IsEmailConfirmed { get; set; }
        public bool IsPhoneConfirmed { get; set; }
        public decimal? Rating { get; set; }
        public DateTimeOffset RegistrationDate { get; set; }
        public DateTimeOffset? LastLoginDate { get; set; }
        public UserStatus Status { get; set; }
        public string? ProfilePictureUrl { get; set; }
    }

    public class VerifyEmailRequest 
    { 
        [Required]
        public string Token { get; set; } = string.Empty; 
    }
    
    public class VerifyPhoneRequest 
    { 
        [Required]
        [StringLength(6, MinimumLength = 4, ErrorMessage = "Code must be between 4 and 6 characters")]
        public string Code { get; set; } = string.Empty; 
    }
    
    public class ForgotPasswordRequest 
    { 
        [Required]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty; 
    }
    
    public class ResetPasswordRequest 
    { 
        [Required]
        public string Token { get; set; } = string.Empty; 
        
        [Required]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{6,}$", 
            ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one digit, and one special character")]
        public string NewPassword { get; set; } = string.Empty; 
    }
}