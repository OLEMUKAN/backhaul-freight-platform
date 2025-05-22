using System;

namespace MessageContracts.Events.Booking
{
    /// <summary>
    /// Event published when a booking is confirmed
    /// </summary>
    public class BookingConfirmedEvent : BookingEventBase
    {
        /// <summary>
        /// Date when the booking was confirmed
        /// </summary>
        public DateTimeOffset ConfirmationDate { get; set; }
        
        /// <summary>
        /// Identifier of the truck assigned to this booking
        /// </summary>
        public Guid TruckId { get; set; }
        
        /// <summary>
        /// Expected pickup time
        /// </summary>
        public DateTimeOffset ScheduledPickupTime { get; set; }
        
        /// <summary>
        /// Expected delivery time
        /// </summary>
        public DateTimeOffset EstimatedDeliveryTime { get; set; }
          /// <summary>
        /// The booked weight in kilograms that will be consumed from route capacity
        /// </summary>
        public decimal BookedWeightKg { get; set; }
        
        /// <summary>
        /// The booked volume in cubic meters that will be consumed from route capacity
        /// </summary>
        public decimal BookedVolumeM3 { get; set; }
    }
}
