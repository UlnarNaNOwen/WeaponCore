﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CoreSystems.Platform;
using CoreSystems.Support;
using Jakaria.API;
using Sandbox.Game.Entities;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using static CoreSystems.ProtoWeaponCompTasks;
using static CoreSystems.Session;
using static CoreSystems.Support.WeaponDefinition.AmmoDef;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.EwarDef.EwarType;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.FragmentDef.TimedSpawnDef;
using static CoreSystems.Support.WeaponDefinition.AmmoDef.TrajectoryDef.ApproachDef;

namespace CoreSystems.Projectiles
{
    internal class Projectile
    {
        internal readonly ProInfo Info = new ProInfo();
        internal readonly List<MyLineSegmentOverlapResult<MyEntity>> MySegmentList = new List<MyLineSegmentOverlapResult<MyEntity>>();
        internal readonly List<MyEntity> MyEntityList = new List<MyEntity>();
        internal readonly List<ProInfo> VrPros = new List<ProInfo>();
        internal readonly List<Ai> Watchers = new List<Ai>();
        internal readonly HashSet<Projectile> Seekers = new HashSet<Projectile>();
        internal ProjectileState State;
        internal MyEntityQueryType PruneQuery;
        internal HadTargetState HadTarget;
        internal EndStates EndState;
        internal Vector3D Position;
        internal Vector3D Direction;

        internal Vector3D LastPosition;
        internal Vector3D Velocity;
        internal Vector3D PrevVelocity;
        internal Vector3D TravelMagnitude;
        internal Vector3D TargetPosition;
        internal Vector3D OffsetTarget;
        internal Vector3 PrevTargetVel;
        internal Vector3 Gravity;
        internal LineD Beam;
        internal BoundingSphereD PruneSphere;
        internal double DistanceToTravelSqr;
        internal double VelocityLengthSqr;
        internal double DistanceToSurfaceSqr;
        internal double DesiredSpeed;
        internal double MaxSpeed;
        internal bool EnableAv;
        internal bool Intersecting;
        internal bool FinalizeIntersection;
        internal int DeaccelRate;
        internal int TargetsSeen;
        internal int PruningProxyId = -1;
        internal enum EndStates
        {
            None,
            AtMaxRange,
            EarlyEnd,
            AtMaxEarly,
        }

        internal enum ProjectileState
        {
            Alive,
            ClientPhantom,
            OneAndDone,
            Detonate,
            Depleted,
            Destroy,
            Dead,
        }

        public enum HadTargetState
        {
            None,
            Projectile,
            Entity,
            Fake,
            Other,
        }

        #region Start
        internal void Start()
        {
            var ai = Info.Ai;
            var session = ai.Session;
            var s = Info.Storage;
            var ammoDef = Info.AmmoDef;
            var aConst = ammoDef.Const;
            var w = Info.Weapon;
            var comp = w.Comp;

            if (aConst.ApproachesCount > 0)
            {
                s.ApproachInfo = aConst.ApproachInfoPool.Count > 0 ? aConst.ApproachInfoPool.Pop() : new ApproachInfo(aConst);
                s.ApproachInfo.TargetPos = TargetPosition;
            }
            else if (aConst.IsDrone)
            {
                s.DroneInfo = session.DroneInfoPool.Count > 0 ? session.DroneInfoPool.Pop() : new DroneInfo();
            }

            if (aConst.FragmentPattern) {
                if (aConst.PatternShuffleArray.Count > 0)
                    Info.PatternShuffle = aConst.PatternShuffleArray.Pop();
                else
                {
                    Info.PatternShuffle = new int[aConst.FragPatternCount];
                    for (int i = 0; i < Info.PatternShuffle.Length; i++)
                        Info.PatternShuffle[i] = i;
                }
            }

            if (aConst.CheckFutureIntersection)
                s.Obstacle = session.ClosestObstaclesPool.Count > 0 ? session.ClosestObstaclesPool.Pop() : new ClosestObstacles();

            if (aConst.FullSync)
                s.FullSyncInfo = session.FullSyncInfoPool.Count > 0 ? session.FullSyncInfoPool.Pop() : new FullSyncInfo();

            EndState = EndStates.None;
            Position = Info.Origin;
            var cameraStart = session.CameraPos;
            double distanceFromCameraSqr;
            Vector3D.DistanceSquared(ref cameraStart, ref Info.Origin, out distanceFromCameraSqr);
            var probability = ammoDef.AmmoGraphics.VisualProbability;
            EnableAv = !aConst.VirtualBeams && !session.DedicatedServer && (distanceFromCameraSqr <= session.SyncDistSqr || ai.AiType == Ai.AiTypes.Phantom) && (probability >= 1 || probability >= MyUtils.GetRandomDouble(0.0f, 1f));
            Info.AvShot = null;
            TargetsSeen = 0;
            PruningProxyId = -1;

            Intersecting = false;
            Info.PrevDistanceTraveled = 0;
            Info.DistanceTraveled = 0;
            DistanceToSurfaceSqr = double.MaxValue;
            var trajectory = ammoDef.Trajectory;
            var guidance = trajectory.Guidance;

            if (aConst.DynamicGuidance && session.AntiSmartActive) 
                DynTrees.RegisterProjectile(this);

            Info.MyPlanet = ai.MyPlanet;
            
            if (!session.VoxelCaches.TryGetValue(Info.UniqueMuzzleId, out Info.VoxelCache))
                Info.VoxelCache = session.VoxelCaches[ulong.MaxValue];

            if (Info.MyPlanet != null)
                Info.VoxelCache.PlanetSphere.Center = ai.ClosestPlanetCenter;

            ai.ProjectileTicker = ai.Session.Tick;
            Info.ObjectsHit = 0;
            Info.BaseHealthPool = aConst.Health;
            Info.BaseEwarPool = aConst.Health;

            if (aConst.IsSmart || aConst.IsDrone)
            {
                s.SmartSlot = Info.Random.Range(0, 10);
            }
            var tTarget = Info.Target.TargetObject as Projectile;
            var eTarget = Info.Target.TargetObject as MyEntity;

            switch (Info.Target.TargetState)
            {
                case Target.TargetStates.WasProjectile:
                    HadTarget = HadTargetState.Projectile;
                    break;
                case Target.TargetStates.IsProjectile:
                    if (tTarget == null)
                    {
                        HadTarget = HadTargetState.None;
                        Info.Target.TargetState = Target.TargetStates.None;
                        TargetPosition = Vector3D.Zero;
                        Log.Line($"ProjectileStart had invalid Projectile target state");
                        break;
                    }
                    HadTarget = HadTargetState.Projectile;
                    TargetPosition = tTarget.Position;
                    tTarget.Seekers.Add(this);
                    break;
                case Target.TargetStates.IsFake:
                    TargetPosition = Info.IsFragment ? TargetPosition : Vector3D.Zero;
                    HadTarget = HadTargetState.Fake;
                    break;
                case Target.TargetStates.IsEntity:
                    if (eTarget == null)
                    {
                        HadTarget = HadTargetState.None;
                        Info.Target.TargetState = Target.TargetStates.None;
                        TargetPosition = Vector3D.Zero;
                        Log.Line($"ProjectileStart had invalid entity target state, isFragment: {Info.IsFragment} - ammo:{ammoDef.AmmoRound} - weapon:{Info.Weapon.System.ShortName}");
                        break;
                    }

                    if (aConst.IsDrone)
                    {
                        s.DroneInfo.DroneMsn = DroneInfo.DroneMission.Attack;//TODO handle initial defensive assignment?
                        s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Launch;
                        s.DroneInfo.NavTargetEnt = eTarget.GetTopMostParent();
                        s.DroneInfo.NavTargetBound = eTarget.PositionComp.WorldVolume;
                    }

                    TargetPosition = eTarget.PositionComp.WorldAABB.Center;
                    HadTarget = HadTargetState.Entity;
                    break;
                default:
                    TargetPosition = Info.IsFragment ? TargetPosition : Vector3D.Zero;
                    break;
            }

            float variance = 0;
            if (aConst.RangeVariance)
            {
                var min = trajectory.RangeVariance.Start;
                var max = trajectory.RangeVariance.End;
                variance = (float)Info.Random.NextDouble() * (max - min) + min;
                Info.MaxTrajectory -= variance;
            }

            var lockedTarget = !Vector3D.IsZero(TargetPosition);
            if (!lockedTarget)
                TargetPosition = Position + (Direction * Info.MaxTrajectory);

            if (lockedTarget && !aConst.IsBeamWeapon && guidance == TrajectoryDef.GuidanceType.TravelTo)
            {
                s.RequestedStage = -2;
                if (!MyUtils.IsZero(TargetPosition))
                {
                    Vector3D targetDir;
                    Vector3D targetPos;
                    if (TrajectoryEstimation(Info.AmmoDef, ref Position, out targetDir, out targetPos))
                        TargetPosition = targetPos;

                    TargetPosition -= (Direction * variance);
                }
                Vector3D.DistanceSquared(ref Info.Origin, ref TargetPosition, out DistanceToTravelSqr);
            }
            else DistanceToTravelSqr = Info.MaxTrajectory * Info.MaxTrajectory;

            PrevTargetVel = Vector3D.Zero;

            var targetSpeed = (float)(!aConst.IsBeamWeapon ? aConst.DesiredProjectileSpeed : Info.MaxTrajectory * MyEngineConstants.UPDATE_STEPS_PER_SECOND);

            if (aConst.SpeedVariance && !aConst.IsBeamWeapon)
            {
                var min = trajectory.SpeedVariance.Start;
                var max = trajectory.SpeedVariance.End;
                var speedVariance = (float)Info.Random.NextDouble() * (max - min) + min;
                DesiredSpeed = targetSpeed + speedVariance;
            }
            else DesiredSpeed = targetSpeed;

            if (aConst.IsSmart && aConst.TargetOffSet && (lockedTarget || Info.Target.TargetState == Target.TargetStates.IsFake))
            {
                OffSetTarget();
            }
            else
            {
                OffsetTarget = Vector3D.Zero;
            }

            s.PickTarget = (aConst.OverrideTarget || comp.ModOverride && !lockedTarget) && Info.Target.TargetState != Target.TargetStates.IsFake;
            if (s.PickTarget || lockedTarget && !Info.IsFragment) TargetsSeen++;
            Info.TracerLength = aConst.TracerLength <= Info.MaxTrajectory ? aConst.TracerLength : Info.MaxTrajectory;

            var staticIsInRange = ai.ClosestStaticSqr * 0.5 < Info.MaxTrajectory * Info.MaxTrajectory;
            var pruneStaticCheck = ai.ClosestPlanetSqr * 0.5 < Info.MaxTrajectory * Info.MaxTrajectory || ai.StaticGridInRange;
            PruneQuery = (aConst.DynamicGuidance && pruneStaticCheck) || aConst.FeelsGravity && staticIsInRange || !aConst.DynamicGuidance && !aConst.FeelsGravity && staticIsInRange ? MyEntityQueryType.Both : MyEntityQueryType.Dynamic;

            if (ai.PlanetSurfaceInRange && ai.ClosestPlanetSqr <= Info.MaxTrajectory * Info.MaxTrajectory)
            {
                PruneQuery = MyEntityQueryType.Both;
            }

            if (aConst.DynamicGuidance && PruneQuery == MyEntityQueryType.Dynamic && staticIsInRange) 
                CheckForNearVoxel(60);

            var desiredSpeed = (Direction * DesiredSpeed);
            var relativeSpeedCap = Info.ShooterVel + desiredSpeed;
            MaxSpeed = relativeSpeedCap.Length();
            if (aConst.AmmoSkipAccel)
            {
                Velocity = relativeSpeedCap;
                VelocityLengthSqr = MaxSpeed * MaxSpeed;
            }
            else Velocity = Info.ShooterVel + (Direction * (aConst.DeltaVelocityPerTick * DeltaTimeRatio));

            if (Info.IsFragment)
                Vector3D.Normalize(ref Velocity, out Direction);


            TravelMagnitude = !Info.IsFragment && aConst.AmmoSkipAccel ? desiredSpeed * DeltaStepConst : Velocity * DeltaStepConst;
            DeaccelRate = aConst.Ewar || aConst.IsMine ? trajectory.DeaccelTime : aConst.IsDrone ? 100: 0;
            State = !aConst.IsBeamWeapon ? ProjectileState.Alive : ProjectileState.OneAndDone;

            if (EnableAv)
            {
                Info.AvShot = session.Av.AvShotPool.Count > 0 ? session.Av.AvShotPool.Pop() : new AvShot(session);
                Info.AvShot.Init(Info, (aConst.DeltaVelocityPerTick * DeltaTimeRatio), MaxSpeed, ref Direction);
                Info.AvShot.SetupSounds(distanceFromCameraSqr); //Pool initted sounds per Projectile type... this is expensive
                if (aConst.HitParticle && !aConst.IsBeamWeapon || aConst.EndOfLifeAoe && !ammoDef.AreaOfDamage.EndOfLife.NoVisuals)
                {
                    var hitPlayChance = Info.AmmoDef.AmmoGraphics.Particles.Hit.Extras.HitPlayChance;
                    Info.AvShot.HitParticleActive = hitPlayChance >= 1 || hitPlayChance >= MyUtils.GetRandomDouble(0.0f, 1f);
                }

                if (aConst.PrimeModel || aConst.TriggerModel)
                {
                    Info.AvShot.HasModel = true;
                    Info.AvShot.ModelOnly = !aConst.DrawLine;
                }
            }
            var monitor = comp.ProjectileMonitors[w.PartId];
            if (monitor.Count > 0)
            {
                comp.Session.MonitoredProjectiles[Info.Id] = this;
                for (int j = 0; j < monitor.Count; j++)
                    monitor[j].Invoke(comp.CoreEntity.EntityId, w.PartId, Info.Id, Info.Target.TargetId, Position, true);
            }
        }

        #endregion

        #region End

        internal void DestroyProjectile()
        {
            Info.Hit = new Hit { Entity = null, SurfaceHit = Position, LastHit = Position, HitVelocity = !Vector3D.IsZero(Gravity) ? Velocity * 0.33f : Velocity, HitTick = Info.Ai.Session.Tick };
            if (EnableAv || Info.AmmoDef.Const.VirtualBeams)
            {
                Info.AvShot.ForceHitParticle = true;
                Info.AvShot.Hit = Info.Hit;
            }

            Intersecting = true;

            if (Info.SyncId != ulong.MaxValue && (Info.AmmoDef.Const.PdDeathSync || Info.AmmoDef.Const.OnHitDeathSync))
                AddToDeathSyncMonitor();

            State = ProjectileState.Depleted;
        }

        internal void AddToDeathSyncMonitor()
        {
            var s = Info.Ai.Session;
            if (Info.Weapon.ProjectileSyncMonitor.Remove(Info.SyncId))
            {
                if (s.AdvSyncServer)
                {
                    s.ProtoDeathSyncMonitor.Collection.Add(new ProjectileSync {WeaponId = Info.Weapon.PartState.Id, SyncId = Info.SyncId});
                }
            }
        }

        internal void ProjectileClose()
        {
            var aConst = Info.AmmoDef.Const;
            var session = Info.Ai.Session;
            var normalfragSpawn = aConst.FragOnEnd && (aConst.FragIgnoreArming || Info.RelativeAge >= aConst.MinArmingTime);
            var eolFragSpawn = aConst.FragOnEolArmed && Info.ObjectsHit > 0 && Info.RelativeAge >= aConst.MinArmingTime;
            
            if ((normalfragSpawn || eolFragSpawn) && Info.SpawnDepth < aConst.FragMaxChildren)
                SpawnShrapnel(false);

            for (int i = 0; i < Watchers.Count; i++) Watchers[i].DeadProjectiles.Add(this);
            Watchers.Clear();

            foreach (var seeker in Seekers)
            {
                if (seeker.Info.Target.TargetObject == this)
                    seeker.Info.Target.Reset(session.Tick, Target.States.ProjectileClose);
            }
            Seekers.Clear();

            if (EnableAv && Info.AvShot.ForceHitParticle)
                Info.AvShot.HitEffects(true);

            State = ProjectileState.Dead;

            var detExp = aConst.EndOfLifeAv && (!aConst.ArmOnlyOnEolHit || Info.ObjectsHit > 0);

            if (EnableAv)
            {
                Info.AvShot.HasModel = false;

                if (!Info.AvShot.Active)
                    Info.AvShot.Close();
                else Info.AvShot.EndState = new AvClose { EndPos = Position, Dirty = true, DetonateEffect = detExp };
            }
            else if (Info.AmmoDef.Const.VirtualBeams)
            {
                for (int i = 0; i < VrPros.Count; i++)
                {
                    var vp = VrPros[i];
                    if (!vp.AvShot.Active)
                        vp.AvShot.Close();
                    else vp.AvShot.EndState = new AvClose { EndPos = Position, Dirty = true, DetonateEffect = detExp };

                    session.Projectiles.VirtInfoPool.Return(vp);
                }
                VrPros.Clear();
            }

            if (aConst.DynamicGuidance && session.AntiSmartActive)
                DynTrees.UnregisterProjectile(this);

            var dmgTotal = Info.DamageDoneAoe + Info.DamageDonePri + Info.DamageDoneShld + Info.DamageDoneProj;

            if (dmgTotal > 0 && Info.Ai?.Construct.RootAi != null && !Info.Ai.MarkedForClose && !Info.Weapon.Comp.CoreEntity.MarkedForClose)
            {
                var comp = Info.Weapon.Comp;
                var construct = Info.Ai.Construct.RootAi.Construct;
                construct.TotalEffect += dmgTotal;
                comp.TotalEffect += dmgTotal;
                comp.TotalPrimaryEffect += Info.DamageDonePri;
                comp.TotalAOEEffect += Info.DamageDoneAoe;
                comp.TotalShieldEffect += Info.DamageDoneShld;
                comp.TotalProjectileEffect += Info.DamageDoneProj;
                construct.TotalPrimaryEffect += Info.DamageDonePri;
                construct.TotalAoeEffect += Info.DamageDoneAoe;
                construct.TotalShieldEffect += Info.DamageDoneShld;
                construct.TotalProjectileEffect += Info.DamageDoneProj;
            }

            if (!Info.IsFragment && (aConst.IsDrone || aConst.IsSmart)) 
                Info.Weapon.LiveSmarts--;

            PruningProxyId = -1;
            HadTarget = HadTargetState.None;
            
            Info.Clean(this);

        }
        #endregion


