using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UserService.API.Models;
using UserService.API.Models.Dtos;

namespace UserService.API.Services
{
    public interface IUserService
    {
        Task<UserResponse> RegisterAsync(RegisterRequest request);
        Task<UserResponse> GetUserByIdAsync(Guid id);
        Task<UserResponse> GetCurrentUserAsync();
        Task<UserResponse> UpdateUserAsync(Guid userId, UpdateProfileRequest request);
        Task<bool> DeleteUserAsync(Guid id);
        Task<string> GeneratePhoneVerificationCodeAsync(Guid userId);
        Task<bool> VerifyPhoneAsync(Guid userId, string code);
    }
} 