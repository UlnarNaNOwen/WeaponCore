﻿using System;
using CoreSystems.Support;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using static CoreSystems.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;
using static CoreSystems.Support.CoreComponent;

namespace CoreSystems.Platform
{
    public partial class Weapon 
    {
        public void AimBarrel()
        {
            LastTrackedTick = Comp.Session.Tick;
            IsHome = false;

            if (HasHardPointSound && PlayTurretAv && !PlayingHardPointSound)
                StartHardPointSound();

            if (AiOnlyWeapon) {

                if (AzimuthTick == Comp.Session.Tick && System.TurretMovement == WeaponSystem.TurretType.Full || System.TurretMovement == WeaponSystem.TurretType.AzimuthOnly) {
                    Matrix azRotMatrix;
                    Matrix.CreateFromAxisAngle(ref AzimuthPart.RotationAxis, (float)Azimuth, out azRotMatrix);
                    var localMatrix = AzimuthPart.OriginalPosition * azRotMatrix;
                    localMatrix.Translation = AzimuthPart.Entity.PositionComp.LocalMatrixRef.Translation;
                    AzimuthPart.Entity.PositionComp.SetLocalMatrix(ref localMatrix);
                }

                if (ElevationTick == Comp.Session.Tick && (System.TurretMovement == WeaponSystem.TurretType.Full || System.TurretMovement == WeaponSystem.TurretType.ElevationOnly)) {
                    Matrix elRotMatrix;
                    Matrix.CreateFromAxisAngle(ref ElevationPart.RotationAxis, -(float)Elevation, out elRotMatrix);
                    var localMatrix = ElevationPart.OriginalPosition * elRotMatrix;
                    localMatrix.Translation = ElevationPart.Entity.PositionComp.LocalMatrixRef.Translation;
                    ElevationPart.Entity.PositionComp.SetLocalMatrix(ref localMatrix);
                }
            }
            else {
                if (ElevationTick == Comp.Session.Tick)
                {
                    Comp.VanillaTurretBase.Elevation = (float)Elevation;
                }

                if (AzimuthTick == Comp.Session.Tick)
                {
                    Comp.VanillaTurretBase.Azimuth = (float)Azimuth;
                }
            }
        }

        public void ScheduleWeaponHome(bool sendNow = false)
        {
            if (ReturingHome)
                return;

            ReturingHome = true;
            if (sendNow)
                SendTurretHome();
            else 
                System.Session.FutureEvents.Schedule(SendTurretHome, null, 300u);
        }

        public void SendTurretHome(object o = null)
        {
            System.Session.HomingWeapons.Add(this);
        }

        public void TurretHomePosition()
        {
            using (Comp.CoreEntity.Pin()) {

                if (Comp.CoreEntity.MarkedForClose || Comp.Platform.State != CorePlatform.PlatformState.Ready) return;

                if (PartState.Action != TriggerActions.TriggerOff || Comp.UserControlled || Target.HasTarget || !ReturingHome) {
                    ReturingHome = false;
                    return;
                }

                if (Comp.TypeSpecific == CompTypeSpecific.VanillaTurret && Comp.VanillaTurretBase != null) {
                    Azimuth = Comp.VanillaTurretBase.Azimuth;
                    Elevation = Comp.VanillaTurretBase.Elevation;
                }

                var azStep = System.AzStep;
                var elStep = System.ElStep;

                var oldAz = Azimuth;
                var oldEl = Elevation;

                var homeEl = System.HomeElevation;
                var homeAz = System.HomeAzimuth;

                if (oldAz > homeAz)
                    Azimuth = oldAz - azStep > homeAz ? oldAz - azStep : homeAz;
                else if (oldAz < homeAz)
                    Azimuth = oldAz + azStep < homeAz ? oldAz + azStep : homeAz;

                if (oldEl > homeEl)
                    Elevation = oldEl - elStep > homeEl ? oldEl - elStep : homeEl;
                else if (oldEl < homeEl)
                    Elevation = oldEl + elStep < homeEl ? oldEl + elStep : homeEl;

                if (!MyUtils.IsEqual((float)oldAz, (float)Azimuth))
                    AzimuthTick = Comp.Session.Tick;

                if (!MyUtils.IsEqual((float)oldEl, (float)Elevation))
                    ElevationTick = Comp.Session.Tick;

                AimBarrel();

                if (Azimuth > homeAz || Azimuth < homeAz || Elevation > homeEl || Elevation < homeEl)
                    IsHome = false;
                else {
                    IsHome = true;
                    ReturingHome = false;

                    if (HasHardPointSound && PlayingHardPointSound)
                        StopHardPointSound();
                }
            }
        }
        
