using System;
using System.Text.Json.Serialization;

namespace TruckService.API.Models
{
    public class Truck
    {
        public Guid Id { get; set; }
        public Guid OwnerId { get; set; }
        public string RegistrationNumber { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int Year { get; set; }
        public decimal CapacityKg { get; set; }
        public decimal? CapacityM3 { get; set; }
        public int Type { get; set; } // 1=Flatbed, 2=BoxTruck, 3=Tipper, etc.
        public decimal? CargoAreaLengthM { get; set; }
        public decimal? CargoAreaWidthM { get; set; }
        public decimal? CargoAreaHeightM { get; set; }
        public string? LicensePlateImageUrl { get; set; }
        public string? RegistrationDocumentUrl { get; set; }
        public string[]? Photos { get; set; }
        public int Status { get; set; } // 1=Active, 2=Inactive, 3=UnderMaintenance, 4=PendingVerification, 5=Rejected
        public bool IsVerified { get; set; }
        public string? VerificationNotes { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
} 