        #region Smart
        internal void RunSmart() // this is grossly inlined thanks to mod profiler... thanks keen.
        {
            Vector3D proposedVel = Velocity;

            var ammo = Info.AmmoDef;
            var aConst = ammo.Const;
            var s = Info.Storage;
            var smarts = ammo.Trajectory.Smarts;
            var w = Info.Weapon;
            var comp = w.Comp;
            var coreParent = comp.TopEntity;
            var startTrack = s.SmartReady || coreParent.MarkedForClose;
            var ai = Info.Ai;
            var session = ai.Session;
            var speedCapMulti = 1d;

            if (aConst.TimedFragments && Info.SpawnDepth < aConst.FragMaxChildren && Info.RelativeAge >= aConst.FragStartTime && Info.RelativeAge - Info.LastFragTime > aConst.FragInterval && Info.Frags < aConst.MaxFrags)
            {
                if (!aConst.HasFragGroup || Info.Frags == 0 || Info.Frags % aConst.FragGroupSize != 0 || Info.RelativeAge - Info.LastFragTime >= aConst.FragGroupDelay)
                    TimedSpawns(aConst);
            }

            var targetLock = false;
            var speedLimitPerTick = aConst.AmmoSkipAccel ? DesiredSpeed : aConst.AccelInMetersPerSec;
            if (!startTrack && Info.DistanceTraveled * Info.DistanceTraveled >= aConst.SmartsDelayDistSqr)
            {
                var lineCheck = new LineD(Position, TargetPosition);
                startTrack = !new MyOrientedBoundingBoxD(coreParent.PositionComp.LocalAABB, coreParent.PositionComp.WorldMatrixRef).Intersects(ref lineCheck).HasValue;
            }

            if (startTrack)
            {
                s.SmartReady = true;
                var fake = Info.Target.TargetState == Target.TargetStates.IsFake;
                var hadTarget = HadTarget != HadTargetState.None;
                var clientSync = aConst.FullSync && Info.Ai.Session.AdvSyncClient;

                var gaveUpChase = !fake && Info.RelativeAge - s.ChaseAge > aConst.MaxChaseTime && hadTarget && !clientSync;
                var overMaxTargets = hadTarget && TargetsSeen > aConst.MaxTargets && aConst.MaxTargets != 0;
                bool validEntity = false;
                if (Info.Target.TargetState == Target.TargetStates.IsEntity) {
                    var targetEnt = (MyEntity)Info.Target.TargetObject;
                    validEntity = !targetEnt.MarkedForClose;
                    
                    var targetChange = validEntity && aConst.FocusOnly && Info.Target.TopEntityId != ai.Construct.Data.Repo.FocusData.Target;
                    if (targetChange && (aConst.FocusEviction || ai.Construct.Data.Repo.FocusData.Target != 0))
                        validEntity = IsFocusTarget(targetEnt);
                }

                var invalidate = !overMaxTargets || clientSync;
                var validTarget = fake || Info.Target.TargetState == Target.TargetStates.IsProjectile || validEntity && invalidate;
                var checkTime = HadTarget != HadTargetState.Projectile ? 30 : 10;

                var prevSlotAge = (Info.PrevRelativeAge + s.SmartSlot) % checkTime;
                var currentSlotAge = (Info.RelativeAge + s.SmartSlot) % checkTime;
                var timeSlot = prevSlotAge < 0 || prevSlotAge > currentSlotAge;

                var prevZombieAge = (s.PrevZombieLifeTime + s.SmartSlot) % checkTime;
                var currentZombieAge = (s.PrevZombieLifeTime + s.SmartSlot) % checkTime;
                var zombieSlot = prevZombieAge < 0 || prevZombieAge > currentZombieAge;

                var prevCheck = Info.PrevRelativeAge  % checkTime;
                var currentCheck = Info.RelativeAge % checkTime;
                var check = prevCheck < 0 || prevCheck > currentCheck;

                var isZombie = aConst.CanZombie && hadTarget && !fake && !validTarget && s.ZombieLifeTime > 0 && zombieSlot;
                var seekNewTarget = timeSlot && hadTarget && !validTarget && !overMaxTargets;
                var seekFirstTarget = !hadTarget && !validTarget && s.PickTarget && (Info.RelativeAge > 120 && timeSlot || check && Info.IsFragment);

                #region TargetTracking
                if ((s.PickTarget && timeSlot && !clientSync || seekNewTarget || gaveUpChase && validTarget || isZombie || seekFirstTarget) && NewTarget() || validTarget)
                {
                    if (s.ZombieLifeTime > 0)
                    {
                        s.ZombieLifeTime = 0;
                        OffSetTarget();
                    }
                    var targetPos = Vector3D.Zero;

                    Ai.FakeTarget.FakeWorldTargetInfo fakeTargetInfo = null;
                    if (fake && s.DummyTargets != null)
                    {
                        var fakeTarget = s.DummyTargets.PaintedTarget.EntityId != 0 ? s.DummyTargets.PaintedTarget : s.DummyTargets.ManualTarget;
                        fakeTargetInfo = fakeTarget.LastInfoTick != session.Tick ? fakeTarget.GetFakeTargetInfo(Info.Ai) : fakeTarget.FakeInfo;
                        targetPos = fakeTargetInfo.WorldPosition;
                        HadTarget = HadTargetState.Fake;
                    }
                    else if (Info.Target.TargetState == Target.TargetStates.IsProjectile)
                    {
                        targetPos = ((Projectile)Info.Target.TargetObject).Position;
                        HadTarget = HadTargetState.Projectile;
                    }
                    else if (Info.Target.TargetState == Target.TargetStates.IsEntity)
                    {
                        targetPos = ((MyEntity)Info.Target.TargetObject).PositionComp.WorldAABB.Center;
                        HadTarget = HadTargetState.Entity;
                    }
                    else
                        HadTarget = HadTargetState.Other;

                    if (aConst.TargetOffSet)
                    {
                        if (Info.RelativeAge - s.LastOffsetTime > 300)
                        {
                            double dist;
                            Vector3D.DistanceSquared(ref Position, ref targetPos, out dist);
                            if (dist < aConst.SmartOffsetSqr + VelocityLengthSqr && Vector3.Dot(Direction, Position - targetPos) > 0)
                                OffSetTarget();
                        }
                        targetPos += OffsetTarget;
                    }

                    TargetPosition = targetPos;
                    targetLock = true;
                    var eTarget = Info.Target.TargetObject as MyEntity;
                    var physics = eTarget != null ? eTarget?.Physics ?? eTarget?.Parent?.Physics : null;

                    var tVel = Vector3.Zero;
                    if (fake && fakeTargetInfo != null) tVel = fakeTargetInfo.LinearVelocity;
                    else if (Info.Target.TargetState == Target.TargetStates.IsProjectile) tVel = ((Projectile)Info.Target.TargetObject).Velocity;
                    else if (physics != null) tVel = physics.LinearVelocity;

                    if (aConst.TargetLossDegree > 0 && Vector3D.DistanceSquared(Info.Origin, Position) >= aConst.SmartsDelayDistSqr)
                    {
                        if (s.WasTracking && Vector3.Dot(Direction, Vector3D.Normalize(targetPos - Position)) < aConst.TargetLossDegree)
                        {
                            s.PickTarget = true;
                        }
                        else if (!s.WasTracking)
                            s.WasTracking = true;
                    }

                    PrevTargetVel = tVel;
                }
                else
                {
                    var roam = smarts.Roam;
                    var straightAhead = roam && TargetPosition != Vector3D.Zero;
                    TargetPosition = straightAhead ? TargetPosition : Position + (Direction * Info.MaxTrajectory);

                    s.ZombieLifeTime += DeltaTimeRatio;
                    if (s.ZombieLifeTime > aConst.TargetLossTime && !smarts.KeepAliveAfterTargetLoss && (smarts.NoTargetExpire || hadTarget))
                    {
                        DistanceToTravelSqr = Info.DistanceTraveled * Info.DistanceTraveled;
                        EndState = EndStates.EarlyEnd;
                    }

                    if (roam && Info.RelativeAge - s.LastOffsetTime > 300 && hadTarget)
                    {

                        double dist;
                        Vector3D.DistanceSquared(ref Position, ref TargetPosition, out dist);
                        if (dist < aConst.SmartOffsetSqr + VelocityLengthSqr && Vector3.Dot(Direction, Position - TargetPosition) > 0)
                        {
                            OffSetTarget(true);
                            TargetPosition += OffsetTarget;
                        }
                    }
                    else if (aConst.IsMine && s.LastActivatedStage >= 0)
                    {
                        ResetMine();
                        return;
                    }
                }
                #endregion

                var accelMpsMulti = speedLimitPerTick;
                var aCount = aConst.ApproachesCount;
                bool disableAvoidance = false;
                if (aCount > 0 && s.RequestedStage < aCount && s.RequestedStage >= -1)
                {
                    var callDepth = aCount;
                    ProcessStage(ref accelMpsMulti, ref speedCapMulti, ref disableAvoidance, TargetPosition, s.RequestedStage, targetLock, callDepth);
                }

                #region Navigation

                Vector3D commandedAccel;
                Vector3D missileToTargetNorm = Vector3D.Zero;
                if (!aConst.NoSteering)
                {
                    Vector3D targetAcceleration = Vector3D.Zero;
                    if (s.LastVelocity.HasValue)
                        targetAcceleration = (PrevTargetVel - s.LastVelocity.Value) * 60;

                    s.LastVelocity = PrevTargetVel;

                    Vector3D missileToTarget = TargetPosition - Position;
                    missileToTargetNorm = Vector3D.Normalize(missileToTarget);
                    Vector3D relativeVelocity = PrevTargetVel - Velocity;
                    Vector3D lateralTargetAcceleration = (targetAcceleration - Vector3D.Dot(targetAcceleration, missileToTargetNorm) * missileToTargetNorm);

                    Vector3D omega = Vector3D.Cross(missileToTarget, relativeVelocity) / Math.Max(missileToTarget.LengthSquared(), 1); //to combat instability at close range
                    var lateralAcceleration = smarts.Aggressiveness * relativeVelocity.Length() * Vector3D.Cross(omega, missileToTargetNorm) + smarts.NavAcceleration * lateralTargetAcceleration;

                    if (Vector3D.IsZero(lateralAcceleration))
                    {
                        commandedAccel = missileToTargetNorm * accelMpsMulti;
                    }
                    else
                    {
                        var diff = accelMpsMulti * accelMpsMulti - lateralAcceleration.LengthSquared();
                        commandedAccel = diff < 0 ? Vector3D.Normalize(lateralAcceleration) * accelMpsMulti : lateralAcceleration + Math.Sqrt(diff) * missileToTargetNorm;
                    }

                    if (Gravity.LengthSquared() > 1e-3)
                    {
                        if (!Vector3D.IsZero(commandedAccel))
                        {
                            var directionNorm = Vector3D.IsUnit(ref commandedAccel) ? commandedAccel : Vector3D.Normalize(commandedAccel);
                            Vector3D gravityCompensationVec;

                            if (Vector3D.IsZero(Gravity) || Vector3D.IsZero(commandedAccel))
                                gravityCompensationVec = Vector3D.Zero;
                            else
                                gravityCompensationVec = (Gravity - Gravity.Dot(commandedAccel) / commandedAccel.LengthSquared() * commandedAccel);

                            var diffSq = accelMpsMulti * accelMpsMulti - gravityCompensationVec.LengthSquared();
                            commandedAccel = diffSq < 0 ? commandedAccel - Gravity : directionNorm * Math.Sqrt(diffSq) + gravityCompensationVec;
                        }
                    }
                }
                else
                    commandedAccel = Direction * accelMpsMulti;

                var offset = false;
                if (smarts.OffsetTime > 0)
                {
                    var prevSmartCheck = Info.PrevRelativeAge % smarts.OffsetTime;
                    var currentSmartCheck = Info.RelativeAge % smarts.OffsetTime;
                    var smartCheck = prevSmartCheck < 0 || prevSmartCheck > currentSmartCheck;

                    if (smartCheck && !Vector3D.IsZero(Direction) && MyUtils.IsValid(Direction))
                    {
                        var up = Vector3D.CalculatePerpendicularVector(Direction);
                        var right = Vector3D.Cross(Direction, up);
                        var angle = Info.Random.NextDouble() * MathHelper.TwoPi;
                        s.RandOffsetDir = Math.Sin(angle) * up + Math.Cos(angle) * right;
                        s.RandOffsetDir *= smarts.OffsetRatio;
                    }

                    double distSqr;
                    Vector3D.DistanceSquared(ref TargetPosition, ref Position, out distSqr);
                    if (distSqr >= aConst.OffsetMinRangeSqr)
                    {
                        commandedAccel += accelMpsMulti * s.RandOffsetDir;
                        offset = true;
                    }
                }

                if (accelMpsMulti > 0)
                {
                    var maxRotationsPerTickInRads = aConst.MaxLateralThrust;

                    if (aConst.AdvancedSmartSteering)
                    {
                        bool isNormalized;
                        var newHeading = ProNavControl(Direction, Velocity, commandedAccel, aConst.PreComputedMath, out isNormalized);
                        proposedVel = Velocity + (isNormalized ? newHeading * accelMpsMulti * DeltaStepConst : commandedAccel * DeltaStepConst);
                    }
                    else
                    {
                        if (maxRotationsPerTickInRads < 1)
                        {
                            var commandNorm = Vector3D.Normalize(commandedAccel);

                            var dot = Vector3D.Dot(Direction, commandNorm);
                            if (offset || dot < 0.98)
                            {
                                var radPerTickDelta = Math.Acos(dot);
                                if (radPerTickDelta == 0)
                                    radPerTickDelta = double.Epsilon;

                                if (radPerTickDelta > maxRotationsPerTickInRads && dot > 0)
                                    commandedAccel = commandNorm * (accelMpsMulti * Math.Abs(radPerTickDelta / MathHelperD.Pi - 1));
                            }
                        }
                        proposedVel = Velocity + (commandedAccel * DeltaStepConst);
                    }

                    Vector3D moddedAccel;
                    if (aConst.CheckFutureIntersection && !disableAvoidance && session.Tick - 1 == s.Obstacle.LastSeenTick && AvoidObstacle(Position + proposedVel, missileToTargetNorm, accelMpsMulti, out moddedAccel))
                        proposedVel = moddedAccel;

                    Vector3D.Normalize(ref proposedVel, out Direction);
                }
                #endregion
            }
            else if (!smarts.AccelClearance || s.SmartReady)
            {
                proposedVel = Velocity + (Direction * (aConst.DeltaVelocityPerTick * DeltaTimeRatio));
            }

            VelocityLengthSqr = proposedVel.LengthSquared();
            if (VelocityLengthSqr <= DesiredSpeed * DesiredSpeed)
                MaxSpeed = DesiredSpeed;

            var speedCap = speedCapMulti * MaxSpeed;
            if (VelocityLengthSqr > speedCap * speedCap) {
                VelocityLengthSqr = proposedVel.LengthSquared();
                proposedVel = Direction * speedCap;
            }
            else
                Info.TotalAcceleration += (proposedVel - PrevVelocity);

            PrevVelocity = Velocity;
            if (Info.TotalAcceleration.LengthSquared() > aConst.MaxAccelerationSqr)
                proposedVel = Velocity;

            Velocity = proposedVel;

            if (aConst.DynamicGuidance)
            {
                if (PruningProxyId != -1 && session.ActiveAntiSmarts > 0)
                {
                    var sphere = new BoundingSphereD(Position, aConst.LargestHitSize);
                    BoundingBoxD result;
                    BoundingBoxD.CreateFromSphere(ref sphere, out result);
                    var displacement = 0.1 * Velocity;
                    session.ProjectileTree.MoveProxy(PruningProxyId, ref result, displacement);
                }
            }
        }

        private bool AvoidObstacle(Vector3D proposedPos, Vector3D missileToTargetNorm, double accelMpsMulti, out Vector3D moddedAccel)
        {
            moddedAccel = Vector3D.Zero;
            var aSphere = Info.Storage.Obstacle.AvoidSphere;

            var intersect = aSphere.Contains(proposedPos) != ContainmentType.Disjoint;

            if (!intersect)
                return false;

            if (SurfaceModifiedCommandAccel(aSphere, Position, TargetPosition, accelMpsMulti, missileToTargetNorm, out moddedAccel)) {
                moddedAccel = Velocity + (moddedAccel * DeltaStepConst);
                return true; 
            }

            return false;
        }

