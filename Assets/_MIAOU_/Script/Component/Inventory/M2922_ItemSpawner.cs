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
    /// Marqueur ÉDITEUR : crée un POOL d'items (plusieurs préfabs × quantité)
    /// directement dans la scène AVANT l'upload. Les instances démarrent MASQUÉES
    /// à l'origine (0,0,0) — comme si elles étaient rangées dans un inventaire :
    /// sous-arbre du pickup DÉSACTIVÉ dans la scène + _startHidden = true
    /// (aucun des N items ne s'affiche au chargement du monde).
    /// Spawnables ensuite par le menu de la map (M2922_ItemAdminSpawner).
    ///
    /// Consommé par l'éditeur (M2922_ItemSpawnerEditor) : bouton "Générer" ou
    /// automatiquement au build (PostProcessScene). Supprimé après génération.
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
