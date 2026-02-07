using Colossal.Serialization.Entities;
using Unity.Entities;

namespace TransitStats.Models.Transfers
{
    /// <summary>
    /// Identifies which route transfer this entity tracks.
    /// Each entity represents one transfer pair (Route A → Route B).
    /// Used as graph edges in the transfer network.
    /// </summary>
    public partial struct TransferPairInfo : IComponentData, ISerializable
    {
        /// <summary>Route transferred from</summary>
        public Entity fromRoute;

        /// <summary>Route transferred to</summary>
        public Entity toRoute;

        /// <summary>Last frame this transfer was observed (for data aging/cleanup)</summary>
        public uint lastUpdatedFrame;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(fromRoute);
            writer.Write(toRoute);
            writer.Write(lastUpdatedFrame);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out fromRoute);
            reader.Read(out toRoute);
            reader.Read(out lastUpdatedFrame);
        }
    }
}
