using Colossal.Serialization.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;

namespace TransitStats.Models.Transfers
{
    /// <summary>
    /// Used to mark Transfer Pairs that have references to be updated after the End of Frame command buffer is processed.
    /// </summary>
    public partial struct TransferPairToUpdate : IComponentData, ISerializable
    {
        public int hash;
        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(hash);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out hash);
        }
    }
}
