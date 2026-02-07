using Colossal.IO.AssetDatabase;
using Colossal.Serialization.Entities;
using Game;
using Game.Citizens;
using Game.Common;
using Game.Creatures;
using Game.Prefabs;
using Game.Routes;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TransitStats.Models;
using TransitStats.Models.Transfers;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Scripting;
using PublicTransport = Game.Vehicles.PublicTransport;

namespace TransitStats.Systems
{
    /// <summary>
    /// Tracks citizen transfers between public transit lines within the same trip.
    /// Uses graph-based approach: each transfer pair entity stores which trip origins led to it.
    /// Enables accurate filtering for "show only trips that started from Route X".
    /// </summary>
    public partial class CitizenTransitTransferSystem : GameSystemBase, IDefaultSerializable, ISerializable
    {
        // Time constants (256 frames = 1 in-game hour, 6144 frames = 1 in-game day = 1 month)
        private const uint MAX_TRANSFER_WINDOW_FRAMES = 4608; // 15 in-game minutes
        private const uint UPDATE_INTERVAL_FRAMES = 192; // ~45 minutes (32 updates per day)
        private const uint HISTORY_MAX_SAMPLES = 192; // 6 in-game days = 6 months

        private EntityQuery citizensOnVehiclesQuery;
        private EntityQuery citizensOffVehiclesQuery;
        private EntityQuery transferPairQuery;

        private SimulationSystem simulationSystem;
        private EntityCommandBufferSystem commandBufferSystem;
        private NativeQueue<TransitTransferEvent> transferEventQueue;
        private NativeParallelHashMap<int, Entity> transferPairLookup; // Hash(fromRoute, toRoute) -> Entity

        private uint lastUpdateFrame;

