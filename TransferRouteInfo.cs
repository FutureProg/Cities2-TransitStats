using Colossal.Serialization.Entities;
using Unity.Entities;

namespace TransitStats
{
    /// <summary>
    /// Component storing which route pair this entity tracks
    /// Attached to entities that have TransferStatisticSample buffers
    /// </summary>
    public partial struct TransferRouteInfo : IComponentData, ISerializable
    {

        public Entity fromRoute;
        public Entity toRoute;

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out fromRoute);
            reader.Read(out toRoute);
        }

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(fromRoute);
            writer.Write(toRoute);
        }
    }
}
