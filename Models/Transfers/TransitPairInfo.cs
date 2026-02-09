using Colossal.Serialization.Entities;
using Unity.Entities;

namespace TransitStats.Models.Transfers
{
    /// <summary>
    /// Identifies which route transfer this entity tracks.
    /// Each entity represents one transfer pair (Route A → Route B).
    /// Used as graph edges in the transfer network.
    /// </summary>
    public struct TransferPairInfo : IComponentData, ISerializable
    {
        /// <summary>Route transferred from</summary>
        public Entity fromRoute;

        /// <summary>Route transferred to</summary>
        public Entity toRoute;

        /// <summary>Total number of transfers recorded for this pair</summary>
        public int totalTransfers;

        /// <summary>Last frame this transfer was observed (for data aging/cleanup)</summary>
        public uint lastTransferFrame;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(fromRoute);
            writer.Write(toRoute);
            writer.Write(totalTransfers);
            writer.Write(lastTransferFrame);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out fromRoute);
            reader.Read(out toRoute);
            reader.Read(out totalTransfers);
            reader.Read(out lastTransferFrame);
        }
    }
}
