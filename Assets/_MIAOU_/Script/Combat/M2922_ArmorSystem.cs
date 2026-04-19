using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Combat
{
    public class M2922_ArmorSystem : M2922_System
    {
        // === IArmored (inline) ===
        [Header("=== ARMOR SETTINGS ===")]
        [SerializeField] private float _baseMaxArmorPoints = 100f;

        [Tooltip("Absorption de base (0-1)")]
        [Range(0f, 1f)]
        [SerializeField] private float _absorption = 0.5f;

        [Tooltip("Multiplicateur d'absorption par type de dégât (ordre: Generic, Bullet, Explosion, Melee, Fire, Energy, Fall)\n" +
                 "1.0 = normal | 0.0 = aucune absorption | 2.0 = double résistance")]
        [SerializeField] private float[] _damageTypeResistance = { 1f, 1f, 1f, 1f, 1f, 1f, 1f };

        private float _maxArmorPoints;  // effective — modifiable par buffs/systèmes
        private float _armorPoints;

        public float ArmorPoints    => _armorPoints;
        public float MaxArmorPoints => _maxArmorPoints;
        public bool HasArmor        => _armorPoints > 0f;

        public void SetResistance(DamageType damageType, float multiplier)
        {
            int idx = (int)damageType;
            if (idx < 0 || idx >= _damageTypeResistance.Length) return;
            _damageTypeResistance[idx] = Mathf.Max(0f, multiplier);
            this.VerboseLog($"SetResistance: {damageType} = {_damageTypeResistance[idx]}");
        }

        private float GetEffectiveAbsorption(DamageType damageType)
        {
            int idx = (int)damageType;
            float resistance = (idx >= 0 && idx < _damageTypeResistance.Length) ? _damageTypeResistance[idx] : 1f;
            return Mathf.Clamp01(_absorption * resistance);
        }

        public void SetMaxArmorPoints(float newMax)
        {
            float ratio = _maxArmorPoints > 0f ? _armorPoints / _maxArmorPoints : 1f;
            _maxArmorPoints = Mathf.Max(0f, newMax);
            _armorPoints = _maxArmorPoints * ratio;  // conserve le % d'armure restant
            this.VerboseLog($"SetMaxArmorPoints: {_maxArmorPoints} | Armor: {_armorPoints}");
        }

        public void ResetMaxArmorPoints()
        {
            SetMaxArmorPoints(_baseMaxArmorPoints);
        }

        public float AbsorbDamage(float incomingDamage, DamageType damageType)
        {
            if (!HasArmor) return incomingDamage;
            float effectiveAbsorption = GetEffectiveAbsorption(damageType);
            float absorbed = Mathf.Min(_armorPoints, incomingDamage * effectiveAbsorption);
            _armorPoints -= absorbed;
            this.VerboseLog($"AbsorbDamage: absorbed {absorbed} ({damageType}, x{effectiveAbsorption}) | Armor: {_armorPoints}/{_maxArmorPoints}");
            return incomingDamage - absorbed;
        }

        public void RepairArmor(float amount)
        {
            _armorPoints = Mathf.Min(_maxArmorPoints, _armorPoints + amount);
            this.VerboseLog($"RepairArmor: +{amount} | Armor: {_armorPoints}/{_maxArmorPoints}");
        }

        public void RepairFull()
        {
            _armorPoints = _maxArmorPoints;
            this.VerboseLog($"RepairFull | Armor: {_armorPoints}/{_maxArmorPoints}");
        }

        // === METHODE ===
        protected override void Start()
        {
            base.Start();
            _maxArmorPoints = _baseMaxArmorPoints;
            _armorPoints    = _maxArmorPoints;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
        }
        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            if (!_showGizmo) return;
            // Offset slot 8x — au-dessus du label Health (6x)
            string status = HasArmor ? $"AP: {_armorPoints:F0}/{_maxArmorPoints:F0}" : "AP: NONE";
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (_gizmoSize * 8f),
                $"[Armor] {status} | Abs: {(_absorption * 100f):F0}%"
            );
        }
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (!_showGizmo) return;
        }
#endif
    }
}