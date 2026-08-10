using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using M2922.Core;

namespace M2922.Component.Utils
{
    /// <summary>
    /// Mode de sélection de la destination.
    /// </summary>
    public enum TeleporterSelectionMode
    {
        Single         = 0,
        Random         = 1,
        Sequential     = 2,
        RandomNoRepeat = 3,
    }

    /// <summary>
    /// Téléporteur avancé — hérite de M2922_Teleporter.
    /// Ajoute : multi-destinations, sélection (Random/Sequential),
    /// et immunité anti ping-pong.
    ///
    /// SETUP :
    ///   Single    → _destination (+ _linkedTeleporter auto-détecté si le GO a un AdvTp)
    ///   Random    → _destinations[] (+ _linkedTeleporters[] optionnel)
    ///   Sequential→ _destinations[] (+ _linkedTeleporters[] optionnel)
    /// </summary>
    [AddComponentMenu("M2922/Utils/Advanced Teleporter")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_AdvancedTeleporter : M2922_Teleporter
    {
        // =====================================================================
        // DESTINATION (avancé)
        // =====================================================================
        [Header("=== DESTINATION (avancé) ===")]
        [Tooltip("Mode de sélection de la destination.")]
        [SerializeField] private TeleporterSelectionMode _selectionMode = TeleporterSelectionMode.Single;

        [Tooltip("Destinations multiples (Random / Sequential).")]
        [SerializeField] private Transform[] _destinations = new Transform[0];

        [Tooltip("Téléporteurs liés (parallèle à _destinations).\n" +
                 "_linkedTeleporters[i] reçoit l'immunité après un tp vers _destinations[i].")]
        [SerializeField] private M2922_AdvancedTeleporter[] _linkedTeleporters = new M2922_AdvancedTeleporter[0];

        [UdonSynced] private int _currentIndex = 0;
        private int _lastIndex = -1;  // pour RandomNoRepeat

        // =====================================================================
        // IMMUNITÉ
        // =====================================================================
        [Header("=== IMMUNITÉ ===")]
        [Tooltip("Téléporteur lié (auto-détecté si _destination a un AdvTp).")]
        [SerializeField] private M2922_AdvancedTeleporter _linkedTeleporter;

        [Tooltip("Durée MAX d'immunité (secondes). L'immunité est levée dès que\n" +
                 "le joueur local sort du trigger. Ce délai = fallback timeout.")]
        [SerializeField] private float _arrivalImmunity = 1.5f;

        // =====================================================================
        // RUNTIME
        // =====================================================================
        private bool                   _arrivalImmune = false;
        private M2922_AdvancedTeleporter _pendingLinkedTeleporter;

        // =====================================================================
        // OVERRIDES
        // =====================================================================

        /// <summary>Résout la destination selon le mode de sélection.</summary>
        protected override Transform GetDestination()
        {
            int count = _destinations != null ? _destinations.Length : 0;

            switch (_selectionMode)
            {
                case TeleporterSelectionMode.Random:
                    if (count == 0) return null;
                    int rIdx = Random.Range(0, count);
                    _pendingLinkedTeleporter = (rIdx < _linkedTeleporters.Length) ? _linkedTeleporters[rIdx] : null;
                    return _destinations[rIdx];

                case TeleporterSelectionMode.RandomNoRepeat:
                    if (count == 0) return null;
                    if (count == 1) { _pendingLinkedTeleporter = (_linkedTeleporters.Length > 0) ? _linkedTeleporters[0] : null; return _destinations[0]; }
                    int nrIdx;
                    do { nrIdx = Random.Range(0, count); } while (nrIdx == _lastIndex);
                    _lastIndex = nrIdx;
                    _pendingLinkedTeleporter = (nrIdx < _linkedTeleporters.Length) ? _linkedTeleporters[nrIdx] : null;
                    return _destinations[nrIdx];

                case TeleporterSelectionMode.Sequential:
                    if (count == 0) return null;
                    // Prendre ownership pour pouvoir sync l'index
                    if (!Networking.IsOwner(gameObject))
                        Networking.SetOwner(Networking.LocalPlayer, gameObject);
                    int sIdx = _currentIndex;
                    _currentIndex = (_currentIndex + 1) % count;
                    RequestSerialization();
                    _pendingLinkedTeleporter = (sIdx < _linkedTeleporters.Length) ? _linkedTeleporters[sIdx] : null;
                    return _destinations[sIdx];

                case TeleporterSelectionMode.Single:
                default:
                    // Auto-détecter le linked teleporter si pas déjà set
                    if (_linkedTeleporter == null && _destination != null)
                        _linkedTeleporter = _destination.GetComponent<M2922_AdvancedTeleporter>();
                    _pendingLinkedTeleporter = _linkedTeleporter;
                    return _destination;
            }
        }

        /// <summary>Après chaque téléportation : immunité du téléporteur lié.</summary>
        protected override void AfterTeleport()
        {
            if (_pendingLinkedTeleporter != null && _arrivalImmunity > 0f)
                _pendingLinkedTeleporter.GrantArrivalImmunity(_arrivalImmunity);
            _pendingLinkedTeleporter = null;
        }

        /// <summary>Callback réseau : un autre client a mis à jour _currentIndex.</summary>
        public override void OnDeserialization()
        {
            // _currentIndex est déjà mis à jour par le réseau,
            // on n'a rien à faire de plus ici.
        }

        // =====================================================================
        // RUNTIME API — destinations modifiables à chaud
        // =====================================================================

        /// <summary>Change le mode de sélection en runtime.</summary>
        public void SetSelectionMode(TeleporterSelectionMode mode)
        {
            _selectionMode = mode;
            this.Log($"[AdvTp] SelectionMode → {mode}");
        }

        /// <summary>Ajoute une destination (et son linked teleporter optionnel) à la liste.</summary>
        public void AddDestination(Transform dest, M2922_AdvancedTeleporter linkedTp = null)
        {
            if (dest == null) return;
            int idx = _destinations.Length;
            // Agrandir les tableaux
            var newDests = new Transform[idx + 1];
            var newLinks = new M2922_AdvancedTeleporter[idx + 1];
            for (int i = 0; i < idx; i++)
            {
                newDests[i] = _destinations[i];
                if (i < _linkedTeleporters.Length) newLinks[i] = _linkedTeleporters[i];
            }
            newDests[idx] = dest;
            newLinks[idx] = linkedTp;
            _destinations = newDests;
            _linkedTeleporters = newLinks;
            this.Log($"[AdvTp] +Destination [{idx}] → {dest.name}");
        }

        /// <summary>Vide la liste des destinations.</summary>
        public void ClearDestinations()
        {
            _destinations = new Transform[0];
            _linkedTeleporters = new M2922_AdvancedTeleporter[0];
            _currentIndex = 0;
            _lastIndex = -1;
            this.Log("[AdvTp] Destinations cleared");
        }

        /// <summary>Change le linked teleporter pour le mode Single (auto-detect si null).</summary>
        public void SetLinkedTeleporter(M2922_AdvancedTeleporter linkedTp)
        {
            _linkedTeleporter = linkedTp;
            this.Log($"[AdvTp] LinkedTeleporter → {(linkedTp != null ? linkedTp.ScriptName : "NULL")}");
        }

        // =====================================================================
        // IMMUNITÉ — API publique (appelée par le téléporteur source)
        // =====================================================================

        /// <summary>Désactive ce téléporteur jusqu'à sortie du joueur (+ fallback timer).</summary>
        public void GrantArrivalImmunity(float duration)
        {
            if (!_isEnabled && !_arrivalImmune) return;
            _isEnabled = false;
            _arrivalImmune = true;
            this.VerboseLog($"[AdvTp] Immunité ({duration}s fallback)");
            SendCustomEventDelayedSeconds(nameof(_RestoreAfterImmunity), duration);
        }

        public void _RestoreAfterImmunity()
        {
            if (!_arrivalImmune) return;
            _isEnabled = true;
            _arrivalImmune = false;
            this.VerboseLog("[AdvTp] Immunité levée (fallback)");
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            if (!_arrivalImmune) return;
            if (!Utilities.IsValid(player)) return;
            if (player.playerId != Networking.LocalPlayer.playerId) return;

            _isEnabled = true;
            _arrivalImmune = false;
            this.VerboseLog("[AdvTp] Joueur sorti → immunité levée");
        }

        // =====================================================================
        // EDITOR
        // =====================================================================
#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            _arrivalImmunity = Mathf.Max(0f, _arrivalImmunity);
        }

        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            var baseValues = base.GetGizmoValues();

            // Remplace l'entrée "Destination" de la base par une version mode-aware
            string destInfo;
            Color  destColor;
            int count = _destinations != null ? _destinations.Length : 0;

            switch (_selectionMode)
            {
                case TeleporterSelectionMode.Random:
                    destInfo = count > 0 ? $"Random ({count})" : "VIDE";
                    destColor = count > 0 ? Color.cyan : Color.red;
                    break;
                case TeleporterSelectionMode.RandomNoRepeat:
                    destInfo = count > 0 ? $"RandNoRpt ({count})" : "VIDE";
                    destColor = count > 0 ? new Color(0.5f, 1f, 0.5f) : Color.red;
                    break;
                case TeleporterSelectionMode.Sequential:
                    destInfo = count > 0 ? $"Seq [{_currentIndex}/{count}]" : "VIDE";
                    destColor = count > 0 ? Color.cyan : Color.red;
                    break;
                case TeleporterSelectionMode.Single:
                default:
                    destInfo = _destination != null ? _destination.name : "AUCUNE";
                    destColor = _linkedTeleporter != null ? Color.magenta
                              : (_destination != null ? Color.cyan : Color.red);
                    break;
            }
            baseValues[0] = new M2922_GizmoDisplayInfo("Destination", destInfo, destColor);

            string linkedInfo = _linkedTeleporter != null ? _linkedTeleporter.ScriptName : "—";
            string immunityInfo = _arrivalImmunity > 0f ? $"{_arrivalImmunity}s" : "OFF";

            var all = new M2922_GizmoDisplayInfo[baseValues.Length + 2];
            for (int i = 0; i < baseValues.Length; i++)
                all[i] = baseValues[i];
            all[baseValues.Length]     = new M2922_GizmoDisplayInfo("Linked To", linkedInfo, _linkedTeleporter != null ? Color.magenta : Color.gray);
            all[baseValues.Length + 1] = new M2922_GizmoDisplayInfo("Immunité",  immunityInfo, _arrivalImmunity > 0f ? Color.yellow : Color.gray);
            return all;
        }

        protected override void DrawDestinationGizmos(float alpha, float sphereScale)
        {
            Gizmos.color = _isEnabled
                ? new Color(0f, 1f, 1f, alpha)
                : new Color(0.5f, 0.5f, 0.5f, alpha * 0.6f);
            float sphereSize = Mathf.Max(0.05f, transform.localScale.magnitude * sphereScale);

            switch (_selectionMode)
            {
                case TeleporterSelectionMode.Random:
                case TeleporterSelectionMode.RandomNoRepeat:
                case TeleporterSelectionMode.Sequential:
                    if (_destinations != null)
                    {
                        for (int i = 0; i < _destinations.Length; i++)
                        {
                            if (_destinations[i] != null)
                            {
                                Gizmos.DrawLine(transform.position, _destinations[i].position);
                                Gizmos.DrawSphere(_destinations[i].position, sphereSize);
                            }
                        }
                    }
                    break;

                default:
                    if (_destination != null)
                    {
                        Gizmos.DrawLine(transform.position, _destination.position);
                        Gizmos.DrawSphere(_destination.position, sphereSize);
                    }
                    break;
            }
        }
#endif
    }
}
