﻿using System;
using System.Collections;
using System.Collections.Generic;
using CoreSystems.Support;
using ProtoBuf;
using Sandbox.Game.Debugging;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Library.Collections;
using VRage.ModAPI;
using VRageMath;
using static CoreSystems.Api.WcApi.DamageHandlerHelper;

namespace CoreSystems.Api
{
    /// <summary>
    /// https://github.com/sstixrud/CoreSystems/blob/master/BaseData/Scripts/CoreSystems/Api/CoreSystemsApiBase.cs
    /// </summary>
    public partial class WcApi
    {
        private bool _apiInit;

        private Action<IList<byte[]>> _getAllWeaponDefinitions;
        private Action<ICollection<MyDefinitionId>> _getCoreWeapons;
        private Action<ICollection<MyDefinitionId>> _getCoreStaticLaunchers;
        private Action<ICollection<MyDefinitionId>> _getCoreTurrets;
        private Action<ICollection<MyDefinitionId>> _getCorePhantoms;
        private Action<ICollection<MyDefinitionId>> _getCoreRifles;
        private Action<IList<byte[]>> _getCoreArmors;

        private Action<IMyEntity, ICollection<MyTuple<IMyEntity, float>>> _getSortedThreats;
        private Action<IMyEntity, ICollection<IMyEntity>> _getObstructions;

        private Func<MyDefinitionId, float> _getMaxPower;
        private Func<IMyEntity, MyTuple<bool, int, int>> _getProjectilesLockedOn;
        private Func<IMyEntity, int, IMyEntity> _getAiFocus;
        private Func<IMyEntity, IMyEntity, int, bool> _setAiFocus;
        private Func<IMyEntity, bool> _hasGridAi;
        private Func<IMyEntity, float> _getOptimalDps;
        private Func<IMyEntity, float> _getConstructEffectiveDps;
        private Func<IMyEntity, MyTuple<bool, bool>> _isInRange;
        private Func<ulong, MyTuple<Vector3D, Vector3D, float, float, long, string>> _getProjectileState;
        private Action<MyEntity, int, Action<long, int, ulong, long, Vector3D, bool>> _addProjectileMonitor;
        private Action<MyEntity, int, Action<long, int, ulong, long, Vector3D, bool>> _removeProjectileMonitor;
        private Action<IMyTerminalBlock, int, Action<long, int, ulong, long, Vector3D, bool>> _monitorProjectile; // Legacy use base version
        private Action<IMyTerminalBlock, int, Action<long, int, ulong, long, Vector3D, bool>> _unMonitorProjectile; // Legacy use base version
        private Action<long, int, List<MyEntity>, Action<ListReader<MyTuple<long, long, int, MyEntity, MyEntity, ListReader<MyTuple<Vector3D, object, float>>>>>> _registerDamageEvent;

        public void GetAllWeaponDefinitions(IList<byte[]> collection) => _getAllWeaponDefinitions?.Invoke(collection);
        public void GetAllCoreWeapons(ICollection<MyDefinitionId> collection) => _getCoreWeapons?.Invoke(collection);
        public void GetAllCoreStaticLaunchers(ICollection<MyDefinitionId> collection) => _getCoreStaticLaunchers?.Invoke(collection);
        public void GetAllCoreTurrets(ICollection<MyDefinitionId> collection) => _getCoreTurrets?.Invoke(collection);
        public void GetAllCorePhantoms(ICollection<MyDefinitionId> collection) => _getCorePhantoms?.Invoke(collection);
        public void GetAllCoreRifles(ICollection<MyDefinitionId> collection) => _getCoreRifles?.Invoke(collection);
        public void GetAllCoreArmors(IList<byte[]> collection) => _getCoreArmors?.Invoke(collection);

