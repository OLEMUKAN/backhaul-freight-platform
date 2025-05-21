using System;
using System.Threading.Tasks;

namespace TruckService.API.Services
{
    public interface IUserValidationService
    {
        Task<bool> ValidateUserExistsAsync(Guid userId);
        Task<bool> ValidateUserIsActiveAsync(Guid userId, string requiredRole = "TruckOwner");
        Task<bool> CheckUserServiceHealthAsync();
    }
} 