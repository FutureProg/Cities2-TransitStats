using Unity.Entities;

namespace TransitStats.Models.Transfers
{
    /// <summary>
    /// Event structure for recording a citizen transfer between transit lines.
    /// Includes trip origin (startingRoute) to enable filtering by starting route.
    /// 
    /// Queued by DetectTransferJob and processed by ProcessTransferEventsJob.
    /// </summary>
    public struct TransitTransferEvent
    {
        /// <summary>Route citizen transferred from</summary>
        public Entity fromRoute;
        
        /// <summary>Route citizen transferred to</summary>
        public Entity toRoute;
        
        /// <summary>First route in this trip (trip origin) - enables "trips from Route X" filtering</summary>
        public Entity startingRoute;
        
        /// <summary>Frames between boarding events (optional, for time analysis)</summary>
        public uint transferTimeFrames;
    }
}
