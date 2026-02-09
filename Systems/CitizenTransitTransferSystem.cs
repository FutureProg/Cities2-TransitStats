using Colossal.Serialization.Entities;
using Game;
using Game.Citizens;
using Game.Common;
using Game.Creatures;
using Game.Routes;
using Game.Simulation;
using Game.Vehicles;
using System.Runtime.CompilerServices;
using TransitStats.Models;
using TransitStats.Models.Transfers;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using PublicTransport = Game.Vehicles.PublicTransport;

namespace TransitStats.Systems
{
    /// <summary>
    /// Tracks citizen transfers between public transit lines within the same trip.
    /// 
    /// ARCHITECTURE (Based on actual game structure):
    /// 1. Vehicle Entity → has Passenger buffer (DynamicBuffer&lt;Passenger&gt;)
    /// 2. Each Passenger entry → Entity with Game.Creatures.Resident component
    /// 3. Resident component → m_Citizen field links to actual Citizen entity
    /// 
    /// MULTI-PASS DETECTION APPROACH:
    /// Pass 1: Track passengers on vehicles - Update ActiveTransitTrip for all passengers
    /// Pass 2: Detect transfers - Query all ActiveTransitTrip entities to find route changes
    ///         (This catches citizens even when between vehicles/walking to next stop)
    /// Pass 3: Cleanup - Remove ActiveTransitTrip from citizens with Arrived component
    /// Pass 4: Process events - Update transfer statistics and graph
    /// 
    /// This ensures we capture transfers during the transition period between vehicles,
    /// not just when citizens are actively boarding.
    /// 
    /// Uses graph-based approach: each transfer pair entity stores which trip origins led to it.
    /// Enables accurate filtering for "show only trips that started from Route X".
    /// </summary>
    public partial class CitizenTransitTransferSystem : GameSystemBase, IDefaultSerializable, ISerializable
    {
        // Time constants (256 frames = 1 in-game hour, 6144 frames = 1 in-game day = 1 month)
        private const uint MAX_TRANSFER_WINDOW_FRAMES = 4608; // 15 in-game minutes
        private const uint UPDATE_INTERVAL_FRAMES = 192; // ~45 minutes (32 updates per day)
        private const uint HISTORY_MAX_SAMPLES = 192; // 6 in-game days = 6 months

        private EntityQuery publicTransportVehicleQuery;
        private EntityQuery activeTransitTripQuery;
        private EntityQuery arrivedCitizensQuery;
        private EntityQuery transferPairQuery;

        private SimulationSystem simulationSystem;
        private EntityCommandBufferSystem commandBufferSystem;
        private NativeQueue<TransitTransferEvent> transferEventQueue;
        private NativeParallelHashMap<int, Entity> transferPairLookup; // Hash(fromRoute, toRoute) -> Entity

        private uint lastUpdateFrame;

