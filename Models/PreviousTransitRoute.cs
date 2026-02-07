using Colossal.Serialization.Entities;
using Unity.Entities;

namespace TransitStats.Models
{
    public partial struct PreviousTransitRoute : IComponentData, IQueryTypeParameter, ISerializable
    {
        /// <summary>Previous route entity the citizen was on</summary>
        public Entity route;

        /// <summary>Station/stop where they exited the previous route</summary>
        public Entity lastStation;

        /// <summary>Simulation frame when they exited the previous route</summary>
        public uint exitFrame;

        /// <summary>Trip purpose when they boarded (to validate same trip)</summary>
        public Game.Citizens.Purpose tripPurpose;

        /// <summary>Original trip destination (from TravelPurpose.m_Data)</summary>
        public Entity tripDestination;        

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(route);
            writer.Write(lastStation);
            writer.Write(exitFrame);
            writer.Write((byte)tripPurpose);
            writer.Write(tripDestination);            
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out route);
            reader.Read(out  lastStation);
            reader.Read(out exitFrame);
            byte purpose;
            reader.Read(out purpose);
            this.tripPurpose = (Game.Citizens.Purpose)purpose;
            reader.Read(out tripDestination);
        }
    }
}
