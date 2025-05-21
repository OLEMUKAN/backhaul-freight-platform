namespace MessageContracts.Events.Truck
{
    /// <summary>
    /// Event published when a truck is deleted from the system
    /// </summary>
    public class TruckDeletedEvent : TruckEventBase
    {
        /// <summary>
        /// Truck's registration number
        /// </summary>
        public string RegistrationNumber { get; set; } = string.Empty;
        
        /// <summary>
        /// Reason for deletion
        /// </summary>
        public string Reason { get; set; } = string.Empty;
    }
} 