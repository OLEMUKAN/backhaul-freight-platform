namespace MessageContracts.Enums
{    /// <summary>
    /// Represents the possible statuses of a route
    /// </summary>
    public enum RouteStatus
    {
        /// <summary>
        /// Route is planned but hasn't started yet
        /// </summary>
        Planned = 1,
        
        /// <summary>
        /// Route is currently active
        /// </summary>
        Active = 2,
        
        /// <summary>
        /// Route has been completed
        /// </summary>
        Completed = 3,
        
        /// <summary>
        /// Route is in progress
        /// </summary>
        InProgress = 7,
        
        /// <summary>
        /// Route has been cancelled
        /// </summary>
        Cancelled = 4,
        
        /// <summary>
        /// Route is partially booked (has remaining capacity)
        /// </summary>
        BookedPartial = 5,
        
        /// <summary>
        /// Route is fully booked (no remaining capacity)
        /// </summary>
        BookedFull = 6
    }
} 