        protected override void OnCreate()
        {
            base.OnCreate();

            simulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            commandBufferSystem = World.GetOrCreateSystemManaged<EndFrameBarrier>();

            // Query: All public transport vehicles with passengers
            publicTransportVehicleQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<PublicTransport>(),
                    ComponentType.ReadOnly<CurrentRoute>(),
                    ComponentType.ReadOnly<Passenger>()  // Buffer
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>()
                }
            });

            // Query: All citizens with active transit trips
            activeTransitTripQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadWrite<ActiveTransitTrip>(),
                    ComponentType.ReadOnly<TravelPurpose>(),
                    ComponentType.ReadOnly<CurrentVehicle>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>()
                }
            });

            // Query: Citizens who have arrived at their destination
            arrivedCitizensQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadWrite<ActiveTransitTrip>(),
                    ComponentType.ReadOnly<Arrived>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>()
                }
            });

            // Query: Transfer tracking entities
            transferPairQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadWrite<TransferPairInfo>(),
                    ComponentType.ReadWrite<TransferOriginCount>()
                }
            });

            transferEventQueue = new NativeQueue<TransitTransferEvent>(Allocator.Persistent);
            transferPairLookup = new NativeParallelHashMap<int, Entity>(100, Allocator.Persistent);

            lastUpdateFrame = 0;
        }

        protected override void OnDestroy()
        {
            if (transferEventQueue.IsCreated)
                transferEventQueue.Dispose();
            if (transferPairLookup.IsCreated)
                transferPairLookup.Dispose();

            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            uint currentFrame = simulationSystem.frameIndex;

            // Job 1: Track passengers on vehicles (update ActiveTransitTrip component)
            var trackJob = new TrackPassengersOnVehiclesJob
            {
                vehicleEntityType = GetEntityTypeHandle(),
                currentRouteType = GetComponentTypeHandle<CurrentRoute>(true),
                passengerBufferType = GetBufferTypeHandle<Passenger>(true),

                residentLookup = GetComponentLookup<Resident>(true),
                travelPurposeLookup = GetComponentLookup<TravelPurpose>(true),
                activeTransitTripLookup = GetComponentLookup<ActiveTransitTrip>(false),

                commandBuffer = commandBufferSystem.CreateCommandBuffer().AsParallelWriter(),
                currentFrame = currentFrame
            };

            var trackHandle = trackJob.ScheduleParallel(publicTransportVehicleQuery, Dependency);

            // Job 2: Detect transfers from all citizens with ActiveTransitTrip
            var detectJob = new DetectTransfersJob
            {
                entityType = GetEntityTypeHandle(),
                activeTransitTripType = GetComponentTypeHandle<ActiveTransitTrip>(false),
                travelPurposeType = GetComponentTypeHandle<TravelPurpose>(true),
                currentVehicleType = GetComponentTypeHandle<CurrentVehicle>(true),

                publicTransportLookup = GetComponentLookup<PublicTransport>(true),
                currentRouteLookup = GetComponentLookup<CurrentRoute>(true),

                transferEventQueue = transferEventQueue.AsParallelWriter(),

                currentFrame = currentFrame,
                maxTransferWindowFrames = MAX_TRANSFER_WINDOW_FRAMES
            };

            var detectHandle = detectJob.ScheduleParallel(activeTransitTripQuery, trackHandle);

            // Job 3: Clean up completed trips (remove ActiveTransitTrip from arrived citizens)
            var cleanupJob = new CleanupCompletedTripsJob
            {
                entityType = GetEntityTypeHandle(),
                commandBuffer = commandBufferSystem.CreateCommandBuffer().AsParallelWriter()
            };

            var cleanupHandle = cleanupJob.ScheduleParallel(arrivedCitizensQuery, detectHandle);

            // Job 4: Process transfer events and update statistics
            var processJob = new ProcessTransferEventsJob
            {
                transferEventQueue = transferEventQueue,
                transferPairLookup = transferPairLookup,
                transferPairInfoLookup = GetComponentLookup<TransferPairInfo>(false),
                transferOriginCountLookup = GetBufferLookup<TransferOriginCount>(false),
                statisticSampleLookup = GetBufferLookup<TransferStatisticSample>(false),
                entityCommandBuffer = commandBufferSystem.CreateCommandBuffer(),
                currentFrame = currentFrame,
                maxHistorySamples = HISTORY_MAX_SAMPLES
            };

            var processHandle = processJob.Schedule(cleanupHandle);

            commandBufferSystem.AddJobHandleForProducer(processHandle);
            Dependency = processHandle;
        }

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(lastUpdateFrame);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out lastUpdateFrame);
            RebuildTransferPairLookup();
        }

        private void RebuildTransferPairLookup()
        {
            transferPairLookup.Clear();

            var entities = transferPairQuery.ToEntityArray(Allocator.Temp);
            var transferPairInfos = transferPairQuery.ToComponentDataArray<TransferPairInfo>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                int hash = HashRoutePair(transferPairInfos[i].fromRoute, transferPairInfos[i].toRoute);
                transferPairLookup[hash] = entities[i];
            }

            entities.Dispose();
            transferPairInfos.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int HashRoutePair(Entity from, Entity to)
        {
            return from.Index * 31 + to.Index;
        }

        public void SetDefaults(Context context)
        {
            lastUpdateFrame = 0;
        }

        // ========== JOBS ==========

        /// <summary>
        /// Job 1: Tracks passengers currently on public transport vehicles.
        /// Creates ActiveTransitTrip component for each passenger.
        /// Does NOT detect transfers - just maintains state.
        /// </summary>
        [BurstCompile]
        private struct TrackPassengersOnVehiclesJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle vehicleEntityType;
            [ReadOnly] public ComponentTypeHandle<CurrentRoute> currentRouteType;
            [ReadOnly] public BufferTypeHandle<Passenger> passengerBufferType;

            // Lookups to resolve passenger representation → actual citizen
            [ReadOnly] public ComponentLookup<Resident> residentLookup;
            [ReadOnly] public ComponentLookup<TravelPurpose> travelPurposeLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<ActiveTransitTrip> activeTransitTripLookup;

            public EntityCommandBuffer.ParallelWriter commandBuffer;

            public uint currentFrame;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var vehicleEntities = chunk.GetNativeArray(vehicleEntityType);
                var currentRoutes = chunk.GetNativeArray(ref currentRouteType);
                var passengerBufferAccessor = chunk.GetBufferAccessor(ref passengerBufferType);

                for (int vehicleIndex = 0; vehicleIndex < chunk.Count; vehicleIndex++)
                {
                    var vehicle = vehicleEntities[vehicleIndex];
                    var currentRoute = currentRoutes[vehicleIndex];
                    var passengers = passengerBufferAccessor[vehicleIndex];

                    Entity routeEntity = currentRoute.m_Route;

                    // Process each passenger on this vehicle
                    for (int passengerIndex = 0; passengerIndex < passengers.Length; passengerIndex++)
                    {
                        Entity passengerRepresentation = passengers[passengerIndex].m_Passenger;

                        // Get the Resident component to find the actual citizen entity
                        if (!residentLookup.TryGetComponent(passengerRepresentation, out Resident resident))
                            continue;

                        Entity citizenEntity = resident.m_Citizen;

                        // Skip if not a valid citizen
                        if (citizenEntity == Entity.Null)
                            continue;

                        // Get citizen's travel purpose
                        if (!travelPurposeLookup.TryGetComponent(citizenEntity, out TravelPurpose purpose))
                            continue;

                        // Create ActiveTransitTrip (don't detect transfers here)
                        if (!activeTransitTripLookup.HasComponent(citizenEntity))                        
                        {
                            // First time boarding - start tracking
                            commandBuffer.AddComponent(unfilteredChunkIndex, citizenEntity, new ActiveTransitTrip
                            {
                                startingRoute = routeEntity,
                                currentRoute = routeEntity,
                                lastBoardingFrame = currentFrame,
                                tripPurpose = purpose.m_Purpose
                            });
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Job 2: Detects transfers by checking all citizens with ActiveTransitTrip.
        /// This catches transfers even when citizens are between vehicles (walking to next stop).
        /// </summary>
        [BurstCompile]
        private struct DetectTransfersJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle entityType;
            public ComponentTypeHandle<ActiveTransitTrip> activeTransitTripType;
            [ReadOnly] public ComponentTypeHandle<TravelPurpose> travelPurposeType;
            [ReadOnly] public ComponentTypeHandle<CurrentVehicle> currentVehicleType;

            [ReadOnly] public ComponentLookup<PublicTransport> publicTransportLookup;
            [ReadOnly] public ComponentLookup<CurrentRoute> currentRouteLookup;

            public NativeQueue<TransitTransferEvent>.ParallelWriter transferEventQueue;

            public uint currentFrame;
            public uint maxTransferWindowFrames;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(entityType);
                var activeTrips = chunk.GetNativeArray(ref activeTransitTripType);
                var travelPurposes = chunk.GetNativeArray(ref travelPurposeType);
                var currentVehicles = chunk.GetNativeArray(ref currentVehicleType);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var citizenEntity = entities[i];
                    var activeTrip = activeTrips[i];
                    var purpose = travelPurposes[i];
                    var currentVehicle = currentVehicles[i];

                    // Check if citizen is on a vehicle
                    if (currentVehicle.m_Vehicle == Entity.Null)
                        continue;

                    // Check if vehicle is public transport
                    if (!publicTransportLookup.HasComponent(currentVehicle.m_Vehicle))
                        continue;

                    // Get the route of current vehicle
                    if (!currentRouteLookup.TryGetComponent(currentVehicle.m_Vehicle, out CurrentRoute routeComponent))
                        continue;

                    Entity currentRouteEntity = routeComponent.m_Route;

                    // Check if this is a transfer (different route, same purpose, within time window)
                    bool isDifferentRoute = activeTrip.currentRoute != currentRouteEntity;
                    bool isSamePurpose = activeTrip.tripPurpose == purpose.m_Purpose;
                    uint framesSinceLastBoarding = currentFrame - activeTrip.lastBoardingFrame;
                    bool withinTimeWindow = framesSinceLastBoarding <= maxTransferWindowFrames;

                    if (isDifferentRoute && isSamePurpose && withinTimeWindow)
                    {
                        // This is a transfer! Enqueue event
                        transferEventQueue.Enqueue(new TransitTransferEvent
                        {
                            fromRoute = activeTrip.currentRoute,
                            toRoute = currentRouteEntity,
                            startingRoute = activeTrip.startingRoute,
                            transferTimeFrames = framesSinceLastBoarding
                        });

                        // Update the ActiveTransitTrip (will be written back by chunk)
                        activeTrips[i] = new ActiveTransitTrip
                        {
                            startingRoute = activeTrip.startingRoute,
                            currentRoute = currentRouteEntity,
                            lastBoardingFrame = currentFrame,
                            tripPurpose = purpose.m_Purpose
                        };
                    }
                }
            }
        }

        /// <summary>
        /// Job 3: Cleans up ActiveTransitTrip component from citizens who have arrived at their destination.
        /// Uses Game.Citizens.Arrived component as the signal to remove tracking.
        /// </summary>
        [BurstCompile]
        private struct CleanupCompletedTripsJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle entityType;
            public EntityCommandBuffer.ParallelWriter commandBuffer;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(entityType);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var citizenEntity = entities[i];

                    // Remove ActiveTransitTrip from arrived citizens
                    commandBuffer.RemoveComponent<ActiveTransitTrip>(unfilteredChunkIndex, citizenEntity);
                }
            }
        }

        /// <summary>
        /// Job 4: Processes queued transfer events and updates the transfer graph.
        /// Creates/updates transfer pair entities and maintains origin tracking.
        /// </summary>
        [BurstCompile]
        private struct ProcessTransferEventsJob : IJob
        {
            public NativeQueue<TransitTransferEvent> transferEventQueue;
            public NativeParallelHashMap<int, Entity> transferPairLookup;

            [NativeDisableParallelForRestriction] public ComponentLookup<TransferPairInfo> transferPairInfoLookup;
            [NativeDisableParallelForRestriction] public BufferLookup<TransferOriginCount> transferOriginCountLookup;
            [NativeDisableParallelForRestriction] public BufferLookup<TransferStatisticSample> statisticSampleLookup;

            public EntityCommandBuffer entityCommandBuffer;

            public uint currentFrame;
            public uint maxHistorySamples;

            public void Execute()
            {
                while (transferEventQueue.TryDequeue(out TransitTransferEvent transferEvent))
                {
                    int hash = HashRoutePair(transferEvent.fromRoute, transferEvent.toRoute);

                    Entity transferPairEntity;

                    // Get or create transfer pair entity
                    if (transferPairLookup.TryGetValue(hash, out transferPairEntity))
                    {
                        // Update existing pair
                        var pairInfo = transferPairInfoLookup[transferPairEntity];
                        pairInfo.totalTransfers++;
                        pairInfo.lastTransferFrame = currentFrame;
                        transferPairInfoLookup[transferPairEntity] = pairInfo;
                    }
                    else
                    {
                        // Create new pair
                        transferPairEntity = entityCommandBuffer.CreateEntity();

                        entityCommandBuffer.AddComponent(transferPairEntity, new TransferPairInfo
                        {
                            fromRoute = transferEvent.fromRoute,
                            toRoute = transferEvent.toRoute,
                            totalTransfers = 1,
                            lastTransferFrame = currentFrame
                        });

                        entityCommandBuffer.AddBuffer<TransferOriginCount>(transferPairEntity);
                        entityCommandBuffer.AddBuffer<TransferStatisticSample>(transferPairEntity);

                        transferPairLookup[hash] = transferPairEntity;
                    }

                    // Update origin count
                    var originBuffer = transferOriginCountLookup[transferPairEntity];
                    bool foundOrigin = false;

                    for (int i = 0; i < originBuffer.Length; i++)
                    {
                        if (originBuffer[i].tripStartRoute == transferEvent.startingRoute)
                        {
                            var origin = originBuffer[i];
                            origin.count++;
                            originBuffer[i] = origin;
                            foundOrigin = true;
                            break;
                        }
                    }

                    if (!foundOrigin)
                    {
                        originBuffer.Add(new TransferOriginCount
                        {
                            tripStartRoute = transferEvent.startingRoute,
                            count = 1
                        });
                    }

                    // Update statistics history
                    var statsBuffer = statisticSampleLookup[transferPairEntity];
                    if (statsBuffer.Length == 0 || currentFrame - statsBuffer[statsBuffer.Length - 1].sampleFrame > 192)
                    {
                        // New sample period
                        statsBuffer.Add(new TransferStatisticSample
                        {
                            sampleFrame = currentFrame,
                            transferCount = 1,
                            averageTransferTime = transferEvent.transferTimeFrames
                        });

                        // Limit history
                        while (statsBuffer.Length > maxHistorySamples)
                        {
                            statsBuffer.RemoveAt(0);
                        }
                    }
                    else
                    {
                        // Update current sample
                        var sample = statsBuffer[statsBuffer.Length - 1];
                        uint oldTotal = sample.transferCount * sample.averageTransferTime;
                        sample.transferCount++;
                        sample.averageTransferTime = (oldTotal + transferEvent.transferTimeFrames) / sample.transferCount;
                        statsBuffer[statsBuffer.Length - 1] = sample;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int HashRoutePair(Entity from, Entity to)
            {
                return from.Index * 31 + to.Index;
            }
        }
    }
}
