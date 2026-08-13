using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Utils
{
    /// <summary>
    /// Oriente le GameObject pour qu'il regarde toujours la caméra
    /// du joueur local (billboard). Utile pour barres de vie, noms, tags.
    /// Options : échelle selon la distance et masquage au-delà d'une distance max.
    /// </summary>
    [AddComponentMenu("M2922/Utils/Look At Camera")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_LookAtCamera : M2922_Tickable
    {
        [Header("=== AXES ===")]
        [Tooltip("Axe(s) à verrouiller (ex: Y seulement pour rester vertical).")]
        [SerializeField] private bool _lockX = false;
        [SerializeField] private bool _lockY = false;
        [SerializeField] private bool _lockZ = false;

        [Header("=== OPTIONS ===")]
        [Tooltip("Inverse le regard (montre le dos à la caméra).")]
        [SerializeField] private bool _flip = false;

        [Header("=== UPDATE MODE ===")]
        [Tooltip("PostLateUpdate pour suivre parfaitement. Décochez = Update.")]
        [SerializeField] private bool _useLateUpdate = false;

        [Header("=== DISTANCE SCALE ===")]
        [Tooltip("Si coché, ajuste l'échelle selon la distance à la caméra.")]
        [SerializeField] private bool _useDistanceScale = false;

        [Tooltip("Échelle minimale (près) et maximale (loin).")]
        [SerializeField] private Vector2 _minMaxScale = new Vector2(0.25f, 1f);

        [Tooltip("Distance minimale et maximale pour l'interpolation de l'échelle.")]
        [SerializeField] private Vector2 _minMaxScaleDistance = new Vector2(1f, 10f);

        [Tooltip("Croissance supplémentaire de l'échelle par mètre au-delà de la distance maximale.")]
        [SerializeField] private float _extraScalePerMeter = 0.2f;

        [Header("=== DISTANCE CULLING ===")]
        [Tooltip("Si coché, masque le canvas au-delà de _maxDistance.")]
        [SerializeField] private bool _useMaxDistance = false;

        [Tooltip("Distance au-delà de laquelle le canvas est masqué.")]
        [SerializeField] private float _maxDistance = 20f;

        [Tooltip("Canvas à masquer/afficher. Si vide, recherche sur cet objet puis ses enfants au Start.")]
        [SerializeField] private Canvas _canvas;

        private VRCPlayerApi _localPlayer;
        private Vector3 _baseLocalScale;

        protected override void Start()
        {
            base.Start();
            _localPlayer = Networking.LocalPlayer;
            _baseLocalScale = transform.localScale;

            if (_canvas == null)
            {
                _canvas = GetComponent<Canvas>();
                if (_canvas == null)
                    _canvas = GetComponentInChildren<Canvas>(true);
            }
        }

        protected override void Update()
        {
            base.Update();
            if (!_useLateUpdate) LookAtCamera();
        }

        public override void PostLateUpdate()
        {
            if (_useLateUpdate) LookAtCamera();
        }

        private void LookAtCamera()
        {
            if (_localPlayer == null) return;

            var tracking = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 dir = tracking.position - transform.position;
            float sqrDist = dir.sqrMagnitude;

            // Masquage au-delà de la distance max
            if (_useMaxDistance)
            {
                float maxSqr = _maxDistance * _maxDistance;
                bool visible = sqrDist <= maxSqr;
                if (_canvas != null && _canvas.gameObject.activeSelf != visible)
                    _canvas.gameObject.SetActive(visible);
            }

            if (_flip) dir = -dir;

            if (_lockX) dir.x = 0f;
            if (_lockY) dir.y = 0f;
            if (_lockZ) dir.z = 0f;

            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir);

            // Échelle selon la distance
            if (_useDistanceScale)
            {
                float dist = Mathf.Sqrt(sqrDist);
                float range = _minMaxScaleDistance.y - _minMaxScaleDistance.x;

                float t = range > 0.001f
                    ? (dist - _minMaxScaleDistance.x) / range
                    : 0f;

                float scale =
                    Mathf.Lerp(_minMaxScale.x, _minMaxScale.y, Mathf.Clamp01(t)) +
                    Mathf.Max(dist - _minMaxScaleDistance.y, 0f) * _extraScalePerMeter;

                transform.localScale = _baseLocalScale * scale;
            }
        }
    }
}
