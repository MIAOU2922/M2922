using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Health
{
    /// <summary>
    /// Système de hitboxes d'une entité (joueur, prop, véhicule).
    /// Centralise les Collider qui représentent le corps de l'entité.
    /// Permet au projectile/HitDetector d'identifier à qui appartient un collider touché,
    /// et de savoir si c'est une zone critique (tête, point faible).
    ///
    /// SETUP :
    ///   1. Attacher ce composant sur le GameObject de l'entité.
    ///   2. Créer des GO enfants (Head, Body, Legs…) avec des Collider (Is Trigger).
    ///   3. Optionnel : M2922_DamageMultiplier sur les colliders (tête ×2, jambes ×0.5...).
    ///   4. Assigner les colliders dans _hitboxColliders.
    ///
    /// UTILISATION (HitDetector) :
    ///   M2922_HitboxSystem hitboxSys = col.GetComponentInParent&lt;M2922_HitboxSystem&gt;();
    ///   if (hitboxSys != null && hitboxSys.IsMyCollider(col))
    ///       receiver.ApplyDamage(damage, owner, isCrit);
    /// </summary>
    [AddComponentMenu("M2922/Health/Hitbox System")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_HitboxSystem : M2922_Base
    {
        [Header("=== HITBOX CONFIG ===")]
        [Tooltip("Colliders du corps de cette entité. Chaque collider doit être un enfant de ce GameObject.")]
        [SerializeField] private Collider[] _hitboxColliders = new Collider[0];
        [Tooltip("Nom du layer Unity pour les hitboxes.")]
        [SerializeField] private string _hitboxLayerName = "Hitbox";
        [Tooltip("Layer ID résolu automatiquement (ne pas modifier).")]
        [SerializeField] private int _hitboxLayer = 8;

        [Header("=== PROXIMITY HITBOX (rocket only) ===")]
        [Tooltip("Capsule optionnelle pour les roquettes : une roquette qui passe près du joueur\n" +
                 "touche ce collider et explose. Les raycasts (hitscan) l'ignorent.")]
        [SerializeField] private Collider _proximityCollider;
        [Tooltip("Nom du layer Unity pour ce collider de proximité.")]
        [SerializeField] private string _proximityHitboxLayerName = "ProximityHitbox";
        [Tooltip("Layer ID résolu (ne pas modifier).")]
        [SerializeField] private int _proximityHitboxLayer = 0;

        [Header("=== PLAYER BINDING ===")]
        [Tooltip("Player ID auquel ce HitboxSystem est lié (-1 = NPC/destructible). Défini par le système de spawn.")]
        [SerializeField] private int _boundPlayerId = -1;

        [Header("=== ENTITY TYPE ===")]
        [Tooltip("TRUE = joueur (avatar), FALSE = NPC/destructible. Défini dans le prefab. Utilisé par _PollAutoBind pour décider si on bind au joueur ou si on enregistre comme NPC.")]
        [SerializeField] private bool _isPlayer = false;

        [Header("=== ENTITY ID ===")]
        [Tooltip("ID unique pour le relai réseau des NPCs (auto-assigné par le Manager). -1 = pas encore enregistré.")]
        [SerializeField] private int _entityId = -1;

        private int _hitboxCount;

        /// <summary>
        /// Nombre de colliders hitbox. Lit directement le tableau sérialisé
        /// pour être disponible même avant le Start() du HitboxSystem (ordre
        /// d'exécution non déterministe entre composants sur le même GameObject).
        /// </summary>
        public int HitboxCount => _hitboxColliders != null ? _hitboxColliders.Length : 0;
        public Collider[] HitboxColliders => _hitboxColliders;
        public Collider ProximityCollider => _proximityCollider;
        public int BoundPlayerId => _boundPlayerId;
        public int EntityId => _entityId;
        public bool IsPlayer => _isPlayer;

        /// <summary>Lie ce HitboxSystem à un joueur spécifique. Appelé par le système de spawn (joueurs uniquement).</summary>
        public void BindToPlayer(int playerId)
        {
            _boundPlayerId = playerId;
        }

        /// <summary>True si ce HitboxSystem est lié au joueur local.</summary>
        public bool IsBoundToLocalPlayer()
        {
            if (_boundPlayerId < 0) return false;
            return Networking.LocalPlayer != null && _boundPlayerId == Networking.LocalPlayer.playerId;
        }

        /// <summary>
        /// True si le client local est l'autorité réseau pour cette entité.
        /// - Joueur lié : le joueur lui-même est l'autorité
        /// - NPC/destructible (non lié) : le master est l'autorité
        /// </summary>
        public bool IsNetworkingAuthority()
        {
            if (IsBoundToLocalPlayer()) return true;
            // NPC, destructible, ou entité non liée → le master est l'autorité
            return Networking.LocalPlayer != null && Networking.LocalPlayer.isMaster;
        }

        // ============================================================
        // API
        // ============================================================

        /// <summary>True si le collider appartient aux hitboxes de cette entité.</summary>
        public bool IsMyCollider(Collider col)
        {
            if (_hitboxColliders == null) return false;
            for (int i = 0; i < _hitboxColliders.Length; i++)
                if (_hitboxColliders[i] == col) return true;
            return false;
        }

        /// <summary>True si le collider est le proximity hitbox de cette entité (rocket only).</summary>
        public bool IsMyProximityCollider(Collider col)
        {
            return _proximityCollider != null && _proximityCollider == col;
        }

        /// <summary>True si le collider a un M2922_DamageMultiplier.</summary>
        public bool HasDamageMultiplier(Collider col)
        {
            M2922_DamageMultiplier dm = col.GetComponent<M2922_DamageMultiplier>();
            return dm != null;
        }

        /// <summary>Multiplicateur de dégâts du collider (1.0 = normal).</summary>
        public float GetDamageMultiplier(Collider col)
        {
            M2922_DamageMultiplier dm = col.GetComponent<M2922_DamageMultiplier>();
            return dm != null ? dm.Multiplier : 1f;
        }

        // ============================================================
        // LIFECYCLE
        // ============================================================

        private int _bindAttempts = 0;
        private const int MAX_BIND_ATTEMPTS = 30; // ~5 secondes à 10 frames/retry

        protected override void Start()
        {
            base.Start();
            _hitboxCount = _hitboxColliders != null ? _hitboxColliders.Length : 0;

            // Lancer le polling d'auto-bind (l'ownership VRChat est asynchrone)
            if (Networking.LocalPlayer != null && _boundPlayerId < 0)
                SendCustomEventDelayedFrames("_PollAutoBind", 2);

            this.Log($"[HitboxSystem] {_hitboxCount} collider(s), boundId={_boundPlayerId}, entityId={_entityId}");
        }

        public void _PollAutoBind()
        {
            if (_boundPlayerId >= 0) return; // déjà bindé

            VRCPlayerApi owner = Networking.GetOwner(gameObject);

            // _isPlayer gère TOUS les cas sans heuristique fragile :
            //   _isPlayer=true  → BindToPlayer (fonctionne pour local ET remote, y compris master)
            //   _isPlayer=false → RegisterAsNpc (objets de scène, même sur le master)
            //   owner == null   → retry loop (ownership VRChat pas encore assignée)
            if (owner != null)
            {
                if (_isPlayer)
                {
                    BindToPlayer(owner.playerId);
                    this.Log($"[HitboxSystem] Auto-bind joueur réussi après {_bindAttempts} tentatives (id={owner.playerId}, isLocal={owner.isLocal})");

                    // Enregistrer le DamageReceiver auprès du Manager (le Start() était trop tôt)
                    var receiver = GetComponent<M2922_DamageReceiver>();
                    if (receiver != null && Manager != null)
                        Manager.RegisterReceiver(owner.playerId, receiver);

                    return;
                }
                else
                {
                    // NPC/destructible : enregistrer comme NPC (même sur le master)
                    RegisterAsNpc();
                    return;
                }
            }

            // owner == null : l'ownership VRChat n'est pas encore assignée, on réessaie
            _bindAttempts++;
            if (_bindAttempts < MAX_BIND_ATTEMPTS)
            {
                SendCustomEventDelayedFrames("_PollAutoBind", 10);
            }
            else
            {
                this.Warning($"[HitboxSystem] Échec auto-bind après {_bindAttempts} tentatives (owner=NULL). Vérifiez le prefab.");
            }
        }

        private void RegisterAsNpc()
        {
            if (Manager == null || _entityId >= 0) return;
            var receiver = GetComponent<M2922_DamageReceiver>();
            if (receiver != null)
            {
                _entityId = Manager.RegisterNpc(receiver);
                this.Log($"[HitboxSystem] NPC enregistré, entityId={_entityId}");
            }
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            _hitboxCount = _hitboxColliders != null ? _hitboxColliders.Length : 0;

            // Résoudre le layer depuis le nom
            _hitboxLayer = UnityEngine.LayerMask.NameToLayer(_hitboxLayerName);
            if (_hitboxLayer < 0) _hitboxLayer = 8; // fallback

            // Résoudre le layer de proximité
            _proximityHitboxLayer = UnityEngine.LayerMask.NameToLayer(_proximityHitboxLayerName);
            if (_proximityHitboxLayer < 0) _proximityHitboxLayer = 0;

            // Auto-assigner le layer à tous les colliders
            if (_hitboxColliders != null)
            {
                for (int i = 0; i < _hitboxColliders.Length; i++)
                {
                    Collider col = _hitboxColliders[i];
                    if (col != null && col.gameObject.layer != _hitboxLayer)
                    {
                        col.gameObject.layer = _hitboxLayer;
                        UnityEditor.EditorUtility.SetDirty(col.gameObject);
                    }
                }
            }

            // Auto-assigner le layer de proximité au collider de proximité
            if (_proximityCollider != null && _proximityCollider.gameObject.layer != _proximityHitboxLayer)
            {
                _proximityCollider.gameObject.layer = _proximityHitboxLayer;
                UnityEditor.EditorUtility.SetDirty(_proximityCollider.gameObject);
            }
        }

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            DrawHitboxGizmos(0.15f);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            DrawHitboxGizmos(0.5f);
        }

        private void DrawHitboxGizmos(float alpha)
        {
            if (_hitboxColliders == null) return;
            for (int i = 0; i < _hitboxCount; i++)
            {
                Collider col = _hitboxColliders[i];
                if (col == null) continue;

                float dmgMult = GetDamageMultiplier(col);
                bool hasMult = dmgMult != 1f;
                Gizmos.color = hasMult
                    ? new Color(1f, 0.2f, 0.2f, alpha)
                    : new Color(0.2f, 1f, 0.2f, alpha);

                if (col is BoxCollider box)
                {
                    Gizmos.matrix = col.transform.localToWorldMatrix;
                    Gizmos.DrawWireCube(box.center, box.size);
                }
                else if (col is SphereCollider sphere)
                {
                    Gizmos.matrix = col.transform.localToWorldMatrix;
                    Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                }
                else if (col is CapsuleCollider capsule)
                {
                    Gizmos.matrix = col.transform.localToWorldMatrix;
                    Gizmos.DrawWireSphere(capsule.center, capsule.radius);
                }
            }
        }

        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            int critCount = 0;
            if (_hitboxColliders != null)
                for (int i = 0; i < _hitboxCount; i++)
                    if (HasDamageMultiplier(_hitboxColliders[i])) critCount++;

            string boundStr = _boundPlayerId >= 0 ? $"Player {_boundPlayerId}" : "NPC/World";
            Color boundColor = _boundPlayerId >= 0 ? Color.green : Color.grey;

            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Hitboxes", $"{_hitboxCount} ({critCount} crit)"),
                new M2922_GizmoDisplayInfo("Bound To", boundStr, boundColor),
            };
        }
#endif
    }
}
