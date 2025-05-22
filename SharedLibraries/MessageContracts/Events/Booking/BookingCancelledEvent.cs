namespace MessageContracts.Events.Booking
{
    /// <summary>
    /// Event published when a booking is cancelled
    /// </summary>
    public class BookingCancelledEvent : BookingEventBase
    {
        /// <summary>
        /// The booked weight in kilograms that will be returned to route capacity
        /// </summary>
        public decimal BookedWeightKg { get; set; }
        
        /// <summary>
        /// The booked volume in cubic meters that will be returned to route capacity
        /// </summary>
        public decimal BookedVolumeM3 { get; set; }
        
        /// <summary>
        /// When the booking was cancelled
        /// </summary>
        public DateTimeOffset CancellationDate { get; set; }
        
        /// <summary>
        /// Optional reason for cancellation
        /// </summary>
        public string? CancellationReason { get; set; }
    }
}
