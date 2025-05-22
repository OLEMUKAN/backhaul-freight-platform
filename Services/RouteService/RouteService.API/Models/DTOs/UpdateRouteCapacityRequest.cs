using System.ComponentModel.DataAnnotations;

namespace RouteService.API.Models.DTOs
{
    /// <summary>
    /// Data Transfer Object for updating the capacity of a route
    /// </summary>
    public class UpdateRouteCapacityRequest
    {
        /// <summary>
        /// The booking associated with this capacity change
        /// </summary>
        public Guid? BookingId { get; set; }
        
        /// <summary>
        /// The amount of weight capacity to add or remove in kilograms (use negative values for booking/removing capacity)
        /// </summary>
        [Required]
        public decimal CapacityChangeKg { get; set; }
        
        /// <summary>
        /// The amount of volume capacity to add or remove in cubic meters (use negative values for booking/removing capacity)
        /// </summary>
        public decimal? CapacityChangeM3 { get; set; }
        
        /// <summary>
        /// Reason for capacity change
        /// </summary>
        [StringLength(500)]
        public string? Reason { get; set; }
    }
} 