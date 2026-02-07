using Colossal.Serialization.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;

namespace TransitStats
{
    /// <summary>
    /// Single sample of transfer count (added each update cycle)
    /// Similar to CityStatistic - stores historical samples in a DynamicBuffer
    /// </summary>
    public partial struct TransferStatisticSample: IBufferElementData, ISerializable
    {
        public int count; // the number of transfers in this sample
        public uint frameIndex; // frame when the sample was recorded

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out count);
            reader.Read(out frameIndex);
        }

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(count);
            writer.Write(frameIndex);
        }
    }
}
