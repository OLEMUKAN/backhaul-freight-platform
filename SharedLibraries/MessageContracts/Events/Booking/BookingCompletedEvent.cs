namespace MessageContracts.Events.Booking
{
    /// <summary>
    /// Event published when a booking is completed
    /// </summary>
    public class BookingCompletedEvent : BookingEventBase
    {
        /// <summary>
        /// When the booking was completed
        /// </summary>
        public DateTimeOffset CompletionDate { get; set; }
        
        /// <summary>
        /// Rating given by the shipper (1-5)
        /// </summary>
        public int? ShipperRatingGiven { get; set; }
        
        /// <summary>
        /// Rating given by the truck owner (1-5)
        /// </summary>
        public int? TruckOwnerRatingGiven { get; set; }
        
        /// <summary>
        /// Final agreed price for the booking
        /// </summary>
        public decimal AgreedPrice { get; set; }
    }
} 