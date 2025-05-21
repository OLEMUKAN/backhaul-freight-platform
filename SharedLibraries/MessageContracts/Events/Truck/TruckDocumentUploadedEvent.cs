namespace MessageContracts.Events.Truck
{
    /// <summary>
    /// Event published when a document is uploaded for a truck
    /// </summary>
    public class TruckDocumentUploadedEvent : TruckEventBase
    {
        /// <summary>
        /// Type of document uploaded (e.g., "LicensePlate", "RegistrationDocument", "Photo")
        /// </summary>
        public string DocumentType { get; set; } = string.Empty;
        
        /// <summary>
        /// URL to the uploaded document
        /// </summary>
        public string DocumentUrl { get; set; } = string.Empty;
    }
} 