        private bool SurfaceModifiedCommandAccel(BoundingSphereD aSphere, Vector3D position, Vector3D desiredPosition, double accelMpsMulti, Vector3D missileToTargetNorm, out Vector3D moddedAccel)
        {
            var targetDir = !Vector3D.IsZero(missileToTargetNorm) ? missileToTargetNorm : Vector3D.Normalize(desiredPosition - position);
            var d = position + targetDir * (accelMpsMulti * DeltaStepConst) - aSphere.Center;
            var desiredDir = Vector3D.Normalize(d);
            var futurePos = position + desiredDir;

            moddedAccel = Vector3D.Zero;
            if (Vector3D.DistanceSquared(futurePos, aSphere.Center) > Vector3D.DistanceSquared(LastPosition, aSphere.Center)) 
                return false;

            moddedAccel = Vector3D.Normalize(futurePos - aSphere.Center) * accelMpsMulti;
            return true;
        }

        private void ProcessStage(ref double accelMpsMulti, ref double speedCapMulti, ref bool disableAvoidance, Vector3D targetPos, int lastActiveStage, bool targetLock, int callDepth)
        {
            if (callDepth-- < 0) {
                if (Info.Ai.Session.HandlesInput && Info.Ai.Session.DebugMod)
                    Info.Ai.Session.ShowLocalNotify("Approach is attempting to infinite loop, fix your approach moveNext/restart conditions", 2000);
                return;
            }

            var s = Info.Storage;
            if (targetLock)
                s.ApproachInfo.TargetPos = targetPos;

            if (!Vector3D.IsZero(s.ApproachInfo.TargetPos))
            {
                if (s.RequestedStage == -1)
                {
                    if (Info.Ai.Session.DebugMod)
                        Log.Line($"StageStart: {Info.AmmoDef.AmmoRound} - last: {s.LastActivatedStage} - age:{Info.RelativeAge}");
                    s.LastActivatedStage = -1;
                    s.RequestedStage = 0;
                }

                var stageChange = s.RequestedStage != lastActiveStage;

                if (stageChange)
                {
                    if (Info.Ai.Session.DebugMod)
                        Log.Line($"state change: {s.RequestedStage} - age:{Info.RelativeAge}");
                    s.ApproachInfo.StartDistanceTraveled = Info.DistanceTraveled;
                }

                var aConst = Info.AmmoDef.Const;
                if (s.RequestedStage >= aConst.Approaches.Length)
                {
                    Log.Line($"ProcessStage outside of bounds: {s.RequestedStage > aConst.Approaches.Length - 1} - lastStage:{lastActiveStage} - {Info.Weapon.System.ShortName} - {Info.Weapon.ActiveAmmoDef.AmmoDef.AmmoRound}");
                    return;
                }
                var approach = aConst.Approaches[s.RequestedStage];
                var def = approach.Definition;

                if (def.StartCondition1 == def.StartCondition2 || def.EndCondition1 == def.EndCondition2)
                    return; // bad modder, failed to read coreparts comment, fail silently so they drive themselves nuts

                disableAvoidance = def.DisableAvoidance;
                var planetExists = Info.MyPlanet != null;

                if (def.AdjustUp || stageChange)
                {
                    switch (def.Up)
                    {
                        case UpRelativeTo.RelativeToBlock:
                            s.ApproachInfo.OffsetDir = Info.OriginUp;
                            break;
                        case UpRelativeTo.RelativeToGravity:
                            s.ApproachInfo.OffsetDir = !planetExists ? Info.OriginUp : Vector3D.Normalize(Position - Info.MyPlanet.PositionComp.WorldAABB.Center);
                            break;
                        case UpRelativeTo.TargetDirection:
                            s.ApproachInfo.OffsetDir = Vector3D.Normalize(s.ApproachInfo.TargetPos - Position);
                            break;
                        case UpRelativeTo.TargetVelocity:
                            s.ApproachInfo.OffsetDir = !Vector3D.IsZero(PrevTargetVel) ? Vector3D.Normalize(PrevTargetVel) : Info.OriginUp;
                            break;
                        case UpRelativeTo.StoredStartDestination:
                            var storedStartDest = s.ApproachInfo.StoredDestination[s.RequestedStage];
                            var destStart = storedStartDest != Vector3D.Zero ? storedStartDest : s.ApproachInfo.TargetPos;
                            s.ApproachInfo.OffsetDir = Vector3D.Normalize(destStart - Position);
                            break;
                        case UpRelativeTo.StoredEndDestination:
                            var storedEndDest = s.ApproachInfo.StoredDestination[s.RequestedStage * 2];
                            var destEnd = storedEndDest != Vector3D.Zero ? storedEndDest : s.ApproachInfo.TargetPos;
                            s.ApproachInfo.OffsetDir = Vector3D.Normalize(destEnd - Position);
                            break;
                        default:
                            s.ApproachInfo.OffsetDir = Info.OriginUp;
                            break;
                    }
                }

                if (!MyUtils.IsZero(def.AngleOffset))
                {
                    var angle = def.AngleOffset * MathHelper.Pi;
                    var forward = Vector3D.CalculatePerpendicularVector(s.ApproachInfo.OffsetDir);
                    var right = Vector3D.Cross(s.ApproachInfo.OffsetDir, forward);
                    s.ApproachInfo.OffsetDir = Math.Sin(angle) * forward + Math.Cos(angle) * right;
                }

                Vector3D surfacePos = Vector3D.Zero;
                if (stageChange || def.AdjustSource)
                {
                    switch (def.Source)
                    {
                        case RelativeTo.Origin:
                            s.ApproachInfo.LookAtPos = Info.Origin;
                            break;
                        case RelativeTo.Shooter:
                            s.ApproachInfo.LookAtPos = Info.Weapon.MyPivotPos;
                           break;
                        case RelativeTo.Target:
                            s.ApproachInfo.LookAtPos = s.ApproachInfo.TargetPos;
                            break;
                        case RelativeTo.Surface:
                            if (planetExists)
                            {
                                PlanetSurfaceHeightAdjustment(Position, Direction, approach, out surfacePos);
                                s.ApproachInfo.LookAtPos = surfacePos;
                            }
                            else
                                s.ApproachInfo.LookAtPos = Info.Origin;
                            break;
                        case RelativeTo.MidPoint:
                            s.ApproachInfo.LookAtPos = Vector3D.Lerp(s.ApproachInfo.TargetPos, Position, 0.5);
                            break;
                        case RelativeTo.Current:
                            s.ApproachInfo.LookAtPos = Position;
                            break;
                        case RelativeTo.StoredStartDestination:
                            var storedStartDest = s.ApproachInfo.StoredDestination[s.RequestedStage];
                            var destStart = storedStartDest != Vector3D.Zero ? storedStartDest : s.ApproachInfo.TargetPos;
                            s.ApproachInfo.LookAtPos = destStart;
                            break;
                        case RelativeTo.StoredEndDestination:
                            var storedEndDest = s.ApproachInfo.StoredDestination[s.RequestedStage * 2];
                            var destEnd = storedEndDest != Vector3D.Zero ? storedEndDest : s.ApproachInfo.TargetPos;
                            s.ApproachInfo.LookAtPos = destEnd;
                            break;
                    }
                }

                var heightOffset = s.ApproachInfo.OffsetDir * def.DesiredElevation;
                var source = s.ApproachInfo.LookAtPos;
                if (stageChange || def.AdjustDestination)
                {
                    switch (def.Destination)
                    {
                        case RelativeTo.Origin:
                            s.ApproachInfo.DestinationPos = s.ApproachInfo.TargetPos; // change to origin in a few days
                            break;
                        case RelativeTo.Shooter:
                            s.ApproachInfo.DestinationPos = Info.Weapon.MyPivotPos;
                            break;
                        case RelativeTo.Target:
                            s.ApproachInfo.DestinationPos = s.ApproachInfo.TargetPos;
                            break;
                        case RelativeTo.Surface:
                            if (planetExists)
                            {
                                PlanetSurfaceHeightAdjustment(Position, Direction, approach, out surfacePos);
                                s.ApproachInfo.DestinationPos = surfacePos;
                            }
                            else
                                s.ApproachInfo.DestinationPos = Info.Origin;
                            break;
                        case RelativeTo.MidPoint:
                            s.ApproachInfo.DestinationPos = Vector3D.Lerp(s.ApproachInfo.TargetPos, Position, 0.5);
                            break;
                        case RelativeTo.Current:
                            s.ApproachInfo.DestinationPos = Position;
                            break;
                        case RelativeTo.StoredStartDestination:
                            var storedStartDest = s.ApproachInfo.StoredDestination[s.RequestedStage];
                            var destStart = storedStartDest != Vector3D.Zero ? storedStartDest : s.ApproachInfo.TargetPos;
                            s.ApproachInfo.DestinationPos = destStart;
                            break;
                        case RelativeTo.StoredEndDestination:
                            var storedEndDest = s.ApproachInfo.StoredDestination[s.RequestedStage * 2];
                            var destEnd = storedEndDest != Vector3D.Zero ? storedEndDest : s.ApproachInfo.TargetPos;
                            s.ApproachInfo.DestinationPos = destEnd;
                            break;
                        case RelativeTo.Nothing:
                            s.ApproachInfo.DestinationPos = Info.Target.TargetPos;
                            break;
                    }
                }
                var destination = s.ApproachInfo.DestinationPos;
                if (def.OffsetMinRadius > 0 && def.OffsetTime > 0)
                {
                    var prevCheck = Info.PrevRelativeAge % def.OffsetTime;
                    var currentCheck = Info.RelativeAge % def.OffsetTime;
                    if (prevCheck < 0 || prevCheck > currentCheck)
                        SetNavTargetOffset(def);
                    destination += s.ApproachInfo.NavTargetBound.Center;
                }

                var heightStart = source + heightOffset;
                var heightend = destination + heightOffset;
                var heightDir = heightend - heightStart;
                var startToEndDist = heightDir.Normalize();
                bool start1;
                switch (def.StartCondition1)
                {
                    case Conditions.DesiredElevation:
                        var plane = new PlaneD(s.ApproachInfo.LookAtPos, s.ApproachInfo.OffsetDir);
                        var distToPlane = plane.DistanceToPoint(Position);
                        var tolernace = def.ElevationTolerance + aConst.CollisionSize;
                        var distFromSurfaceSqr = !Vector3D.IsZero(surfacePos) ? Vector3D.DistanceSquared(Position, surfacePos) : distToPlane * distToPlane;
                        var lessThanTolerance = (def.Start1Value + tolernace) * (def.Start1Value + tolernace);
                        var greaterThanTolerance = (def.Start1Value - tolernace) * (def.Start1Value - tolernace);
                        start1 = distFromSurfaceSqr >= greaterThanTolerance && distFromSurfaceSqr <= lessThanTolerance;
                        break;
                    case Conditions.DistanceFromTarget: // could save a sqrt by inlining and using heightDir
                        if (Info.Ai.Session.DebugMod && Info.Ai.Session.HandlesInput)
                            DsDebugDraw.DrawLine(heightend, destination, Color.Green, 10);
                        start1 = MyUtils.GetPointLineDistance(ref heightend, ref destination, ref Position) - aConst.CollisionSize <= def.Start1Value;
                        break;
                    case Conditions.DistanceToTarget: // could save a sqrt by inlining and using heightDir
                        if (Info.Ai.Session.DebugMod && Info.Ai.Session.HandlesInput)
                            DsDebugDraw.DrawLine(heightend, destination, Color.Green, 10);
                        start1 = MyUtils.GetPointLineDistance(ref heightend, ref destination, ref Position) - aConst.CollisionSize >= def.Start1Value;
                        break;
                    case Conditions.Lifetime:
                        start1 = Info.RelativeAge >= def.Start1Value;
                        break;
                    case Conditions.Deadtime:
                        start1 = Info.RelativeAge <= def.Start1Value;
                        break;
                    case Conditions.MinTravelRequired:
                        start1 = Info.DistanceTraveled - s.ApproachInfo.StartDistanceTraveled >= def.Start1Value;
                        break;
                    case Conditions.MaxTravelRequired:
                        start1 = Info.DistanceTraveled - s.ApproachInfo.StartDistanceTraveled <= def.Start1Value;
                        break;
                    case Conditions.Spawn:
                    case Conditions.Ignore:
                        start1 = true;
                        break;
                    default:
                        start1 = false;
                        break;
                }

                bool start2;
                switch (def.StartCondition2)
                {
                    case Conditions.DesiredElevation:
                        var plane = new PlaneD(s.ApproachInfo.LookAtPos, s.ApproachInfo.OffsetDir);
                        var distToPlane = plane.DistanceToPoint(Position);
                        var tolernace = def.ElevationTolerance + aConst.CollisionSize;
                        var distFromSurfaceSqr = !Vector3D.IsZero(surfacePos) ? Vector3D.DistanceSquared(Position, surfacePos) : distToPlane * distToPlane;
                        var lessThanTolerance = (def.Start2Value + tolernace) * (def.Start2Value + tolernace);
                        var greaterThanTolerance = (def.Start2Value - tolernace) * (def.Start2Value - tolernace);
                        start2 = distFromSurfaceSqr >= greaterThanTolerance && distFromSurfaceSqr <= lessThanTolerance;
                        break;
                    case Conditions.DistanceFromTarget: // could save a sqrt by inlining and using heightDir
                        if (Info.Ai.Session.DebugMod)
                            DsDebugDraw.DrawLine(heightend, destination, Color.Blue, 10);
                        start2 = MyUtils.GetPointLineDistance(ref heightend, ref destination, ref Position) - aConst.CollisionSize <= def.Start2Value;
                        break;
                    case Conditions.DistanceToTarget: 
                        if (Info.Ai.Session.DebugMod)
                            DsDebugDraw.DrawLine(heightend, destination, Color.Green, 10);
                        start2 = MyUtils.GetPointLineDistance(ref heightend, ref destination, ref Position) - aConst.CollisionSize >= def.Start2Value;
                        break;
                    case Conditions.Lifetime:
                        start2 = Info.RelativeAge >= def.Start2Value;
                        break;
                    case Conditions.Deadtime:
                        start2 = Info.RelativeAge <= def.Start2Value;
                        break;
                    case Conditions.MinTravelRequired:
                        start2 = Info.DistanceTraveled - s.ApproachInfo.StartDistanceTraveled >= def.Start2Value;
                        break;
                    case Conditions.MaxTravelRequired:
                        start2 = Info.DistanceTraveled - s.ApproachInfo.StartDistanceTraveled <= def.Start2Value;
                        break;
                    case Conditions.Spawn:
                    case Conditions.Ignore:
                        start2 = true;
                        break;
                    default:
                        start2 = false;
                        break;
                }

                if (approach.StartAnd && start1 && start2 || !approach.StartAnd && (start1 || start2) || s.LastActivatedStage >= 0 && !def.CanExpireOnceStarted)
                {


                    accelMpsMulti = aConst.AccelInMetersPerSec * def.AccelMulti;
                    speedCapMulti = def.SpeedCapMulti;

                    var travelLead = Info.DistanceTraveled - s.ApproachInfo.StartDistanceTraveled >= def.TrackingDistance ? Info.DistanceTraveled : 0;
                    var desiredLead = (def.PushLeadByTravelDistance ? travelLead : 0) + def.LeadDistance;
                    var clampedLead = MathHelperD.Clamp(desiredLead, approach.ModFutureStep, double.MaxValue);
                    var leadPosition = heightStart + heightDir * clampedLead;
                    Vector3D heightAdjLeadPos;
                    switch (def.Elevation)
                    {
                        case RelativeTo.Surface:
                        {
                            if (Info.MyPlanet != null && planetExists && def.Elevation == RelativeTo.Surface)
                            {
                                Vector3D followSurfacePos;
                                heightAdjLeadPos = PlanetSurfaceHeightAdjustment(leadPosition, s.ApproachInfo.OffsetDir, approach, out followSurfacePos);
                            }
                            else
                                heightAdjLeadPos = leadPosition;
                            break;
                        }
                        case RelativeTo.Origin:
                        {
                            var plane = new PlaneD(Info.Origin, heightDir);
                            var distToPlane = plane.DistanceToPoint(leadPosition);
                            heightAdjLeadPos = leadPosition + (heightDir * distToPlane);
                            break;
                        }
                        case RelativeTo.MidPoint:
                        {
                            var projetedPos = Vector3D.Lerp(destination, leadPosition, 0.5);
                            var plane = new PlaneD(projetedPos, heightDir);
                            var distToPlane = plane.DistanceToPoint(leadPosition);
                            heightAdjLeadPos = leadPosition + (heightDir * distToPlane);
                            break;
                        }
                        case RelativeTo.Shooter:
                        {
                            var plane = new PlaneD(Info.Weapon.MyPivotPos, heightDir);
                            var distToPlane = plane.DistanceToPoint(leadPosition);
                            heightAdjLeadPos = leadPosition + (heightDir * distToPlane);
                            break;
                        }
                        case RelativeTo.Target:
                        {
                            var plane = new PlaneD(destination, heightDir);
                            var distToPlane = plane.DistanceToPoint(leadPosition);
                            heightAdjLeadPos = leadPosition + (heightDir * distToPlane);
                            break;
                        }
                        case RelativeTo.Current:
                        {
                            var plane = new PlaneD(Position, heightDir);
                            var distToPlane = plane.DistanceToPoint(leadPosition);
                            heightAdjLeadPos = leadPosition + (heightDir * distToPlane);
                            break;
                        }
                        case RelativeTo.StoredStartDestination:
                        {
                            var storedDest = s.ApproachInfo.StoredDestination[s.RequestedStage];
                            var dest = storedDest != Vector3D.Zero ? storedDest : s.ApproachInfo.TargetPos;
                            var plane = new PlaneD(dest, heightDir);
                            var distToPlane = plane.DistanceToPoint(leadPosition);
                            heightAdjLeadPos = leadPosition + (heightDir * distToPlane);
                            break;
                        }
                        case RelativeTo.StoredEndDestination:
                        {
                            var storedDest = s.ApproachInfo.StoredDestination[s.RequestedStage * 2];
                            var dest = storedDest != Vector3D.Zero ? storedDest : s.ApproachInfo.TargetPos;
                            var plane = new PlaneD(dest, heightDir);
                            var distToPlane = plane.DistanceToPoint(leadPosition);
                            heightAdjLeadPos = leadPosition + (heightDir * distToPlane);
                            break;
                        }
                        case RelativeTo.Nothing:
                        {
                            heightAdjLeadPos = s.ApproachInfo.TargetPos;
                            break;
                        }
                        default:
                            heightAdjLeadPos = leadPosition;
                            break;
                    }

                    if (MyUtils.IsValid(heightAdjLeadPos - destination))
                    {
                        var destPerspectiveDir = Vector3D.Normalize(heightAdjLeadPos - destination);
                        if (!def.Orbit)
                        {
                            if (def.Elevation != RelativeTo.Nothing && !Vector3D.IsZero(heightDir - destPerspectiveDir))
                                TargetPosition = MyUtils.LinePlaneIntersection(heightAdjLeadPos, heightDir, destination, destPerspectiveDir);
                            else
                                TargetPosition = heightAdjLeadPos;
                        }
                        else
                            TargetPosition = ApproachOrbits(ref def, s.ApproachInfo.OffsetDir, heightAdjLeadPos, accelMpsMulti, speedCapMulti);
                    }

                    if (s.LastActivatedStage != s.RequestedStage)
                    {
                        if (Info.Ai.Session.DebugMod)
                            Log.Line($"stage: age:{Info.RelativeAge} - {s.RequestedStage} - CanExpireOnceStarted:{def.CanExpireOnceStarted}");
                        s.LastActivatedStage = s.RequestedStage;

                        switch (def.StartEvent)
                        {
                            case StageEvents.EndProjectile:
                                EndState = EndStates.EarlyEnd;
                                DistanceToTravelSqr = Info.DistanceTraveled * Info.DistanceTraveled;
                                break;
                            case StageEvents.DoNothing:
                                break;
                            case StageEvents.StoreDestination:
                                s.ApproachInfo.StoredDestination[s.RequestedStage] = TargetPosition;
                                break;
                        }
                    }

                    if (Info.Ai.Session.DebugMod && Info.Ai.Session.HandlesInput)
                    {
                        DsDebugDraw.DrawLine(heightAdjLeadPos, destination, Color.White, 3);
                        DsDebugDraw.DrawSingleVec(destination, 10, Color.LightSkyBlue);
                    }
                }

                if (Info.Ai.Session.DebugMod && Info.Ai.Session.HandlesInput)
                {
                    DsDebugDraw.DrawSingleVec(heightStart, 10, Color.GreenYellow);
                    DsDebugDraw.DrawSingleVec(heightend, 10, Color.LightSkyBlue);
                    DsDebugDraw.DrawSingleVec(TargetPosition, 10, Color.Red);
                }

                bool end1;
                switch (def.EndCondition1)
                {
                    case Conditions.DesiredElevation:
                        var plane = new PlaneD(s.ApproachInfo.LookAtPos, s.ApproachInfo.OffsetDir);
                        var distToPlane = plane.DistanceToPoint(Position);
                        var tolernace = def.ElevationTolerance + aConst.CollisionSize;
                        var distFromSurfaceSqr = !Vector3D.IsZero(surfacePos) ? Vector3D.DistanceSquared(Position, surfacePos) : distToPlane * distToPlane;
                        var lessThanTolerance = (def.End1Value + tolernace) * (def.End1Value + tolernace);
                        var greaterThanTolerance = (def.End1Value - tolernace) * (def.End1Value - tolernace);
                        end1 = distFromSurfaceSqr >= greaterThanTolerance && distFromSurfaceSqr <= lessThanTolerance;
                        break;
                    case Conditions.DistanceFromTarget:
                        if (def.EndCondition1 == def.StartCondition1)
                            end1 = start1;
                        else if (def.EndCondition1 == def.StartCondition2)
                            end1 = start2;
                        else
                        {
                            if (Info.Ai.Session.DebugMod && Info.Ai.Session.HandlesInput)
                                DsDebugDraw.DrawLine(heightend, destination, Color.Red, 10);
                            end1 = MyUtils.GetPointLineDistance(ref heightend, ref destination, ref Position) - aConst.CollisionSize <= def.End1Value;
                        }
                        break;
                    case Conditions.DistanceToTarget: 
                        if (Info.Ai.Session.DebugMod && Info.Ai.Session.HandlesInput)
                            DsDebugDraw.DrawLine(heightend, destination, Color.Green, 10);
                        end1 = MyUtils.GetPointLineDistance(ref heightend, ref destination, ref Position) - aConst.CollisionSize >= def.End1Value;
                        break;
                    case Conditions.Lifetime:
                        end1 = Info.RelativeAge >= def.End1Value;
                        break;
                    case Conditions.Deadtime:
                        end1 = Info.RelativeAge <= def.End1Value;
                        break;
                    case Conditions.MinTravelRequired:
                        end1 = Info.DistanceTraveled - s.ApproachInfo.StartDistanceTraveled >= def.End1Value;
                        break;
                    case Conditions.MaxTravelRequired:
                        end1 = Info.DistanceTraveled - s.ApproachInfo.StartDistanceTraveled <= def.End1Value;
                        break;
                    case Conditions.Ignore:
                        end1 = true;
                        break;
                    default:
                        end1 = false;
                        break;
                }

                bool end2;
                switch (def.EndCondition2)
                {
                    case Conditions.DesiredElevation:
                        var plane = new PlaneD(s.ApproachInfo.LookAtPos, s.ApproachInfo.OffsetDir);
                        var distToPlane = plane.DistanceToPoint(Position);
                        var tolernace = def.ElevationTolerance + aConst.CollisionSize;
                        var distFromSurfaceSqr = !Vector3D.IsZero(surfacePos) ? Vector3D.DistanceSquared(Position, surfacePos) : distToPlane * distToPlane;
                        var lessThanTolerance = (def.End2Value + tolernace) * (def.End2Value + tolernace);
                        var greaterThanTolerance = (def.End2Value - tolernace) * (def.End2Value - tolernace);
                        end2 = distFromSurfaceSqr >= greaterThanTolerance && distFromSurfaceSqr <= lessThanTolerance;
                        break;
                    case Conditions.DistanceFromTarget:
                        if (def.EndCondition2 == def.StartCondition1)
                            end2 = start1;
                        else if (def.EndCondition2 == def.StartCondition2)
                            end2 = start2;
                        else
                        {
                            if (Info.Ai.Session.DebugMod && Info.Ai.Session.HandlesInput)
                                DsDebugDraw.DrawLine(heightend, destination, Color.Yellow, 10);
                            end2 = MyUtils.GetPointLineDistance(ref heightend, ref destination, ref Position) - aConst.CollisionSize <= def.End2Value;
                        }
                        break;
                    case Conditions.DistanceToTarget: 
                        if (Info.Ai.Session.DebugMod && Info.Ai.Session.HandlesInput)
                            DsDebugDraw.DrawLine(heightend, destination, Color.Green, 10);
                        end2 = MyUtils.GetPointLineDistance(ref heightend, ref destination, ref Position) - aConst.CollisionSize >= def.End2Value;
                        break;
                    case Conditions.Lifetime:
                        end2 = Info.RelativeAge >= def.End2Value;
                        break;
                    case Conditions.Deadtime:
                        end2 = Info.RelativeAge <= def.End2Value;
                        break;
                    case Conditions.MinTravelRequired:
                        end2 = Info.DistanceTraveled - s.ApproachInfo.StartDistanceTraveled >= def.End2Value;
                        break;
                    case Conditions.MaxTravelRequired:
                        end2 = Info.DistanceTraveled - s.ApproachInfo.StartDistanceTraveled <= def.End2Value;
                        break;
                    case Conditions.Ignore:
                        end2 = true;
                        break;
                    default:
                        end2 = false;
                        break;
                }

                if (Info.Ai.Session.DebugMod)
                {
                    var session = Info.Ai.Session;
                    if (session.ApproachDebug.ProId == Info.Id || session.Tick != session.ApproachDebug.LastTick)
                    {
                        session.ApproachDebug = new ApproachDebug { 
                            LastTick = session.Tick, Approach = approach, 
                            Start1 = start1, Start2 = start2, End1 = end1, End2 = end2, 
                            ProId = Info.Id, Stage = s.LastActivatedStage
                        };
                    }
                }

                if (approach.EndAnd && end1 && end2 || !approach.EndAnd && (end1 || end2))
                {
                    var hasNextStep = s.RequestedStage + 1 < aConst.ApproachesCount;
                    var isActive = s.LastActivatedStage >= 0;
                    var activeNext = isActive && !def.ForceRestart && (def.RestartCondition == ReInitCondition.Wait || def.RestartCondition == ReInitCondition.MoveToPrevious || def.RestartCondition == ReInitCondition.MoveToNext);
                    var inActiveNext = !isActive && !def.ForceRestart && def.RestartCondition == ReInitCondition.MoveToNext;
                    var moveForward = hasNextStep && (activeNext || inActiveNext);
                    var reStart = def.RestartCondition == ReInitCondition.MoveToPrevious && !isActive || def.RestartCondition == ReInitCondition.ForceRestart;

                    if (def.EndEvent == StageEvents.EndProjectile || def.EndEvent == StageEvents.EndProjectileOnRestart && (reStart || !moveForward && hasNextStep)) {
                        EndState = EndStates.EarlyEnd;
                        DistanceToTravelSqr = Info.DistanceTraveled * Info.DistanceTraveled;
                    }
                    else if (def.EndEvent == StageEvents.StoreDestination)
                    {
                        s.ApproachInfo.StoredDestination[s.RequestedStage * 2] = TargetPosition;
                    }

                    if (moveForward)
                    {
                        var oldLast = s.LastActivatedStage;
                        s.LastActivatedStage = s.RequestedStage;
                        ++s.RequestedStage;
                        if (Info.Ai.Session.DebugMod)
                            Log.Line($"stageEnd: age:{Info.RelativeAge} - next: {s.RequestedStage} - last:{oldLast} - eCon1:{def.EndCondition1} - eCon2:{def.EndCondition2}");
                        ProcessStage(ref accelMpsMulti, ref speedCapMulti, ref disableAvoidance, targetPos, s.LastActivatedStage, targetLock, callDepth);
                    }
                    else if (reStart || def.ForceRestart)
                    {
                        s.LastActivatedStage = s.RequestedStage;
                        var prev = s.RequestedStage;
                        s.RequestedStage = def.RestartCondition == ReInitCondition.MoveToPrevious ? prev : def.OnRestartRevertTo;
                        if (Info.Ai.Session.DebugMod)
                            Log.Line($"stageEnd:age:{Info.RelativeAge} - previous:{prev} to {s.RequestedStage} - eCon1:{def.EndCondition1} - eCon2:{def.EndCondition2}");
                    }
                    else if (!hasNextStep)
                    {
                        if (Info.Ai.Session.DebugMod)
                            Log.Line($"Approach ended, no more steps - age:{Info.RelativeAge} - strages:[r:{s.RequestedStage} l:{s.LastActivatedStage}] - ec1:{def.EndCondition1} - ec1:{def.End1Value} - ec1:{def.EndCondition2} - ec1:{def.End2Value} - failure:{def.RestartCondition}");
                        s.LastActivatedStage = aConst.Approaches.Length;
                        s.RequestedStage = aConst.Approaches.Length;
                    }
                    else
                    {
                        if (Info.Ai.Session.DebugMod)
                            Log.Line($"end met no valid condition");
                    }
                }
            }
        }

