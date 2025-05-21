using System;
using System.ComponentModel.DataAnnotations;

namespace TruckService.API.Models.Dtos
{
    // Request DTOs
    public class CreateTruckRequest
    {
        [Required]
        [StringLength(50)]
        public string RegistrationNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Make { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Model { get; set; } = string.Empty;

        [Required]
        [Range(1900, 2100)]
        public int Year { get; set; }

        [Required]
        [Range(0.01, 100000)]
        public decimal CapacityKg { get; set; }

        [Range(0.01, 10000)]
        public decimal? CapacityM3 { get; set; }

        [Required]
        [Range(1, 10)]
        public int Type { get; set; } // 1=Flatbed, 2=BoxTruck, 3=Tipper, etc.

        [Range(0.01, 30)]
        public decimal? CargoAreaLengthM { get; set; }

        [Range(0.01, 10)]
        public decimal? CargoAreaWidthM { get; set; }
        
        [Range(0.01, 10)]
        public decimal? CargoAreaHeightM { get; set; }
    }

    public class UpdateTruckRequest
    {
        [StringLength(100)]
        public string? Make { get; set; }

        [StringLength(100)]
        public string? Model { get; set; }

        [Range(0.01, 100000)]
        public decimal? CapacityKg { get; set; }

        [Range(0.01, 10000)]
        public decimal? CapacityM3 { get; set; }

        [Range(1, 10)]
        public int? Type { get; set; }

        [Range(0.01, 30)]
        public decimal? CargoAreaLengthM { get; set; }

        [Range(0.01, 10)]
        public decimal? CargoAreaWidthM { get; set; }
        
        [Range(0.01, 10)]
        public decimal? CargoAreaHeightM { get; set; }

        [Range(1, 5)]
        public int? Status { get; set; }
    }

    public class VerifyTruckRequest
    {
        [Required]
        public bool IsVerified { get; set; }

        [StringLength(500)]
        public string? VerificationNotes { get; set; }
    }

    public class UploadTruckDocumentRequest
    {
        [Required]
        public string DocumentType { get; set; } = string.Empty; // "LicensePlate", "RegistrationDocument", "Photo"
    }

    // Response DTOs
    public class TruckResponse
    {
        public Guid Id { get; set; }
        public Guid OwnerId { get; set; }
        public string RegistrationNumber { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int Year { get; set; }
        public decimal CapacityKg { get; set; }
        public decimal? CapacityM3 { get; set; }
        public int Type { get; set; }
        public string TypeName { get; set; } = string.Empty; // Friendly name like "Flatbed", "Box Truck"
        public decimal? CargoAreaLengthM { get; set; }
        public decimal? CargoAreaWidthM { get; set; }
        public decimal? CargoAreaHeightM { get; set; }
        public string? LicensePlateImageUrl { get; set; }
        public string? RegistrationDocumentUrl { get; set; }
        public string[]? Photos { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty; // Friendly name like "Active", "Under Maintenance"
        public bool IsVerified { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    // Helper class to convert between numeric types and friendly names
    public static class TruckEnumMappings
    {
        public static string GetTruckTypeName(int typeValue) => typeValue switch
        {
            1 => "Flatbed",
            2 => "Box Truck",
            3 => "Tipper",
            4 => "Refrigerated",
            5 => "Tanker",
            6 => "Container",
            7 => "Livestock",
            8 => "Car Carrier",
            9 => "Log Carrier",
            10 => "Other",
            _ => "Unknown"
        };

        public static string GetTruckStatusName(int statusValue) => statusValue switch
        {
            1 => "Active",
            2 => "Inactive",
            3 => "Under Maintenance",
            4 => "Pending Verification",
            5 => "Rejected",
            _ => "Unknown"
        };
    }
} 