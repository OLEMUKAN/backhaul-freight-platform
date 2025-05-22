using System.ComponentModel.DataAnnotations;

namespace RouteService.API.Models.DTOs
{
    /// <summary>
    /// Data Transfer Object for filtering routes
    /// </summary>
    public class RouteFilterRequest
    {
        /// <summary>
        /// Filter by truck owner ID
        /// </summary>
        public Guid? OwnerId { get; set; }
        
        /// <summary>
        /// Filter by truck ID
        /// </summary>
        public Guid? TruckId { get; set; }
        
        /// <summary>
        /// Filter by return leg status
        /// </summary>
        public bool? IsReturnLeg { get; set; }
        
        /// <summary>
        /// Filter by route status
        /// </summary>
        public int? Status { get; set; }
        
        /// <summary>
        /// Filter by minimum available weight capacity in kilograms
        /// </summary>
        public decimal? MinCapacityKg { get; set; }
        
        /// <summary>
        /// Filter by minimum available volume capacity in cubic meters
        /// </summary>
        public decimal? MinCapacityM3 { get; set; }
        
        /// <summary>
        /// Filter by departure time range (start)
        /// </summary>
        public DateTimeOffset? DepartAfter { get; set; }
        
        /// <summary>
        /// Filter by departure time range (end)
        /// </summary>
        public DateTimeOffset? DepartBefore { get; set; }
        
        /// <summary>
        /// Filter by arrival time range (start)
        /// </summary>
        public DateTimeOffset? ArriveAfter { get; set; }
        
        /// <summary>
        /// Filter by arrival time range (end)
        /// </summary>
        public DateTimeOffset? ArriveBefore { get; set; }
        
        /// <summary>
        /// Origin point coordinates for proximity searching [longitude, latitude]
        /// </summary>
        public double[]? NearOrigin { get; set; }
        
        /// <summary>
        /// Maximum distance from origin point in kilometers for proximity searching
        /// </summary>
        [Range(0, 500)]
        public double? OriginRadiusKm { get; set; }
        
        /// <summary>
        /// Destination point coordinates for proximity searching [longitude, latitude]
        /// </summary>
        public double[]? NearDestination { get; set; }
        
        /// <summary>
        /// Maximum distance from destination point in kilometers for proximity searching
        /// </summary>
        [Range(0, 500)]
        public double? DestinationRadiusKm { get; set; }
        
        /// <summary>
        /// Page number (1-based)
        /// </summary>
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;
        
        /// <summary>
        /// Page size
        /// </summary>
        [Range(1, 100)]
        public int PageSize { get; set; } = 20;
    }
} 