        private Vector3D PlanetSurfaceHeightAdjustment(Vector3D checkPosition, Vector3D upDir, ApproachConstants approach, out Vector3D surfacePos)
        {
            var planetCenter = Info.MyPlanet.PositionComp.WorldAABB.Center;

            Vector3D waterSurfacePos = checkPosition;
            double waterSurface = 0;

            WaterData water = null;
            if (Info.Weapon.Comp.Session.WaterApiLoaded && Info.Weapon.Comp.Session.WaterMap.TryGetValue(Info.MyPlanet.EntityId, out water))
            {
                waterSurfacePos = WaterModAPI.GetClosestSurfacePoint(checkPosition, water.Planet);
                Vector3D.DistanceSquared(ref waterSurfacePos, ref planetCenter, out waterSurface);
            }

            var voxelSurfacePos = Info.MyPlanet.GetClosestSurfacePointGlobal(ref checkPosition);

            double surfaceToCenterSqr;
            Vector3D.DistanceSquared(ref voxelSurfacePos, ref planetCenter, out surfaceToCenterSqr);

            surfacePos = surfaceToCenterSqr > waterSurface ? voxelSurfacePos : waterSurfacePos;

            return surfacePos + (upDir * approach.Definition.DesiredElevation);
        }

        private static Vector3D ProNavControl(Vector3D currentDir, Vector3D velocity, Vector3D commandAccel, PreComputedMath preComp, out bool isNormalized)
        {
            Vector3D actualHeading;
            isNormalized = false;
            if (velocity.LengthSquared() < MathHelper.EPSILON10 || commandAccel.LengthSquared() < MathHelper.EPSILON10)
                actualHeading = commandAccel;
            else if (Vector3D.Dot(currentDir, Vector3D.Normalize(commandAccel)) < preComp.SteeringCos)
            {
                isNormalized = true;
                var normalVec = Vector3D.Normalize(Vector3D.Cross(Vector3D.Cross(currentDir, commandAccel), currentDir));

                if (normalVec.LengthSquared() < MathHelper.EPSILON10)
                    normalVec = Vector3D.CalculatePerpendicularVector(currentDir);

                actualHeading = preComp.SteeringSign * currentDir * preComp.SteeringParallelLen + normalVec * preComp.SteeringNormLen;
            }
            else
                actualHeading = commandAccel;

            return actualHeading;
        }

        private bool IsFocusTarget(MyEntity targetEnt)
        {
            var ai = Info.Ai;
            MyEntity fTarget;
            if (MyEntities.TryGetEntityById(ai.Construct.Data.Repo.FocusData.Target, out fTarget))
            {
                var targetBlock = targetEnt as MyCubeBlock;
                var targetGrid = targetBlock?.CubeGrid;
                var focusGrid = fTarget as MyCubeGrid;

                var validEntity = targetEnt == fTarget || targetGrid == fTarget;
                if (!validEntity && targetGrid != null && focusGrid != null)
                    validEntity = targetGrid.IsSameConstructAs(focusGrid);

                return validEntity;
            }

            return false;
        }

        private Vector3D ApproachOrbits(ref TrajectoryDef.ApproachDef def, Vector3D upDir, Vector3D orbitCenter, double accelMpsMulti, double speedCapMulti)
        {
            var tangentCoeff = accelMpsMulti / (speedCapMulti * MaxSpeed);
            var orbitPlane = new PlaneD(orbitCenter, upDir);

            var normPerp = Vector3D.CalculatePerpendicularVector(Vector3D.Normalize(orbitCenter - Position));
            var navGoal = orbitCenter + normPerp * def.OrbitRadius * (1 + tangentCoeff); //Orbit radius multiplied as a ratio of accel/speed as we're leading with a 90* phasing.  this approximates the tangent           
            var planeNavGoal = navGoal - upDir * orbitPlane.DistanceToPoint(navGoal); //constrained to plane
            return planeNavGoal;
        }

        private void SetNavTargetOffset(TrajectoryDef.ApproachDef def)
        {
            Vector3D rndDir;
            rndDir.X = (Info.Random.NextDouble() * 2) - 1;
            rndDir.Y = (Info.Random.NextDouble() * 2) - 1;
            rndDir.Z = (Info.Random.NextDouble() * 2) - 1;
            rndDir.Normalize();

            var offsetRadius = def.OffsetMaxRadius <= def.OffsetMinRadius ? def.OffsetMinRadius : Info.Random.NextDouble() * (def.OffsetMaxRadius - def.OffsetMinRadius) + def.OffsetMinRadius;
            Info.Storage.ApproachInfo.NavTargetBound = new BoundingSphereD(Vector3D.Zero + rndDir * Info.Storage.ApproachInfo.NavTargetBound.Radius, offsetRadius);
        }

        #endregion

