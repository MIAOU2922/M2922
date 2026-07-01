using UdonSharp;
using UnityEngine;

namespace M2922.Component.Health
{
    /// <summary>
    /// Multiplicateur de dégâts par zone. Placez ce composant sur un collider
    /// pour que les tirs reçus sur cette zone soient multipliés.
    /// Exemple : tête = 2.0, point faible = 3.0, jambes = 0.5.
    /// </summary>
    [AddComponentMenu("M2922/Health/Damage Multiplier")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_DamageMultiplier : UdonSharpBehaviour
    {
        [SerializeField] private float _multiplier = 2f;
        public float Multiplier => _multiplier;
    }
}
