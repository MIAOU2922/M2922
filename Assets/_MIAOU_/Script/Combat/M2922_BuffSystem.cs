using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Combat
{
    public class M2922_BuffSystem : M2922_System
    {
        // === IBuffable (inline) ===
        // Parallel arrays — Udon ne supporte pas Dictionary ni generics

        // === PRESET BUFFS (définis dans l'Inspector) ===
        [Header("=== PRESET BUFFS ===")]
        [Tooltip("Types des buffs à appliquer au démarrage")]
        [SerializeField] private BuffType[]   _presetTypes       = new BuffType[0];
        [Tooltip("Magnitude de chaque buff preset (même index)")]
        [SerializeField] private float[]      _presetMagnitudes  = new float[0];
        [Tooltip("Durée de chaque buff preset en secondes (-1 = permanent)")]
        [SerializeField] private float[]      _presetDurations   = new float[0];
        [Tooltip("Type de dégât pour les PassiveDamage uniquement (ignoré sinon)")]
        [SerializeField] private DamageType[] _presetDamageTypes = new DamageType[0];

        // === RUNTIME BUFFS ===
        private const int MAX_BUFF_SLOTS = 16;
        private bool[]       _active     = new bool[MAX_BUFF_SLOTS];
        private BuffType[]   _types      = new BuffType[MAX_BUFF_SLOTS];
        private float[]      _magnitudes = new float[MAX_BUFF_SLOTS];
        private float[]      _timers     = new float[MAX_BUFF_SLOTS];
        private DamageType[] _damageTypes = new DamageType[MAX_BUFF_SLOTS];

        public bool HasActiveBuff
        {
            get { for (int i = 0; i < MAX_BUFF_SLOTS; i++) if (_active[i]) return true; return false; }
        }

        public void ApplyBuff(BuffType buffType, float magnitude, float duration)
            => ApplyBuffWithDamage(buffType, magnitude, duration, DamageType.Generic);

        public void ApplyBuffWithDamage(BuffType buffType, float magnitude, float duration, DamageType damageType)
        {
            // Override si le buff est déjà actif
            for (int i = 0; i < MAX_BUFF_SLOTS; i++)
            {
                if (_active[i] && _types[i] == buffType)
                { _magnitudes[i] = magnitude; _timers[i] = duration; _damageTypes[i] = damageType; return; }
            }
            // Sinon, premier slot libre
            for (int i = 0; i < MAX_BUFF_SLOTS; i++)
            {
                if (!_active[i])
                { _active[i] = true; _types[i] = buffType; _magnitudes[i] = magnitude; _timers[i] = duration; _damageTypes[i] = damageType; return; }
            }
            this.Log($"ApplyBuff: no free slot for {buffType}");
        }

        /// Retourne le DamageType du premier PassiveDamage actif.
        public DamageType GetPassiveDamageType()
        {
            for (int i = 0; i < MAX_BUFF_SLOTS; i++)
                if (_active[i] && _types[i] == BuffType.PassiveDamage) return _damageTypes[i];
            return DamageType.Generic;
        }

        public void RemoveBuff(BuffType buffType)
        {
            for (int i = 0; i < MAX_BUFF_SLOTS; i++)
                if (_active[i] && _types[i] == buffType) _active[i] = false;
        }

        public bool HasBuff(BuffType buffType)
        {
            for (int i = 0; i < MAX_BUFF_SLOTS; i++)
                if (_active[i] && _types[i] == buffType) return true;
            return false;
        }

        /// <summary>
        /// Pour MoveSpeed, JumpHeight, MaxHealth, MaxArmor : retourne un multiplicateur (1.0 = neutre).
        /// Pour HealRate, PassiveDamage, ArmorHealRate : retourne la somme des magnitudes (HP ou AP par seconde).
        /// Pour Invincibility, Stun, Flash : retourne > 0 si le buff est actif.
        /// </summary>
        public float GetStatMultiplier(StatType statType)
        {
            float result = (statType == StatType.Speed ||
                            statType == StatType.JumpHeight ||
                            statType == StatType.MaxHealth ||
                            statType == StatType.MaxArmor) ? 1f : 0f;

            for (int i = 0; i < MAX_BUFF_SLOTS; i++)
            {
                if (!_active[i]) continue;
                switch (statType)
                {
                    case StatType.Speed:         if (_types[i] == BuffType.MoveSpeed)     result *= _magnitudes[i]; break;
                    case StatType.JumpHeight:    if (_types[i] == BuffType.JumpHeight)    result *= _magnitudes[i]; break;
                    case StatType.MaxHealth:     if (_types[i] == BuffType.MaxHealth)     result *= _magnitudes[i]; break;
                    case StatType.MaxArmor:      if (_types[i] == BuffType.MaxArmor)      result *= _magnitudes[i]; break;
                    case StatType.HealRate:      if (_types[i] == BuffType.HealthRegen)   result += _magnitudes[i]; break;
                    case StatType.PassiveDamage: if (_types[i] == BuffType.PassiveDamage) result += _magnitudes[i]; break;
                    case StatType.ArmorHealRate: if (_types[i] == BuffType.ArmorRegen)    result += _magnitudes[i]; break;
                    case StatType.Invincibility: if (_types[i] == BuffType.Invincibility) result += 1f;            break;
                    case StatType.Stun:          if (_types[i] == BuffType.Stun)          result += 1f;            break;
                    case StatType.Flash:         if (_types[i] == BuffType.Flash)         result += 1f;            break;
                }
            }
            return result;
        }

        public void ClearAllBuffs()
        {
            for (int i = 0; i < MAX_BUFF_SLOTS; i++) _active[i] = false;
            this.VerboseLog("ClearAllBuffs");
        }

        // === METHODE ===
        protected override void Awake()
        {
            ApplyPresetBuffs();
        }

        protected override void Start()
        {
            base.Start();
        }

        private void ApplyPresetBuffs()
        {
            int count = Mathf.Min(_presetTypes.Length, Mathf.Min(_presetMagnitudes.Length, _presetDurations.Length));
            for (int i = 0; i < count; i++)
            {
                DamageType dmgType = (i < _presetDamageTypes.Length) ? _presetDamageTypes[i] : DamageType.Generic;
                ApplyBuffWithDamage(_presetTypes[i], _presetMagnitudes[i], _presetDurations[i], dmgType);
            }
            if (count > 0) this.VerboseLog($"ApplyPresetBuffs: {count} buff(s) appliqués");
        }

        protected override void Update()
        {
            base.Update();
            float dt = Time.deltaTime;
            for (int i = 0; i < MAX_BUFF_SLOTS; i++)
            {
                if (!_active[i]) continue;
                if (_timers[i] < 0f) continue; // permanent
                _timers[i] -= dt;
                if (_timers[i] <= 0f) { _active[i] = false; this.VerboseLog($"Buff expired: {_types[i]}"); }
            }
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (UnityEngine.Application.isPlaying)
            {
                ClearAllBuffs();
                ApplyPresetBuffs();
            }
        }
        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            if (!_showGizmo) return;

            int activeCount = 0;
            string buffList = "";

            if (UnityEngine.Application.isPlaying)
            {
                if (_active == null || _types == null || _magnitudes == null || _timers == null || _damageTypes == null) return;
                int len = Mathf.Min(_active.Length, Mathf.Min(_types.Length, Mathf.Min(_magnitudes.Length, Mathf.Min(_timers.Length, _damageTypes.Length))));
                for (int i = 0; i < len; i++)
                {
                    if (!_active[i]) continue;
                    activeCount++;
                    string dur = _timers[i] < 0f ? "perm" : $"{_timers[i]:F0}s";
                    buffList += $"\n  {_types[i]}: {FormatMagnitude(_types[i], _magnitudes[i], _damageTypes[i])} [{dur}]";
                }
            }
            else
            {
                if (_presetTypes == null) return;
                int len = Mathf.Min(_presetTypes.Length, Mathf.Min(_presetMagnitudes.Length, _presetDurations.Length));
                for (int i = 0; i < len; i++)
                {
                    activeCount++;
                    DamageType dmgType = (i < _presetDamageTypes.Length) ? _presetDamageTypes[i] : DamageType.Generic;
                    string dur = _presetDurations[i] < 0f ? "perm" : $"{_presetDurations[i]:F0}s";
                    buffList += $"\n  {_presetTypes[i]}: {FormatMagnitude(_presetTypes[i], _presetMagnitudes[i], dmgType)} [{dur}]";
                }
            }

            string header = activeCount > 0 ? $"[Buffs] {activeCount}" : "[Buffs] aucun";
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (_gizmoSize * 10f),
                header + buffList
            );
        }

        private string FormatMagnitude(BuffType buffType, float magnitude, DamageType dmgType)
        {
            switch (buffType)
            {
                case BuffType.HealthRegen:   return $"{magnitude:F1} HP/s";
                case BuffType.ArmorRegen:    return $"{magnitude:F1} AP/s";
                case BuffType.PassiveDamage: return $"{magnitude:F1} DMG/s [{dmgType}]";
                case BuffType.MoveSpeed:     return $"x{magnitude:F2} vitesse";
                case BuffType.JumpHeight:    return $"x{magnitude:F2} saut";
                case BuffType.MaxHealth:     return $"x{magnitude:F2} HP max";
                case BuffType.MaxArmor:      return $"x{magnitude:F2} AP max";
                case BuffType.Invincibility: return "invincible";
                case BuffType.Stun:          return "stun";
                case BuffType.Flash:         return "flash";
                default:                     return $"{magnitude:F2}";
            }
        }
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (!_showGizmo) return;
        }
#endif
    }
}