        #region Drones
        internal void RunDrone()
        {
            var ammo = Info.AmmoDef;
            var aConst = ammo.Const;
            var s = Info.Storage;
            var newVel = new Vector3D();
            var w = Info.Weapon;
            var comp = w.Comp;
            var parentEnt = comp.TopEntity;
            
            if (aConst.TimedFragments && Info.SpawnDepth < aConst.FragMaxChildren && Info.RelativeAge >= aConst.FragStartTime && Info.RelativeAge - Info.LastFragTime > aConst.FragInterval && Info.Frags < aConst.MaxFrags)
            {
                if (!aConst.HasFragGroup || Info.Frags == 0 || Info.Frags % aConst.FragGroupSize != 0 || Info.RelativeAge - Info.LastFragTime >= aConst.FragGroupDelay)
                    TimedSpawns(aConst);
            }

            if (s.DroneInfo.DroneStat == DroneInfo.DroneStatus.Launch)
                DroneLaunch(parentEnt, aConst, s);

            if (s.DroneInfo.DroneStat != DroneInfo.DroneStatus.Launch)//Start of main nav after clear of launcher
                DroneNav(parentEnt, ref newVel);
            else
            {
                newVel = Velocity + (Direction * (aConst.DeltaVelocityPerTick * DeltaTimeRatio));
                VelocityLengthSqr = newVel.LengthSquared();

                if (VelocityLengthSqr > MaxSpeed * MaxSpeed)
                    newVel = (Direction * 0.95 + Vector3D.CalculatePerpendicularVector(Direction) * 0.05) * MaxSpeed;

                Velocity = newVel;
            }

            if (aConst.DynamicGuidance)
            {
                if (PruningProxyId != -1 && comp.Session.ActiveAntiSmarts > 0)
                {
                    var sphere = new BoundingSphereD(Position, aConst.LargestHitSize);
                    BoundingBoxD result;
                    BoundingBoxD.CreateFromSphere(ref sphere, out result);
                    var displacement = 0.1 * Velocity;
                    comp.Session.ProjectileTree.MoveProxy(PruningProxyId, ref result, displacement);
                }
            }
        }

        private void TimedSpawns(AmmoConstants aConst)
        {
            var storage = Info.Storage;
            var approachSkip = aConst.ApproachesCount > 0 && storage.RequestedStage < aConst.ApproachesCount && storage.RequestedStage >= 0 && aConst.Approaches[storage.RequestedStage].NoSpawns;
            if (!approachSkip)
            {
                if (!aConst.HasFragProximity)
                    SpawnShrapnel();
                else if (Info.Target.TargetState == Target.TargetStates.IsEntity)
                {
                    var topEnt = ((MyEntity)Info.Target.TargetObject).GetTopMostParent();
                    var inflatedSize = aConst.FragProximity + topEnt.PositionComp.LocalVolume.Radius;
                    if (Vector3D.DistanceSquared(topEnt.PositionComp.WorldAABB.Center, Position) <= inflatedSize * inflatedSize)
                        SpawnShrapnel();
                }
                else if (Info.Target.TargetObject is Projectile)
                {
                    var projectile = (Projectile)Info.Target.TargetObject;
                    var inflatedSize = aConst.FragProximity + projectile.Info.AmmoDef.Const.CollisionSize;
                    if (Vector3D.DistanceSquared(projectile.Position, Position) <= inflatedSize * inflatedSize)
                    {
                        SpawnShrapnel();
                    }
                }
            }
        }

        private void DroneLaunch(MyEntity parentEnt, AmmoConstants aConst, SmartStorage s)
        {
            if (s.DroneInfo.DroneStat == DroneInfo.DroneStatus.Launch && Info.DistanceTraveled * Info.DistanceTraveled >= aConst.SmartsDelayDistSqr && Info.Ai.AiType == Ai.AiTypes.Grid)//Check for LOS & delaytrack after launch
            {
                var lineCheck = new LineD(Position, TargetPosition);
                var startTrack = !new MyOrientedBoundingBoxD(parentEnt.PositionComp.LocalAABB, parentEnt.PositionComp.WorldMatrixRef).Intersects(ref lineCheck).HasValue;

                if (startTrack)
                    s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Transit;
            }
            else if (Info.Ai.AiType != Ai.AiTypes.Grid)
                s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Transit;
        }

        private void DroneNav(MyEntity parentEnt, ref Vector3D newVel)
        {
            var ammo = Info.AmmoDef;
            var aConst = ammo.Const;
            var s = Info.Storage;
            var w = Info.Weapon;
            var comp = w.Comp;
            var target = Info.Target;

            var tasks = comp.Data.Repo.Values.State.Tasks;
            var updateTask = tasks.UpdatedTick == Info.Ai.Session.Tick - 1;
            var tracking = aConst.DeltaVelocityPerTick <= 0 || (s.DroneInfo.DroneStat == DroneInfo.DroneStatus.Dock || Vector3D.DistanceSquared(Info.Origin, Position) >= aConst.SmartsDelayDistSqr);
            var parentPos = Vector3D.Zero;

            if (!updateTask)//Top level check for a current target or update to tasks
                UpdateExistingTargetState(parentEnt, target, aConst, s);
            else
                UpdateTask(parentEnt, target, aConst, s);

            //Hard break, everything below sorts out the best navigation action to conduct based on the drone position and target/status/mission info from above

            //General use vars
            var targetSphere = s.DroneInfo.NavTargetBound;
            var orbitSphere = targetSphere; //desired orbit dist
            var orbitSphereClose = targetSphere; //"Too close" or collision imminent

            DroneMissions(parentEnt, ref orbitSphere, ref orbitSphereClose, ref targetSphere, aConst, s, ref parentPos);

            if (w.System.WConst.DebugMode && !w.System.Session.DedicatedServer)
                DroneDebug(ref orbitSphere);

            if (tracking && s.DroneInfo.DroneMsn != DroneInfo.DroneMission.Rtb && !DroneTracking(target, s, aConst))
                return;

            if (s.DroneInfo.DroneMsn == DroneInfo.DroneMission.Rtb || tracking)
                ComputeSmartVelocity(ref orbitSphere, ref orbitSphereClose, ref targetSphere, ref parentPos, out newVel);

            UpdateSmartVelocity(newVel, tracking);
        }

        private void DroneMissions(MyEntity parentEnt, ref BoundingSphereD orbitSphere, ref BoundingSphereD orbitSphereClose, ref BoundingSphereD targetSphere, AmmoConstants aConst, SmartStorage s, ref Vector3D parentPos)
        {
            var comp = Info.Weapon.Comp;
            var ammo = Info.AmmoDef;
            var speedLimitPerTick = aConst.AmmoSkipAccel ? DesiredSpeed : aConst.AccelInMetersPerSec;
            var fragProx = aConst.FragProximity;
            var hasObstacle = s.Obstacle.Entity != parentEnt && comp.Session.Tick - 1 == s.Obstacle.LastSeenTick;
            var hasStrafe = ammo.Fragment.TimedSpawns.PointType == PointTypes.Direct && ammo.Fragment.TimedSpawns.PointAtTarget == false;
            var hasKamikaze = ammo.AreaOfDamage.ByBlockHit.Enable || (ammo.AreaOfDamage.EndOfLife.Enable && Info.RelativeAge >= ammo.AreaOfDamage.EndOfLife.MinArmingTime); //check for explosive payload on drone
            var maxLife = aConst.MaxLifeTime;
            var orbitSphereFar = orbitSphere; //Indicates start of approach

            switch (s.DroneInfo.DroneMsn)
            {
                case DroneInfo.DroneMission.Attack:

                    orbitSphere.Radius += fragProx;
                    orbitSphereFar.Radius += fragProx + speedLimitPerTick + MaxSpeed; //first whack at dynamic setting   
                    orbitSphereClose.Radius += MaxSpeed * 0.3f + ammo.Shape.Diameter; //Magic number, needs logical work?
                    if (hasObstacle && orbitSphereClose.Contains(s.Obstacle.Entity.PositionComp.GetPosition()) != ContainmentType.Contains && s.DroneInfo.DroneStat != DroneInfo.DroneStatus.Kamikaze)
                    {
                        orbitSphereClose = s.Obstacle.Entity.PositionComp.WorldVolume;
                        orbitSphereClose.Radius = s.Obstacle.Entity.PositionComp.WorldVolume.Radius + MaxSpeed * 0.3f;
                        s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Escape;
                        break;
                    }

                    if (s.DroneInfo.DroneStat != DroneInfo.DroneStatus.Transit && orbitSphereFar.Contains(Position) == ContainmentType.Disjoint)
                    {
                        s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Transit;
                        break;
                    }
                    if (s.DroneInfo.DroneStat != DroneInfo.DroneStatus.Kamikaze && s.DroneInfo.DroneStat != DroneInfo.DroneStatus.Return && s.DroneInfo.DroneStat != DroneInfo.DroneStatus.Escape)
                    {
                        if (orbitSphere.Contains(Position) != ContainmentType.Disjoint)
                        {
                            if (orbitSphereClose.Contains(Position) != ContainmentType.Disjoint)
                            {
                                s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Escape;
                            }
                            else if (s.DroneInfo.DroneStat != DroneInfo.DroneStatus.Escape)
                            {
                                switch (hasStrafe)
                                {
                                    case false:
                                        s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Orbit;
                                        break;
                                    case true:
                                        {
                                            var fragInterval = aConst.FragInterval;
                                            var fragGroupDelay = aConst.FragGroupDelay;
                                            var timeSinceLastFrag = Info.RelativeAge - Info.LastFragTime;

                                            if (fragGroupDelay == 0 && timeSinceLastFrag >= fragInterval)
                                                s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Strafe;//TODO incorporate group delays
                                            else if (fragGroupDelay > 0 && (timeSinceLastFrag >= fragGroupDelay || timeSinceLastFrag <= fragInterval))
                                                s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Strafe;
                                            else s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Orbit;
                                            break;
                                        }
                                }
                            }
                        }
                        else if (s.DroneInfo.DroneStat == DroneInfo.DroneStatus.Transit && orbitSphereFar.Contains(Position) != ContainmentType.Disjoint)
                        {
                            s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Approach;
                        }
                    }
                    else if (s.DroneInfo.DroneStat == DroneInfo.DroneStatus.Escape)
                    {
                        if (orbitSphere.Contains(Position) == ContainmentType.Disjoint)
                            s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Orbit;
                    }

                    if ((hasKamikaze) && s.DroneInfo.DroneStat != DroneInfo.DroneStatus.Kamikaze && maxLife > 0)//Parenthesis for everyone!
                    {
                        var kamiFlightTime = orbitSphere.Radius / MaxSpeed * 60 * 1.05; //time needed for final dive into target
                        if (maxLife - Info.RelativeAge <= kamiFlightTime || (Info.Frags >= aConst.MaxFrags))
                        {
                            s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Kamikaze;
                        }
                    }
                    else if (!hasKamikaze && s.DroneInfo.NavTargetEnt != parentEnt)
                    {
                        parentPos = comp.CoreEntity.PositionComp.WorldAABB.Center;
                        if (parentPos != Vector3D.Zero && s.DroneInfo.DroneStat != DroneInfo.DroneStatus.Return)
                        {
                            var rtbFlightTime = Vector3D.Distance(Position, parentPos) / MaxSpeed * 60 * 1.1d;//added multiplier to ensure final docking time?
                            if ((maxLife > 0 && maxLife - Info.RelativeAge <= rtbFlightTime) || (Info.Frags >= aConst.MaxFrags))
                            {
                                var rayTestPath = new RayD(Position, Vector3D.Normalize(parentPos - Position));//Check for clear LOS home
                                if (rayTestPath.Intersects(orbitSphereClose) == null)
                                {
                                    s.DroneInfo.DroneMsn = DroneInfo.DroneMission.Rtb;
                                    s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Transit;
                                }
                            }
                        }
                    }
                    break;
                case DroneInfo.DroneMission.Defend:
                    orbitSphere.Radius += fragProx / 2;
                    orbitSphereFar.Radius += speedLimitPerTick + MaxSpeed;
                    orbitSphereClose.Radius += MaxSpeed * 0.3f + ammo.Shape.Diameter;
                    if (hasObstacle)
                    {
                        orbitSphereClose = s.Obstacle.Entity.PositionComp.WorldVolume;
                        orbitSphereClose.Radius = s.Obstacle.Entity.PositionComp.WorldVolume.Radius + MaxSpeed * 0.3f;
                        s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Escape;
                        break;
                    }

                    if (s.DroneInfo.DroneStat == DroneInfo.DroneStatus.Escape) s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Transit;

                    if (s.DroneInfo.DroneStat != DroneInfo.DroneStatus.Transit && orbitSphereFar.Contains(Position) == ContainmentType.Disjoint)
                    {
                        s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Transit;
                        break;
                    }

                    if (s.DroneInfo.DroneStat != DroneInfo.DroneStatus.Transit)
                    {
                        if (orbitSphere.Contains(Position) != ContainmentType.Disjoint)
                        {
                            s.DroneInfo.DroneStat = orbitSphereClose.Contains(Position) != ContainmentType.Disjoint ? DroneInfo.DroneStatus.Escape : DroneInfo.DroneStatus.Orbit;
                        }
                    }
                    else if (orbitSphereFar.Contains(Position) != ContainmentType.Disjoint && (s.DroneInfo.DroneStat == DroneInfo.DroneStatus.Transit || s.DroneInfo.DroneStat == DroneInfo.DroneStatus.Orbit))
                    {
                        s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Approach;
                    }

                    parentPos = comp.CoreEntity.PositionComp.WorldAABB.Center;
                    if (parentPos != Vector3D.Zero && s.DroneInfo.DroneStat != DroneInfo.DroneStatus.Return && !hasKamikaze)//TODO kamikaze return suppressed to prevent damaging parent, until docking mechanism developed
                    {
                        var rtbFlightTime = Vector3D.Distance(Position, parentPos) / MaxSpeed * 60 * 1.05d;//added multiplier to ensure final docking time
                        if ((maxLife > 0 && maxLife - Info.RelativeAge <= rtbFlightTime) || (Info.Frags >= Info.AmmoDef.Fragment.TimedSpawns.MaxSpawns))
                        {
                            if (s.DroneInfo.NavTargetEnt != parentEnt)
                            {
                                var rayTestPath = new RayD(Position, Vector3D.Normalize(parentPos - Position));//Check for clear LOS home
                                if (rayTestPath.Intersects(orbitSphereClose) == null)
                                {
                                    s.DroneInfo.DroneMsn = DroneInfo.DroneMission.Rtb;
                                    s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Transit;
                                }
                            }
                            else//already orbiting parent, head in to dock
                            {
                                s.DroneInfo.DroneMsn = DroneInfo.DroneMission.Rtb;
                                s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Transit;
                            }
                        }
                    }

                    break;
                case DroneInfo.DroneMission.Rtb:

                    orbitSphere.Radius += MaxSpeed;
                    orbitSphereFar.Radius += MaxSpeed * 2;
                    orbitSphereClose.Radius = targetSphere.Radius;

                    if (hasObstacle && s.DroneInfo.DroneStat != DroneInfo.DroneStatus.Dock)
                    {
                        orbitSphereClose = s.Obstacle.Entity.PositionComp.WorldVolume;
                        orbitSphereClose.Radius = s.Obstacle.Entity.PositionComp.WorldVolume.Radius + MaxSpeed * 0.3f;
                        s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Escape;
                        break;
                    }

                    if (s.DroneInfo.DroneStat == DroneInfo.DroneStatus.Escape) s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Transit;

                    if (s.DroneInfo.DroneStat != DroneInfo.DroneStatus.Return && s.DroneInfo.DroneStat != DroneInfo.DroneStatus.Dock)
                    {
                        if (orbitSphere.Contains(Position) != ContainmentType.Disjoint)
                        {
                            s.DroneInfo.DroneStat = orbitSphereClose.Contains(Position) != ContainmentType.Disjoint ? DroneInfo.DroneStatus.Escape : DroneInfo.DroneStatus.Return;
                        }
                        else if (orbitSphereFar.Contains(Position) != ContainmentType.Disjoint && (s.DroneInfo.DroneStat == DroneInfo.DroneStatus.Transit || s.DroneInfo.DroneStat == DroneInfo.DroneStatus.Orbit))
                        {
                            s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Approach;
                        }
                    }
                    break;
            }

        }

        private void DroneDebug(ref BoundingSphereD orbitSphere)
        {
            var s = Info.Storage;
            if (orbitSphere.Center != Vector3D.Zero)
            {
                var debugLine = new LineD(Position, orbitSphere.Center);
                if (s.DroneInfo.DroneStat == DroneInfo.DroneStatus.Transit) DsDebugDraw.DrawLine(debugLine, Color.Blue, 0.5f);
                if (s.DroneInfo.DroneStat == DroneInfo.DroneStatus.Approach) DsDebugDraw.DrawLine(debugLine, Color.Cyan, 0.5f);
                if (s.DroneInfo.DroneStat == DroneInfo.DroneStatus.Kamikaze) DsDebugDraw.DrawLine(debugLine, Color.White, 0.5f);
                if (s.DroneInfo.DroneStat == DroneInfo.DroneStatus.Return) DsDebugDraw.DrawLine(debugLine, Color.Yellow, 0.5f);
                if (s.DroneInfo.DroneStat == DroneInfo.DroneStatus.Dock) DsDebugDraw.DrawLine(debugLine, Color.Purple, 0.5f);
                if (s.DroneInfo.DroneStat == DroneInfo.DroneStatus.Strafe) DsDebugDraw.DrawLine(debugLine, Color.Pink, 0.5f);
                if (s.DroneInfo.DroneStat == DroneInfo.DroneStatus.Escape) DsDebugDraw.DrawLine(debugLine, Color.Red, 0.5f);
                if (s.DroneInfo.DroneStat == DroneInfo.DroneStatus.Orbit) DsDebugDraw.DrawLine(debugLine, Color.Green, 0.5f);
            }

            switch (s.DroneInfo.DroneMsn)
            {
                case DroneInfo.DroneMission.Attack:
                    DsDebugDraw.DrawSphere(new BoundingSphereD(Position, 10), Color.Red);
                    break;
                case DroneInfo.DroneMission.Defend:
                    DsDebugDraw.DrawSphere(new BoundingSphereD(Position, 10), Color.Blue);
                    break;
                case DroneInfo.DroneMission.Rtb:
                    DsDebugDraw.DrawSphere(new BoundingSphereD(Position, 10), Color.Green);
                    break;
            }
        }


