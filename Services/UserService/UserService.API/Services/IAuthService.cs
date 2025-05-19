using System.Threading.Tasks;
using UserService.API.Models;
using UserService.API.Models.Dtos;

namespace UserService.API.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request);
        Task<LoginResponse> RefreshTokenAsync(string refreshToken);
        Task<bool> RevokeTokenAsync(string refreshToken);
        Task<string> GenerateEmailVerificationTokenAsync(ApplicationUser user);
        Task<bool> VerifyEmailAsync(string userId, string token);
        Task<string> GeneratePasswordResetTokenAsync(string email);
        Task<bool> ResetPasswordAsync(string email, string token, string newPassword);
    }
} 