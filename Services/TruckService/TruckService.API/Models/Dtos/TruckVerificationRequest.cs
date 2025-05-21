using System.ComponentModel.DataAnnotations;

namespace TruckService.API.Models.Dtos
{
    public class TruckVerificationRequest
    {
        [Required]
        public bool IsVerified { get; set; }

        [StringLength(500)]
        public string? VerificationNotes { get; set; }
    }
} 