        private bool DroneTracking(Target target, SmartStorage s, AmmoConstants aConst)
        {
            var validEntity = target.TargetState == Target.TargetStates.IsEntity && !((MyEntity)target.TargetObject).MarkedForClose;

            var prevSlotAge = (Info.PrevRelativeAge + s.SmartSlot) % 30;
            var currentSlotAge = (Info.RelativeAge + s.SmartSlot) % 30;
            var timeSlot = prevSlotAge < 0 || prevSlotAge > currentSlotAge;

            var prevCheck = Info.PrevRelativeAge % 30;
            var currentCheck = Info.RelativeAge % 30;
            var check = prevCheck < 0 || prevCheck > currentCheck;

            var prevZombieAge = (s.PrevZombieLifeTime + s.SmartSlot) % 30;
            var currentZombieAge = (s.PrevZombieLifeTime + s.SmartSlot) % 30;
            var zombieSlot = prevZombieAge < 0 || prevZombieAge > currentZombieAge;

            var hadTarget = HadTarget != HadTargetState.None;
            var overMaxTargets = hadTarget && TargetsSeen > aConst.MaxTargets && aConst.MaxTargets != 0;
            var fake = target.TargetState == Target.TargetStates.IsFake;
            var validTarget = fake || target.TargetState == Target.TargetStates.IsProjectile || validEntity && !overMaxTargets;
            var seekFirstTarget = !hadTarget && !validTarget && s.PickTarget && (Info.RelativeAge > 120 && timeSlot || check && Info.IsFragment);
            var gaveUpChase = !fake && Info.RelativeAge - s.ChaseAge > aConst.MaxChaseTime && hadTarget;
            var isZombie = aConst.CanZombie && hadTarget && !fake && !validTarget && s.ZombieLifeTime > 0 && zombieSlot;
            var seekNewTarget = timeSlot && hadTarget && !validEntity && !overMaxTargets;
            var needsTarget = (s.PickTarget && timeSlot || seekNewTarget || gaveUpChase && validTarget || isZombie || seekFirstTarget);

            if (needsTarget && NewTarget() || validTarget)
                TrackSmartTarget(fake);
            else if (!SmartRoam())
                return false;

            return true;
        }

        private void UpdateExistingTargetState(MyEntity parentEnt, Target target, AmmoConstants aConst, SmartStorage s)
        {
            var comp = Info.Weapon.Comp;
            var fragProx = aConst.FragProximity;
            var tasks = Info.Weapon.Comp.Data.Repo.Values.State.Tasks;
            var hasTarget = false;

            switch (HadTarget)//Internal drone target reassignment
            {
                case HadTargetState.Entity:
                    var entity = target.TargetObject as MyEntity;
                    if (entity != null && !entity.MarkedForClose)
                    {
                        hasTarget = true;
                    }
                    else
                    {
                        NewTarget();
                        var myEntity = target.TargetObject as MyEntity;
                        if (myEntity != null)
                        {
                            s.DroneInfo.NavTargetEnt = myEntity.GetTopMostParent();
                            s.DroneInfo.NavTargetBound = s.DroneInfo.NavTargetEnt.PositionComp.WorldVolume;
                            hasTarget = true;
                        }
                    }
                    break;
                case HadTargetState.Projectile: //TODO evaluate whether TargetBound should remain unchanged (ie, keep orbiting assigned target but shoot at projectile)
                    if (target.TargetState == Target.TargetStates.IsProjectile)
                        NewTarget();

                    break;
                case HadTargetState.Fake:
                    if (s.DummyTargets != null)
                    {
                        var fakeTarget = s.DummyTargets.PaintedTarget.EntityId != 0 ? s.DummyTargets.PaintedTarget : s.DummyTargets.ManualTarget;
                        if (fakeTarget == s.DummyTargets.PaintedTarget)
                        {
                            MyEntities.TryGetEntityById(fakeTarget.EntityId, out s.DroneInfo.NavTargetEnt);
                            if (s.DroneInfo.NavTargetEnt.PositionComp.WorldVolume.Radius <= 0)
                            {
                                NewTarget();
                            }
                        }
                        else
                        {
                            s.DroneInfo.NavTargetBound = new BoundingSphereD(fakeTarget.FakeInfo.WorldPosition, fragProx * 0.5f);
                            hasTarget = true;
                        }
                    }
                    else
                        NewTarget();
                    break;
            }

            if (s.DroneInfo.NavTargetEnt != null && hasTarget)
                s.DroneInfo.NavTargetBound = s.DroneInfo.NavTargetEnt.PositionComp.WorldVolume;//Refresh position info
                                                                           //Logic to handle loss of target and reassigment to come home
            if (!hasTarget && s.DroneInfo.DroneMsn == DroneInfo.DroneMission.Attack)
            {
                s.DroneInfo.DroneMsn = DroneInfo.DroneMission.Defend;//Try to return to parent in defensive state
                s.DroneInfo.NavTargetBound = parentEnt.PositionComp.WorldVolume;
                s.DroneInfo.NavTargetEnt = parentEnt;
            }
            else if (s.DroneInfo.DroneMsn == DroneInfo.DroneMission.Rtb || s.DroneInfo.DroneMsn == DroneInfo.DroneMission.Defend)
            {
                if (s.DroneInfo.DroneMsn == DroneInfo.DroneMission.Rtb || tasks.FriendId == 0)
                {
                    s.DroneInfo.NavTargetBound = parentEnt.PositionComp.WorldVolume;
                    s.DroneInfo.NavTargetEnt = parentEnt;
                }
                else if (tasks.Friend != null && s.DroneInfo.DroneMsn != DroneInfo.DroneMission.Rtb && tasks.Friend != null)//If all else fails, try to protect a friendly
                {
                    s.DroneInfo.NavTargetBound = tasks.Friend.PositionComp.WorldVolume;
                    s.DroneInfo.NavTargetEnt = tasks.Friend;
                }
            }
        }

        private void UpdateTask(MyEntity parentEnt, Target target, AmmoConstants aConst, SmartStorage s)
        {
            var comp = Info.Weapon.Comp;
            var tasks = comp.Data.Repo.Values.State.Tasks;
            var fragProx = aConst.FragProximity;

            switch (tasks.Task)
            {
                case Tasks.Attack:
                    s.DroneInfo.DroneMsn = DroneInfo.DroneMission.Attack;
                    s.DroneInfo.NavTargetEnt = tasks.Enemy;
                    s.DroneInfo.NavTargetBound = s.DroneInfo.NavTargetEnt.PositionComp.WorldVolume;
                    var tTargetDist = Vector3D.Distance(Position, tasks.Enemy.PositionComp.WorldVolume.Center);
                    target.Set(tasks.Enemy, tasks.Enemy.PositionComp.WorldVolume.Center, tTargetDist, tTargetDist, tasks.EnemyId);
                    break;
                case Tasks.Defend:
                    s.DroneInfo.DroneMsn = DroneInfo.DroneMission.Defend;
                    s.DroneInfo.NavTargetEnt = tasks.Friend;
                    s.DroneInfo.NavTargetBound = s.DroneInfo.NavTargetEnt.PositionComp.WorldVolume;
                    break;
                case Tasks.Screen:
                    s.DroneInfo.DroneMsn = DroneInfo.DroneMission.Defend;
                    s.DroneInfo.NavTargetEnt = parentEnt;
                    s.DroneInfo.NavTargetBound = s.DroneInfo.NavTargetEnt.PositionComp.WorldVolume;
                    break;
                case Tasks.Recall:
                    s.DroneInfo.DroneMsn = DroneInfo.DroneMission.Rtb;
                    s.DroneInfo.NavTargetEnt = parentEnt;
                    s.DroneInfo.NavTargetBound = s.DroneInfo.NavTargetEnt.PositionComp.WorldVolume;
                    break;
                case Tasks.RoamAtPoint:
                    s.DroneInfo.DroneMsn = DroneInfo.DroneMission.Defend;
                    s.DroneInfo.NavTargetEnt = null;
                    s.DroneInfo.NavTargetBound = new BoundingSphereD(tasks.Position, fragProx * 0.5f);
                    break;
                case Tasks.None:
                    break;
            }
            s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Transit;
        }


        private void OffsetSmartVelocity(ref Vector3D commandedAccel)
        {
            var ammo = Info.AmmoDef;
            var aConst = Info.AmmoDef.Const;

            var smarts = ammo.Trajectory.Smarts;
            var s = Info.Storage;
            var speedLimitPerTick = aConst.AmmoSkipAccel ? DesiredSpeed : aConst.AccelInMetersPerSec;

            var offsetTime = smarts.OffsetTime;
            var revCmdAccel = -commandedAccel / speedLimitPerTick;
            var revOffsetDir = MyUtils.IsZero(s.RandOffsetDir.X - revCmdAccel.X, 1E-03f) && MyUtils.IsZero(s.RandOffsetDir.Y - revCmdAccel.Y, 1E-03f) && MyUtils.IsZero(Info.Storage.RandOffsetDir.Z - revCmdAccel.Z, 1E-03f);

            var prevCheck = Info.PrevRelativeAge % offsetTime;
            var currentCheck = Info.RelativeAge % offsetTime;
            var check = prevCheck < 0 || prevCheck > currentCheck;

            if (check || revOffsetDir)
            {

                double angle = Info.Random.NextDouble() * MathHelper.TwoPi;
                var up = Vector3D.CalculatePerpendicularVector(Direction);
                var right = Vector3D.Cross(Direction, up);
                s.RandOffsetDir = Math.Sin(angle) * up + Math.Cos(angle) * right;
                s.RandOffsetDir *= smarts.OffsetRatio;
            }

            commandedAccel += speedLimitPerTick * s.RandOffsetDir;
            commandedAccel = Vector3D.Normalize(commandedAccel) * speedLimitPerTick;
        }

        private void ComputeSmartVelocity(ref BoundingSphereD orbitSphere, ref BoundingSphereD orbitSphereClose, ref BoundingSphereD targetSphere, ref Vector3D parentPos, out Vector3D newVel)
        {
            var s = Info.Storage;
            var droneNavTarget = Vector3D.Zero;
            var ammo = Info.AmmoDef;
            var smarts = ammo.Trajectory.Smarts;

            var aConst = Info.AmmoDef.Const;
            var parentCubePos = Info.Weapon.Comp.CoreEntity.PositionComp.GetPosition();
            var parentCubeOrientation = Info.Weapon.Comp.CoreEntity.PositionComp.GetOrientation();
            var droneSize = Math.Max(ammo.Shape.Diameter, 5);//Minimum drone "size" clamped to 5m for nav purposes, prevents chasing tiny points in space
            var speedLimitPerTick = aConst.AmmoSkipAccel ? DesiredSpeed : aConst.AccelInMetersPerSec;

            switch (s.DroneInfo.DroneStat)
            {
                case DroneInfo.DroneStatus.Transit:
                    droneNavTarget = Vector3D.Normalize(targetSphere.Center - Position);
                    break;
                case DroneInfo.DroneStatus.Approach:
                    if (s.DroneInfo.DroneMsn == DroneInfo.DroneMission.Rtb)//Check for LOS to docking target
                    {
                        var returnTargetTest = new Vector3D(parentCubePos + parentCubeOrientation.Forward * orbitSphere.Radius);
                        var droneNavTargetAim = returnTargetTest;
                        var testPathRayCheck = new RayD(returnTargetTest, -droneNavTargetAim);//Ray looking out from dock approach point

                        if (testPathRayCheck.Intersects(orbitSphereClose) == null)
                        {
                            s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Return;
                            break;
                        }
                    }
                    //tangential tomfoolery
                    var lineToCenter = new LineD(Position, orbitSphere.Center);
                    var distToCenter = lineToCenter.Length;
                    var radius = orbitSphere.Radius * 0.99;//Multiplier to ensure drone doesn't get "stuck" on periphery
                    var centerOffset = distToCenter - Math.Sqrt((distToCenter * distToCenter) - (radius * radius));//TODO Chase down the boogey-NaN here
                    var offsetDist = Math.Sqrt((radius * radius) - (centerOffset * centerOffset));
                    var offsetPoint = new Vector3D(orbitSphere.Center + (centerOffset * -lineToCenter.Direction));
                    var angleQuat = Vector3D.CalculatePerpendicularVector(lineToCenter.Direction); //TODO placeholder for a possible rand-rotated quat.  Should be 90*, rand*, 0* 
                    var tangentPoint = new Vector3D(offsetPoint + offsetDist * angleQuat);
                    droneNavTarget = Vector3D.Normalize(tangentPoint - Position);
                    if (double.IsNaN(droneNavTarget.X) || Vector3D.IsZero(droneNavTarget)) droneNavTarget = Direction; //Error catch
                    break;

                case DroneInfo.DroneStatus.Orbit://Orbit & shoot behavior
                    var insideOrbitSphere = new BoundingSphereD(orbitSphere.Center, orbitSphere.Radius * 0.90);
                    if (insideOrbitSphere.Contains(Position) != ContainmentType.Disjoint)
                    {
                        droneNavTarget = Position + (Direction + new Vector3D(0, 0.5, 0));//Strafe or too far inside sphere recovery
                    }
                    else
                    {
                        var noseOffset = new Vector3D(Position + (Direction * (speedLimitPerTick)));
                        double length;
                        Vector3D.Distance(ref orbitSphere.Center, ref noseOffset, out length);
                        var dir = (noseOffset - orbitSphere.Center) / length;
                        var deltaDist = length - orbitSphere.Radius * 0.95; //0.95 modifier for hysterisis, keeps target inside dronesphere
                        var navPoint = noseOffset + (-dir * deltaDist);
                        droneNavTarget = Vector3D.Normalize(navPoint - Position);
                    }
                    break;

                case DroneInfo.DroneStatus.Strafe:
                    droneNavTarget = Vector3D.Normalize(TargetPosition - Position);
                    break;
                case DroneInfo.DroneStatus.Escape:
                    var metersInSideOrbit = MyUtils.GetSmallestDistanceToSphere(ref Position, ref orbitSphereClose);
                    if (metersInSideOrbit < 0)
                    {
                        var futurePos = (Position + (TravelMagnitude * Math.Abs(metersInSideOrbit * 0.5)));
                        var dirToFuturePos = Vector3D.Normalize(futurePos - orbitSphereClose.Center);
                        var futureSurfacePos = orbitSphereClose.Center + (dirToFuturePos * orbitSphereClose.Radius);
                        droneNavTarget = Vector3D.Normalize(futureSurfacePos - Position);
                    }
                    else
                    {
                        droneNavTarget = Direction;
                    }
                    break;

                case DroneInfo.DroneStatus.Kamikaze:
                    droneNavTarget = Vector3D.Normalize(TargetPosition - Position);
                    break;
                case DroneInfo.DroneStatus.Return:
                    var returnTarget = new Vector3D(parentCubePos + parentCubeOrientation.Forward * orbitSphere.Radius);
                    droneNavTarget = Vector3D.Normalize(returnTarget - Position);
                    DeaccelRate = 30;
                    if (Vector3D.Distance(Position, returnTarget) <= droneSize) s.DroneInfo.DroneStat = DroneInfo.DroneStatus.Dock;
                    break;
                case DroneInfo.DroneStatus.Dock: //This is ugly and I hate it...
                    var sphereTarget = new Vector3D(parentCubePos + parentCubeOrientation.Forward * (orbitSphereClose.Radius + MaxSpeed / 2));

                    if (Vector3D.Distance(sphereTarget, Position) >= droneSize)
                    {
                        if (DeaccelRate >= 25)//Final Approach
                        {
                            droneNavTarget = Vector3D.Normalize(sphereTarget - Position);
                            DeaccelRate = 25;
                        }

                    }
                    else if (DeaccelRate >= 25)
                    {
                        DeaccelRate = 15;
                    }

                    if (DeaccelRate <= 15)
                    {
                        if (Vector3D.Distance(parentCubePos, Position) >= droneSize)
                        {
                            droneNavTarget = Vector3D.Normalize(parentCubePos - Position);
                        }
                        else//docked TODO despawn and restock ammo?
                        {
                            Info.Age = int.MaxValue;
                            Info.RelativeAge = double.MaxValue;
                        }
                    }
                    break;
            }

            // var commandedAccel = s.Navigation.Update(Position, Velocity, speedLimitPerTick, droneNavTarget, PrevTargetVel, Gravity, smarts.Aggressiveness, Info.AmmoDef.Const.MaxLateralThrust, smarts.NavAcceleration);
            var missileToTarget = droneNavTarget;
            var relativeVelocity = PrevTargetVel - Velocity;
            var normalMissileAcceleration = (relativeVelocity - (relativeVelocity.Dot(missileToTarget) * missileToTarget)) * smarts.Aggressiveness;

            Vector3D commandedAccel;
            if (Vector3D.IsZero(normalMissileAcceleration))
            {
                commandedAccel = (missileToTarget * aConst.AccelInMetersPerSec);
            }
            else
            {
                var maxLateralThrust = aConst.AccelInMetersPerSec * Math.Min(1, Math.Max(0, Info.AmmoDef.Const.MaxLateralThrust));
                if (normalMissileAcceleration.LengthSquared() > maxLateralThrust * maxLateralThrust)
                {
                    Vector3D.Normalize(ref normalMissileAcceleration, out normalMissileAcceleration);
                    normalMissileAcceleration *= maxLateralThrust;
                }
                commandedAccel = Math.Sqrt(Math.Max(0, aConst.AccelInMetersPerSec * aConst.AccelInMetersPerSec - normalMissileAcceleration.LengthSquared())) * missileToTarget + normalMissileAcceleration;
            }
            if (smarts.OffsetTime > 0 && s.DroneInfo.DroneStat != DroneInfo.DroneStatus.Strafe && s.DroneInfo.DroneStat != DroneInfo.DroneStatus.Return && s.DroneInfo.DroneStat != DroneInfo.DroneStatus.Dock) // suppress offsets when strafing or docking
                OffsetSmartVelocity(ref commandedAccel);

            newVel = Velocity + (commandedAccel * DeltaStepConst);

            Vector3D.Normalize(ref newVel, out Direction);
        }

