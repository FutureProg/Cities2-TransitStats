using Colossal.Serialization.Entities;
using Unity.Entities;

namespace TransitStats.Models
{
    /// <summary>
    /// Tracks a citizen's active transit trip to detect transfers and maintain trip context.
    /// Stores the starting route to enable trip origin tracking for accurate Sankey filtering.
    /// </summary>
    public partial struct ActiveTransitTrip : IComponentData, IQueryTypeParameter, ISerializable
    {
        /// <summary>First route the citizen boarded in this trip (trip origin)</summary>
        public Entity startingRoute;

        /// <summary>Current/most recent route the citizen is on</summary>
        public Entity currentRoute;

        /// <summary>Purpose of the current trip (work, shopping, etc.)</summary>
        public Game.Citizens.Purpose tripPurpose;

        /// <summary>Frame when citizen last boarded a vehicle</summary>
        public uint lastBoardingFrame;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(startingRoute);
            writer.Write(currentRoute);
            writer.Write((byte)tripPurpose);
            writer.Write(lastBoardingFrame);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out startingRoute);
            reader.Read(out currentRoute);
            byte purpose;
            reader.Read(out purpose);
            tripPurpose = (Game.Citizens.Purpose) purpose;
            reader.Read(out lastBoardingFrame);
        }
    }
}
