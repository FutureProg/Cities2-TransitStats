using Colossal.Serialization.Entities;
using Unity.Entities;

namespace TransitStats.Models.Transfers
{
    /// <summary>
    /// Tracks how many times a specific trip origin led to this transfer.
    /// Stored as a buffer on transfer tracking entities.
    /// 
    /// Example: "5 trips starting from Route 1 made this Route 2→3 transfer"
    /// 
    /// This enables accurate filtering:
    /// - Query "Route 1": Only count transfers with tripStartRoute == Route 1
    /// - Query "Route 2": Sum all origins where fromRoute == Route 2 OR has Route 2 origin
    /// </summary>
    public partial struct TransferOriginCount : IBufferElementData, ISerializable
    {
        /// <summary>The route where trips started (trip origin)</summary>
        public Entity tripStartRoute;

        /// <summary>Number of trips from this origin that made this transfer</summary>
        public int count;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(tripStartRoute);
            writer.Write(count);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out tripStartRoute);
            reader.Read(out count);
        }
    }
}
