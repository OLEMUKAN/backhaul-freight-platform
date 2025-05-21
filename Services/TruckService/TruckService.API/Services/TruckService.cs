using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TruckService.API.Data;
using TruckService.API.Events;
using TruckService.API.Models;
using TruckService.API.Models.Dtos;
using MessageContracts.Events.Truck;

namespace TruckService.API.Services
{
    public class TruckService : ITruckService
    {
        private readonly TruckDbContext _dbContext;
        private readonly IBlobStorageService _blobStorageService;
        private readonly IEventPublisher _eventPublisher;
        private readonly IUserValidationService _userValidationService;
        private readonly ILogger<TruckService> _logger;
        private const string TruckDocumentsContainer = "truck-documents";
        private const string TruckPhotosContainer = "truck-photos";

        public TruckService(
            TruckDbContext dbContext,
            IBlobStorageService blobStorageService,
            IEventPublisher eventPublisher,
            IUserValidationService userValidationService,
            ILogger<TruckService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
            _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
            _userValidationService = userValidationService ?? throw new ArgumentNullException(nameof(userValidationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TruckResponse> CreateTruckAsync(Guid ownerId, CreateTruckRequest request)
        {
            _logger.LogInformation("Creating new truck for owner {OwnerId} with registration {Registration}", 
                ownerId, request.RegistrationNumber);

            // Validate that user exists and is an active truck owner
            var userIsValid = await _userValidationService.ValidateUserIsActiveAsync(ownerId);
            if (!userIsValid)
            {
                _logger.LogWarning("Truck creation failed: Owner {OwnerId} is not a valid active truck owner", ownerId);
                throw new InvalidOperationException($"User with ID {ownerId} is not a valid active truck owner.");
            }

            // Check if registration number is already used
            var registrationExists = await _dbContext.Trucks
                .AnyAsync(t => t.RegistrationNumber == request.RegistrationNumber);

            if (registrationExists)
            {
                _logger.LogWarning("Truck creation failed: Registration number {Registration} already exists", 
                    request.RegistrationNumber);
                throw new InvalidOperationException($"A truck with registration number '{request.RegistrationNumber}' already exists.");
            }

            var truck = new Truck
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                RegistrationNumber = request.RegistrationNumber,
                Make = request.Make,
                Model = request.Model,
                Year = request.Year,
                CapacityKg = request.CapacityKg,
                CapacityM3 = request.CapacityM3,
                Type = request.Type,
                CargoAreaLengthM = request.CargoAreaLengthM,
                CargoAreaWidthM = request.CargoAreaWidthM,
                CargoAreaHeightM = request.CargoAreaHeightM,
                Status = 4, // PendingVerification
                IsVerified = false,
                Photos = new string[0],
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.Trucks.Add(truck);
            await _dbContext.SaveChangesAsync();

            // Publish event
            await _eventPublisher.PublishTruckRegisteredAsync(new TruckRegisteredEvent
            {
                TruckId = truck.Id,
                OwnerId = truck.OwnerId,
                RegistrationNumber = truck.RegistrationNumber,
                Make = truck.Make,
                Model = truck.Model,
                Year = truck.Year,
                CapacityKg = truck.CapacityKg,
                CapacityM3 = truck.CapacityM3,
                Type = truck.Type
            });

            _logger.LogInformation("Successfully created truck {TruckId} for owner {OwnerId}", 
                truck.Id, truck.OwnerId);

            return MapToTruckResponse(truck);
        }

        public async Task<IEnumerable<TruckResponse>> GetTrucksAsync(Guid? ownerId, int? status = null)
        {
            IQueryable<Truck> query = _dbContext.Trucks;

            if (ownerId.HasValue)
            {
                _logger.LogInformation("Filtering trucks by owner {OwnerId}", ownerId);
                query = query.Where(t => t.OwnerId == ownerId);
            }

            if (status.HasValue)
            {
                _logger.LogInformation("Filtering trucks by status {Status}", status);
                query = query.Where(t => t.Status == status.Value);
            }

            var trucks = await query.ToListAsync();
            
            _logger.LogInformation("Retrieved {Count} trucks", trucks.Count);
            
            return trucks.Select(MapToTruckResponse);
        }

        public async Task<TruckResponse> GetTruckByIdAsync(Guid id)
        {
            var truck = await _dbContext.Trucks.FindAsync(id);
            
            if (truck == null)
            {
                _logger.LogWarning("Truck not found with ID {TruckId}", id);
                throw new KeyNotFoundException($"No truck found with ID {id}");
            }
            
            _logger.LogInformation("Retrieved truck {TruckId}", id);
            
            return MapToTruckResponse(truck);
        }

        public async Task<TruckResponse> UpdateTruckAsync(Guid id, Guid requestingUserId, UpdateTruckRequest request)
        {
            var truck = await _dbContext.Trucks.FindAsync(id);
            
            if (truck == null)
            {
                _logger.LogWarning("Update failed: Truck not found with ID {TruckId}", id);
                throw new KeyNotFoundException($"No truck found with ID {id}");
            }
            
            // Check if the requester is the owner of the truck
            if (truck.OwnerId != requestingUserId)
            {
                _logger.LogWarning("Update failed: User {UserId} is not the owner of truck {TruckId}", 
                    requestingUserId, id);
                throw new UnauthorizedAccessException("You are not authorized to update this truck");
            }
            
            _logger.LogInformation("Updating truck {TruckId}", id);
            
            bool hasChanges = false;
            var updateEvent = new TruckUpdatedEvent
            {
                TruckId = truck.Id,
                OwnerId = truck.OwnerId
            };
            
            // Update properties if they're included in the request
            if (request.Make != null)
            {
                truck.Make = request.Make;
                updateEvent.Make = request.Make;
                hasChanges = true;
            }
            
            if (request.Model != null)
            {
                truck.Model = request.Model;
                updateEvent.Model = request.Model;
                hasChanges = true;
            }
            
            if (request.CapacityKg.HasValue)
            {
                truck.CapacityKg = request.CapacityKg.Value;
                updateEvent.CapacityKg = request.CapacityKg.Value;
                hasChanges = true;
            }
            
            if (request.CapacityM3.HasValue)
            {
                truck.CapacityM3 = request.CapacityM3.Value;
                updateEvent.CapacityM3 = request.CapacityM3.Value;
                hasChanges = true;
            }
            
            if (request.Type.HasValue)
            {
                truck.Type = request.Type.Value;
                updateEvent.Type = request.Type.Value;
                hasChanges = true;
            }
            
            if (request.CargoAreaLengthM.HasValue)
            {
                truck.CargoAreaLengthM = request.CargoAreaLengthM.Value;
                hasChanges = true;
            }
            
            if (request.CargoAreaWidthM.HasValue)
            {
                truck.CargoAreaWidthM = request.CargoAreaWidthM.Value;
                hasChanges = true;
            }
            
            if (request.CargoAreaHeightM.HasValue)
            {
                truck.CargoAreaHeightM = request.CargoAreaHeightM.Value;
                hasChanges = true;
            }
            
            if (request.Status.HasValue)
            {
                var previousStatus = truck.Status;
                truck.Status = request.Status.Value;
                hasChanges = true;
                
                // Publish status updated event
                await _eventPublisher.PublishTruckStatusUpdatedAsync(new TruckStatusUpdatedEvent
                {
                    TruckId = truck.Id,
                    OwnerId = truck.OwnerId,
                    PreviousStatus = previousStatus,
                    NewStatus = request.Status.Value
                });
            }
            
            if (hasChanges)
            {
                truck.UpdatedAt = DateTimeOffset.UtcNow;
                await _dbContext.SaveChangesAsync();
                
                // Publish general update event
                await _eventPublisher.PublishTruckUpdatedAsync(updateEvent);
                
                _logger.LogInformation("Successfully updated truck {TruckId}", id);
            }
            else
            {
                _logger.LogInformation("No changes detected for truck {TruckId}", id);
            }
            
            return MapToTruckResponse(truck);
        }

        public async Task<bool> DeleteTruckAsync(Guid id, Guid requestingUserId)
        {
            var truck = await _dbContext.Trucks.FindAsync(id);
            
            if (truck == null)
            {
                _logger.LogWarning("Delete failed: Truck not found with ID {TruckId}", id);
                return false;
            }
            
            // Check if the requester is the owner of the truck
            if (truck.OwnerId != requestingUserId)
            {
                _logger.LogWarning("Delete failed: User {UserId} is not the owner of truck {TruckId}", 
                    requestingUserId, id);
                throw new UnauthorizedAccessException("You are not authorized to delete this truck");
            }
            
            _logger.LogInformation("Deleting truck {TruckId}", id);
            
            // Logic to handle active routes or bookings could be added here
            // For now, we'll just mark the truck as inactive rather than physically deleting it
            truck.Status = 2; // Inactive
            truck.UpdatedAt = DateTimeOffset.UtcNow;
            
            await _dbContext.SaveChangesAsync();
            
            // Publish truck deleted event
            await _eventPublisher.PublishTruckDeletedAsync(new TruckDeletedEvent
            {
                TruckId = truck.Id,
                OwnerId = truck.OwnerId,
                RegistrationNumber = truck.RegistrationNumber,
                Reason = "Owner requested deletion"
            });
            
            _logger.LogInformation("Successfully marked truck {TruckId} as inactive", id);
            
            return true;
        }

        public async Task<TruckResponse> VerifyTruckAsync(Guid id, TruckVerificationRequest request)
        {
            var truck = await _dbContext.Trucks.FindAsync(id);
            
            if (truck == null)
            {
                _logger.LogWarning("Verification failed: Truck not found with ID {TruckId}", id);
                throw new KeyNotFoundException($"No truck found with ID {id}");
            }
            
            _logger.LogInformation("Verifying truck {TruckId}, setting IsVerified to {IsVerified}", 
                id, request.IsVerified);
            
            truck.IsVerified = request.IsVerified;
            truck.VerificationNotes = request.VerificationNotes ?? string.Empty;
            
            // If verified, update status to Active, otherwise to Rejected
            if (request.IsVerified)
            {
                truck.Status = 1; // Active
            }
            else if (truck.Status == 4) // Only change to Rejected if currently PendingVerification
            {
                truck.Status = 5; // Rejected
            }
            
            truck.UpdatedAt = DateTimeOffset.UtcNow;
            
            await _dbContext.SaveChangesAsync();
            
            // Publish verification event
            await _eventPublisher.PublishTruckVerifiedAsync(new TruckVerifiedEvent
            {
                TruckId = truck.Id,
                OwnerId = truck.OwnerId,
                IsVerified = truck.IsVerified,
                VerificationNotes = truck.VerificationNotes,
                VerifiedAt = truck.UpdatedAt
            });
            
            _logger.LogInformation("Successfully verified truck {TruckId}", id);
            
            return MapToTruckResponse(truck);
        }

        public async Task<string> UploadTruckDocumentAsync(Guid id, Guid requestingUserId, IFormFile file, string documentType)
        {
            var truck = await _dbContext.Trucks.FindAsync(id);
            
            if (truck == null)
            {
                _logger.LogWarning("Upload failed: Truck not found with ID {TruckId}", id);
                throw new KeyNotFoundException($"No truck found with ID {id}");
            }
            
            // Check if the requester is the owner of the truck
            if (truck.OwnerId != requestingUserId)
            {
                _logger.LogWarning("Upload failed: User {UserId} is not the owner of truck {TruckId}", 
                    requestingUserId, id);
                throw new UnauthorizedAccessException("You are not authorized to upload documents for this truck");
            }
            
            _logger.LogInformation("Uploading {DocumentType} document for truck {TruckId}", documentType, id);
            
            // Determine the container based on document type
            string containerName = documentType.ToLower() == "photo" ? TruckPhotosContainer : TruckDocumentsContainer;
            string filePrefix = $"truck-{id}-{documentType.ToLower()}";
            
            // Upload the file to blob storage
            string blobUrl = await _blobStorageService.UploadFileAsync(file, containerName, filePrefix);
            
            // Update the truck record based on document type
            switch (documentType.ToLower())
            {
                case "licenseplate":
                    // Delete old file if exists
                    if (!string.IsNullOrEmpty(truck.LicensePlateImageUrl))
                    {
                        await _blobStorageService.DeleteFileAsync(truck.LicensePlateImageUrl, TruckDocumentsContainer);
                    }
                    truck.LicensePlateImageUrl = blobUrl;
                    break;
                    
                case "registrationdocument":
                    // Delete old file if exists
                    if (!string.IsNullOrEmpty(truck.RegistrationDocumentUrl))
                    {
                        await _blobStorageService.DeleteFileAsync(truck.RegistrationDocumentUrl, TruckDocumentsContainer);
                    }
                    truck.RegistrationDocumentUrl = blobUrl;
                    break;
                    
                case "photo":
                    // Add to photos array
                    var photos = truck.Photos?.ToList() ?? new List<string>();
                    photos.Add(blobUrl);
                    truck.Photos = photos.ToArray();
                    break;
                    
                default:
                    _logger.LogWarning("Unknown document type: {DocumentType}", documentType);
                    throw new ArgumentException($"Unknown document type: {documentType}");
            }
            
            truck.UpdatedAt = DateTimeOffset.UtcNow;
            
            await _dbContext.SaveChangesAsync();
            
            // Publish document uploaded event
            await _eventPublisher.PublishTruckDocumentUploadedAsync(new TruckDocumentUploadedEvent
            {
                TruckId = truck.Id,
                OwnerId = truck.OwnerId,
                DocumentType = documentType,
                DocumentUrl = blobUrl
            });
            
            _logger.LogInformation("Successfully uploaded {DocumentType} for truck {TruckId}", documentType, id);
            
            return blobUrl;
        }

        // Helper method to map from domain entity to response DTO
        private TruckResponse MapToTruckResponse(Truck truck)
        {
            return new TruckResponse
            {
                Id = truck.Id,
                OwnerId = truck.OwnerId,
                RegistrationNumber = truck.RegistrationNumber,
                Make = truck.Make,
                Model = truck.Model,
                Year = truck.Year,
                CapacityKg = truck.CapacityKg,
                CapacityM3 = truck.CapacityM3,
                Type = truck.Type,
                TypeName = TruckEnumMappings.GetTruckTypeName(truck.Type),
                CargoAreaLengthM = truck.CargoAreaLengthM,
                CargoAreaWidthM = truck.CargoAreaWidthM,
                CargoAreaHeightM = truck.CargoAreaHeightM,
                LicensePlateImageUrl = truck.LicensePlateImageUrl,
                RegistrationDocumentUrl = truck.RegistrationDocumentUrl,
                Photos = truck.Photos,
                Status = truck.Status,
                StatusName = TruckEnumMappings.GetTruckStatusName(truck.Status),
                IsVerified = truck.IsVerified,
                CreatedAt = truck.CreatedAt,
                UpdatedAt = truck.UpdatedAt
            };
        }
    }
} 