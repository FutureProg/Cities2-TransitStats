using Colossal.Entities;
using Colossal.UI.Binding;
using Game.Prefabs;
using Game.Routes;
using Game.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TransitStats.Models.Transfers;
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
    /// </summary>
    public partial class TransitTransferUISystem : UISystemBase
    {
        private NameSystem nameSystem;
        private ImageSystem imageSystem;

        private ValueBinding<TransitTransferSankeyData> transferDataBinding;
        private EntityQuery transportLineQuery;
        private EntityQuery transferPairQuery;        
        private Entity selectedRoute;  // Currently selected route for analysis

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

            // Create binding for UI
            AddBinding(transferDataBinding = new ValueBinding<TransitTransferSankeyData>(
                "TransitStats",
                "transferData",
                new TransitTransferSankeyData()
            ));

            selectedRoute = Entity.Null;
        }

        protected override void OnUpdate()
        {
            // Only update if a route is selected
            if (selectedRoute == Entity.Null)
                return;

            // Build Sankey data for selected route
            var sankeyData = BuildSankeyDataForRoute(selectedRoute);

            // Update binding
            transferDataBinding.Update(sankeyData);
        }

        /// <summary>
        /// Set which route to analyze. Call this when user selects a route.
        /// </summary>
        public void SetSelectedRoute(Entity route)
        {
            selectedRoute = route;
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
            if (EntityManager.TryGetComponent<Color>(route, out Color routeColor))
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
