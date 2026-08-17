using System;
using UnityEngine;

namespace M2922.Component.Inventory
{
    [Serializable]
    public class M2922_ItemSpawnEntry
    {
        [Tooltip("Préfab d'item à mettre dans le pool.")]
        public GameObject Prefab;

        [Min(0)]
        [Tooltip("Nombre d'instances de ce préfab.")]
        public int Count = 1;
    }

    /// <summary>
    /// Marqueur ÉDITEUR : décrit un POOL d'items (plusieurs préfabs × quantité).
    /// Les instances sont dupliquées AUTOMATIQUEMENT à la compilation :
    ///   - entrée en Play Mode (ClientSim / Build & Test),
    ///   - build / upload (PostProcessScene).
    /// La scène éditée reste propre (pas d'instance de pool dans la hiérarchie).
    /// Les instances sont PARENTÉES à ce marqueur (position locale 0,0,0) et
    /// démarrent MASQUÉES — comme si elles
    /// étaient rangées dans un inventaire : sous-arbre du pickup DÉSACTIVÉ +
    /// _startHidden = true (aucun des N items ne s'affiche au chargement).
    /// Spawnables ensuite par le menu de la map (M2922_ItemAdminSpawner).
    ///
    /// Consommé par l'éditeur (M2922_ItemSpawnerEditor) : hook Play Mode +
    /// PostProcessScene. Supprimé après génération (RemoveAfterGenerate).
    /// </summary>
    [AddComponentMenu("M2922/Inventory/Item Spawner (Editor)")]
    public class M2922_ItemSpawner : MonoBehaviour
    {
        [Tooltip("Liste des préfabs à pooler, avec la quantité de chacun.")]
        public M2922_ItemSpawnEntry[] Entries = new M2922_ItemSpawnEntry[0];

        [Tooltip("Supprimer ce marqueur après la génération (recommandé).")]
        public bool RemoveAfterGenerate = true;
    }
}