        private bool SmartRoam()
        {
            var smarts = Info.AmmoDef.Trajectory.Smarts;
            var hadTaret = HadTarget != HadTargetState.None;
            TargetPosition = Position + (Direction * Info.MaxTrajectory);

            Info.Storage.ZombieLifeTime += DeltaTimeRatio;
            if (Info.Storage.ZombieLifeTime > Info.AmmoDef.Const.TargetLossTime && !smarts.KeepAliveAfterTargetLoss && (smarts.NoTargetExpire || hadTaret))
            {
                DistanceToTravelSqr = Info.DistanceTraveled * Info.DistanceTraveled;
                EndState = EndStates.EarlyEnd;
            }

            return true;
        }

        private void UpdateSmartVelocity(Vector3D newVel, bool tracking)
        {
            if (!tracking)
                newVel = Velocity += (Direction * (Info.AmmoDef.Const.DeltaVelocityPerTick * DeltaTimeRatio));
            VelocityLengthSqr = newVel.LengthSquared();

            if (VelocityLengthSqr > MaxSpeed * MaxSpeed || (DeaccelRate < 100 && Info.AmmoDef.Const.IsDrone)) newVel = Direction * MaxSpeed * DeaccelRate / 100;

            Velocity = newVel;
        }

        private void TrackSmartTarget(bool fake)
        {
            var aConst = Info.AmmoDef.Const;
            if (Info.Storage.ZombieLifeTime > 0)
            {
                Info.Storage.ZombieLifeTime = 0;
                OffSetTarget();
            }

            var eTarget = Info.Target.TargetObject as MyEntity;
            var pTarget = Info.Target.TargetObject as Projectile;

            var targetPos = Vector3D.Zero;

            Ai.FakeTarget.FakeWorldTargetInfo fakeTargetInfo = null;
            MyPhysicsComponentBase physics = null;
            if (fake && Info.Storage.DummyTargets != null)
            {
                var fakeTarget = Info.Storage.DummyTargets.PaintedTarget.EntityId != 0 ? Info.Storage.DummyTargets.PaintedTarget : Info.Storage.DummyTargets.ManualTarget;
                fakeTargetInfo = fakeTarget.LastInfoTick != Info.Ai.Session.Tick ? fakeTarget.GetFakeTargetInfo(Info.Ai) : fakeTarget.FakeInfo;
                targetPos = fakeTargetInfo.WorldPosition;
                HadTarget = HadTargetState.Fake;
            }
            else if (Info.Target.TargetState == Target.TargetStates.IsProjectile && pTarget != null)
            {
                targetPos = pTarget.Position;
                HadTarget = HadTargetState.Projectile;
            }
            else if (Info.Target.TargetState == Target.TargetStates.IsEntity && eTarget != null)
            {
                targetPos = eTarget.PositionComp.WorldAABB.Center;
                HadTarget = HadTargetState.Entity;
                physics = eTarget.Physics;

            }
            else
                HadTarget = HadTargetState.Other;

            if (aConst.TargetOffSet && Info.Storage.WasTracking)
            {
                if (Info.RelativeAge - Info.Storage.LastOffsetTime > 300)
                {

                    double dist;
                    Vector3D.DistanceSquared(ref Position, ref targetPos, out dist);
                    if (dist < aConst.SmartOffsetSqr + VelocityLengthSqr && Vector3.Dot(Direction, Position - targetPos) > 0)
                        OffSetTarget();
                }
                targetPos += OffsetTarget;
            }

            TargetPosition = targetPos;

            var tVel = Vector3.Zero;
            if (fake && fakeTargetInfo != null)
            {
                tVel = fakeTargetInfo.LinearVelocity;
            }
            else if (Info.Target.TargetState == Target.TargetStates.IsProjectile && pTarget != null)
            {
                tVel = pTarget.Velocity;
            }
            else if (physics != null)
            {
                tVel = physics.LinearVelocity;
            }

            if (aConst.TargetLossDegree > 0 && Vector3D.DistanceSquared(Info.Origin, Position) >= aConst.SmartsDelayDistSqr)
                SmartTargetLoss(targetPos);

            PrevTargetVel = tVel;
        }

        private void SmartTargetLoss(Vector3D targetPos)
        {

            if (Info.Storage.WasTracking && (Info.Ai.Session.Tick20 || Vector3.Dot(Direction, Position - targetPos) > 0) || !Info.Storage.WasTracking)
            {
                var targetDir = -Direction;
                var refDir = Vector3D.Normalize(Position - targetPos);
                if (!MathFuncs.IsDotProductWithinTolerance(ref targetDir, ref refDir, Info.AmmoDef.Const.TargetLossDegree))
                {
                    if (Info.Storage.WasTracking)
                        Info.Storage.PickTarget = true;
                }
                else if (!Info.Storage.WasTracking)
                    Info.Storage.WasTracking = true;
            }
        }
        #endregion

        #region Targeting
        internal void OffSetTarget(bool roam = false)
        {
            var randAzimuth = (Info.Random.NextDouble() * 1) * 2 * Math.PI;
            var randElevation = ((Info.Random.NextDouble() * 1) * 2 - 1) * 0.5 * Math.PI;
            var offsetAmount = roam ? 100 : Info.AmmoDef.Trajectory.Smarts.Inaccuracy;
            Vector3D randomDirection;
            Vector3D.CreateFromAzimuthAndElevation(randAzimuth, randElevation, out randomDirection); // this is already normalized
            OffsetTarget = (randomDirection * offsetAmount);
            if (Info.PrevRelativeAge > -1)
            {
                Info.Storage.LastOffsetTime = (int) Info.RelativeAge;
            }
        }

        internal bool NewTarget()
        {
            var aConst = Info.AmmoDef.Const;
            var s = Info.Storage;
            var giveUp = HadTarget != HadTargetState.None && ++TargetsSeen > aConst.MaxTargets && aConst.MaxTargets != 0;
            s.ChaseAge = (int) Info.RelativeAge;
            s.PickTarget = false;
            var eTarget = Info.Target.TargetObject as MyEntity;
            var pTarget = Info.Target.TargetObject as Projectile;
            var newTarget = true;

            var oldTarget = Info.Target.TargetObject;
            if (HadTarget != HadTargetState.Projectile)
            {
                if (giveUp || !Ai.ReacquireTarget(this))
                {
                    var activeEntity = Info.Target.TargetState == Target.TargetStates.IsEntity && eTarget != null;
                    var badEntity = !Info.AcquiredEntity && activeEntity && eTarget.MarkedForClose || Info.AcquiredEntity && activeEntity && (eTarget.GetTopMostParent()?.MarkedForClose ?? true);
                    if (!giveUp && !Info.AcquiredEntity || Info.AcquiredEntity && giveUp || !Info.AmmoDef.Trajectory.Smarts.NoTargetExpire || badEntity)
                    {
                        if (Info.Target.TargetState == Target.TargetStates.IsEntity)
                            Info.Target.Reset(Info.Ai.Session.Tick, Target.States.ProjectileNewTarget);
                    }
                    newTarget = false;
                }

                if (Info.Ai.Session.AdvSyncServer && aConst.FullSync) {
                    if (Info.Target.TargetObject is MyEntity && eTarget != Info.Target.TargetObject)
                        SyncTargetServerProjectile();
                }
            }
            else
            {

                if (Info.Target.TargetState == Target.TargetStates.IsProjectile)
                    pTarget?.Seekers.Remove(this);

                if (giveUp || !Ai.ReAcquireProjectile(this))
                {
                    if (Info.Target.TargetState == Target.TargetStates.IsProjectile)
                        Info.Target.Reset(Info.Ai.Session.Tick, Target.States.ProjectileNewTarget);

                    newTarget = false;
                }
            }

            if (newTarget && aConst.Health > 0 && !aConst.IsBeamWeapon && (Info.Target.TargetState == Target.TargetStates.IsFake || Info.Target.TargetObject != null && oldTarget != Info.Target.TargetObject))
                Info.Ai.Session.Projectiles.AddProjectileTargets(this);

            return newTarget;
        }

        internal void ForceNewTarget()
        {
            Info.Storage.ChaseAge = (int) Info.RelativeAge;
            Info.Storage.PickTarget = false;
        }

        internal bool TrajectoryEstimation(WeaponDefinition.AmmoDef ammoDef, ref Vector3D shooterPos, out Vector3D targetDirection, out Vector3D estimatedPosition)
        {
            var aConst = Info.AmmoDef.Const;
            var eTarget = Info.Target.TargetObject as MyEntity;
            var pTarget = Info.Target.TargetObject as Projectile;

            if (eTarget?.GetTopMostParent()?.Physics?.LinearVelocity == null && pTarget == null)
            {
                targetDirection = Vector3D.Zero;
                estimatedPosition = Vector3D.Zero;
                return false;
            }

            var targetPos = eTarget != null ? eTarget.PositionComp.WorldAABB.Center : pTarget.Position;

            if (aConst.FragPointType == PointTypes.Direct)
            {
                targetDirection = Vector3D.Normalize(targetPos - Position);
                estimatedPosition = targetPos;
                return true;
            }

            var targetVel = eTarget != null ? eTarget.GetTopMostParent().Physics.LinearVelocity : (Vector3)pTarget.Velocity;
            var shooterVel = !Info.AmmoDef.Const.FragDropVelocity ? Velocity : Vector3D.Zero;

            var projectileMaxSpeed = ammoDef.Const.DesiredProjectileSpeed;
            Vector3D deltaPos = targetPos - shooterPos;
            Vector3D deltaVel = targetVel - shooterVel;
            Vector3D deltaPosNorm;
            if (Vector3D.IsZero(deltaPos)) deltaPosNorm = Vector3D.Zero;
            else if (Vector3D.IsUnit(ref deltaPos)) deltaPosNorm = deltaPos;
            else Vector3D.Normalize(ref deltaPos, out deltaPosNorm);

            double closingSpeed;
            Vector3D.Dot(ref deltaVel, ref deltaPosNorm, out closingSpeed);

            Vector3D closingVel = closingSpeed * deltaPosNorm;
            Vector3D lateralVel = deltaVel - closingVel;
            double projectileMaxSpeedSqr = projectileMaxSpeed * projectileMaxSpeed;
            double ttiDiff = projectileMaxSpeedSqr - lateralVel.LengthSquared();

            if (ttiDiff < 0)
            {
                targetDirection = Direction;
                estimatedPosition = targetPos;
                return aConst.FragPointType == PointTypes.Direct;
            }

            double projectileClosingSpeed = Math.Sqrt(ttiDiff) - closingSpeed;

            double closingDistance;
            Vector3D.Dot(ref deltaPos, ref deltaPosNorm, out closingDistance);

            double timeToIntercept = ttiDiff < 0 ? 0 : closingDistance / projectileClosingSpeed;

            if (timeToIntercept < 0)
            {

                if (aConst.FragPointType == PointTypes.Lead)
                {

                    estimatedPosition = targetPos + timeToIntercept * (targetVel - shooterVel);
                    targetDirection = Vector3D.Normalize(estimatedPosition - shooterPos);
                    return true;
                }

                estimatedPosition = targetPos;
                targetDirection = Direction;
                return false;
            }

            estimatedPosition = targetPos + timeToIntercept * (targetVel - shooterVel);
            targetDirection = Vector3D.Normalize(estimatedPosition - shooterPos);
            return true;
        }


        #endregion

        #region Mines
        internal void ActivateMine()
        {
            Info.Storage.RequestedStage = -2;
            EndState = EndStates.None;
            var ent = (MyEntity)Info.Target.TargetObject;

            var targetPos = ent.PositionComp.WorldAABB.Center;
            var deltaPos = targetPos - Position;
            var targetVel = ent.Physics?.LinearVelocity ?? Vector3.Zero;
            var deltaVel = targetVel - Vector3.Zero;
            var timeToIntercept = MathFuncs.Intercept(deltaPos, deltaVel, DesiredSpeed);
            var predictedPos = targetPos + (float)timeToIntercept * deltaVel;
            var ammo = Info.AmmoDef;
            var aConst = ammo.Const;
            TargetPosition = predictedPos;

            if (ammo.Trajectory.Guidance == TrajectoryDef.GuidanceType.DetectFixed) return;
            Vector3D.DistanceSquared(ref Info.Origin, ref predictedPos, out DistanceToTravelSqr);
            Info.DistanceTraveled = 0;
            Info.PrevDistanceTraveled = 0;

            Direction = Vector3D.Normalize(predictedPos - Position);
            VelocityLengthSqr = 0;

            if (aConst.AmmoSkipAccel)
            {
                Velocity = (Direction * MaxSpeed);
                VelocityLengthSqr = MaxSpeed * MaxSpeed;
            }
            else Velocity += Direction * (aConst.DeltaVelocityPerTick * DeltaTimeRatio);

            if (ammo.Trajectory.Guidance == TrajectoryDef.GuidanceType.DetectSmart)
            {
                if (aConst.TargetOffSet)
                {
                    OffSetTarget();
                }
                else
                {
                    OffsetTarget = Vector3D.Zero;
                }
            }

            TravelMagnitude = Velocity * DeltaStepConst;
        }


        internal void SeekEnemy()
        {
            var mineInfo = Info.AmmoDef.Trajectory.Mines;
            var detectRadius = mineInfo.DetectRadius;
            var deCloakRadius = mineInfo.DeCloakRadius;

            var targetEnt = Info.Target.TargetObject as MyEntity;

            var wakeRadius = detectRadius > deCloakRadius ? detectRadius : deCloakRadius;
            PruneSphere = new BoundingSphereD(Position, wakeRadius);
            var inRange = false;
            var activate = false;
            var minDist = double.MaxValue;
            if (Info.Storage.RequestedStage != -2)
            {
                MyEntity closestEnt = null;
                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref PruneSphere, MyEntityList, MyEntityQueryType.Dynamic);
                for (int i = 0; i < MyEntityList.Count; i++)
                {
                    var ent = MyEntityList[i];
                    var grid = ent as MyCubeGrid;
                    var character = ent as IMyCharacter;
                    if (grid == null && character == null || ent.MarkedForClose || !ent.InScene) continue;
                    MyDetectedEntityInfo entInfo;

                    if (!Info.Ai.CreateEntInfo(ent, Info.Ai.AiOwner, out entInfo)) continue;
                    switch (entInfo.Relationship)
                    {
                        case MyRelationsBetweenPlayerAndBlock.Owner:
                            continue;
                        case MyRelationsBetweenPlayerAndBlock.FactionShare:
                            continue;
                    }
                    var entSphere = ent.PositionComp.WorldVolume;
                    entSphere.Radius += Info.AmmoDef.Const.CollisionSize;
                    var dist = MyUtils.GetSmallestDistanceToSphereAlwaysPositive(ref Position, ref entSphere);
                    if (dist >= minDist) continue;
                    minDist = dist;
                    closestEnt = ent;
                }
                MyEntityList.Clear();

                if (closestEnt != null)
                {
                    ForceNewTarget();
                    Info.Target.TargetObject = closestEnt;
                }
            }
            else if (Info.Target.TargetState == Target.TargetStates.IsEntity && targetEnt != null && !targetEnt.MarkedForClose)
            {
                var entSphere = targetEnt.PositionComp.WorldVolume;
                entSphere.Radius += Info.AmmoDef.Const.CollisionSize;
                minDist = MyUtils.GetSmallestDistanceToSphereAlwaysPositive(ref Position, ref entSphere);
            }
            else
                TriggerMine(true);

