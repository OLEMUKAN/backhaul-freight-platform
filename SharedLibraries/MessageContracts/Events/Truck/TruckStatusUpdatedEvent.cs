namespace MessageContracts.Events.Truck
{
    /// <summary>
    /// Event published when a truck's status is updated
    /// </summary>
    public class TruckStatusUpdatedEvent : TruckEventBase
    {
        /// <summary>
        /// Previous status code (1=Active, 2=Inactive, 3=UnderMaintenance, 4=PendingVerification, 5=Rejected)
        /// </summary>
        public int PreviousStatus { get; set; }
        
        /// <summary>
        /// New status code (1=Active, 2=Inactive, 3=UnderMaintenance, 4=PendingVerification, 5=Rejected)
        /// </summary>
        public int NewStatus { get; set; }
    }
} 