using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using M2922.Core;

namespace M2922.Component.Weapon
{
    /// <summary>
    /// Composant runtime pour une arme avec pools de perks/masterworks/mods.
    /// Au Start(), un élément est tiré aléatoirement dans chaque pool.
    /// Les stats finales sont calculées à partir de la sélection.
    /// 
    /// UTILISATION :
    /// 1. Ajoutez ce composant sur un GameObject
    /// 2. Glissez un WeaponDefinition dans le champ Weapon Definition
    /// 3. Le baking se fait automatiquement (OnValidate)
    /// </summary>
    [AddComponentMenu("M2922/Weapon/Weapon")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_Weapon : M2922_Base
    {
        // ===================================================
        // SOURCE DATA — éditeur uniquement, invisible pour UdonSharp
        // #if + NonSerialized = double protection
        // ===================================================
#if !COMPILER_UDONSHARP && UNITY_EDITOR
        public WeaponDefinition _weaponDefinition;

        [Header("=== EDITOR PREVIEW (selection manuelle) ===")]
        [Tooltip("-1 = Random (tire au sort au runtime) ; 0..N = selection fixe")]
        [SerializeField] private int _previewPerk1Index = -1;
        [SerializeField] private int _previewPerk2Index = -1;
        [SerializeField] private int _previewPerk3Index = -1;
        [SerializeField] private int _previewPerk4Index = -1;
        [SerializeField] private int _previewMasterworkIndex = -1;
        [SerializeField] private int _previewModIndex = -1;
#endif

        // ===================================================
        // BAKED FRAME (base, fixe)
        // ===================================================
        [Header("=== BAKED FRAME (Base) ===")]
        [SerializeField] private string _bakedWeaponName = "";
        [SerializeField] private int _bakedWeaponType = 0;
        [SerializeField] private int _bakedDamageType = 0;
        [SerializeField] private int _bakedSlot = 1;
        [SerializeField] private int _bakedRarity = 4;
        [SerializeField] private string _bakedFrameName = "";
        [SerializeField] private float _bakedFrameImpact;
        [SerializeField] private float _bakedFrameRange;
        [SerializeField] private float _bakedFrameStability;
        [SerializeField] private float _bakedFrameHandling;
        [SerializeField] private float _bakedFrameReloadSpeed;
        [SerializeField] private float _bakedFrameAimAssistance;
        [SerializeField] private float _bakedFrameZoom;
        [SerializeField] private float _bakedFrameAirborneEffectiveness;
        [SerializeField] private float _bakedFrameRecoilDirection;
        [SerializeField] private float _bakedFrameRPM;
        [SerializeField] private float _bakedFrameChargeTime;
        [SerializeField] private float _bakedFrameDrawTime;
        [SerializeField] private int _bakedFrameMagazine;
        [SerializeField] private float _bakedFrameBlastRadius;
        [SerializeField] private float _bakedFrameVelocity;
        [SerializeField] private float _bakedFrameAccuracy;
        [SerializeField] private int _bakedFrameReloadStyle = 0;  // ReloadStyle cast to int
        [SerializeField] private int _bakedFrameExplosionTrigger = 0;  // ExplosionTrigger cast to int
        [SerializeField] private float _bakedFrameExplosionDelay = 0f;

        // ===================================================
        // BAKED POOLS : NOMS
        // ===================================================
        [Header("=== BAKED POOL NAMES ===")]
        [SerializeField] private string[] _bakedPerk1PoolNames = new string[0];
        [SerializeField] private string[] _bakedPerk2PoolNames = new string[0];
        [SerializeField] private string[] _bakedPerk3PoolNames = new string[0];
        [SerializeField] private string[] _bakedPerk4PoolNames = new string[0];
        [SerializeField] private string[] _bakedMasterworkPoolNames = new string[0];
        [SerializeField] private string[] _bakedModPoolNames = new string[0];

        // ===================================================
        // BAKED POOLS : STATS (flat arrays, un élément = toutes les stats)
        // Format : pour N éléments, l'index i est à i*STAT_STRIDE
        // ===================================================
        [Header("=== BAKED POOL STATS (flat) ===")]
        [SerializeField] private float[] _bakedPerk1PoolStats = new float[0];
        [SerializeField] private float[] _bakedPerk2PoolStats = new float[0];
        [SerializeField] private float[] _bakedPerk3PoolStats = new float[0];
        [SerializeField] private float[] _bakedPerk4PoolStats = new float[0];
        [SerializeField] private float[] _bakedMasterworkPoolStats = new float[0];
        [SerializeField] private float[] _bakedModPoolStats = new float[0];

        // ===================================================
        // BAKED MOD DAMAGE MULTIPLIERS (un set par mod dans le pool)
        // ===================================================
        [Header("=== BAKED MOD DMG MULTIPLIERS ===")]
        [SerializeField] private float[] _bakedModPoolBossDmg = new float[0];
        [SerializeField] private float[] _bakedModPoolMajorDmg = new float[0];
        [SerializeField] private float[] _bakedModPoolMinorDmg = new float[0];
        [SerializeField] private float[] _bakedModPoolPlayerDmg = new float[0];

        // ===================================================
        // RUNTIME SELECTION (indices tirés au Start)
        // ===================================================
        [Header("=== RUNTIME ROLLED INDICES ===")]
        [UdonSynced] private int _rolledPerk1Index = -1;
        [UdonSynced] private int _rolledPerk2Index = -1;
        [UdonSynced] private int _rolledPerk3Index = -1;
        [UdonSynced] private int _rolledPerk4Index = -1;
        [UdonSynced] private int _rolledMasterworkIndex = -1;
        [UdonSynced] private int _rolledModIndex = -1;

        // ===================================================
        // RUNTIME COMPUTED FINAL STATS
        // ===================================================
        [Header("=== RUNTIME FINAL STATS (computed at Start) ===")]
        private float _finalImpact;
        private float _finalRange;
        private float _finalStability;
        private float _finalHandling;
        private float _finalReloadSpeed;
        private float _finalAimAssistance;
        private float _finalZoom;
        private float _finalAirborneEffectiveness;
        private float _finalRecoilDirection;
        private float _finalRPM;
        private float _finalChargeTime;
        private float _finalDrawTime;
        private int _finalMagazine;
        private float _finalBlastRadius;
        private float _finalVelocity;
        private float _finalAccuracy;
        private int _finalExplosionTrigger;    // ExplosionTrigger cast to int
        private float _finalExplosionDelay;

        private float _finalBossDmgMult = 1f;
        private float _finalMajorDmgMult = 1f;
        private float _finalMinorDmgMult = 1f;
        private float _finalPlayerDmgMult = 1f;

        // ===================================================
        // RUNTIME STATE
        // ===================================================
        [Header("=== RUNTIME STATE ===")]
        [UdonSynced] private bool _isEquipped = false;
        [UdonSynced] private int _currentAmmo = 0;
        [UdonSynced] private int _ownerPlayerID = -1;

        [Header("=== AMMO ===")]
        [Tooltip("Munitions infinies (pas de consommation, pas de rechargement).")]
        [SerializeField] private bool _infiniteAmmo = false;
        public bool InfiniteAmmo { get { return _infiniteAmmo; } }

        [Header("=== DEBUG ===")]
        [SerializeField] private bool _logWeaponStatsOnStart = true;

        // --- Propriétés publiques ---
        public string WeaponName { get { return _bakedWeaponName; } }
        public int WeaponTypeAsInt { get { return _bakedWeaponType; } }
        public int DamageTypeAsInt { get { return _bakedDamageType; } }
        public int SlotAsInt { get { return _bakedSlot; } }
        public int RarityAsInt { get { return _bakedRarity; } }
        public bool IsEquipped { get { return _isEquipped; } }
        public int CurrentAmmo { get { return _currentAmmo; } }
        public int OwnerPlayerID { get { return _ownerPlayerID; } }

        public float Impact { get { return _finalImpact; } }
        public float Range { get { return _finalRange; } }
        public float Stability { get { return _finalStability; } }
        public float Handling { get { return _finalHandling; } }
        public float ReloadSpeed { get { return _finalReloadSpeed; } }
        public float AimAssistance { get { return _finalAimAssistance; } }
        public float Zoom { get { return _finalZoom; } }
        public float AirborneEffectiveness { get { return _finalAirborneEffectiveness; } }
        public float RecoilDirection { get { return _finalRecoilDirection; } }
        public float RPM { get { return _finalRPM; } }
        public float ChargeTime { get { return _finalChargeTime; } }
        public float DrawTime { get { return _finalDrawTime; } }
        public int Magazine { get { return _finalMagazine; } }
        public float BlastRadius { get { return _finalBlastRadius; } }
        public float Velocity { get { return _finalVelocity; } }
        public int ExplosionTriggerAsInt { get { return _finalExplosionTrigger; } }
        public float ExplosionDelay { get { return _finalExplosionDelay; } }

        // Stats BAKÉES de la frame (utilisées par le gizmo et l'éditeur)
        public float BakedFrameImpact { get { return _bakedFrameImpact; } }
        public float BakedFrameRange { get { return _bakedFrameRange; } }
        public float BakedFrameStability { get { return _bakedFrameStability; } }
        public float BakedFrameHandling { get { return _bakedFrameHandling; } }
        public float BakedFrameReloadSpeed { get { return _bakedFrameReloadSpeed; } }
        public float BakedFrameAimAssistance { get { return _bakedFrameAimAssistance; } }
        public float BakedFrameZoom { get { return _bakedFrameZoom; } }
        public float BakedFrameAirborneEffectiveness { get { return _bakedFrameAirborneEffectiveness; } }
        public float BakedFrameRecoilDirection { get { return _bakedFrameRecoilDirection; } }
        public float BakedFrameRPM { get { return _bakedFrameRPM; } }
        public float BakedFrameChargeTime { get { return _bakedFrameChargeTime; } }
        public float BakedFrameDrawTime { get { return _bakedFrameDrawTime; } }
        public int BakedFrameMagazine { get { return _bakedFrameMagazine; } }
        public float BakedFrameBlastRadius { get { return _bakedFrameBlastRadius; } }
        public float BakedFrameVelocity { get { return _bakedFrameVelocity; } }
        public float BakedFrameAccuracy { get { return _bakedFrameAccuracy; } }
        public int FrameReloadStyle { get { return _bakedFrameReloadStyle; } }
        public int FrameExplosionTrigger { get { return _bakedFrameExplosionTrigger; } }
        public float FrameExplosionDelay { get { return _bakedFrameExplosionDelay; } }

        // Noms des perks/masterwork/mod rollés
        public string RolledPerk1Name { get { return GetPoolName(_bakedPerk1PoolNames, _rolledPerk1Index); } }
        public string RolledPerk2Name { get { return GetPoolName(_bakedPerk2PoolNames, _rolledPerk2Index); } }
        public string RolledPerk3Name { get { return GetPoolName(_bakedPerk3PoolNames, _rolledPerk3Index); } }
        public string RolledPerk4Name { get { return GetPoolName(_bakedPerk4PoolNames, _rolledPerk4Index); } }
        public string RolledMasterworkName { get { return GetPoolName(_bakedMasterworkPoolNames, _rolledMasterworkIndex); } }
        public string RolledModName { get { return GetPoolName(_bakedModPoolNames, _rolledModIndex); } }

        private const int STAT_STRIDE = 16;

        // ===================================================
        // LIFECYCLE
        // ===================================================

        protected override void Start()
        {
            base.Start();

            if (string.IsNullOrEmpty(_bakedWeaponName))
            {
                this.Error("Aucune donnee bakee !");
                return;
            }

            // Roll les perks/masterwork/mod si pas déjà fait (network sync)
            if (_rolledPerk1Index < 0) RollAllPools();

            // Calculer les stats finales
            ComputeFinalStats();

            _currentAmmo = _finalMagazine;

            if (_logWeaponStatsOnStart)
                LogWeaponInfo();
        }

        // ===================================================
        // ROLL SYSTEM
        // ===================================================

        private void RollAllPools()
        {
            _rolledPerk1Index = RollIndex(_bakedPerk1PoolNames.Length);
            _rolledPerk2Index = RollIndex(_bakedPerk2PoolNames.Length);
            _rolledPerk3Index = RollIndex(_bakedPerk3PoolNames.Length);
            _rolledPerk4Index = RollIndex(_bakedPerk4PoolNames.Length);
            _rolledMasterworkIndex = RollIndex(_bakedMasterworkPoolNames.Length);
            _rolledModIndex = RollIndex(_bakedModPoolNames.Length);
            RequestSerialization();
        }

        private int RollIndex(int poolSize)
        {
            if (poolSize <= 0) return -1;
            return Random.Range(0, poolSize);
        }

        private string GetPoolName(string[] pool, int index)
        {
            if (pool == null || index < 0 || index >= pool.Length) return "Aucun";
            return pool[index];
        }

        private void ComputeFinalStats()
        {
            // Partir de la frame (base)
            _finalImpact = _bakedFrameImpact;
            _finalRange = _bakedFrameRange;
            _finalStability = _bakedFrameStability;
            _finalHandling = _bakedFrameHandling;
            _finalReloadSpeed = _bakedFrameReloadSpeed;
            _finalAimAssistance = _bakedFrameAimAssistance;
            _finalZoom = _bakedFrameZoom;
            _finalAirborneEffectiveness = _bakedFrameAirborneEffectiveness;
            _finalRecoilDirection = _bakedFrameRecoilDirection;
            _finalRPM = _bakedFrameRPM;
            _finalChargeTime = _bakedFrameChargeTime;
            _finalDrawTime = _bakedFrameDrawTime;
            _finalMagazine = _bakedFrameMagazine;
            _finalBlastRadius = _bakedFrameBlastRadius;
            _finalVelocity = _bakedFrameVelocity;
            _finalAccuracy = _bakedFrameAccuracy;
            _finalExplosionTrigger = _bakedFrameExplosionTrigger;
            _finalExplosionDelay = _bakedFrameExplosionDelay;

            // Ajouter les perks rollés
            AddPoolStats(_bakedPerk1PoolStats, _rolledPerk1Index);
            AddPoolStats(_bakedPerk2PoolStats, _rolledPerk2Index);
            AddPoolStats(_bakedPerk3PoolStats, _rolledPerk3Index);
            AddPoolStats(_bakedPerk4PoolStats, _rolledPerk4Index);
            AddPoolStats(_bakedMasterworkPoolStats, _rolledMasterworkIndex);
            AddPoolStats(_bakedModPoolStats, _rolledModIndex);

            // Appliquer les multiplicateurs de dégâts du mod rollé
            _finalBossDmgMult = GetModDmgMult(_bakedModPoolBossDmg, _rolledModIndex, 1f);
            _finalMajorDmgMult = GetModDmgMult(_bakedModPoolMajorDmg, _rolledModIndex, 1f);
            _finalMinorDmgMult = GetModDmgMult(_bakedModPoolMinorDmg, _rolledModIndex, 1f);
            _finalPlayerDmgMult = GetModDmgMult(_bakedModPoolPlayerDmg, _rolledModIndex, 1f);
        }

        private void AddPoolStats(float[] pool, int index)
        {
            if (pool == null || index < 0) return;
            int offset = index * STAT_STRIDE;
            if (offset + STAT_STRIDE > pool.Length) return;

            _finalImpact += pool[offset];
            _finalRange += pool[offset + 1];
            _finalStability += pool[offset + 2];
            _finalHandling += pool[offset + 3];
            _finalReloadSpeed += pool[offset + 4];
            _finalAimAssistance += pool[offset + 5];
            _finalZoom += pool[offset + 6];
            _finalAirborneEffectiveness += pool[offset + 7];
            _finalRecoilDirection += pool[offset + 8];
            _finalRPM += pool[offset + 9];
            _finalChargeTime += pool[offset + 10];
            _finalDrawTime += pool[offset + 11];
            _finalMagazine += (int)pool[offset + 12];
            _finalBlastRadius += pool[offset + 13];
            _finalVelocity += pool[offset + 14];
            _finalAccuracy += pool[offset + 15];
        }

        private float GetModDmgMult(float[] pool, int index, float defaultVal)
        {
            if (pool == null || index < 0 || index >= pool.Length) return defaultVal;
            return pool[index];
        }

        // ===================================================
        // PUBLIC METHODS
        // ===================================================

        public void Equip(VRCPlayerApi player)
        {
            if (player == null) return;
            Networking.SetOwner(player, gameObject);
            _isEquipped = true;
            _ownerPlayerID = player.playerId;
            this.Log(_bakedWeaponName + " equipee par " + player.displayName);
            RequestSerialization();
        }

        public void Unequip()
        {
            _isEquipped = false;
            _ownerPlayerID = -1;
            this.Log(_bakedWeaponName + " desequipee.");
            RequestSerialization();
        }

        public bool Fire()
        {
            if (!_infiniteAmmo && _currentAmmo <= 0)
            {
                this.Log("Plus de munitions !");
                return false;
            }
            if (!_infiniteAmmo)
                _currentAmmo = _currentAmmo - 1;
            this.Log("Tir ! Munitions: " + (_infiniteAmmo ? "∞" : _currentAmmo.ToString() + "/" + _finalMagazine.ToString()));
            RequestSerialization();
            return true;
        }

        public bool NeedsReload()
        {
            return !_infiniteAmmo && _currentAmmo <= 0;
        }

        public void Reload()
        {
            _currentAmmo = _finalMagazine;
            this.Log("Rechargement termine.");
            RequestSerialization();
        }

        /// <summary>
        /// Ajoute UNE balle dans le chargeur (rechargement séquentiel).
        /// </summary>
        public void ReloadOne()
        {
            if (_currentAmmo < _finalMagazine)
                _currentAmmo = _currentAmmo + 1;
            RequestSerialization();
        }

        /// <summary>
        /// Retourne le multiplicateur de dégâts.
        /// EnemyTypeConst : Minor=0, Major=1, Champion=2, Boss=3, Vehicle=4, Player=5
        /// </summary>
        public float GetDamageMultiplier(int enemyType)
        {
            if (enemyType == 3) return _finalBossDmgMult;
            if (enemyType == 4) return _finalBossDmgMult;
            if (enemyType == 1) return _finalMajorDmgMult;
            if (enemyType == 2) return _finalMajorDmgMult;
            if (enemyType == 5) return _finalPlayerDmgMult;
            return _finalMinorDmgMult;
        }

        // ===================================================
        // LOGGING
        // ===================================================

        private void LogWeaponInfo()
        {
            this.Log("══════ " + _bakedWeaponName + " ══════");
            this.Log("Type: " + _bakedWeaponType.ToString());
            this.Log("Frame: " + _bakedFrameName);
            this.Log("Perk 1: " + RolledPerk1Name + " [" + _rolledPerk1Index.ToString() + "/" + _bakedPerk1PoolNames.Length.ToString() + "]");
            this.Log("Perk 2: " + RolledPerk2Name + " [" + _rolledPerk2Index.ToString() + "/" + _bakedPerk2PoolNames.Length.ToString() + "]");
            this.Log("Perk 3: " + RolledPerk3Name + " [" + _rolledPerk3Index.ToString() + "/" + _bakedPerk3PoolNames.Length.ToString() + "]");
            this.Log("Perk 4: " + RolledPerk4Name + " [" + _rolledPerk4Index.ToString() + "/" + _bakedPerk4PoolNames.Length.ToString() + "]");
            this.Log("Masterwork: " + RolledMasterworkName);
            this.Log("Mod: " + RolledModName);
            this.Log("Impact:" + _finalImpact.ToString() + " Range:" + _finalRange.ToString() + " Stability:" + _finalStability.ToString());
            this.Log("Handling:" + _finalHandling.ToString() + " Reload:" + _finalReloadSpeed.ToString() + " RPM:" + _finalRPM.ToString());
            this.Log("Mag:" + _finalMagazine.ToString() + " AA:" + _finalAimAssistance.ToString());
            this.Log("DmgMult Boss:" + _finalBossDmgMult.ToString() + " Major:" + _finalMajorDmgMult.ToString() + " Player:" + _finalPlayerDmgMult.ToString());
            this.Log("════════════════════════");
        }

        // ===================================================
        // EDITOR-ONLY : BAKING
        // ===================================================
#if !COMPILER_UDONSHARP && UNITY_EDITOR

        [ContextMenu("Bake Weapon Data")]
        public void BakeWeaponData()
        {
            if (_weaponDefinition == null)
            {
                ClearBakedData();
                return;
            }

            var def = _weaponDefinition;

            // Identity
            _bakedWeaponName = def.WeaponName;
            _bakedWeaponType = (int)def.WeaponType;
            _bakedDamageType = (int)def.DamageType;
            _bakedSlot = (int)def.Slot;
            _bakedRarity = (int)def.Rarity;
            _bakedFrameName = (def.Frame != null) ? def.Frame.FrameName : "Aucune";

            // Frame stats
            if (def.Frame != null)
            {
                var fs = def.Frame.BaseStats;
                _bakedFrameImpact = fs.Impact;
                _bakedFrameRange = fs.Range;
                _bakedFrameStability = fs.Stability;
                _bakedFrameHandling = fs.Handling;
                _bakedFrameReloadSpeed = fs.ReloadSpeed;
                _bakedFrameAimAssistance = fs.AimAssistance;
                _bakedFrameZoom = fs.Zoom;
                _bakedFrameAirborneEffectiveness = fs.AirborneEffectiveness;
                _bakedFrameRecoilDirection = fs.RecoilDirection;
                _bakedFrameRPM = fs.RPM;
                _bakedFrameChargeTime = fs.ChargeTime;
                _bakedFrameDrawTime = fs.DrawTime;
                _bakedFrameMagazine = fs.Magazine;
                _bakedFrameBlastRadius = fs.BlastRadius;
                _bakedFrameVelocity = fs.Velocity;
                _bakedFrameAccuracy = fs.Accuracy;
                _bakedFrameReloadStyle = (int)def.Frame.ReloadStyle;
                _bakedFrameExplosionTrigger = (int)def.Frame.ExplosionTrigger;
                _bakedFrameExplosionDelay = def.Frame.ExplosionDelay;
            }

            // Bake perk pools
            BakePerkPool(def.PerkColumn1Pool, ref _bakedPerk1PoolNames, ref _bakedPerk1PoolStats);
            BakePerkPool(def.PerkColumn2Pool, ref _bakedPerk2PoolNames, ref _bakedPerk2PoolStats);
            BakePerkPool(def.PerkColumn3Pool, ref _bakedPerk3PoolNames, ref _bakedPerk3PoolStats);
            BakePerkPool(def.PerkColumn4Pool, ref _bakedPerk4PoolNames, ref _bakedPerk4PoolStats);

            // Bake masterwork pool
            BakeMasterworkPool(def.MasterworkPool, ref _bakedMasterworkPoolNames, ref _bakedMasterworkPoolStats);

            // Bake mod pool
            BakeModPool(def.ModPool, ref _bakedModPoolNames, ref _bakedModPoolStats,
                ref _bakedModPoolBossDmg, ref _bakedModPoolMajorDmg,
                ref _bakedModPoolMinorDmg, ref _bakedModPoolPlayerDmg);

            UnityEditor.EditorUtility.SetDirty(this);
        }

        private void BakePerkPool(WeaponPerkData[] pool, ref string[] names, ref float[] stats)
        {
            if (pool == null || pool.Length == 0)
            {
                names = new string[0];
                stats = new float[0];
                return;
            }

            names = new string[pool.Length];
            stats = new float[pool.Length * STAT_STRIDE];

            for (int i = 0; i < pool.Length; i++)
            {
                var p = pool[i];
                names[i] = (p != null) ? p.PerkName : "Vide";
                int offset = i * STAT_STRIDE;
                if (p != null)
                {
                    var s = p.StatModifiers;
                    stats[offset] = s.Impact;
                    stats[offset + 1] = s.Range;
                    stats[offset + 2] = s.Stability;
                    stats[offset + 3] = s.Handling;
                    stats[offset + 4] = s.ReloadSpeed;
                    stats[offset + 5] = s.AimAssistance;
                    stats[offset + 6] = s.Zoom;
                    stats[offset + 7] = s.AirborneEffectiveness;
                    stats[offset + 8] = s.RecoilDirection;
                    stats[offset + 9] = s.RPM;
                    stats[offset + 10] = s.ChargeTime;
                    stats[offset + 11] = s.DrawTime;
                    stats[offset + 12] = s.Magazine;
                    stats[offset + 13] = s.BlastRadius;
                    stats[offset + 14] = s.Velocity;
                    stats[offset + 15] = s.Accuracy;
                }
            }
        }

        private void BakeMasterworkPool(WeaponMasterworkData[] pool, ref string[] names, ref float[] stats)
        {
            if (pool == null || pool.Length == 0)
            {
                names = new string[0];
                stats = new float[0];
                return;
            }

            names = new string[pool.Length];
            stats = new float[pool.Length * STAT_STRIDE];

            for (int i = 0; i < pool.Length; i++)
            {
                var mw = pool[i];
                names[i] = (mw != null) ? mw.MasterworkName : "Vide";
                int offset = i * STAT_STRIDE;
                if (mw != null)
                {
                    var s = mw.GetStatModifiers();
                    stats[offset] = s.Impact;
                    stats[offset + 1] = s.Range;
                    stats[offset + 2] = s.Stability;
                    stats[offset + 3] = s.Handling;
                    stats[offset + 4] = s.ReloadSpeed;
                    stats[offset + 5] = s.AimAssistance;
                    stats[offset + 6] = s.Zoom;
                    stats[offset + 7] = s.AirborneEffectiveness;
                    stats[offset + 8] = s.RecoilDirection;
                    stats[offset + 9] = s.RPM;
                    stats[offset + 10] = s.ChargeTime;
                    stats[offset + 11] = s.DrawTime;
                    stats[offset + 12] = s.Magazine;
                    stats[offset + 13] = s.BlastRadius;
                    stats[offset + 14] = s.Velocity;
                    stats[offset + 15] = s.Accuracy;
                }
            }
        }

        private void BakeModPool(WeaponModData[] pool, ref string[] names, ref float[] stats,
            ref float[] bossDmg, ref float[] majorDmg, ref float[] minorDmg, ref float[] playerDmg)
        {
            if (pool == null || pool.Length == 0)
            {
                names = new string[0];
                stats = new float[0];
                bossDmg = new float[0];
                majorDmg = new float[0];
                minorDmg = new float[0];
                playerDmg = new float[0];
                return;
            }

            names = new string[pool.Length];
            stats = new float[pool.Length * STAT_STRIDE];
            bossDmg = new float[pool.Length];
            majorDmg = new float[pool.Length];
            minorDmg = new float[pool.Length];
            playerDmg = new float[pool.Length];

            for (int i = 0; i < pool.Length; i++)
            {
                var mod = pool[i];
                names[i] = (mod != null) ? mod.ModName : "Vide";
                bossDmg[i] = (mod != null) ? mod.BossDamageMultiplier : 1f;
                majorDmg[i] = (mod != null) ? mod.MajorDamageMultiplier : 1f;
                minorDmg[i] = (mod != null) ? mod.MinorDamageMultiplier : 1f;
                playerDmg[i] = (mod != null) ? mod.PlayerDamageMultiplier : 1f;

                int offset = i * STAT_STRIDE;
                if (mod != null)
                {
                    var s = mod.StatModifiers;
                    stats[offset] = s.Impact;
                    stats[offset + 1] = s.Range;
                    stats[offset + 2] = s.Stability;
                    stats[offset + 3] = s.Handling;
                    stats[offset + 4] = s.ReloadSpeed;
                    stats[offset + 5] = s.AimAssistance;
                    stats[offset + 6] = s.Zoom;
                    stats[offset + 7] = s.AirborneEffectiveness;
                    stats[offset + 8] = s.RecoilDirection;
                    stats[offset + 9] = s.RPM;
                    stats[offset + 10] = s.ChargeTime;
                    stats[offset + 11] = s.DrawTime;
                    stats[offset + 12] = s.Magazine;
                    stats[offset + 13] = s.BlastRadius;
                    stats[offset + 14] = s.Velocity;
                    stats[offset + 15] = s.Accuracy;
                }
            }
        }

        private void ClearBakedData()
        {
            _bakedWeaponName = "";
            _bakedWeaponType = 0;
            _bakedDamageType = 0;
            _bakedSlot = 1;
            _bakedRarity = 4;
            _bakedFrameName = "";
            _bakedFrameImpact = 0f; _bakedFrameRange = 0f; _bakedFrameStability = 0f;
            _bakedFrameHandling = 0f; _bakedFrameReloadSpeed = 0f; _bakedFrameAimAssistance = 0f;
            _bakedFrameZoom = 0f; _bakedFrameAirborneEffectiveness = 0f; _bakedFrameRecoilDirection = 0f;
            _bakedFrameRPM = 0f; _bakedFrameChargeTime = 0f; _bakedFrameDrawTime = 0f;
            _bakedFrameMagazine = 0; _bakedFrameBlastRadius = 0f; _bakedFrameVelocity = 0f; _bakedFrameAccuracy = 0f;
            _bakedFrameReloadStyle = 0;
            _bakedFrameExplosionTrigger = 0;
            _bakedFrameExplosionDelay = 0f;

            _bakedPerk1PoolNames = new string[0]; _bakedPerk1PoolStats = new float[0];
            _bakedPerk2PoolNames = new string[0]; _bakedPerk2PoolStats = new float[0];
            _bakedPerk3PoolNames = new string[0]; _bakedPerk3PoolStats = new float[0];
            _bakedPerk4PoolNames = new string[0]; _bakedPerk4PoolStats = new float[0];
            _bakedMasterworkPoolNames = new string[0]; _bakedMasterworkPoolStats = new float[0];
            _bakedModPoolNames = new string[0]; _bakedModPoolStats = new float[0];
            _bakedModPoolBossDmg = new float[0]; _bakedModPoolMajorDmg = new float[0];
            _bakedModPoolMinorDmg = new float[0]; _bakedModPoolPlayerDmg = new float[0];

            UnityEditor.EditorUtility.SetDirty(this);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (_weaponDefinition != null)
                BakeWeaponData();
        }

#endif // !COMPILER_UDONSHARP && UNITY_EDITOR
    }

    /// <summary>
    /// Constantes pour les types de cibles (compatible UdonSharp).
    /// </summary>
    public static class EnemyTypeConst
    {
        public const int Minor = 0;
        public const int Major = 1;
        public const int Champion = 2;
        public const int Boss = 3;
        public const int Vehicle = 4;
        public const int Player = 5;
    }
}