        public MyTuple<bool, int, int> GetProjectilesLockedOn(IMyEntity victim) =>
            _getProjectilesLockedOn?.Invoke(victim) ?? new MyTuple<bool, int, int>();
        public void GetSortedThreats(IMyEntity shooter, ICollection<MyTuple<IMyEntity, float>> collection) =>
            _getSortedThreats?.Invoke(shooter, collection);
        public void GetObstructions(IMyEntity shooter, ICollection<IMyEntity> collection) =>
            _getObstructions?.Invoke(shooter, collection);
        public IMyEntity GetAiFocus(IMyEntity shooter, int priority = 0) => _getAiFocus?.Invoke(shooter, priority);
        public bool SetAiFocus(IMyEntity shooter, IMyEntity target, int priority = 0) =>
            _setAiFocus?.Invoke(shooter, target, priority) ?? false;
        public MyTuple<bool, bool, bool, IMyEntity> GetWeaponTarget(IMyTerminalBlock weapon, int weaponId = 0) =>
            _getWeaponTarget?.Invoke(weapon, weaponId) ?? new MyTuple<bool, bool, bool, IMyEntity>();
        public float GetMaxPower(MyDefinitionId weaponDef) => _getMaxPower?.Invoke(weaponDef) ?? 0f;
        public bool HasGridAi(IMyEntity entity) => _hasGridAi?.Invoke(entity) ?? false;
        public float GetOptimalDps(IMyEntity entity) => _getOptimalDps?.Invoke(entity) ?? 0f;
        public MyTuple<Vector3D, Vector3D, float, float, long, string> GetProjectileState(ulong projectileId) =>
            _getProjectileState?.Invoke(projectileId) ?? new MyTuple<Vector3D, Vector3D, float, float, long, string>();
        public float GetConstructEffectiveDps(IMyEntity entity) => _getConstructEffectiveDps?.Invoke(entity) ?? 0f;
        public MyTuple<Vector3D, Vector3D> GetWeaponScope(IMyTerminalBlock weapon, int weaponId) =>
            _getWeaponScope?.Invoke(weapon, weaponId) ?? new MyTuple<Vector3D, Vector3D>();

        public void AddProjectileCallback(MyEntity entity, int weaponId, Action<long, int, ulong, long, Vector3D, bool> action) =>
            _addProjectileMonitor?.Invoke(entity, weaponId, action);

        public void RemoveProjectileCallback(MyEntity entity, int weaponId, Action<long, int, ulong, long, Vector3D, bool> action) =>
            _removeProjectileMonitor?.Invoke(entity, weaponId, action);


        // block/grid, Threat, Other 
        public MyTuple<bool, bool> IsInRange(IMyEntity entity) =>
            _isInRange?.Invoke(entity) ?? new MyTuple<bool, bool>();


        // register for damage events
        public void RegisterDamageEvent(long modId, int type, List<MyEntity> entities, Action<ListReader<MyTuple<long, long, int, MyEntity, MyEntity, ListReader<MyTuple<Vector3D, object, float>>>>> callback)
        {
            _registerDamageEvent?.Invoke(modId, type, entities, callback);
        }


        private const long Channel = 67549756549;
        private bool _getWeaponDefinitions;
        private bool _isRegistered;
        private Action _readyCallback;

        /// <summary>
        /// True if CoreSystems replied when <see cref="Load"/> got called.
        /// </summary>
        public bool IsReady { get; private set; }

        /// <summary>
        /// Only filled if giving true to <see cref="Load"/>.
        /// </summary>
        public readonly List<WcApiDef.WeaponDefinition> WeaponDefinitions = new List<WcApiDef.WeaponDefinition>();

        /// <summary>
        /// Ask CoreSystems to send the API methods.
        /// <para>Throws an exception if it gets called more than once per session without <see cref="Unload"/>.</para>
        /// </summary>
        /// <param name="readyCallback">Method to be called when CoreSystems replies.</param>
        /// <param name="getWeaponDefinitions">Set to true to fill <see cref="WeaponDefinitions"/>.</param>
        public void Load(Action readyCallback = null, bool getWeaponDefinitions = false)
        {
            if (_isRegistered)
                throw new Exception($"{GetType().Name}.Load() should not be called multiple times!");

            _readyCallback = readyCallback;
            _getWeaponDefinitions = getWeaponDefinitions;
            _isRegistered = true;
            MyAPIGateway.Utilities.RegisterMessageHandler(Channel, HandleMessage);
            MyAPIGateway.Utilities.SendModMessage(Channel, "ApiEndpointRequest");
        }

        public void Unload()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(Channel, HandleMessage);

            ApiAssign(null);

            _isRegistered = false;
            _apiInit = false;
            IsReady = false;
        }

        private void HandleMessage(object obj)
        {
            if (_apiInit || obj is string
            ) // the sent "ApiEndpointRequest" will also be received here, explicitly ignoring that
                return;

            var dict = obj as IReadOnlyDictionary<string, Delegate>;

            if (dict == null)
                return;

            ApiAssign(dict, _getWeaponDefinitions);

            IsReady = true;
            _readyCallback?.Invoke();
        }

