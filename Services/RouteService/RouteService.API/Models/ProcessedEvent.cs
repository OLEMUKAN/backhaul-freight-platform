using System;
using System.ComponentModel.DataAnnotations;

namespace RouteService.API.Models
{
    public class ProcessedEvent
    {
        [Key]
        public Guid EventId { get; set; } // This will store the unique BookingId from the event message.

        // Optional: Could add an EventType string if EventId alone isn't unique enough across different event sources.
        // public string EventType { get; set; } 

        public DateTimeOffset ProcessedAt { get; set; }
    }
}
