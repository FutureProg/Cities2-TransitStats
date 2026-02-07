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
    [InternalBufferCapacity(0)]
    public partial struct TransferStatisticSample : IBufferElementData, ISerializable
    {
        /// <summary>Total transfers this sample period (sum of all origin counts)</summary>
        public int totalCount;

        /// <summary>Frame when this sample was taken</summary>
        public uint sampleFrame;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(totalCount);
            writer.Write(sampleFrame);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out totalCount);
            reader.Read(out sampleFrame);
        }
    }
}
