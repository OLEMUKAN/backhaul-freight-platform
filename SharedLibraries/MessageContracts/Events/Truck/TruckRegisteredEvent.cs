namespace MessageContracts.Events.Truck
{
    /// <summary>
    /// Event published when a new truck is registered
    /// </summary>
    public class TruckRegisteredEvent : TruckEventBase
    {
        /// <summary>
        /// Truck's registration number (license plate)
        /// </summary>
        public string RegistrationNumber { get; set; } = string.Empty;
        
        /// <summary>
        /// Truck manufacturer
        /// </summary>
        public string Make { get; set; } = string.Empty;
        
        /// <summary>
        /// Truck model
        /// </summary>
        public string Model { get; set; } = string.Empty;
        
        /// <summary>
        /// Manufacturing year
        /// </summary>
        public int Year { get; set; }
        
        /// <summary>
        /// Maximum weight capacity in kilograms
        /// </summary>
        public decimal CapacityKg { get; set; }
        
        /// <summary>
        /// Maximum volume capacity in cubic meters
        /// </summary>
        public decimal? CapacityM3 { get; set; }
        
        /// <summary>
        /// Type of truck (1=Flatbed, 2=BoxTruck, 3=Tipper, etc.)
        /// </summary>
        public int Type { get; set; }
    }
} 