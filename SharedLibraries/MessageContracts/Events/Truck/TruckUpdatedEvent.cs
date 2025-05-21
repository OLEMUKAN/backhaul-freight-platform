namespace MessageContracts.Events.Truck
{
    /// <summary>
    /// Event published when a truck's details are updated
    /// </summary>
    public class TruckUpdatedEvent : TruckEventBase
    {
        /// <summary>
        /// Updated manufacturer (null if not changed)
        /// </summary>
        public string? Make { get; set; }
        
        /// <summary>
        /// Updated model (null if not changed)
        /// </summary>
        public string? Model { get; set; }
        
        /// <summary>
        /// Updated weight capacity in kilograms (null if not changed)
        /// </summary>
        public decimal? CapacityKg { get; set; }
        
        /// <summary>
        /// Updated volume capacity in cubic meters (null if not changed)
        /// </summary>
        public decimal? CapacityM3 { get; set; }
        
        /// <summary>
        /// Updated truck type (null if not changed)
        /// </summary>
        public int? Type { get; set; }
    }
} 