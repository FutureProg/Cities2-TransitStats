using Colossal.Serialization.Entities;
using Unity.Entities;

namespace TransitStats.Models.Transfers
{
    /// <summary>
    /// Single historical sample of transfer counts.
    /// Stores snapshot of total transfers at a point in time.
    /// Pattern matches CityStatisticsSystem for time-series data.
    /// 
    /// Optional: Used for historical trend analysis, not required for basic Sankey visualization.
    /// </summary>
    public struct TransferStatisticSample : IBufferElementData, ISerializable
    {
        /// <summary>Frame when this sample was taken</summary>
        public uint sampleFrame;

        /// <summary>Number of transfers recorded in this sample period</summary>
        public uint transferCount;

        /// <summary>Average time (in frames) passengers spent in transit before transferring</summary>
        public uint averageTransferTime;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(sampleFrame);
            writer.Write(transferCount);
            writer.Write(averageTransferTime);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out sampleFrame);
            reader.Read(out transferCount);
            reader.Read(out averageTransferTime);
        }
    }
}
