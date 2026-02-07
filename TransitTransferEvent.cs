using Game.Citizens;
using Game.Prefabs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;

namespace TransitStats
{
    public struct TransitTransferEvent
    {
        public Entity citizen;
        public Entity fromRoute;
        public Entity toRoute;
        public Entity transferStation;
        public TransportType fromType;
        public TransportType toType;
        public Purpose tripPurpose;
        public uint transformTimeFrames; // number of frames between routes
    }
}
