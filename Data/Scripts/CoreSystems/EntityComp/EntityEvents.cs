﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using CoreSystems.Control;
using CoreSystems.Platform;
using CoreSystems.Projectiles;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using static CoreSystems.Platform.CorePlatform;
using static CoreSystems.Session;
using static VRage.Game.ObjectBuilders.Definitions.MyObjectBuilder_GameDefinition;

namespace CoreSystems.Support
{
    public partial class CoreComponent
    {
        internal void RegisterEvents(bool register = true)
        {
            try
            {
                if (register)
                {
                    if (Registered)
                        Log.Line("BaseComp RegisterEvents error");
                    //TODO change this
                    Registered = true;
                    if (IsBlock)
                    {
                        if (Type == CompType.Weapon)
                            TerminalBlock.AppendingCustomInfo += AppendingCustomInfoWeapon;
                        else if (TypeSpecific == CompTypeSpecific.Support)
                            TerminalBlock.AppendingCustomInfo += AppendingCustomInfoSupport;
                        else if (TypeSpecific == CompTypeSpecific.Upgrade)
                            TerminalBlock.AppendingCustomInfo += AppendingCustomInfoUpgrade;
                        else if (TypeSpecific == CompTypeSpecific.Control)
                            TerminalBlock.AppendingCustomInfo += AppendingCustomInfoControl;

                        Cube.IsWorkingChanged += IsWorkingChanged;
                        IsWorkingChanged(Cube);
                    }

                    if (CoreInventory == null)
                    {
                        if (TypeSpecific != CompTypeSpecific.Phantom && TypeSpecific != CompTypeSpecific.Control && !IsBomb)
                            Log.Line("BlockInventory is null");
                    }
                    else
                    {
                        CoreInventory.InventoryContentChanged += OnContentsChanged;
                        Session.I.CoreInventoryItems[CoreInventory] = new ConcurrentDictionary<uint, BetterInventoryItem>();
                        Session.I.ConsumableItemList[CoreInventory] = Session.I.BetterItemsListPool.Get();

                        var items = CoreInventory.GetItems();
                        for (int i = 0; i < items.Count; i++)
                        {
                            var bItem = Session.I.BetterInventoryItems.Get();
                            var item = items[i];
                            bItem.Amount = (int)item.Amount;
                            bItem.Item = item;
                            bItem.Content = item.Content;

                            Session.I.CoreInventoryItems[CoreInventory][items[i].ItemId] = bItem;
                        }
                    }
                }
                else
                {
                    if (!Registered)
                        Log.Line("BaseComp UnRegisterEvents error");

                    if (Registered)
                    {
                        //TODO change this
                        Registered = false;

                        if (IsBlock)
                        {

                            if (Type == CompType.Weapon)
                                TerminalBlock.AppendingCustomInfo -= AppendingCustomInfoWeapon;
                            else if (TypeSpecific == CompTypeSpecific.Support)
                                TerminalBlock.AppendingCustomInfo -= AppendingCustomInfoSupport;
                            else if (TypeSpecific == CompTypeSpecific.Upgrade)
                                TerminalBlock.AppendingCustomInfo -= AppendingCustomInfoUpgrade;
                            else if (TypeSpecific == CompTypeSpecific.Control)
                                TerminalBlock.AppendingCustomInfo -= AppendingCustomInfoControl;
                            Cube.IsWorkingChanged -= IsWorkingChanged;
                        }

                        if (CoreInventory == null)
                        {
                            if ((TypeSpecific != CompTypeSpecific.Control && TypeSpecific != CompTypeSpecific.Phantom) && !IsBomb)
                                Log.Line("BlockInventory is null");
                        }
                        else
                        {
                            CoreInventory.InventoryContentChanged -= OnContentsChanged;
                            ConcurrentDictionary<uint, BetterInventoryItem> removedItems;
                            MyConcurrentList<BetterInventoryItem> removedList;

                            if (Session.I.CoreInventoryItems.TryRemove(CoreInventory, out removedItems))
                            {
                                foreach (var inventoryItems in removedItems)
                                    Session.I.BetterInventoryItems.Return(inventoryItems.Value);

                                removedItems.Clear();
                            }

                            if (Session.I.ConsumableItemList.TryRemove(CoreInventory, out removedList))
                                Session.I.BetterItemsListPool.Return(removedList);
                        }
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in RegisterEvents: {ex}", null, true); }
        }

        private void OnContentsChanged(MyInventoryBase inv, MyPhysicalInventoryItem item, MyFixedPoint amount)
        {
            BetterInventoryItem cachedItem;
            if (!Session.I.CoreInventoryItems[CoreInventory].TryGetValue(item.ItemId, out cachedItem))
            {
                cachedItem = Session.I.BetterInventoryItems.Get();
                cachedItem.Amount = (int)amount;
                cachedItem.Content = item.Content;
                cachedItem.Item = item;
                Session.I.CoreInventoryItems[CoreInventory].TryAdd(item.ItemId, cachedItem);
            }
            else if (cachedItem.Amount + amount > 0)
            {
                cachedItem.Amount += (int)amount;
            }
            else if (cachedItem.Amount + amount <= 0)
            {
                BetterInventoryItem removedItem;
                if (Session.I.CoreInventoryItems[CoreInventory].TryRemove(item.ItemId, out removedItem))
                    Session.I.BetterInventoryItems.Return(removedItem);
            }
            var collection = TypeSpecific != CompTypeSpecific.Phantom ? Platform.Weapons : Platform.Phantoms;
            if (Session.I.IsServer && amount <= 0) {
                for (int i = 0; i < collection.Count; i++)
                    collection[i].CheckInventorySystem = true;
            }
        }

        private static void OnMarkForClose(MyEntity myEntity)
        {
            
        }


        private void IsWorkingChanged(MyCubeBlock myCubeBlock)
        {
            try {
                var wasFunctional = IsFunctional;
                IsFunctional = myCubeBlock.IsFunctional;

                if (Registered && Ai.Construct.RootAi !=null)
                    Ai.Construct.RootAi.Construct.DirtyWeaponGroups = true;


                if (Platform.State == PlatformState.Incomplete) {
                    Log.Line("Init on Incomplete");
                    Init();
                }
                else {

                    if (!wasFunctional && IsFunctional && IsWorkingChangedTick > 0)
                        Status = Start.ReInit;

                    IsWorking = myCubeBlock.IsWorking;
                    if (Type == CompType.Weapon)
                    {
                        var wComp = (Weapon.WeaponComponent)this;
                        if (IsWorking)
                            Session.I.FutureEvents.Schedule(wComp.ActivateWhileOn, null, 30);
                        else if (!IsWorking)
                            Session.I.FutureEvents.Schedule(wComp.DeActivateWhileOn, null, 30);

                    }
                    if (Cube.ResourceSink.CurrentInputByType(GId) < 0) Log.Line($"IsWorking:{IsWorking}(was:{wasFunctional}) - Func:{IsFunctional} - GridAvailPow:{Ai.GridAvailablePower} - SinkPow:{SinkPower} - SinkReq:{Cube.ResourceSink.RequiredInputByType(GId)} - SinkCur:{Cube.ResourceSink.CurrentInputByType(GId)}");

                    if (!IsWorking && Registered) {

                        var collection = TypeSpecific != CompTypeSpecific.Phantom ? Platform.Weapons : Platform.Phantoms;
                        foreach (var w in collection)
                                w.StopShooting();
                    }
                    IsWorkingChangedTick = Session.I.Tick;

                }
                if (Platform.State == PlatformState.Ready) {

                    if (Type == CompType.Weapon)
                    {
                        var wComp = ((Weapon.WeaponComponent) this);
                        if (wasFunctional && !IsFunctional)
                            wComp.NotFunctional();


                        if (wasFunctional != IsFunctional && Ai.Construct.RootAi != null && wComp.Data.Repo.Values.Set.Overrides.WeaponGroupId > 0)
                            Ai.Construct.RootAi.Construct.DirtyWeaponGroups = true;
                    }
                }
                
                if (Session.I.MpActive && Session.I.IsServer) {

                    if (Type == CompType.Weapon)
                        ((Weapon.WeaponComponent)this).PowerLoss();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in IsWorkingChanged: {ex}", null, true); }
        }

        internal string GetSystemStatus()
        {
            if (!Cube.IsFunctional) return Localization.GetText("SystemStatusFault");
            if (!Cube.IsWorking) return Localization.GetText("SystemStatusOffline");
            return Ai.AiOwner != 0 ? Localization.GetText("SystemStatusOnline") : Localization.GetText("SystemStatusRogueAi");
        }

        private void AppendingCustomInfoWeapon(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var comp = ((Weapon.WeaponComponent)this);

                if (comp.Data.Repo.Values.Set.Overrides.LeadGroup == 0 && !comp.IsBomb && (!comp.HasTurret && !comp.OverrideLeads || comp.HasTurret && comp.OverrideLeads))
                    stringBuilder.Append("\nWARNING: fixed weapon detected\n")
                        .Append("  - without a Target Lead Group set!\n\n");

                var status = GetSystemStatus();
                var debug = Debug || comp.Data.Repo.Values.Set.Overrides.Debug;
                var advanced = (Session.I.Settings.ClientConfig.AdvancedMode || debug) && !comp.HasAlternateUi;
                
                if (!comp.HasAlternateUi)
                    stringBuilder.Append(advanced ? $"{status}\n" : $"{status}");

                if (HasServerOverrides)
                    stringBuilder.Append("\nWeapon modified by server!\n")
                        .Append("Report issues to server admins.\n");

                if (advanced)
                {
                    stringBuilder.Append($"\n{Localization.GetText("WeaponInfoConstructDPS")}: " + Ai.EffectiveDps.ToString("e2"))
                        .Append($"\n{Localization.GetText("WeaponInfoPeakDps")}: " + comp.PeakDps.ToString("0.0"))
                        .Append($"\n{Localization.GetText("WeaponInfoBaseDps")}: " + comp.BaseDps.ToString("0.0"))
                        .Append($"\n{Localization.GetText("WeaponInfoAreaDps")}: " + comp.AreaDps.ToString("0.0"))
                        .Append($"\n{Localization.GetText("WeaponInfoExplode")}: " + comp.DetDps.ToString("0.0"))
                        .Append("\n")
                        .Append($"\n{Localization.GetText("WeaponTotalEffect")}: " + comp.TotalEffect.ToString("e2"))
                        .Append($"\n              [ " + Ai.Construct.RootAi?.Construct.TotalEffect.ToString("e2") + " ]")
                        .Append($"\n{Localization.GetText("WeaponTotalEffectAvgDps")}: " + comp.AverageEffect.ToString("N0") + " - (" + comp.AddEffect.ToString("N0") + ")")
                        .Append($"\n             [ " + Ai.Construct.RootAi?.Construct.AverageEffect.ToString("N0") + " - (" + Ai.Construct.RootAi?.Construct.AddEffect.ToString("N0") + ") ]");
                }
                else
                {
                    if (!comp.HasAlternateUi)
                        stringBuilder.Append($"\n{Localization.GetText("WeaponInfoPeakDps")}: " + comp.PeakDps.ToString("0.0"));
                }


                if (HeatPerSecond > 0 && advanced)
                    stringBuilder.Append("\n__________________________________" )
                        .Append($"\n{Localization.GetText("WeaponInfoHeatGenerated")}: {HeatPerSecond:0.0} W ({(HeatPerSecond / MaxHeat) :P}/s)")
                        .Append($"\n{Localization.GetText("WeaponInfoHeatDissipated")}: {HeatSinkRate:0.0} W ({(HeatSinkRate / MaxHeat):P}/s)")
                        .Append($"\n{Localization.GetText("WeaponInfoCurrentHeat")}: {CurrentHeat:0.0} J ({(CurrentHeat / MaxHeat):P})");

                if (!comp.HasAlternateUi)
                {
                    stringBuilder.Append(advanced ? "\n__________________________________\n" : string.Empty)
                        .Append($"\n{Localization.GetText("WeaponInfoShotsPerSec")}: " + comp.RealShotsPerSec.ToString("0.00") + " (" + comp.ShotsPerSec.ToString("0.00") + ")")
                        .Append($"\n{Localization.GetText("WeaponInfoCurrentDraw")}: " + SinkPower.ToString("0.000") + " MW");
                }

                if (comp.HasEnergyWeapon && advanced)
                    stringBuilder.Append($"\n{Localization.GetText("WeaponInfoRequiredPower")}: " + Platform.Structure.ActualPeakPowerCombined.ToString("0.00") + " MW");

                if (!comp.HasAlternateUi)
                    stringBuilder.Append($"\n\n{Localization.GetText("WeaponInfoDividerLineWeapon")}");

                var collection = comp.HasAlternateUi ? SortAndGetTargetTypes() : TypeSpecific != CompTypeSpecific.Phantom ? Platform.Weapons : Platform.Phantoms;
                for (int i = 0; i < collection.Count; i++)
                {
                    var w = collection[i];
                    string shots;
                    if ((w.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo || w.ActiveAmmoDef.AmmoDef.Const.IsHybrid) && !comp.HasAlternateUi)
                    {
                        var chargeTime = w.AssignedPower > 0 ? (int)((w.MaxCharge - w.ProtoWeaponAmmo.CurrentCharge) / w.AssignedPower * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS) : 0;

                        shots = "\nCharging: " + w.Charging +" ("+ chargeTime+")";
                        shots += "\nMax Charge Rate: "+ w.ActiveAmmoDef.AmmoDef.Const.PowerPerTick.ToString("0.00") + " MW";

                        if (w.ActiveAmmoDef.AmmoDef.Const.IsHybrid) shots += "\n" + w.ActiveAmmoDef.AmmoDef.AmmoRound + ": " + w.ProtoWeaponAmmo.CurrentAmmo;
                    }

                    else shots = "\n" + w.ActiveAmmoDef.AmmoDef.AmmoRound + ": " + w.ProtoWeaponAmmo.CurrentAmmo;

                    var burst = w.ActiveAmmoDef.AmmoDef.Const.BurstMode && !comp.HasAlternateUi ? $"\nShootMode: " + w.ShotsFired + "(" + w.System.ShotsPerBurst + $") - {Localization.GetText("WeaponInfoDelay")}: " + w .System.Values.HardPoint.Loading.DelayAfterBurst : string.Empty;

                    var endReturn = i + 1 != collection.Count ? "\n" : string.Empty;

                    if (!comp.HasAlternateUi)
                        stringBuilder.Append($"\n{Localization.GetText("WeaponInfoName")}: " + w.System.PartName + shots + burst + $"\n{Localization.GetText("WeaponInfoHasTarget")}: " + (w.ActiveAmmoDef.AmmoDef.Const.RequiresTarget ? w.Target.HasTarget.ToString() : "n/a") + $"\n{Localization.GetText("WeaponInfoReloading")}: " + w.Loading +  $"\n{Localization.GetText("WeaponInfoLoS")}: " + !w.PauseShoot + endReturn);
                    else
                        stringBuilder.Append($"\n{Localization.GetText("WeaponInfoName")}: " + w.System.PartName + (w.Target.HasTarget ? $"\n{Localization.GetText("WeaponInfoTargetState")}: " + w.Target.CurrentState : string.Empty));

                    string otherAmmo = null;
                    if (!comp.HasAlternateUi)
                    {
                        for (int j = 0; j < w.System.AmmoTypes.Length; j++)
                        {
                            var ammo = w.System.AmmoTypes[j];
                            if (ammo == w.ActiveAmmoDef || !ammo.AmmoDef.Const.IsTurretSelectable || string.IsNullOrEmpty(ammo.AmmoDef.AmmoRound) || ammo.AmmoDef.AmmoRound == "Energy")
                                continue;

                            if (otherAmmo == null)
                                otherAmmo = "\n\nAlternate Magazines:";

                            otherAmmo += $"\n{ammo.AmmoDef.AmmoRound}";
                        }

                        if (otherAmmo != null)
                            stringBuilder.Append(otherAmmo);
                    }
                }

                if (advanced)
                {
                    foreach (var weapon in collection)
                    {
                        var chargeTime = weapon.AssignedPower > 0 ? (int)((weapon.MaxCharge - weapon.ProtoWeaponAmmo.CurrentCharge) / weapon.AssignedPower * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS) : 0;
                        stringBuilder.Append($"\n\nWeapon: {weapon.System.PartName} - Enabled: {IsWorking}");
                        stringBuilder.Append($"\nTargetState: {weapon.Target.CurrentState} - Manual: {weapon.BaseComp.UserControlled || weapon.Target.TargetState == Target.TargetStates.IsFake}");
                        stringBuilder.Append($"\nEvent: {weapon.LastEvent} - ProtoWeaponAmmo :{!weapon.NoMagsToLoad}");
                        stringBuilder.Append($"\nOverHeat: {weapon.PartState.Overheated} - Shooting: {weapon.IsShooting}");
                        stringBuilder.Append($"\nisAligned: {weapon.Target.IsAligned}");
                        stringBuilder.Append($"\nCanShoot: {weapon.ShotReady} - Charging: {weapon.Charging}");
                        stringBuilder.Append($"\nAiShooting: {weapon.AiShooting}");
                        stringBuilder.Append($"\n{(weapon.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo ? "ChargeSize: " + weapon.ActiveAmmoDef.AmmoDef.Const.ChargSize : "MagSize: " +  weapon.ActiveAmmoDef.AmmoDef.Const.MagazineSize)} ({weapon.ProtoWeaponAmmo.CurrentCharge})");
                        stringBuilder.Append($"\nChargeTime: {chargeTime}");
                        stringBuilder.Append($"\nCharging: {weapon.Charging}({weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge})");
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Weapon AppendingCustomInfo: {ex}", null, true); }
        }

        private List<Weapon> SortAndGetTargetTypes()
        {
            var collection = TypeSpecific != CompTypeSpecific.Phantom ? Platform.Weapons : Platform.Phantoms;

            Session.I.TmpWeaponEventSortingList.Clear();
            foreach (var w in collection)
            {
                if (!w.Target.HasTarget)
                    continue;

                w.Target.CurrentState = GetTargetState(w);
                Session.I.TmpWeaponEventSortingList.Add(w);
            }
            var n = Session.I.TmpWeaponEventSortingList.Count;
            for (int i = 1; i < n; ++i)
            {
                var key = Session.I.TmpWeaponEventSortingList[i];
                var j = i - 1;

                while (j >= 0 && Session.I.TmpWeaponEventSortingList[j].Target.CurrentState != key.Target.CurrentState)
                {
                    Session.I.TmpWeaponEventSortingList[j + 1] = Session.I.TmpWeaponEventSortingList[j];
                    j -= 1;
                }
                Session.I.TmpWeaponEventSortingList[j + 1] = key;
            }
            return Session.I.TmpWeaponEventSortingList;
        }

        private Target.States GetTargetState(Weapon w)
        {
            if (w.Target.TargetObject != null)
            {
                if (w.Target.TargetObject is MyPlanet)
                    return Target.States.Planet;

                if (w.Target.TargetObject is MyVoxelBase)
                    return Target.States.Roid;

                if (w.Target.TargetObject is Projectile)
                    return Target.States.Projectile;

                var entity = w.Target.TargetObject as MyEntity;
                if (entity == null)
                    return Target.States.Fake;

                Sandbox.ModAPI.Ingame.MyDetectedEntityInfo entInfo;
                Ai.CreateEntInfo(entity, w.Comp.Ai.AiOwner, out entInfo);
                if (entInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Owner)
                    return Target.States.YourGrid;

                if (entInfo.Relationship == MyRelationsBetweenPlayerAndBlock.FactionShare || entInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Friends)
                    return entity is IMyCharacter ? Target.States.FriendlyCharacter : Target.States.FriendlyGrid;

                if (entInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Neutral)
                    return entity is IMyCharacter ? Target.States.NeutralCharacter : Target.States.NeutralGrid;

                if (entInfo.Relationship == MyRelationsBetweenPlayerAndBlock.NoOwnership)
                    return entity is IMyCharacter ? Target.States.NeutralCharacter : Target.States.UnOwnedGrid;

                if (entInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies)
                    return entity is IMyCharacter ? Target.States.EnemyCharacter : Target.States.EnemyGrid;
            }

            return Target.States.NotSet;
        }

        private void AppendingCustomInfoControl(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var comp = ((ControlSys.ControlComponent)this);

                stringBuilder.Append("\n==== ControlSys ====\n");

                var ai = Platform.Control?.TopAi?.RootComp?.Ai;
                var initted = ai != null;

                stringBuilder.Append($"Ai Detected:{initted && !Platform.Control.TopAi.RootComp.Ai.MarkedForClose}\n\n");

                if (initted)
                {
                    stringBuilder.Append($"Weapons: {ai.WeaponComps.Count}\nTools: {ai.Tools.Count}\nCamera: {comp.Controller.Camera != null}\n");
                }

                if (Debug)
                {
                    foreach (var support in Platform.Support)
                    {
                        stringBuilder.Append($"\n\nPart: {support.CoreSystem.PartName} - Enabled: {IsWorking}");
                        stringBuilder.Append($"\nManual: {support.BaseComp.UserControlled}");
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in AppendingCustomInfoSupport: {ex}", null, true); }
        }

        private void AppendingCustomInfoSupport(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var status = GetSystemStatus();

                stringBuilder.Append(status + "\nCurrent: )");

                stringBuilder.Append("\n\n==== SupportSys ====");

                var weaponCnt = Platform.Support.Count;
                for (int i = 0; i < weaponCnt; i++)
                {
                    var a = Platform.Support[i];
                }

                if (Debug)
                {
                    foreach (var support in Platform.Support)
                    {
                        stringBuilder.Append($"\n\nPart: {support.CoreSystem.PartName} - Enabled: {IsWorking}");
                        stringBuilder.Append($"\nManual: {support.BaseComp.UserControlled}");
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in AppendingCustomInfoSupport: {ex}", null, true); }
        }

        private void AppendingCustomInfoUpgrade(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var status = GetSystemStatus();

                stringBuilder.Append(status + "\nCurrent: )");

                stringBuilder.Append("\n\n==== Upgrade ====");

                var weaponCnt = Platform.Support.Count;
                for (int i = 0; i < weaponCnt; i++)
                {
                    var a = Platform.Upgrades[i];
                }

                if (Debug)
                {
                    foreach (var upgrade in Platform.Upgrades)
                    {
                        stringBuilder.Append($"\n\nPart: {upgrade.CoreSystem.PartName} - Enabled: {IsWorking}");
                        stringBuilder.Append($"\nManual: {upgrade.BaseComp.UserControlled}");
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in AppendingCustomInfoUpgrade: {ex}", null, true); }
        }
    }
}
