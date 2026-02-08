using Colossal.Entities;
using Colossal.UI.Binding;
using Game.Prefabs;
using Game.Routes;
using Game.UI;
using Game.UI.InGame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TransitStats.Models.Transfers;
using TransitStats.Systems.TransitTransfers.Extensions;
using Unity.Collections;
using Unity.Entities;

namespace TransitStats.Systems
{
    /// <summary>
    /// Provides transit transfer data formatted for Sankey chart visualization.
    /// Supports filtering by selected route to show only trips originating from that route.
    /// Similar to ProductionDataSankey component structure.
    /// 
    /// Key Feature: Accurately filters Person A vs Person B based on trip origin.
    /// Example:
    ///   - Person A: Route 1 → 2 → 3
    ///   - Person B: Route 2 → 3
    ///   - Query "Route 1": Shows only Person A's transfers
    ///   - Query "Route 2": Shows both Person A and B transfers
    /// Integration: Extends InfoSectionBase for proper Selected Info Panel integration.
    /// The section automatically appears/disappears based on the 'visible' property.
    /// </summary>
    public partial class TransitTransferUISystem : ExtendedInfoSectionBase
    {
        private NameSystem nameSystem;
        private ImageSystem imageSystem;
        private ValueBindingHelper<TransitTransferSankeyData> transferDataBinding;
        private EntityQuery transportLineQuery;
        private EntityQuery transferPairQuery;
        private Entity previousSelectedEntity;

        /// <summary>
        /// Mod identifier - used for data binding namespace and section registration.
        /// IMPORTANT: Must match the namespace used in UI component mapping.
        /// </summary>
        protected override string ModId => "TransitStats";

        /// <summary>
        /// Group identifier for the Selected Info Panel section.
        /// This value is used by the game to register this system as a section provider.
        /// </summary>
        protected override string group => ModId;

        /// <summary>
        /// Optional: Allow this section to show for upgrades too.
        /// </summary>
        protected override bool displayForUpgrades => false;

