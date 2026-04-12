using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Combat
{
    /// <summary>
    /// Collider de zone de hit — générique, fonctionne sur tout type d'entité.
    /// Reçoit les impacts et les transmet à l'entité parente.
    ///
    /// CIBLES SUPPORTÉES (auto-détectées sur les parents) :
    ///   M2922_PlayerController → pipeline joueur (FF + Buff + Armor + HP)
    ///   M2922_Entity           → pipeline générique (Buff + Armor + HP)
    ///   Si PlayerController est trouvé, Entity est ignorée.
    ///
    /// NOTE UdonSharp :
    ///   GetComponentInParent<BaseType>() ne détecte pas les sous-classes compilées séparément.
    ///   C'est pourquoi on cherche PlayerController EN PREMIER, puis Entity.
    ///
    /// SETUP :
    ///   1. Crée des GameObjects enfants sur l'entité
    ///   2. Ajoute un Collider sur chaque enfant
    ///   3. Ajoute ce script sur chaque enfant
    ///   4. Configure _zoneType et _damageMultiplier dans l'inspecteur
    ///   5. Cibles auto-détectées (ou assignées manuellement)
    ///
    /// HIÉRARCHIE TYPE :
    ///   AnyEntityRoot
    ///   ├── M2922_PlayerController  OU  M2922_Entity
    ///   ├── M2922_HealthController
    ///   ├── M2922_ArmorSystem       (optionnel)
    ///   ├── M2922_BuffSystem        (optionnel)
    ///   ├── HitZone (Collider + M2922_HitZone zoneType=Head damageMultiplier=2.0)
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_HitZone : UdonSharpBehaviour
    {
        [Header("=== HIT ZONE CONFIGURATION ===")]

        [Tooltip("Multiplicateur de dégâts pour cette zone.\n"
               + "Tête=2.0  Corps=1.0  Membres=0.75  Blindé=0.5")]
        [SerializeField] private float _damageMultiplier = 1f;

        [Header("=== CIBLE (auto-détectée sur les parents) ===")]

        [Tooltip("Joueur — pipeline complet avec FF check. Auto-détecté.\n"
               + "Si présent, Entity est ignorée.")]
        [SerializeField] private M2922.Player.M2922_PlayerController _playerController;

        [Tooltip("Entité générique (prop, véhicule, NPC…) — pipeline Buff+Armor+HP. Auto-détectée.")]
        [SerializeField] private M2922.Core.M2922_Entity _entity;

        [Header("=== DEBUG ===")]
        [Tooltip("Affiche un log à chaque impact reçu.")]
        [SerializeField] private bool _debugHits = false;

        // === LIFECYCLE ===

        private void Start()
        {
            if (_playerController == null)
                _playerController = GetComponentInParent<M2922.Player.M2922_PlayerController>();

            if (_entity == null)
                _entity = GetComponentInParent<M2922.Core.M2922_Entity>();

            if (_playerController == null && _entity == null)
                Debug.LogWarning($"[HitZone:{gameObject.name}] Aucune cible trouvée "
                               + "(M2922_PlayerController ou M2922_Entity attendu sur les parents).", this);
        }

        // === INTERFACE : IHittable ===

        /// <summary>Ce collider peut-il recevoir des hits ?</summary>
        public bool IsHittable
        {
            get
            {
                if (!isActiveAndEnabled) return false;
                if (_playerController != null) return _playerController.IsAlive;
                if (_entity           != null) return _entity.IsAlive;
                return false;
            }
        }

        /// <summary>Multiplicateur de dégâts de cette zone.</summary>
        public float DamageMultiplier => _damageMultiplier;

        /// <summary>
        /// Appelé par une arme ou un projectile lors d'un impact sur ce collider.
        /// Route automatiquement vers le bon pipeline selon la cible.
        /// </summary>
        /// <param name="rawDamage">Dégâts bruts de la source (avant multiplicateur de zone)</param>
        /// <param name="attackerId">PlayerId de l'attaquant (-1 = environnement)</param>
        /// <param name="damageType">Type de dégât</param>
        /// <param name="hitPoint">Position mondiale de l'impact (pour VFX)</param>
        /// <param name="hitNormal">Normale de surface au point d'impact (pour VFX)</param>
        public void OnHit(float rawDamage, int attackerId, DamageType damageType,
                          Vector3 hitPoint, Vector3 hitNormal)
        {
            if (!IsHittable) return;

            float zoneDamage = rawDamage * _damageMultiplier;

            if (_debugHits)
                Debug.Log($"[HitZone:{gameObject.name}] "
                        + $"raw={rawDamage:F1}×{_damageMultiplier}={zoneDamage:F1} "
                        + $"type={damageType} attacker={attackerId}");

            // Joueur — pipeline avec FF check
            if (_playerController != null)
            {
                _playerController.TakeDamage(zoneDamage, attackerId, damageType);
                return;
            }

            // Entité générique — pipeline Buff+Armor+HP
            if (_entity != null)
                _entity.TakeDamage(zoneDamage, attackerId, damageType);
        }

        /// <summary>Version simplifiée sans info de surface (raycast simple).</summary>
        public void OnHitSimple(float rawDamage, int attackerId, DamageType damageType)
            => OnHit(rawDamage, attackerId, damageType, Vector3.zero, Vector3.up);
    }
}
