using UdonSharp;
using UnityEngine;
using M2922.Entity.Player;
using M2922.Entity.Prop;

namespace M2922.Combat
{
    /// <summary>
    /// Lien entre un collider hitbox et son entité propriétaire.
    /// Placer ce composant sur le MÊME GO que le Collider (chest, head…).
    /// Détecté via GetComponent&lt;M2922_HitboxOwner&gt;() — méthode sûre en UdonSharp.
    ///
    /// GetComponentInParent&lt;T&gt;() pour les UdonSharpBehaviours n'est pas fiable en Udon ;
    /// ce composant marqueur est la solution recommandée pour relier un hitbox à son entité.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_HitboxOwner : UdonSharpBehaviour
    {
        [Tooltip("PlayerController propriétaire de ce hitbox. Assigner dans l'Inspector.")]
        public M2922_PlayerController PlayerController;

        [Tooltip("Prop propriétaire de ce hitbox (si entité prop). Assigner dans l'Inspector.")]
        public M2922_Prop Prop;
    }
}