        protected override void OnCreate()
        {
            base.OnCreate();

            nameSystem = World.GetOrCreateSystemManaged<NameSystem>();
            imageSystem = World.GetOrCreateSystemManaged<ImageSystem>();

            transportLineQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<TransportLine>(),
                    ComponentType.ReadOnly<PrefabRef>()
                }
            });

            transferPairQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<TransferPairInfo>(),
                    ComponentType.ReadOnly<TransferOriginCount>()
                }
            });

            // Create binding using helper method
            transferDataBinding = CreateBinding(
                "transferSankeyData",
                new TransitTransferSankeyData { nodes = new SankeyNode[0], links = new SankeyLink[0] }
            );

            previousSelectedEntity = Entity.Null;
            this.m_InfoUISystem.AddMiddleSection(this);
        }

        /// <summary>
        /// Write section properties to JSON for the UI.
        /// Called when the section needs to serialize its data.
        /// </summary>
        public override void OnWriteProperties(IJsonWriter writer)
        {
            // Write any additional properties if needed
            // The data binding is handled separately
        }

        /// <summary>
        /// Core processing logic - called by InfoSectionBase system.
        /// </summary>
        protected override void OnProcess()
        {
            // Main processing happens in OnUpdate for this system
        }

        /// <summary>
        /// Reset section state.
        /// </summary>
        protected override void Reset()
        {
            previousSelectedEntity = Entity.Null;
            transferDataBinding.Update(new TransitTransferSankeyData
            {
                nodes = new SankeyNode[0],
                links = new SankeyLink[0]
            });
        }

        protected override void OnUpdate()
        {
            // Get the currently selected entity
            Entity selectedEntity = this.selectedEntity;

            // Determine if this section should be visible
            bool shouldBeVisible = false;

            if (selectedEntity != Entity.Null &&
                selectedEntity != previousSelectedEntity &&
                EntityManager.HasComponent<TransportLine>(selectedEntity))
            {
                Mod.log.Info("Selected entity is a transport line");
                // Check if it's a public transit line (not cargo/work routes)
                if (EntityManager.TryGetComponent<PrefabRef>(selectedEntity, out PrefabRef prefabRef) &&
                    EntityManager.TryGetComponent<TransportLineData>(prefabRef.m_Prefab, out TransportLineData lineData))
                {
                    Mod.log.Info(lineData.m_PassengerTransport
                        ? $"Selected line {selectedEntity.Index} is passenger transport - showing transfer section"
                        : $"Selected line {selectedEntity.Index} is NOT passenger transport - hiding transfer section");
                    // Only show for passenger transport (exclude cargo)
                    shouldBeVisible = lineData.m_PassengerTransport;
                }
            }

            // Update visibility
            base.visible = true;//shouldBeVisible;            

            // Update data when visible and selection changes
            if (visible)
            {
                if (selectedEntity != previousSelectedEntity)
                {
                    previousSelectedEntity = selectedEntity;
                    var sankeyData = BuildSankeyDataForRoute(selectedEntity);
                    transferDataBinding.Update(sankeyData);
                }
            }
            else
            {
                // Clear data when not visible
                if (previousSelectedEntity != Entity.Null)
                {
                    previousSelectedEntity = Entity.Null;
                    transferDataBinding.Update(new TransitTransferSankeyData
                    {
                        nodes = new SankeyNode[0],
                        links = new SankeyLink[0]
                    });
                }
            }
        }

        /// <summary>
        /// Builds Sankey data showing transfers for trips that started from selectedRoute.
        /// Only includes passengers who actually rode the selected route.
        /// 
        /// Logic:
        /// - Direct transfers FROM selectedRoute: include ALL passengers (they started here)
        /// - Downstream transfers: only include passengers whose trip started at selectedRoute
        /// </summary>
        private TransitTransferSankeyData BuildSankeyDataForRoute(Entity selectedRoute)
        {
            var nodes = new List<SankeyNode>();
            var links = new List<SankeyLink>();
            var routeSet = new HashSet<Entity>();
            var transferCounts = new Dictionary<(Entity, Entity), int>();

            // Query all transfer pair entities
            var transferEntities = transferPairQuery.ToEntityArray(Allocator.Temp);

            foreach (var transferEntity in transferEntities)
            {
                var pairInfo = EntityManager.GetComponentData<TransferPairInfo>(transferEntity);
                var origins = EntityManager.GetBuffer<TransferOriginCount>(transferEntity);

                int transferCount = 0;

                // Direct transfers FROM selected route: include ALL passengers
                if (pairInfo.fromRoute == selectedRoute)
                {
                    // Sum all origins (all passengers making this direct transfer)
                    foreach (var origin in origins)
                    {
                        transferCount += origin.count;
                    }
                }
                else
                {
                    // Downstream transfers: only include passengers whose trip started at selectedRoute
                    foreach (var origin in origins)
                    {
                        if (origin.tripStartRoute == selectedRoute)
                        {
                            transferCount = origin.count;
                            break;
                        }
                    }
                }

                // Only include if there are trips from selectedRoute
                if (transferCount > 0)
                {
                    routeSet.Add(pairInfo.fromRoute);
                    routeSet.Add(pairInfo.toRoute);
                    transferCounts[(pairInfo.fromRoute, pairInfo.toRoute)] = transferCount;
                }
            }

            transferEntities.Dispose();

            // Build links
            foreach (var kvp in transferCounts)
            {
                var (fromRoute, toRoute) = kvp.Key;
                links.Add(new SankeyLink
                {
                    source = fromRoute.Index.ToString(),
                    target = toRoute.Index.ToString(),
                    value = kvp.Value,
                    color = GetRouteColor(fromRoute)
                });
            }

            // Build nodes from unique routes
            foreach (Entity route in routeSet)
            {
                nodes.Add(CreateNodeFromRoute(route));
            }

            return new TransitTransferSankeyData
            {
                nodes = nodes.ToArray(),
                links = links.ToArray()
            };
        }

        private SankeyNode CreateNodeFromRoute(Entity route)
        {
            // Get route information
            string routeName = "Route";
            string iconUrl = "coui://uil/Standard/Bus.svg";
            string color = "#4b91e2";

            // Try to get custom name first (handles user-renamed routes)
            if (nameSystem.TryGetCustomName(route, out string customName))
            {
                routeName = customName;
            }
            else
            {
                // Fall back to route number
                if (EntityManager.TryGetComponent<RouteNumber>(route, out RouteNumber routeNumber))
                {
                    // Use entity index as unique identifier if route number is reused
                    // but display the route number for readability
                    routeName = $"Route {routeNumber.m_Number}";
                }
                else
                {
                    // Last resort: use entity index
                    routeName = $"Route {route.Index}";
                }
            }

            // Get icon from ImageSystem (handles all entity types properly)
            iconUrl = imageSystem.GetInstanceIcon(route) ?? "coui://uil/Standard/Bus.svg";

            // Get route color
            if (EntityManager.TryGetComponent<Game.Routes.Color>(route, out Game.Routes.Color routeColor))
            {
                color = GetRouteColor(route);
            }

            return new SankeyNode
            {
                id = route.Index.ToString(),
                name = routeName,
                iconUrl = iconUrl,
                color = color
            };
        }

        private string GetRouteColor(Entity route)
        {
            if (EntityManager.TryGetComponent(route, out Color routeColor))
            {
                // Convert Unity color to hex string
                return $"#{(int)(routeColor.m_Color.r * 255):X2}{(int)(routeColor.m_Color.g * 255):X2}{(int)(routeColor.m_Color.b * 255):X2}";
            }
            return "#4b91e2"; // Default blue
        }
    }

    // Data structures for UI binding

    public struct TransitTransferSankeyData
    {
        public SankeyNode[] nodes;
        public SankeyLink[] links;
    }

    public struct SankeyNode
    {
        public string id;
        public string name;
        public string iconUrl;
        public string color;
    }

    public struct SankeyLink
    {
        public string source;
        public string target;
        public int value;
        public string color;
    }
}