        public void ApiAssign(IReadOnlyDictionary<string, Delegate> delegates, bool getWeaponDefinitions = false)
        {
            _apiInit = (delegates != null);
            /// base methods
            AssignMethod(delegates, "GetAllWeaponDefinitions", ref _getAllWeaponDefinitions);
            AssignMethod(delegates, "GetCoreWeapons", ref _getCoreWeapons);
            AssignMethod(delegates, "GetCoreStaticLaunchers", ref _getCoreStaticLaunchers);
            AssignMethod(delegates, "GetCoreTurrets", ref _getCoreTurrets);
            AssignMethod(delegates, "GetCorePhantoms", ref _getCorePhantoms);
            AssignMethod(delegates, "GetCoreRifles", ref _getCoreRifles);
            AssignMethod(delegates, "GetCoreArmors", ref _getCoreArmors);

            AssignMethod(delegates, "GetBlockWeaponMap", ref _getBlockWeaponMap);
            AssignMethod(delegates, "GetSortedThreats", ref _getSortedThreats);
            AssignMethod(delegates, "GetObstructions", ref _getObstructions);
            AssignMethod(delegates, "GetMaxPower", ref _getMaxPower);
            AssignMethod(delegates, "GetProjectilesLockedOn", ref _getProjectilesLockedOn);
            AssignMethod(delegates, "GetAiFocus", ref _getAiFocus);
            AssignMethod(delegates, "SetAiFocus", ref _setAiFocus);
            AssignMethod(delegates, "HasGridAi", ref _hasGridAi);
            AssignMethod(delegates, "GetOptimalDps", ref _getOptimalDps);
            AssignMethod(delegates, "GetConstructEffectiveDps", ref _getConstructEffectiveDps);
            AssignMethod(delegates, "IsInRange", ref _isInRange);
            AssignMethod(delegates, "GetProjectileState", ref _getProjectileState);
            AssignMethod(delegates, "AddMonitorProjectile", ref _addProjectileMonitor);
            AssignMethod(delegates, "RemoveMonitorProjectile", ref _removeProjectileMonitor);

            /// block methods
            AssignMethod(delegates, "GetWeaponTarget", ref _getWeaponTarget);
            AssignMethod(delegates, "SetWeaponTarget", ref _setWeaponTarget);
            AssignMethod(delegates, "FireWeaponOnce", ref _fireWeaponOnce);
            AssignMethod(delegates, "ToggleWeaponFire", ref _toggleWeaponFire);
            AssignMethod(delegates, "IsWeaponReadyToFire", ref _isWeaponReadyToFire);
            AssignMethod(delegates, "GetMaxWeaponRange", ref _getMaxWeaponRange);
            AssignMethod(delegates, "GetTurretTargetTypes", ref _getTurretTargetTypes);
            AssignMethod(delegates, "SetTurretTargetTypes", ref _setTurretTargetTypes);
            AssignMethod(delegates, "SetBlockTrackingRange", ref _setBlockTrackingRange);
            AssignMethod(delegates, "IsTargetAligned", ref _isTargetAligned);
            AssignMethod(delegates, "IsTargetAlignedExtended", ref _isTargetAlignedExtended);
            AssignMethod(delegates, "CanShootTarget", ref _canShootTarget);
            AssignMethod(delegates, "GetPredictedTargetPosition", ref _getPredictedTargetPos);
            AssignMethod(delegates, "GetHeatLevel", ref _getHeatLevel);
            AssignMethod(delegates, "GetCurrentPower", ref _currentPowerConsumption);
            AssignMethod(delegates, "DisableRequiredPower", ref _disableRequiredPower);
            AssignMethod(delegates, "HasCoreWeapon", ref _hasCoreWeapon);
            AssignMethod(delegates, "GetActiveAmmo", ref _getActiveAmmo);
            AssignMethod(delegates, "SetActiveAmmo", ref _setActiveAmmo);
            AssignMethod(delegates, "MonitorProjectile", ref _monitorProjectile);
            AssignMethod(delegates, "UnMonitorProjectile", ref _unMonitorProjectile);
            AssignMethod(delegates, "GetPlayerController", ref _getPlayerController);
            AssignMethod(delegates, "GetWeaponAzimuthMatrix", ref _getWeaponAzimuthMatrix);
            AssignMethod(delegates, "GetWeaponElevationMatrix", ref _getWeaponElevationMatrix);
            AssignMethod(delegates, "IsTargetValid", ref _isTargetValid);
            AssignMethod(delegates, "GetWeaponScope", ref _getWeaponScope);

            //Phantom methods
            AssignMethod(delegates, "GetTargetAssessment", ref _getTargetAssessment);
            //AssignMethod(delegates, "GetPhantomInfo", ref _getPhantomInfo);
            AssignMethod(delegates, "SetTriggerState", ref _setTriggerState);
            AssignMethod(delegates, "AddMagazines", ref _addMagazines);
            AssignMethod(delegates, "SetAmmo", ref _setAmmo);
            AssignMethod(delegates, "ClosePhantom", ref _closePhantom);
            AssignMethod(delegates, "SpawnPhantom", ref _spawnPhantom);
            AssignMethod(delegates, "SetFocusTarget", ref _setPhantomFocusTarget);

            //Hakerman's Beam Logic
            AssignMethod(delegates, "IsWeaponShooting", ref _isWeaponShooting);
            AssignMethod(delegates, "GetShotsFired", ref _getShotsFired);
            AssignMethod(delegates, "GetMuzzleInfo", ref _getMuzzleInfo);

            if (getWeaponDefinitions)
            {
                var byteArrays = new List<byte[]>();
                GetAllWeaponDefinitions(byteArrays);
                foreach (var byteArray in byteArrays)
                    WeaponDefinitions.Add(MyAPIGateway.Utilities.SerializeFromBinary<WcApiDef.WeaponDefinition>(byteArray));
            }
        }

