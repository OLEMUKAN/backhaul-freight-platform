using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TruckService.API.Models;
using TruckService.API.Models.Dtos;
using Microsoft.AspNetCore.Http;

namespace TruckService.API.Services
{
    public interface ITruckService
    {
        Task<TruckResponse> CreateTruckAsync(Guid ownerId, CreateTruckRequest request);
        Task<IEnumerable<TruckResponse>> GetTrucksAsync(Guid? ownerId, int? status = null);
        Task<TruckResponse> GetTruckByIdAsync(Guid id);
        Task<TruckResponse> UpdateTruckAsync(Guid id, Guid requestingUserId, UpdateTruckRequest request);
        Task<bool> DeleteTruckAsync(Guid id, Guid requestingUserId);
        Task<TruckResponse> VerifyTruckAsync(Guid id, TruckVerificationRequest request);
        Task<string> UploadTruckDocumentAsync(Guid id, Guid requestingUserId, IFormFile file, string documentType);
    }
}
