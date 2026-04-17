using UdonSharp;
using UnityEngine;

namespace M2922.Combat
{
    /// <summary>
    /// Marqueur de zone critique. Placez ce composant sur le collider qui doit
    /// compter comme un tir critique (ex : tête, point faible).
    /// Détecté via GetComponent&lt;M2922_CritZone&gt;() qui est exposé à Udon,
    /// contrairement à CompareTag / gameObject.tag.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class M2922_CritZone : UdonSharpBehaviour { }
}