        private void AssignMethod<T>(IReadOnlyDictionary<string, Delegate> delegates, string name, ref T field)
            where T : class
        {
            if (delegates == null)
            {
                field = null;
                return;
            }

            Delegate del;
            if (!delegates.TryGetValue(name, out del))
                throw new Exception($"{GetType().Name} :: Couldn't find {name} delegate of type {typeof(T)}");

            field = del as T;

            if (field == null)
                throw new Exception(
                    $"{GetType().Name} :: Delegate {name} is not type {typeof(T)}, instead it's: {del.GetType()}");
        }

        internal class DamageHandlerHelper
        {
            public void YourCallBackFunction(List<ProjectileHitTick> list)
            {
                // Your code goes here
                //
                // Once this function completes the data in the list will be deleted... if you need to use the data in this list
                // after this function completes make a copy of it.
                //
            }


            /// Don't touch anything below this line
            public void RegisterForDamage(long modId, EventType type, List<MyEntity> listOfWeaponsAndOrGrids)
            {
                _wcApi.RegisterDamageEvent(modId, (int) type, listOfWeaponsAndOrGrids, DefaultCallBack);
            }

            private void DefaultCallBack(ListReader<MyTuple<long, long, int, MyEntity, MyEntity, ListReader<MyTuple<Vector3D, object, float>>>> listReader)
            {
                YourCallBackFunction(ProcessEvents(listReader));
                CleanUpEvents();
            }

            private readonly List<ProjectileHitTick> _convertedObjects = new List<ProjectileHitTick>();
            private readonly Stack<List<ProjectileHitTick.ProHit>> _hitPool = new Stack<List<ProjectileHitTick.ProHit>>(256);

            private List<ProjectileHitTick> ProcessEvents(ListReader<MyTuple<long, long, int, MyEntity, MyEntity, ListReader<MyTuple<Vector3D, object, float>>>> projectiles)
            {
                foreach (var p in projectiles)
                {
                    var hits = _hitPool.Count > 0 ? _hitPool.Pop() : new List<ProjectileHitTick.ProHit>();

                    foreach (var hitObj in p.Item6)
                    {
                        hits.Add(new ProjectileHitTick.ProHit { HitPosition = hitObj.Item1, ObjectHit = hitObj.Item2, Damage = hitObj.Item3 });
                    }
                    _convertedObjects.Add(new ProjectileHitTick { ProId = p.Item1, PlayerId = p.Item2, WeaponId = p.Item3, WeaponEntity = p.Item4, WeaponParent = p.Item5, ObjectsHit = hits });
                }

                return _convertedObjects;
            }

            private void CleanUpEvents()
            {
                foreach (var p in _convertedObjects)
                {
                    p.ObjectsHit.Clear();
                    _hitPool.Push(p.ObjectsHit);
                }
                _convertedObjects.Clear();
            }

            public struct ProjectileHitTick
            {
                public long ProId;
                public long PlayerId;
                public int WeaponId;
                public MyEntity WeaponEntity;
                public MyEntity WeaponParent;
                public List<ProHit> ObjectsHit;

                public struct ProHit
                {
                    public Vector3D HitPosition; // To == first hit, From = projectile start position this frame
                    public object ObjectHit; // block, player, etc... 
                    public float Damage;
                }
            }


            private readonly WcApi _wcApi;
            public DamageHandlerHelper(WcApi wcApi)
            {
                _wcApi = wcApi;
            }

            public enum EventType
            {
                Unregister,
                SystemWideDamageEvents,
                ListOfWeaponsOrConstructs, // Only need to specify one Topentity/Construct to enable for whole network/subgrids
            }
        }

    }

}
