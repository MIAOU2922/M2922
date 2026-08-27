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
    /// Les instances sont dupliquées AUTOMATIQUEMENT à l'entrée en Play Mode
    /// (ClientSim / Build & Test). Avant un build / upload, le pool doit être
    /// BAKÉ MANUELLEMENT : bouton « Bake Item Pool » de l'inspector (ou menu
    /// M2922/Inventory/Generate Item Pool).
    /// La scène éditée reste propre (pas d'instance de pool dans la hiérarchie).
    /// Les instances sont PARENTÉES à ce marqueur (position locale 0,0,0) et
    /// démarrent MASQUÉES — comme si elles
    /// étaient rangées dans un inventaire : sous-arbre du pickup DÉSACTIVÉ +
    /// _startHidden = true (aucun des N items ne s'affiche au chargement).
    /// Spawnables ensuite par le menu de la map (M2922_ItemAdminSpawner).
    ///
    /// ⚠ INITIALISATION : le sous-arbre du pickup étant DÉSACTIVÉ, les scripts
    /// enfants (armes, pools de projectiles...) exécutent leur Start à la
    /// PREMIÈRE ACTIVATION — c'est-à-dire au premier spawn — puisque Start se
    /// déclenche quand le GameObject devient actif pour la première fois.
    /// Aucun pré-spawn n'est nécessaire : l'item masqué au Start
    /// (_startHidden) le reste jusqu'à son spawn.
    ///
    /// Consommé par l'éditeur (M2922_ItemSpawnerEditor) : hook Play Mode +
    /// bouton Bake manuel. Supprimé après génération (RemoveAfterGenerate).
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
