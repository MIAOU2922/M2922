using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace M2922.Component.Weapon
{
    /// <summary>
    /// Zone de détection du chargeur sur l'arme.
    /// À placer sur un Collider (IsTrigger = true) positionné au niveau du puits de chargeur.
    /// Détecte les objets tagués "Magazine" et déclenche le rechargement.
    /// 
    /// SETUP :
    ///   1. Ajouter ce script sur un GameObject enfant de l'arme (ex: "MagazineWell")
    ///   2. Ajouter un Collider (Sphere/Box) avec IsTrigger = true
    ///   3. Positionner le collider à l'emplacement d'insertion du chargeur
    ///   4. Le FireHandler est auto-détecté (GetComponentInParent)
    ///   5. Les chargeurs doivent avoir le tag "Magazine"
    /// </summary>
    [AddComponentMenu("M2922/Weapon/Magazine Well")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_MagazineWell : UdonSharpBehaviour
    {
        [Header("=== REFERENCES ===")]
        [Tooltip("FireHandler de l'arme (auto-détecté si vide).")]
        [SerializeField] private M2922_WeaponFireHandler _fireHandler;

        [Header("=== SETTINGS ===")]
        [Tooltip("Délai minimum entre deux rechargements (secondes).")]
        [SerializeField] private float _reloadCooldown = 0.5f;

        private float _lastReloadTime = -999f;
        private bool _magazineInWell = false;

        private void Start()
        {
            if (_fireHandler == null)
                _fireHandler = GetComponentInParent<M2922_WeaponFireHandler>();

            if (_fireHandler == null)
                Debug.LogWarning("[MagazineWell] Aucun M2922_WeaponFireHandler trouvé !");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_fireHandler == null) return;
            if (other == null) return;
            // Détection via le composant marqueur (seule approche compatible UdonSharp)
            if (other.GetComponent<M2922_MagazineMarker>() == null) return;
            if (Time.time - _lastReloadTime < _reloadCooldown) return;

            _magazineInWell = true;
            _lastReloadTime = Time.time;

            _fireHandler.TryInsertShellOrReload();
        }

        private void OnTriggerExit(Collider other)
        {
            if (other != null && other.GetComponent<M2922_MagazineMarker>() != null)
                _magazineInWell = false;
        }

        public bool IsMagazineInWell()
        {
            return _magazineInWell;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Affiche la zone de détection en éditeur
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                Gizmos.matrix = transform.localToWorldMatrix;
                if (col is BoxCollider box)
                    Gizmos.DrawCube(box.center, box.size);
                else if (col is SphereCollider sphere)
                    Gizmos.DrawSphere(sphere.center, sphere.radius);
                else if (col is CapsuleCollider capsule)
                    Gizmos.DrawSphere(capsule.center, capsule.radius);
            }
        }
#endif
    }
}