        protected override void OnCreate()
        {
            base.OnCreate();

            commandBufferSystem = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            simulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();

            // Query: Citizens currently on transit vehicles with active trip tracking
            citizensOnVehiclesQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Citizen>(),
                    ComponentType.ReadOnly<CurrentVehicle>(),
                    ComponentType.ReadOnly<HumanCurrentLane>(),
                    ComponentType.ReadOnly<TravelPurpose>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>()
                }
            });

            // Query: Citizens not on vehicles (for trip completion detection)
            citizensOffVehiclesQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Citizen>(),
                    ComponentType.ReadOnly<ActiveTransitTrip>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<CurrentVehicle>(),
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

            // Job 1: Clear completed trips (citizens no longer on vehicles)
            var clearJob = new ClearCompletedTripsJob
            {
                citizenType = GetComponentTypeHandle<Citizen>(true),
                activeTransitTripType = GetComponentTypeHandle<ActiveTransitTrip>(true),
                travelPurposeType = GetComponentTypeHandle<TravelPurpose>(true),
                currentVehicleType = GetComponentTypeHandle<CurrentVehicle>(true),
                entityType = GetEntityTypeHandle(),
                commandBuffer = commandBufferSystem.CreateCommandBuffer().AsParallelWriter()
            };

            var clearHandle = clearJob.ScheduleParallel(citizensOffVehiclesQuery, Dependency);

            // Job 2: Detect transfers (citizens boarding vehicles)
            var detectJob = new DetectTransferJob
            {
                citizenType = GetComponentTypeHandle<Citizen>(true),
                currentVehicleType = GetComponentTypeHandle<CurrentVehicle>(true),
                travelPurposeType = GetComponentTypeHandle<TravelPurpose>(true),
                activeTransitTripType = GetComponentTypeHandle<ActiveTransitTrip>(false),
                entityType = GetEntityTypeHandle(),
                publicTransportVehicleLookup = GetComponentLookup<PublicTransport>(true),
                currentRouteLookup = GetComponentLookup<CurrentRoute>(true),
                commandBuffer = commandBufferSystem.CreateCommandBuffer().AsParallelWriter(),
                transferEventQueue = transferEventQueue.AsParallelWriter(),
                currentFrame = currentFrame,
                maxTransferWindowFrames = MAX_TRANSFER_WINDOW_FRAMES
            };

            var detectHandle = detectJob.ScheduleParallel(citizensOnVehiclesQuery, clearHandle);

            // Job 3: Process transfer events and update statistics
            var processJob = new ProcessTransferEventsJob
            {
                transferEventQueue = transferEventQueue,
                transferPairLookup = transferPairLookup,
                transferPairInfoLookup = GetComponentLookup<TransferPairInfo>(false),
                transferOriginCountLookup = GetBufferLookup<TransferOriginCount>(false),
                entityCommandBuffer = commandBufferSystem.CreateCommandBuffer(),
                currentFrame = currentFrame
            };

            var processHandle = processJob.Schedule(detectHandle);

            commandBufferSystem.AddJobHandleForProducer(processHandle);
            Dependency = processHandle;
        }

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(lastUpdateFrame);

            // Transfer pair lookup will be rebuilt from entities on load
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out lastUpdateFrame);

            // Rebuild transfer pair lookup from existing entities
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

        [BurstCompile]
        private partial struct ClearCompletedTripsJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<Citizen> citizenType;
            [ReadOnly] public ComponentTypeHandle<ActiveTransitTrip> activeTransitTripType;
            [ReadOnly] public ComponentTypeHandle<TravelPurpose> travelPurposeType;
            [ReadOnly] public ComponentTypeHandle<CurrentVehicle> currentVehicleType;
            [ReadOnly] public EntityTypeHandle entityType;

            public EntityCommandBuffer.ParallelWriter commandBuffer;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(entityType);
                var activeTrips = chunk.GetNativeArray(ref activeTransitTripType);
                var purposes = chunk.GetNativeArray(ref travelPurposeType);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var entity = entities[i];
                    var activeTrip = activeTrips[i];
                    var currentPurpose = purposes[i].m_Purpose;

                    // Remove ActiveTransitTrip if:
                    // 1. Citizen is no longer on a vehicle (already filtered by query)
                    // 2. Trip purpose has changed (new trip started)
                    if (currentPurpose != activeTrip.tripPurpose)
                    {
                        commandBuffer.RemoveComponent<ActiveTransitTrip>(unfilteredChunkIndex, entity);
                    }
                }
            }
        }

        [BurstCompile]
        private partial struct DetectTransferJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<Citizen> citizenType;
            [ReadOnly] public ComponentTypeHandle<CurrentVehicle> currentVehicleType;
            [ReadOnly] public ComponentTypeHandle<TravelPurpose> travelPurposeType;
            public ComponentTypeHandle<ActiveTransitTrip> activeTransitTripType;
            [ReadOnly] public EntityTypeHandle entityType;

            [ReadOnly] public ComponentLookup<PublicTransport> publicTransportVehicleLookup;
            [ReadOnly] public ComponentLookup<CurrentRoute> currentRouteLookup;

            public EntityCommandBuffer.ParallelWriter commandBuffer;
            public NativeQueue<TransitTransferEvent>.ParallelWriter transferEventQueue;

            public uint currentFrame;
            public uint maxTransferWindowFrames;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(entityType);
                var currentVehicles = chunk.GetNativeArray(ref currentVehicleType);
                var purposes = chunk.GetNativeArray(ref travelPurposeType);
                var activeTrips = chunk.GetNativeArray(ref activeTransitTripType);

                bool hasActiveTrip = chunk.Has(ref activeTransitTripType);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var entity = entities[i];
                    var vehicle = currentVehicles[i].m_Vehicle;
                    var purpose = purposes[i].m_Purpose;

                    // Check if this is a public transport vehicle
                    if (!publicTransportVehicleLookup.HasComponent(vehicle))
                        continue;

                    // Get the route this vehicle is on
                    if (!currentRouteLookup.TryGetComponent(vehicle, out CurrentRoute currentRoute))
                        continue;

                    Entity routeEntity = currentRoute.m_Route;

                    if (hasActiveTrip)
                    {
                        var activeTrip = activeTrips[i];

                        // Check if this is a transfer (different route, same purpose, within time window)
                        bool isDifferentRoute = activeTrip.currentRoute != routeEntity;
                        bool isSamePurpose = activeTrip.tripPurpose == purpose;
                        uint framesSinceLastBoarding = currentFrame - activeTrip.lastBoardingFrame;
                        bool withinTimeWindow = framesSinceLastBoarding <= maxTransferWindowFrames;

                        if (isDifferentRoute && isSamePurpose && withinTimeWindow)
                        {
                            // This is a transfer! Enqueue event
                            transferEventQueue.Enqueue(new TransitTransferEvent
                            {
                                fromRoute = activeTrip.currentRoute,
                                toRoute = routeEntity,
                                startingRoute = activeTrip.startingRoute,
                                transferTimeFrames = framesSinceLastBoarding
                            });

                            // Update current route and boarding time
                            activeTrips[i] = new ActiveTransitTrip
                            {
                                startingRoute = activeTrip.startingRoute, // Keep original starting route
                                currentRoute = routeEntity,
                                tripPurpose = purpose,
                                lastBoardingFrame = currentFrame
                            };
                        }
                        else if (isDifferentRoute && !isSamePurpose)
                        {
                            // New trip started with different purpose
                            commandBuffer.RemoveComponent<ActiveTransitTrip>(unfilteredChunkIndex, entity);
                            commandBuffer.AddComponent(unfilteredChunkIndex, entity, new ActiveTransitTrip
                            {
                                startingRoute = routeEntity,
                                currentRoute = routeEntity,
                                tripPurpose = purpose,
                                lastBoardingFrame = currentFrame
                            });
                        }
                        else if (!isDifferentRoute)
                        {
                            // Still on same route, update boarding time
                            activeTrips[i] = new ActiveTransitTrip
                            {
                                startingRoute = activeTrip.startingRoute,
                                currentRoute = routeEntity,
                                tripPurpose = purpose,
                                lastBoardingFrame = currentFrame
                            };
                        }
                    }
                    else
                    {
                        // First time on transit in this trip - create ActiveTransitTrip
                        commandBuffer.AddComponent(unfilteredChunkIndex, entity, new ActiveTransitTrip
                        {
                            startingRoute = routeEntity,
                            currentRoute = routeEntity,
                            tripPurpose = purpose,
                            lastBoardingFrame = currentFrame
                        });
                    }
                }
            }
        }

        [BurstCompile]
        private partial struct ProcessTransferEventsJob : IJob
        {
            public NativeQueue<TransitTransferEvent> transferEventQueue;
            public NativeParallelHashMap<int, Entity> transferPairLookup;

            public ComponentLookup<TransferPairInfo> transferPairInfoLookup;
            public BufferLookup<TransferOriginCount> transferOriginCountLookup;

            public EntityCommandBuffer entityCommandBuffer;
            public uint currentFrame;

            public void Execute()
            {
                while (transferEventQueue.TryDequeue(out TransitTransferEvent transferEvent))
                {
                    int hash = HashRoutePair(transferEvent.fromRoute, transferEvent.toRoute);

                    Entity transferEntity;

                    // Find or create transfer pair entity
                    if (!transferPairLookup.TryGetValue(hash, out transferEntity))
                    {
                        // Create new transfer pair entity
                        transferEntity = entityCommandBuffer.CreateEntity();

                        entityCommandBuffer.AddComponent(transferEntity, new TransferPairInfo
                        {
                            fromRoute = transferEvent.fromRoute,
                            toRoute = transferEvent.toRoute,
                            lastUpdatedFrame = currentFrame
                        });

                        entityCommandBuffer.AddBuffer<TransferOriginCount>(transferEntity);
                        entityCommandBuffer.AddBuffer<TransferStatisticSample>(transferEntity);

                        transferPairLookup[hash] = transferEntity;
                    }
                    else
                    {
                        // Update existing entity's last updated frame
                        if (transferPairInfoLookup.HasComponent(transferEntity))
                        {
                            var info = transferPairInfoLookup[transferEntity];
                            info.lastUpdatedFrame = currentFrame;
                            transferPairInfoLookup[transferEntity] = info;
                        }
                    }

                    // Update origin count
                    if (transferOriginCountLookup.HasBuffer(transferEntity))
                    {
                        var originCounts = transferOriginCountLookup[transferEntity];

                        // Find or add origin count entry
                        bool found = false;
                        for (int i = 0; i < originCounts.Length; i++)
                        {
                            if (originCounts[i].tripStartRoute == transferEvent.startingRoute)
                            {
                                var originCount = originCounts[i];
                                originCount.count++;
                                originCounts[i] = originCount;
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {
                            originCounts.Add(new TransferOriginCount
                            {
                                tripStartRoute = transferEvent.startingRoute,
                                count = 1
                            });
                        }
                    }
                }
            }
            private static int HashRoutePair(Entity from, Entity to)
            {
                return from.Index * 31 + to.Index;
            }
        }
    }
}