            if (EnableAv)
            {
                if (Info.AvShot.Cloaked && minDist <= deCloakRadius) Info.AvShot.Cloaked = false;
                else if (Info.AvShot.AmmoDef.Trajectory.Mines.Cloak && !Info.AvShot.Cloaked && minDist > deCloakRadius) Info.AvShot.Cloaked = true;
            }

            if (minDist <= Info.AmmoDef.Const.CollisionSize) 
                activate = true;
            if (minDist <= detectRadius && Info.Target.TargetObject is MyEntity) 
                inRange = true;

            if (Info.Storage.RequestedStage == -2)
            {
                if (!inRange)
                    TriggerMine(true);
            }
            else if (inRange) ActivateMine();

            if (activate)
            {
                TriggerMine(false);
                if (targetEnt != null) 
                    MyEntityList.Add(targetEnt);
            }
        }
        internal void TriggerMine(bool startTimer)
        {
            DistanceToTravelSqr = double.MinValue;
            if (Info.AmmoDef.Const.Ewar)
            {
                Info.AvShot.Triggered = true;
            }

            if (startTimer) DeaccelRate = Info.AmmoDef.Trajectory.Mines.FieldTime;
            Info.Storage.RequestedStage = -3; // stage1, Guidance == DetectSmart and DistanceToTravelSqr != double.MaxValue means smart tracking is active.
        }

        internal void ResetMine()
        {
            if (Info.Storage.RequestedStage == -3)
            {
                Info.DistanceTraveled = double.MaxValue;
                DeaccelRate = 0;
                return;
            }

            DeaccelRate = Info.AmmoDef.Const.Ewar || Info.AmmoDef.Const.IsMine ? Info.AmmoDef.Trajectory.DeaccelTime : 0;
            DistanceToTravelSqr = Info.MaxTrajectory * Info.MaxTrajectory;

            Info.AvShot.Triggered = false;
            Info.Storage.LastActivatedStage = Info.Storage.RequestedStage;
            Info.Storage.RequestedStage = -1;
            TargetPosition = Vector3D.Zero;
            if (Info.AmmoDef.Trajectory.Guidance == TrajectoryDef.GuidanceType.DetectSmart)
            {
                OffsetTarget = Vector3D.Zero;
            }

            Direction = Vector3D.Zero;

            Velocity = Vector3D.Zero;
            TravelMagnitude = Vector3D.Zero;
            VelocityLengthSqr = 0;
        }

        #endregion

        #region Ewar
        internal void RunEwar()
        {
            if (Info.AmmoDef.Const.Pulse && !Info.EwarAreaPulse && (VelocityLengthSqr <= 0 || EndState == EndStates.AtMaxRange) && !Info.AmmoDef.Const.IsMine)
            {
                Info.EwarAreaPulse = true;
                PrevVelocity = Velocity;
                Velocity = Vector3D.Zero;
                DistanceToTravelSqr = Info.DistanceTraveled * Info.DistanceTraveled;
            }

            if (Info.EwarAreaPulse)
            {
                var maxSteps = Info.AmmoDef.Const.PulseGrowTime;
                if (Info.TriggerGrowthSteps++ < maxSteps)
                {
                    var areaSize = Info.AmmoDef.Const.EwarRadius;
                    var expansionPerTick = areaSize / maxSteps;
                    var nextSize = Info.TriggerGrowthSteps * expansionPerTick;
                    if (nextSize <= areaSize)
                    {
                        var nextRound = nextSize + 1;
                        if (nextRound > areaSize)
                        {
                            if (nextSize < areaSize)
                            {
                                nextSize = areaSize;
                                ++Info.TriggerGrowthSteps;
                            }
                        }
                        Info.TriggerMatrix = MatrixD.Identity;
                        Info.TriggerMatrix.Translation = Position;
                        MatrixD.Rescale(ref Info.TriggerMatrix, nextSize);
                        if (EnableAv)
                        {
                            Info.AvShot.Triggered = true;
                            Info.AvShot.TriggerMatrix = Info.TriggerMatrix;
                        }
                    }
                }
            }

            var prevCheck = Info.PrevRelativeAge % Info.AmmoDef.Const.PulseInterval;
            var currentCheck = Info.RelativeAge % Info.AmmoDef.Const.PulseInterval;
            var check = prevCheck < 0 || prevCheck > currentCheck;

            if (!Info.AmmoDef.Const.Pulse || Info.AmmoDef.Const.Pulse && check)
                EwarEffects();
            else Info.EwarActive = false;
        }

        internal void EwarEffects()
        {
            switch (Info.AmmoDef.Const.EwarType)
            {
                case AntiSmart:
                    var eWarSphere = new BoundingSphereD(Position, Info.AmmoDef.Const.EwarRadius);

                    var s = Info.Ai.Session;
                    DynTrees.GetAllProjectilesInSphere(Info.Ai.Session, ref eWarSphere, s.EwaredProjectiles, false);
                    for (int j = 0; j < s.EwaredProjectiles.Count; j++)
                    {
                        var netted = s.EwaredProjectiles[j];

                        if (eWarSphere.Intersects(new BoundingSphereD(netted.Position, netted.Info.AmmoDef.Const.CollisionSize)))
                        {
                            if (netted.Info.Ai.TopEntityMap.GroupMap.Construct.ContainsKey(Info.Weapon.Comp.TopEntity) || netted.Info.Target.TargetState == Target.TargetStates.IsProjectile) continue;
                            if (Info.Random.NextDouble() * 100f < Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                            {
                                Info.BaseEwarPool -= (float)netted.Info.AmmoDef.Const.HealthHitModifier;
                                if (Info.BaseEwarPool <= 0 && Info.BaseHealthPool-- > 0)
                                {
                                    Info.EwarActive = true;
                                    netted.Info.Target.TargetObject = this;
                                    netted.Info.Target.TargetState = Target.TargetStates.IsProjectile;
                                    Seekers.Add(netted);
                                }
                            }
                        }
                    }
                    s.EwaredProjectiles.Clear();
                    return;
                case Push:
                    if (Info.EwarAreaPulse && Info.Random.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case Pull:
                    if (Info.EwarAreaPulse && Info.Random.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case Tractor:
                    if (Info.EwarAreaPulse && Info.Random.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case JumpNull:
                    if (Info.EwarAreaPulse && Info.Random.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case Anchor:
                    if (Info.EwarAreaPulse && Info.Random.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case EnergySink:
                    if (Info.EwarAreaPulse && Info.Random.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case Emp:
                    if (Info.EwarAreaPulse && Info.Random.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case Offense:
                    if (Info.EwarAreaPulse && Info.Random.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                        Info.EwarActive = true;
                    break;
                case Nav:
                    if (!Info.AmmoDef.Const.Pulse || Info.EwarAreaPulse && Info.Random.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance)
                        Info.EwarActive = true;
                    break;
                case Dot:
                    if (Info.EwarAreaPulse && Info.Random.NextDouble() * 100f <= Info.AmmoDef.Const.PulseChance || !Info.AmmoDef.Const.Pulse)
                    {
                        Info.EwarActive = true;
                    }
                    break;
            }
        }
        #endregion

        #region Misc
        internal void SpawnShrapnel(bool timedSpawn = true) // inception begins
        {
            var ammoDef = Info.AmmoDef;
            var aConst = ammoDef.Const;
            var skipPatttern = !timedSpawn && aConst.TimedFragments && (aConst.ArmWhenShot && Info.ObjectsHit > 0 || aConst.FragOnEnd);
            var patternIndex = !skipPatttern ? aConst.FragPatternCount : 1;

            var pattern = ammoDef.Pattern;
            if (aConst.FragmentPattern && !skipPatttern)
            {
                if (pattern.Random)
                {
                    if (pattern.TriggerChance >= 1 || pattern.TriggerChance >= Info.Random.NextDouble())
                        patternIndex = Info.Random.Range(pattern.RandomMin, pattern.RandomMax);

                    for (int w = 0; w < aConst.FragPatternCount; w++)
                    {

                        var y = Info.Random.Range(0, w + 1);
                        Info.PatternShuffle[w] = Info.PatternShuffle[y];
                        Info.PatternShuffle[y] = w;
                    }
                }
                else if (pattern.PatternSteps > 0 && pattern.PatternSteps <= aConst.FragPatternCount)
                {
                    patternIndex = pattern.PatternSteps;
                    for (int p = 0; p < aConst.FragPatternCount; ++p)
                    {   
                        Info.PatternShuffle[p] = (Info.PatternShuffle[p] + patternIndex) % aConst.FragPatternCount;
                    }
                }
            }

            var fireOnTarget = timedSpawn && aConst.HasFragProximity && aConst.FragPointAtTarget;

            Vector3D newOrigin;
            if (!aConst.HasFragmentOffset)
                newOrigin = !Vector3D.IsZero(Info.Hit.LastHit) ? Info.Hit.LastHit : Position;
            else
            {
                var pos = !Vector3D.IsZero(Info.Hit.LastHit) ? Info.Hit.LastHit : Position;
                var offSet = (Direction * aConst.FragmentOffset);
                newOrigin = aConst.HasNegFragmentOffset ? pos - offSet : pos + offSet;
            }

            var spawn = false;
            for (int i = 0; i < patternIndex; i++)
            {
                var fragAmmoDef = aConst.FragmentPattern && !skipPatttern ? aConst.AmmoPattern[Info.PatternShuffle[i] > 0 ? Info.PatternShuffle[i] - 1 : aConst.FragPatternCount - 1] : Info.Weapon.System.AmmoTypes[aConst.FragmentId].AmmoDef;
                Vector3D pointDir;
                if (!fireOnTarget)
                {
                    pointDir = Direction;
                    if (aConst.UseAimCone)
                    {
                        var eTarget = Info.Target.TargetObject as MyEntity;
                        var pTarget = Info.Target.TargetObject as Projectile;

                        var radius = eTarget != null ? eTarget.PositionComp.LocalVolume.Radius : pTarget != null ? pTarget.Info.AmmoDef.Const.CollisionSize : 1;
                        var targetSphere = new BoundingSphereD(TargetPosition, radius);

                        MathFuncs.Cone aimCone;
                        aimCone.ConeDir = Direction;
                        aimCone.ConeTip = Position;
                        aimCone.ConeAngle = aConst.DirectAimCone;
                        if (!MathFuncs.TargetSphereInCone(ref targetSphere, ref aimCone)) break;
                    }
                }
                else
                {
                    if (aConst.UseAimCone)
                    {
                        var eTarget = Info.Target.TargetObject as MyEntity;
                        var pTarget = Info.Target.TargetObject as Projectile;

                        var radius = eTarget != null ? eTarget.PositionComp.LocalVolume.Radius : pTarget != null ? pTarget.Info.AmmoDef.Const.CollisionSize : 1;
                        var targetSphere = new BoundingSphereD(TargetPosition, radius);

                        MathFuncs.Cone aimCone;
                        aimCone.ConeDir = Direction;
                        aimCone.ConeTip = Position;
                        aimCone.ConeAngle = aConst.DirectAimCone;
                        if (!MathFuncs.TargetSphereInCone(ref targetSphere, ref aimCone)) break;
                    }

                    Vector3D estimatedTargetPos;
                    if (!TrajectoryEstimation(fragAmmoDef, ref newOrigin, out pointDir, out estimatedTargetPos))
                        continue;
                }


                spawn = true;

                if (fragAmmoDef.Const.HasAdvFragOffset)
                {
                    MatrixD matrix;
                    MatrixD.CreateWorld(ref Position, ref Direction, ref Info.OriginUp, out matrix);

                    Vector3D advOffSet;
                    var offSet = fragAmmoDef.Const.FragOffset;
                    Vector3D.Rotate(ref offSet, ref matrix, out advOffSet);
                    newOrigin += advOffSet;
                }

                var projectiles = Info.Ai.Session.Projectiles;
                var shrapnel = projectiles.ShrapnelPool.Count > 0 ? projectiles.ShrapnelPool.Pop() : new Fragments();
                shrapnel.Init(this, projectiles.FragmentPool, fragAmmoDef, ref newOrigin, ref pointDir);
                projectiles.ShrapnelToSpawn.Add(shrapnel);
            }

            if (!spawn)
                return;

            ++Info.SpawnDepth;

            if (timedSpawn && ++Info.Frags == aConst.MaxFrags && aConst.FragParentDies)
                DistanceToTravelSqr = Info.DistanceTraveled * Info.DistanceTraveled;
            Info.LastFragTime = (int) Info.RelativeAge;
        }


        internal void CheckForNearVoxel(uint steps)
        {
            var possiblePos = BoundingBoxD.CreateFromSphere(new BoundingSphereD(Position, ((MaxSpeed) * (steps + 1) * DeltaStepConst) + Info.AmmoDef.Const.CollisionSize));
            if (MyGamePruningStructure.AnyVoxelMapInBox(ref possiblePos))
            {
                PruneQuery = MyEntityQueryType.Both;
            }
        }

        internal void SyncPosServerProjectile(ProtoProPosition.ProSyncState state)
        {
            var session = Info.Ai.Session;
            var proSync = session.ProtoWeaponProSyncPosPool.Count > 0 ? session.ProtoWeaponProSyncPosPool.Pop() : new ProtoProPosition();
            proSync.Position = Position;
            proSync.State = state;
            proSync.Velocity = Velocity;
            proSync.ProId = Info.SyncId;
            Info.Weapon.ProPositionSync.Collection.Add(proSync);
            session.GlobalProPosSyncs[Info.Weapon.PartState.Id] = Info.Weapon.ProPositionSync;
        }

        internal void SyncTargetServerProjectile()
        {
            var session = Info.Ai.Session;
            var proSync = session.ProtoWeaponProSyncTargetPool.Count > 0 ? session.ProtoWeaponProSyncTargetPool.Pop() : new ProtoProTarget();
            proSync.ProId = Info.SyncId;
            var targetId = ((MyEntity) Info.Target.TargetObject).EntityId;
            proSync.EntityId = targetId;
            Info.Weapon.ProTargetSync.Collection.Add(proSync);
            session.GlobalProTargetSyncs[Info.Weapon.PartState.Id] = Info.Weapon.ProTargetSync;
        }

        internal void SyncClientProjectile(int posSlot)
        {
            var s = Info.Ai.Session;
            var w = Info.Weapon;
            ClientProSync sync;
            if (w.WeaponProSyncs.TryGetValue(Info.SyncId, out sync))
            {
                if (RelativeTime - sync.UpdateTick > 30)
                {
                    w.WeaponProSyncs.Remove(Info.SyncId);
                    return;
                }

                if (RelativeTime - sync.UpdateTick <= 1 && sync.CurrentOwl < 30)
                {
                    var proPosSync = sync.ProPosition;

                    if (proPosSync.State == ProtoProPosition.ProSyncState.Dead)
                    {
                        State = ProjectileState.Destroy;
                        w.WeaponProSyncs.Remove(Info.SyncId);
                        return;
                    }

                    var oldPos = Position;
                    var oldVels = Velocity;

                    var checkSlot = (int)Math.Round(posSlot - sync.CurrentOwl >= 0 ? posSlot - sync.CurrentOwl : (posSlot - sync.CurrentOwl) + 30);

                    var estimatedStepSize = sync.CurrentOwl * DeltaStepConst;

                    var estimatedDistTraveledToPresent = proPosSync.Velocity * (float) estimatedStepSize;
                    var clampedEstimatedDistTraveledSqr = Math.Max(estimatedDistTraveledToPresent.LengthSquared(), 25);
                    var pastServerProPos = proPosSync.Position;
                    var futurePosition = pastServerProPos + estimatedDistTraveledToPresent;

                    var pastClientProPos = Info.Storage.FullSyncInfo.PastProInfos[checkSlot];
                    if (Vector3D.DistanceSquared(pastClientProPos, pastServerProPos) > clampedEstimatedDistTraveledSqr)
                    {
                        if (++Info.Storage.FullSyncInfo.ProSyncPosMissCount > 1)
                        {
                            Info.Storage.FullSyncInfo.ProSyncPosMissCount = 0;
                            Position = futurePosition;
                            Velocity = proPosSync.Velocity;
                            Vector3D.Normalize(ref Velocity, out Direction);
                        }
                    }
                    else
                        Info.Storage.FullSyncInfo.ProSyncPosMissCount = 0;

                    if (w.System.WConst.DebugMode)
                    {
                        List<ClientProSyncDebugLine> lines;
                        if (!w.System.Session.ProSyncLineDebug.TryGetValue(Info.SyncId, out lines))
                        {
                            lines = new List<ClientProSyncDebugLine>();
                            w.System.Session.ProSyncLineDebug[Info.SyncId] = lines;
                        }

                        var pastServerLine = lines.Count == 0 ? new LineD(pastServerProPos - (proPosSync.Velocity * (float) DeltaStepConst), pastServerProPos) : new LineD(lines[lines.Count - 1].Line.To, pastServerProPos);

                        lines.Add(new ClientProSyncDebugLine { CreateTick = s.Tick, Line = pastServerLine, Color = Color.Red});

                        //Log.Line($"ProSyn: Id:{Info.Id} - age:{Info.Age} - owl:{sync.CurrentOwl} - jumpDist:{Vector3D.Distance(oldPos, Position)}[{Vector3D.Distance(oldVels, Velocity)}] - nVel:{oldVels.Length()} - oVel:{proPosSync.Velocity.Length()})");
                    }
                }
                w.WeaponProSyncs.Remove(Info.SyncId);
            }
        }
        #endregion
    }
}