        internal void UpdatePivotPos()
        {
            if (Comp.Session == null || PosChangedTick == Comp.Session.Tick || Comp.CoreEntity == null || Comp.IsBlock && (AzimuthPart?.Entity?.Parent == null || ElevationPart?.Entity?.Parent == null)  || ElevationPart?.Entity == null || MuzzlePart?.Entity == null || Comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            PosChangedTick = Comp.Session.Tick;

            var justBlock = AzimuthPart.IsBlock && ElevationPart.IsBlock && MuzzlePart.IsBlock;
            MatrixD azimuthMatrix;
            MatrixD elevationMatrix;
            Vector3D weaponCenter;
            MatrixD azParentMatrix;

            if (!justBlock)
            {
                if (!AzimuthPart.IsBlock) {
                    var aLocalMatrix = AzimuthPart.Entity.PositionComp.LocalMatrixRef;
                    azParentMatrix = AzimuthPart.Entity.Parent.PositionComp.WorldMatrixRef;
                    MatrixD.Multiply(ref aLocalMatrix, ref azParentMatrix, out azimuthMatrix);
                }
                else
                {
                    azimuthMatrix = Comp.CoreEntity.PositionComp.WorldMatrixRef;
                    azParentMatrix = azimuthMatrix;
                }

                if (ElevationPart.Entity == AzimuthPart.Entity)
                    elevationMatrix = azimuthMatrix;
                else if (!ElevationPart.IsBlock)
                {
                    var eLocalMatrix = ElevationPart.Entity.PositionComp.LocalMatrixRef;
                    var eParent = ElevationPart.Entity.Parent.WorldMatrix;
                    MatrixD.Multiply(ref eLocalMatrix, ref eParent, out elevationMatrix);
                }
                else 
                    elevationMatrix = Comp.CoreEntity.PositionComp.WorldMatrixRef;

                if (MuzzlePart.Entity == AzimuthPart.Entity)
                {
                    var localCenter = MuzzlePart.Entity.PositionComp.LocalAABB.Center;
                    Vector3D.Transform(ref localCenter, ref azimuthMatrix, out weaponCenter);
                }
                else if (MuzzlePart.Entity == ElevationPart.Entity)
                {
                    var localCenter = MuzzlePart.Entity.PositionComp.LocalAABB.Center;
                    Vector3D.Transform(ref localCenter, ref elevationMatrix, out weaponCenter);
                }
                else if (!MuzzlePart.IsBlock)
                {
                    var mLocalMatrix = MuzzlePart.Entity.PositionComp.LocalMatrixRef;
                    var mParent = MuzzlePart.Entity.Parent.WorldMatrix;
                    MatrixD muzzleMatrix;
                    MatrixD.Multiply(ref mLocalMatrix, ref mParent, out muzzleMatrix);

                    var localCenter = MuzzlePart.Entity.PositionComp.LocalAABB.Center;
                    Vector3D.Transform(ref localCenter, ref muzzleMatrix, out weaponCenter);
                }
                else
                    weaponCenter = Comp.CoreEntity.PositionComp.WorldAABB.Center;
            }
            else
            {
                azimuthMatrix = Comp.CoreEntity.PositionComp.WorldMatrixRef;
                azParentMatrix = azimuthMatrix;
                elevationMatrix = azimuthMatrix;
                weaponCenter = Comp.CoreEntity.PositionComp.WorldAABB.Center;
            }

            BarrelOrigin = weaponCenter;
            var centerTestPos = azimuthMatrix.Translation;
            MyPivotUp = azimuthMatrix.Up;
            MyPivotFwd = elevationMatrix.Forward;

            if (System.TurretMovement == WeaponSystem.TurretType.ElevationOnly)
            {
                Vector3D forward;
                var eLeft = elevationMatrix.Left;
                Vector3D.Cross(ref eLeft, ref MyPivotUp, out forward);
                WeaponConstMatrix = new MatrixD { Forward = forward, Up = MyPivotUp, Left = elevationMatrix.Left };
            }
            else
            {
                var forward = !AlternateForward ? azParentMatrix.Forward : Vector3D.TransformNormal(AzimuthInitFwdDir, ref azParentMatrix);

                Vector3D left;
                Vector3D.Cross(ref MyPivotUp, ref forward, out left);

                WeaponConstMatrix = new MatrixD { Forward = forward, Up = MyPivotUp, Left = left };
            }

            Vector3D pivotLeft;
            Vector3D.Cross(ref MyPivotUp ,ref MyPivotFwd, out pivotLeft);
            if (Vector3D.IsZero(pivotLeft))
                MyPivotPos = centerTestPos;
            else
            {
                Vector3D barrelUp;
                Vector3D.Cross(ref MyPivotFwd, ref pivotLeft, out barrelUp);
                
                var elToMuzzleOrigin = weaponCenter - elevationMatrix.Translation;
                var offset = Vector3D.ProjectOnVector(ref elToMuzzleOrigin, ref barrelUp);

                MyPivotPos = elevationMatrix.Translation + offset;
            }
            if (!Vector3D.IsZero(AimOffset))
            {
                var pivotRotMatrix = new MatrixD { Forward = MyPivotFwd, Left = elevationMatrix.Left, Up = elevationMatrix.Up };
                Vector3D offSet;
                Vector3D.Rotate(ref AimOffset, ref pivotRotMatrix, out offSet);

                MyPivotPos += offSet;
            }

            if (!Comp.Debug) return;

            MyCenterTestLine = new LineD(centerTestPos, centerTestPos + (MyPivotUp * 20));
            MyPivotTestLine = new LineD(MyPivotPos, MyPivotPos - (WeaponConstMatrix.Left * 10));
            MyBarrelTestLine = new LineD(weaponCenter, weaponCenter + (MyPivotFwd * 16));
            MyAimTestLine = new LineD(MyPivotPos, MyPivotPos + (MyPivotFwd * 20));
            AzimuthFwdLine = new LineD(weaponCenter, weaponCenter + (WeaponConstMatrix.Forward * 19));
            if (Target.HasTarget)
                MyShootAlignmentLine = new LineD(MyPivotPos, Target.TargetPos);
        }

        internal void UpdateWeaponHeat(object o = null)
        {
            try
            {
                Comp.CurrentHeat = Comp.CurrentHeat >= HsRate ? Comp.CurrentHeat - HsRate : 0;
                PartState.Heat = PartState.Heat >= HsRate ? PartState.Heat - HsRate : 0;

                var set = PartState.Heat - LastHeat > 0.001 || PartState.Heat - LastHeat < 0.001;

                LastHeatUpdateTick = Comp.Session.Tick;

                if (!Comp.Session.DedicatedServer)
                {
                    var heatOffset = HeatPerc = PartState.Heat / System.MaxHeat;

                    if (set && heatOffset > .33)
                    {
                        if (heatOffset > 1) heatOffset = 1;

                        heatOffset -= .33f;

                        var intensity = .7f * heatOffset;

                        var color = Comp.Session.HeatEmissives[(int)(heatOffset * 100)];

                        for(int i = 0; i < HeatingParts.Count; i++)
                            HeatingParts[i]?.SetEmissiveParts("Heating", color, intensity);
                    }
                    else if (set)
                        for(int i = 0; i < HeatingParts.Count; i++)
                            HeatingParts[i]?.SetEmissiveParts("Heating", Color.Transparent, 0);

                    LastHeat = PartState.Heat;
                }

                if (set && System.DegRof && PartState.Heat >= (System.MaxHeat * .8))
                {
                    CurrentlyDegrading = true;
                    UpdateRof();
                }
                else if (set && CurrentlyDegrading)
                {
                    if (PartState.Heat <= (System.MaxHeat * .4)) 
                        CurrentlyDegrading = false;

                    UpdateRof();
                }

                if (PartState.Overheated && PartState.Heat <= (System.MaxHeat * System.WepCoolDown))
                {
                    EventTriggerStateChanged(EventTriggers.Overheated, false);
                    if (System.Session.IsServer)
                    {
                        PartState.Overheated = false;
                        if (System.Session.MpActive)
                            System.Session.SendState(Comp);
                    }

                }

                if (PartState.Heat > 0)
                    Comp.Session.FutureEvents.Schedule(UpdateWeaponHeat, null, 20);
                else
                {
                    HeatLoopRunning = false;
                    LastHeatUpdateTick = 0;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateWeaponHeat: {ex} - {System == null}- BaseComp:{Comp == null} - ProtoRepo:{Comp?.Data.Repo == null}  - Session:{Comp?.Session == null}  - Weapons:{Comp.Data.Repo?.Values.State.Weapons[PartId] == null}", null, true); }
        }

        internal void UpdateRof()
        {
            var systemRate = System.WConst.RateOfFire * Comp.Data.Repo.Values.Set.RofModifier;
            var barrelRate = System.BarrelSpinRate * Comp.Data.Repo.Values.Set.RofModifier;
            var heatModifier = MathHelper.Lerp(1f, .25f, PartState.Heat / System.MaxHeat);

            systemRate *= CurrentlyDegrading ? heatModifier : 1;

            if (systemRate < 1)
                systemRate = 1;

            RateOfFire = (int)systemRate;
            BarrelSpinRate = (int)barrelRate;
            TicksPerShot = (uint)(3600f / RateOfFire);
            if (System.HasBarrelRotation) UpdateBarrelRotation();
        }

        internal void TurnOnAV(object o)
        {
            if (Comp.CoreEntity == null || Comp.CoreEntity.MarkedForClose || Comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            for (int j = 0; j < AnimationsSet[EventTriggers.TurnOn].Length; j++)
                PlayEmissives(AnimationsSet[EventTriggers.TurnOn][j]);

            PlayParticleEvent(EventTriggers.TurnOn, true, Vector3D.DistanceSquared(Comp.Session.CameraPos, MyPivotPos), null);
        }

        internal void TurnOffAv(object o)
        {
            if (Comp.CoreEntity == null || Comp.CoreEntity.MarkedForClose || Comp.Platform.State != CorePlatform.PlatformState.Ready) return;

            for (int j = 0; j < AnimationsSet[EventTriggers.TurnOff].Length; j++)
                PlayEmissives(AnimationsSet[EventTriggers.TurnOff][j]);

            PlayParticleEvent(EventTriggers.TurnOff, true, Vector3D.DistanceSquared(Comp.Session.CameraPos, MyPivotPos), null);
        }

        internal void SetWeaponDps(object o = null) // Need to test client sends MP request and receives response
        {
            if (System.DesignatorWeapon) return;

            
            var oldHeatPSec = Comp.HeatPerSecond;
            UpdateShotEnergy();
            UpdateDesiredPower();


            HeatPShot = (float) (System.WConst.HeatPerShot * ActiveAmmoDef.AmmoDef.Const.HeatModifier);

            MaxCharge = ActiveAmmoDef.AmmoDef.Const.ChargSize;

            TicksPerShot = (uint)(3600f / RateOfFire);

            var newHeatPSec = (60f / TicksPerShot) * HeatPShot * System.BarrelsPerShot;

            var heatDif = oldHeatPSec - newHeatPSec;
            
            Comp.HeatPerSecond -= heatDif;

            if (InCharger)
            {
                NewPowerNeeds = true;
            }
        }

        internal bool SpinBarrel(bool spinDown = false)
        {
            var translation = SpinPart.Entity.PositionComp.LocalMatrixRef.Translation;
            var matrix = SpinPart.Entity.PositionComp.LocalMatrixRef * BarrelRotationPerShot[BarrelRate];
            matrix.Translation = translation;
            SpinPart.Entity.PositionComp.SetLocalMatrix(ref matrix);

            if (Comp.TypeSpecific == CompTypeSpecific.VanillaFixed)
            {
                var testSphere = Comp.Cube.PositionComp.WorldVolume;
                if (Vector3D.DistanceSquared(System.Session.CameraPos, testSphere.Center) < 62500 && System.Session.Camera.IsInFrustum(ref testSphere))
                    MuzzlePart.Entity.Render.AddRenderObjects();
            }

            if (PlayTurretAv && RotateEmitter != null && !RotateEmitter.IsPlaying)
            { 
                RotateEmitter?.PlaySound(RotateSound, true, false, false, false, false, false);
            }

            if (_spinUpTick <= Comp.Session.Tick && spinDown)
            {
                _spinUpTick = Comp.Session.Tick + _ticksBeforeSpinUp;
                BarrelRate--;
            }
            if (BarrelRate < 0)
            {
                BarrelRate = 0;
                BarrelSpinning = false;

                if (PlayTurretAv && RotateEmitter != null && RotateEmitter.IsPlaying)
                    RotateEmitter.StopSound(true);
            }
            else BarrelSpinning = true;

            if (!spinDown)
            {
                if (BarrelRate < 9)
                {
                    if (_spinUpTick <= Comp.Session.Tick)
                    {
                        BarrelRate++;
                        _spinUpTick = Comp.Session.Tick + _ticksBeforeSpinUp;
                    }
                    return false;
                }
            }

            return true;
        }
    }
}
