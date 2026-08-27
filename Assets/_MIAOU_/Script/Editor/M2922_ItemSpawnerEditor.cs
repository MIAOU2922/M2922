using System.Collections.Generic;
using UdonSharp;
using UdonSharp.Compiler;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using UdonSharpEditor;
using M2922.Component.Health;
using M2922.Component.Inventory;
using M2922.Core;

namespace M2922.Editor
{
    /// <summary>
    /// Outil d'édition : génère les items marqués par M2922_ItemSpawner dans la
    /// scène, puis met à jour les tailles de registres du M2922_Manager
    /// (_inventoryItemMax / _npcMax) selon le contenu réel de la scène.
    /// </summary>
    public static class M2922_ItemSpawnerGenerator
    {
        private const int MIN_REGISTRY = 32;
        private const int REGISTRY_MARGIN = 16;

        /// <summary>
        /// Préfabs sources dont le StackId a été généré pendant la génération —
        /// synchronisés proxy → Udon APRÈS la génération (les assets ne sont pas
        /// dans la scène : SyncAllProxiesToUdon ne les voit pas).
        /// </summary>
        private static readonly HashSet<M2922_InventoryItem> _pendingPrefabSourceSync =
            new HashSet<M2922_InventoryItem>();

        /// <summary>
        /// Cache par programme U# du test « le programme compilé contient-il TOUS
        /// les champs sérialisés ? » — évite N × RetrieveProgram() quand 500
        /// items partagent le même programme.
        /// </summary>
        private static readonly Dictionary<UdonSharpProgramAsset, bool> _programFieldCache =
            new Dictionary<UdonSharpProgramAsset, bool>();

        /// <summary>
        /// Ordre IMPORTANT : la GÉNÉRATION (instances, flags, StackId) doit être
        /// terminée AVANT toute opération Udon. Les méthodes de génération ne
        /// font donc AUCUN appel UdonSharpEditorUtility ; tout le proxy → Udon
        /// est fait ici, après.
        /// </summary>
        public static void GenerateAndUpdate(Scene scene)
        {
            _pendingPrefabSourceSync.Clear();
            _programFieldCache.Clear();

            List<GameObject> createdInstances = new List<GameObject>();

            // 0. CONTENEUR DE PROJECTILES : garantit que l'objet racine
            // « Projectil Pool » existe dans la scène (créé si absent) — Udon
            // ne peut pas créer de GameObject vide au runtime (pas de
            // new GameObject() dans l'UdonSharp vendu avec com.vrchat.worlds).
            EnsureProjectilePoolContainer(scene);

            // 1. GÉNÉRATION pure (instances, _startHidden, StackId, pickup off).
            GenerateAllInScene(scene, createdInstances);

            // 2. UDON : sync proxy → programme sur TOUTE la scène (les instances
            //    générées sont incluses). ⚠ Au start du play mode, l'UdonBehaviour
            //    écrase les champs des proxies C# avec SES bytes sérialisés ; si
            //    ces bytes sont vides (préfabs jamais synchronisés), TOUTES les
            //    variables semblent "écrasées" (None / défauts). UdonSharp ne
            //    pousse proxy → Udon qu'au BUILD : on le fait donc ici, après la
            //    génération.
            SyncAllProxiesToUdon(scene);

            // 3. UDON : sync des préfabs SOURCES touchés (StackId généré) —
            //    pas couverts par l'étape 2.
            SyncPendingPrefabSources();

            // 4. BUILD JOUEUR : si UdonSharp a déjà traité la scène (proxies C#
            //    retirés) AVANT notre génération, on retire les proxies des
            //    instances qu'on vient de créer — sinon elles arriveraient dans
            //    le monde avec leur proxy C# vivant (double exécution du Start).
            StripProxiesIfUdonSharpAlreadyProcessed(scene, createdInstances);

            UpdateManagerRegistrySizes(scene);
        }

        /// <summary>
        /// Pousse les valeurs des proxies C# vers leur UdonBehaviour pour TOUS les
        /// UdonSharpBehaviours de la scène (équivalent de ce que fait UdonSharp au
        /// build). Sans ça, le runtime écrase les champs au play mode avec des bytes
        /// vides → perte apparente de toutes les variables des préfabs.
        /// </summary>
        public static void SyncAllProxiesToUdon(Scene scene)
        {
            int synced = 0;
            int failed = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                UdonSharpBehaviour[] behaviours = root.GetComponentsInChildren<UdonSharpBehaviour>(true);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    UdonSharpBehaviour behaviour = behaviours[i];
                    if (behaviour == null) continue;

                    if (TrySyncProxyToUdon(behaviour, behaviour.name, ProxySerializationPolicy.All))
                        synced++;
                    else
                        failed++;
                }
            }

            if (synced > 0 || failed > 0)
            {
                Debug.Log($"[ItemSpawner] Sync proxy → Udon : {synced} OK, {failed} échec(s)." +
                          (failed > 0 ? " ⚠ Sans sync, les valeurs des préfabs paraissent 'réinitialisées' (None / défauts) en play mode — voir les erreurs ci-dessus." : ""));
            }
        }

        /// <summary>
        /// Copie proxy → programme Udon de façon ROBUSTE :
        ///  - UdonBehaviour backing absent → Warning explicite (échec).
        ///  - Programme U# compilé OBSOLÈTE (champs manquants → le sérialiseur
        ///    logge "Field for ... does not exist" SANS exception et le sync
        ///    reste PARTIEL) → détection par table des symboles + recompilation
        ///    BLOQUANTE (CompileSync), sauf pendant un build joueur.
        ///  - Lien programme du backing réparé (prefabs pointant encore un
        ///    programme intermédiaire PrefabBuild périmé).
        ///  - Échec final → Error, pour que le problème soit visible : sans sync,
        ///    les items arrivent au runtime avec leurs valeurs par défaut.
        /// </summary>
        private static bool TrySyncProxyToUdon(UdonSharpBehaviour behaviour, string context, ProxySerializationPolicy policy)
        {
            if (behaviour == null) return false;

            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour);
            if (backing == null)
            {
                Debug.LogWarning($"[ItemSpawner] Sync impossible sur '{context}' ({behaviour.GetType().Name}) : aucun UdonBehaviour backing. Recompilez les programmes Udon (Build & Test) et vérifiez le prefab.");
                return false;
            }

            UdonSharpProgramAsset programAsset = UdonSharpEditorUtility.GetUdonSharpProgramAsset(behaviour);
            if (programAsset == null)
            {
                Debug.LogWarning($"[ItemSpawner] Sync impossible sur '{context}' ({behaviour.GetType().Name}) : aucun UdonSharpProgramAsset trouvé pour ce script.");
                return false;
            }

            // Test « programme à jour » mis en cache par asset (une seule lecture
            // du programme stocké par génération, même avec 500 items).
            bool programCurrent;
            if (!_programFieldCache.TryGetValue(programAsset, out programCurrent))
            {
                programCurrent = ProgramHasAllFields(programAsset);

                if (!programCurrent && !BuildPipeline.isBuildingPlayer)
                {
                    Debug.LogWarning($"[ItemSpawner] Programme U# obsolète pour '{behaviour.GetType().Name}' ({context}) — recompilation bloquante.");
                    UdonSharpCompilerV1.CompileSync();
                    programCurrent = ProgramHasAllFields(programAsset);
                }

                _programFieldCache[programAsset] = programCurrent;
            }

            if (!programCurrent)
            {
                Debug.LogError($"[ItemSpawner] Programme U# de '{behaviour.GetType().Name}' ne contient pas tous les champs sérialisés ({context}). Sync IMPOSSIBLE — les valeurs seront ABSENTES au runtime. Compilez les scripts U# (Build & Test).");
                return false;
            }

            // Répare le lien du backing vers le programme correct (un prefab peut
            // encore pointer un programme intermédiaire PrefabBuild périmé).
            EnsureBackingProgramLink(backing, programAsset);

            try
            {
                UdonSharpEditorUtility.CopyProxyToUdon(behaviour, policy);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ItemSpawner] ÉCHEC de sync proxy → Udon sur '{context}' ({behaviour.GetType().Name}) : {e.Message}. Les valeurs de ce prefab seront ABSENTES au runtime.");
                return false;
            }
        }

        /// <summary>
        /// TRUE si le programme compilé contient bien TOUS les champs sérialisés
        /// du proxy (comparaison des clés fieldDefinitions avec la table des
        /// symboles du programme STOCKÉ). Détecte le cas où le sérialiseur Udon
        /// logge "Field for ... does not exist" sans lever d'exception.
        /// </summary>
        private static bool ProgramHasAllFields(UdonSharpProgramAsset programAsset)
        {
            if (programAsset == null) return false;
            if (programAsset.fieldDefinitions == null || programAsset.fieldDefinitions.Count == 0)
                return true;

            IUdonProgram program = null;
            AbstractSerializedUdonProgramAsset serialized = programAsset.GetSerializedUdonProgramAsset();
            if (serialized != null)
                program = serialized.RetrieveProgram();
            if (program == null)
            {
                programAsset.UpdateProgram();
                program = programAsset.GetRealProgram();
            }
            if (program == null) return false;

            foreach (string fieldName in programAsset.fieldDefinitions.Keys)
            {
                // Les symboles Udon sont NON manglés (les backing fields de
                // propriétés "<X>k__BackingField" deviennent "_X_k__BackingField").
                string symbol = fieldName.Replace('<', '_').Replace('>', '_');
                if (!program.SymbolTable.TryGetAddressFromSymbol(symbol, out _))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Réécrit le lien serializedProgramAsset du backing vers le programme
        /// CORRECT du proxy (répare les prefabs qui pointent encore un programme
        /// intermédiaire PrefabBuild périmé). Retourne true si le lien a été réparé.
        /// </summary>
        private static bool EnsureBackingProgramLink(UdonBehaviour backing, UdonSharpProgramAsset programAsset)
        {
            AbstractSerializedUdonProgramAsset expected = programAsset.GetSerializedUdonProgramAsset();
            if (expected == null) return false;

            SerializedObject ub = new SerializedObject(backing);
            SerializedProperty link = ub.FindProperty("serializedProgramAsset");
            if (link == null || link.objectReferenceValue == expected) return false;

            link.objectReferenceValue = expected;
            ub.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>
        /// RÉPARATION SEULE des liens programme Udon (AUCUNE génération de pool) :
        /// ré-affecte le serializedProgramAsset de chaque UdonBehaviour de la
        /// scène vers le programme compilé CORRECT du proxy. Élimine les
        /// « Field for System.Int32 does not exist » d'UdonSharp au build,
        /// causés par des prefabs pointant un programme intermédiaire périmé.
        /// Utilisé par le hook de build (PostProcessScene) — la génération du
        /// pool reste MANUELLE (bake), décision utilisateur.
        /// </summary>
        public static void RepairProgramLinks(Scene scene)
        {
            _programFieldCache.Clear();

            int repaired = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                UdonSharpBehaviour[] behaviours = root.GetComponentsInChildren<UdonSharpBehaviour>(true);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    UdonSharpBehaviour behaviour = behaviours[i];
                    if (behaviour == null) continue;

                    UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour);
                    if (backing == null) continue;

                    UdonSharpProgramAsset programAsset = UdonSharpEditorUtility.GetUdonSharpProgramAsset(behaviour);
                    if (programAsset == null) continue;

                    if (EnsureBackingProgramLink(backing, programAsset)) repaired++;
                }
            }

            if (repaired > 0)
                Debug.Log($"[ItemSpawner] Build : {repaired} lien(s) programme Udon réparé(s) (anti « Field does not exist »).");
        }

        /// <summary>
        /// Sync proxy → programme Udon des PRÉFABS SOURCES dont le StackId a été
        /// généré pendant GenerateAllInScene. Appelé APRÈS la génération.
        /// Policy Default (profondeur 1) : comme UdonSharp au build, on ne
        /// sérialise pas récursivement les références EXTERNES à l'asset.
        /// </summary>
        private static void SyncPendingPrefabSources()
        {
            foreach (M2922_InventoryItem source in _pendingPrefabSourceSync)
            {
                if (source == null) continue;
                TrySyncProxyToUdon(source, $"prefab source '{source.name}'", ProxySerializationPolicy.Default);
            }

            _pendingPrefabSourceSync.Clear();
        }

        /// <summary>
        /// BUILD JOUEUR uniquement : si UdonSharp a déjà traité la scène (ses
        /// PostProcessScene retirent les proxies C# des UdonBehaviours lors d'un
        /// build joueur) AVANT notre génération, les instances qu'on vient de
        /// créer garderaient leur proxy C# dans le monde (double exécution du
        /// Start : Udon + C#). On retire donc nous-mêmes leurs proxies, comme
        /// UdonSharp le fait sur le reste de la scène.
        /// </summary>
        private static void StripProxiesIfUdonSharpAlreadyProcessed(Scene scene, List<GameObject> createdInstances)
        {
            if (createdInstances == null || createdInstances.Count == 0) return;
            if (!BuildPipeline.isBuildingPlayer) return;

            bool alreadyProcessed = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                UdonBehaviour[] udons = root.GetComponentsInChildren<UdonBehaviour>(true);
                for (int i = 0; i < udons.Length; i++)
                {
                    UdonBehaviour udon = udons[i];
                    if (udon == null) continue;
                    if (UdonSharpEditorUtility.IsUdonSharpBehaviour(udon) &&
                        UdonSharpEditorUtility.GetProxyBehaviour(udon) == null)
                    {
                        alreadyProcessed = true;
                        break;
                    }
                }
                if (alreadyProcessed) break;
            }

            // UdonSharp passera APRÈS nous : il retirera les proxies lui-même.
            if (!alreadyProcessed) return;

            int stripped = 0;
            for (int i = 0; i < createdInstances.Count; i++)
            {
                GameObject instance = createdInstances[i];
                if (instance == null) continue;

                UdonSharpBehaviour[] proxies = instance.GetComponentsInChildren<UdonSharpBehaviour>(true);
                for (int p = 0; p < proxies.Length; p++)
                {
                    if (proxies[p] == null) continue;
                    Object.DestroyImmediate(proxies[p]);
                    stripped++;
                }
            }

            if (stripped > 0)
                Debug.Log($"[ItemSpawner] Build joueur : {stripped} proxy C# retiré(s) des instances du pool (UdonSharp avait déjà traité la scène).");
        }

        /// <summary>
        /// Instancie tous les M2922_ItemSpawner de la scène, puis supprime les marqueurs.
        /// </summary>
        public static void GenerateAllInScene(Scene scene)
        {
            GenerateAllInScene(scene, null);
        }

        /// <summary>
        /// Instancie tous les M2922_ItemSpawner de la scène, puis supprime les
        /// marqueurs. En ÉDITEUR (bake manuel), les instances sont de VRAIES
        /// instances de préfab (lien vers l'asset conservé).
        /// <paramref name="createdInstances"/> reçoit les instances créées
        /// (null = pas de suivi) — utilisé pour le retrait des proxies lors
        /// d'un build joueur si UdonSharp a déjà traité la scène.
        /// </summary>
        public static void GenerateAllInScene(Scene scene, List<GameObject> createdInstances)
        {
            List<M2922_ItemSpawner> spawners = FindAll<M2922_ItemSpawner>(scene);
            if (spawners.Count == 0) return;

            // ÉDITEUR (bake manuel) : purge le pool du bake PRÉCÉDENT avant de
            // régénérer (sinon les instances s'accumulent à chaque bake). En
            // play mode / build, on ne purge RIEN (voir ClearPreviousBakedPool).
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                ClearPreviousBakedPool(scene);

            int created = 0;
            for (int s = 0; s < spawners.Count; s++)
            {
                M2922_ItemSpawner spawner = spawners[s];
                if (spawner == null || spawner.Entries == null) continue;

                for (int e = 0; e < spawner.Entries.Length; e++)
                {
                    M2922_ItemSpawnEntry entry = spawner.Entries[e];
                    if (entry == null || entry.Prefab == null) continue;

                    for (int i = 0; i < entry.Count; i++)
                    {
                        // ÉDITEUR (bake manuel) : VRAIE instance de préfab
                        // (icône bleue, lien vers l'asset conservé, overrides
                        // visibles). PLAY MODE / build : PrefabUtility est
                        // INTERDIT → clone transitoire sans lien (jeté au
                        // retour en édition par Unity).
                        GameObject instance = InstantiatePoolInstance(entry.Prefab, spawner.transform);
                        if (instance == null) continue;

                        if (createdInstances != null) createdInstances.Add(instance);

                        instance.name = $"{entry.Prefab.name} (Pool {i + 1})";

                        MarkStartHidden(instance, entry.Prefab);

                        created++;
                    }
                }

                if (spawner.RemoveAfterGenerate)
                {
                    // Détache les instances poolées AVANT de supprimer le
                    // marqueur (sinon DestroyImmediate détruirait ses enfants).
                    while (spawner.transform.childCount > 0)
                        spawner.transform.GetChild(0).SetParent(null);
                    Object.DestroyImmediate(spawner);
                }
            }

            if (created > 0)
            {
                // En play mode / build, Unity gère lui-même l'état de la scène
                // (les instances de play mode sont retirées au retour en édition).
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"[ItemSpawner] {created} instance(s) ajoutée(s) au pool.");
            }
        }

        /// <summary>
        /// Instancie un item du pool avec le LIEN prefab conservé en ÉDITEUR
        /// (bake manuel → vraie instance de préfab, icône bleue dans la
        /// hiérarchie). En play mode / build, PrefabUtility.InstantiatePrefab
        /// est interdit : clones transitoires sans lien (jetés au retour en
        /// édition).
        /// </summary>
        private static GameObject InstantiatePoolInstance(GameObject prefab, Transform parent)
        {
            GameObject instance;
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !BuildPipeline.isBuildingPlayer)
            {
                if (PrefabUtility.IsPartOfPrefabAsset(prefab))
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                }
                else
                {
                    Debug.LogWarning($"[ItemSpawner] '{prefab.name}' n'est pas un asset de préfab : clone simple sans lien prefab.");
                    instance = Object.Instantiate(prefab, parent, false);
                }
            }
            else
            {
                instance = Object.Instantiate(prefab, parent, false);
            }

            if (instance == null) return null;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            return instance;
        }

        /// <summary>
        /// Détruit les instances de pool laissées par un bake PRÉCÉDENT,
        /// identifiées par le nom généré « {Préfab} (Pool N) » — seul signal
        /// fiable (les anciens bakes sont des clones sans lien prefab, et les
        /// nouveaux sont détachés à la racine si RemoveAfterGenerate).
        /// ⚠ ÉDITEUR uniquement : en play mode / build, on ne nettoie RIEN
        /// (les clones transitoires sont jetés par Unity au retour en édition,
        /// et les instances bakées de la scène ne doivent pas être supprimées).
        /// </summary>
        private static void ClearPreviousBakedPool(Scene scene)
        {
            int removed = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] all = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    GameObject go = all[i] != null ? all[i].gameObject : null;
                    if (go == null) continue;
                    if (!IsPoolInstanceName(go.name)) continue;

                    Object.DestroyImmediate(go);
                    removed++;
                }
            }

            if (removed > 0)
                Debug.Log($"[ItemSpawner] Pool précédent nettoyé : {removed} instance(s) supprimée(s).");
        }

        /// <summary>TRUE si le nom suit le pattern généré « {Préfab} (Pool N) ».</summary>
        private static bool IsPoolInstanceName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            const string marker = " (Pool ";
            int idx = name.LastIndexOf(marker);
            if (idx <= 0) return false;

            string suffix = name.Substring(idx + marker.Length);
            if (!suffix.EndsWith(")")) return false;

            int number;
            return int.TryParse(suffix.Substring(0, suffix.Length - 1), out number);
        }

        /// <summary>
        /// Marque l'item de l'instance générée pour qu'il démarre masqué :
        /// 1. M2922_InventoryItem._startHidden = true (le Start le masque au runtime) ;
        /// 2. désactive IMMÉDIATEMENT le sous-arbre du pickup dans la scène —
        ///    les items du pool sont "rangés" (masqués) comme dans un inventaire,
        ///    pas 500 objets actifs empilés en (0,0,0) au chargement du monde.
        /// ⚠ Ne jamais désactiver le GameObject qui porte le M2922_InventoryItem
        /// (son Start ne tournerait pas → item non enregistré et non spawnable).
        /// </summary>
        private static void MarkStartHidden(GameObject instance, GameObject prefabAsset)
        {
            M2922_InventoryItem item = instance.GetComponentInChildren<M2922_InventoryItem>(true);
            if (item == null)
            {
                Debug.LogWarning($"[ItemSpawner] Aucun M2922_InventoryItem trouvé sur '{instance.name}' : l'item ne démarrera pas masqué.");
                return;
            }

            // S'assure que le StackId existe sur le PREFAB ASSET (partagé par
            // toutes les instances), sinon le stacking ne fonctionnerait pas.
            EnsurePrefabStackId(item, prefabAsset);

            SerializedObject iso = new SerializedObject(item);
            SerializedProperty prop = iso.FindProperty("_startHidden");
            if (prop != null)
            {
                prop.boolValue = true;
                iso.ApplyModifiedPropertiesWithoutUndo();
            }

            // ⚠ UDON : AUCUNE opération Udon ici. La sync proxy → programme des
            // instances générées est faite APRÈS toute la génération, par
            // SyncAllProxiesToUdon(scene) dans GenerateAndUpdate (ordre garanti :
            // génération AVANT Udon).

            // Désactive le sous-arbre du pickup DÈS LA GÉNÉRATION (état "rangé").
            // Si l'item est posé À PLAT sur le pickup (même GameObject), on ne
            // désactive rien : le flag _startHidden le masquera au Start.
            VRCPickup pickup = item.Pickup;
            if (pickup == null)
                pickup = item.GetComponentInChildren<VRCPickup>(true);

            if (pickup != null && pickup.gameObject != item.gameObject)
                pickup.gameObject.SetActive(false);
        }

        /// <summary>
        /// Génère un StackId sur le PREFAB ASSET si absent (partagé par toutes
        /// les instances du pool, stable entre les sessions).
        /// </summary>
        private static void EnsurePrefabStackId(M2922_InventoryItem item, GameObject prefabAsset)
        {
            if (!string.IsNullOrEmpty(item.StackId)) return;

            // Les clones sont des Object.Instantiate SANS lien prefab : on cible
            // directement le composant de l'ASSET via entry.Prefab.
            M2922_InventoryItem source = null;
            if (prefabAsset != null)
                source = prefabAsset.GetComponentInChildren<M2922_InventoryItem>(true);
            if (source == null) source = item;

            SerializedObject so = new SerializedObject(source);
            SerializedProperty p = so.FindProperty("StackId");
            if (p != null && string.IsNullOrEmpty(p.stringValue))
            {
                p.stringValue = System.Guid.NewGuid().ToString("N");
                so.ApplyModifiedPropertiesWithoutUndo();

                // ⚠ UDON : pas de sync ici (génération AVANT Udon). Le source est
                // enregistré pour être synchronisé après la génération, dans
                // SyncPendingPrefabSources (appelé par GenerateAndUpdate).
                _pendingPrefabSourceSync.Add(source);

                EditorUtility.SetDirty(source);
            }

            // ⚠ PREMIER RUN : la GUID vient d'être écrite sur l'ASSET, mais
            // l'instance déjà instanciée garde StackId vide. On la recopie sur
            // l'instance pour que SyncAllProxiesToUdon la pousse dans SES bytes
            // (sinon toutes les instances du pool partagent un StackId vide).
            if (source != item && p != null && !string.IsNullOrEmpty(p.stringValue))
            {
                SerializedObject iso = new SerializedObject(item);
                SerializedProperty ip = iso.FindProperty("StackId");
                if (ip != null && string.IsNullOrEmpty(ip.stringValue))
                {
                    ip.stringValue = p.stringValue;
                    iso.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        /// <summary>
        /// Ajuste _inventoryItemMax et _npcMax du Manager selon le nombre réel
        /// d'items/NPCs actifs présents dans la scène (avec une marge de sécurité).
        /// </summary>
        public static void UpdateManagerRegistrySizes(Scene scene)
        {
            M2922_Manager manager = FindManager(scene);
            if (manager == null)
            {
                Debug.LogWarning("[ItemSpawner] Aucun M2922_Manager trouvé dans la scène : tailles de registres non mises à jour.");
                return;
            }

            int itemCount = CountActive<M2922_InventoryItem>(scene);
            int npcCount = CountActiveNpcs(scene);

            int itemMax = Mathf.Max(MIN_REGISTRY, itemCount + REGISTRY_MARGIN);
            int npcMax = Mathf.Max(MIN_REGISTRY, npcCount + REGISTRY_MARGIN);

            SerializedObject so = new SerializedObject(manager);
            SerializedProperty itemMaxProp = so.FindProperty("_inventoryItemMax");
            SerializedProperty npcMaxProp = so.FindProperty("_npcMax");
            if (itemMaxProp != null) itemMaxProp.intValue = itemMax;
            if (npcMaxProp != null) npcMaxProp.intValue = npcMax;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);

            Debug.Log($"[ItemSpawner] Registres Manager : {itemCount} items → max {itemMax}, {npcCount} NPCs → max {npcMax}.");
        }

        /// <summary>
        /// Garantit que l'objet racine « Projectil Pool » existe dans la scène
        /// (créé si absent) : Udon ne peut pas créer de GameObject vide au
        /// runtime — il doit donc être présent dans la scène uploadée.
        /// </summary>
        private static void EnsureProjectilePoolContainer(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Projectil Pool") return;
            }

            GameObject container = new GameObject("Projectil Pool");
            SceneManager.MoveGameObjectToScene(container, scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[ItemSpawner] 'Projectil Pool' créé dans la scène (conteneur des projectiles de pool).");
        }

        private static M2922_Manager FindManager(Scene scene)
        {
            List<M2922_Manager> managers = FindAll<M2922_Manager>(scene);
            if (managers.Count > 0) return managers[0];

            GameObject go = GameObject.Find("Manager");
            if (go != null) return go.GetComponent<M2922_Manager>();
            return null;
        }

        private static int CountActiveNpcs(Scene scene)
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                M2922_DamageReceiver[] receivers = root.GetComponentsInChildren<M2922_DamageReceiver>(true);
                for (int i = 0; i < receivers.Length; i++)
                {
                    M2922_DamageReceiver receiver = receivers[i];
                    if (receiver == null || !receiver.gameObject.activeInHierarchy) continue;
                    if (receiver.HitboxSystem != null && !receiver.HitboxSystem.IsPlayer) count++;
                }
            }
            return count;
        }

        private static List<T> FindAll<T>(Scene scene) where T : UnityEngine.Component
        {
            List<T> result = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T[] comps = root.GetComponentsInChildren<T>(true);
                result.AddRange(comps);
            }
            return result;
        }

        private static int CountActive<T>(Scene scene) where T : UnityEngine.Component
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T[] comps = root.GetComponentsInChildren<T>(true);
                for (int i = 0; i < comps.Length; i++)
                    if (comps[i] != null && comps[i].gameObject.activeInHierarchy) count++;
            }
            return count;
        }
    }

    /// <summary>
    /// Menu M2922 : génération manuelle du pool d'items dans la scène active.
    /// </summary>
    public static class M2922_ItemSpawnerMenu
    {
        [MenuItem("M2922/Inventory/Generate Item Pool (Active Scene)", priority = 30)]
        public static void GeneratePool()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[ItemSpawner] Aucune scène active : génération du pool impossible.");
                return;
            }

            M2922_ItemSpawnerGenerator.GenerateAndUpdate(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    /// <summary>
    /// RÉPARATION SEULE au build (PAS de génération de pool — celle-ci est
    /// manuelle via le bouton Bake) : avant la sync d'UdonSharp, ré-affecte les
    /// liens programme des UdonBehaviours vers l'asset compilé CORRECT.
    /// Élimine les « Field for System.Int32 does not exist » causés par des
    /// prefabs pointant un programme intermédiaire PrefabBuild périmé.
    /// </summary>
    public static class M2922_UdonProgramLinkRepairBuildHook
    {
        [PostProcessScene(-100)]
        public static void OnPostProcessScene()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid()) return;

            M2922_ItemSpawnerGenerator.RepairProgramLinks(scene);
        }
    }

    /// <summary>
    /// Duplication automatique à l'ENTRÉE EN PLAY MODE (ClientSim / Build & Test).
    /// Les instances créées en play mode sont retirées automatiquement au retour
    /// en mode édition (Unity restaure la scène) → la hiérarchie éditeur reste propre.
    /// </summary>
    public static class M2922_ItemSpawnerPlayModeHook
    {
        [InitializeOnLoadMethod]
        private static void Register()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;

            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid()) return;

            M2922_ItemSpawnerGenerator.GenerateAndUpdate(scene);
        }
    }

    /// <summary>
    /// Inspector custom du marqueur M2922_ItemSpawner.
    /// Le tableau Entries est affiché comme une liste style CounterUI
    /// (colonnes # / Prefab / Count + boutons +/-) au lieu de
    /// "Element 0 → Prefab / Count" du draw default.
    /// </summary>
    [CustomEditor(typeof(M2922_ItemSpawner))]
    public class M2922_ItemSpawnerEditor : UnityEditor.Editor
    {
        private SerializedProperty _propEntries;
        private SerializedProperty _propRemoveAfterGenerate;

        private ReorderableList _entriesList;

        private void OnEnable()
        {
            _propEntries = serializedObject.FindProperty("Entries");
            _propRemoveAfterGenerate = serializedObject.FindProperty("RemoveAfterGenerate");

            BuildEntriesList();
        }

        private void BuildEntriesList()
        {
            _entriesList = new ReorderableList(
                serializedObject,
                _propEntries,
                draggable: true,
                displayHeader: true,
                displayAddButton: true,
                displayRemoveButton: true)
            {
                drawHeaderCallback = DrawEntriesHeader,
                drawElementCallback = DrawEntriesElement,
                onAddCallback = OnAddEntry,
                onRemoveCallback = OnRemoveEntry,
                onReorderCallbackWithDetails = OnReorderEntries,
                elementHeight = EditorGUIUtility.singleLineHeight + 2f
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Lazy init : l'inspector peut être construit sans OnEnable().
            if (_propEntries == null)
            {
                _propEntries = serializedObject.FindProperty("Entries");
                _propRemoveAfterGenerate = serializedObject.FindProperty("RemoveAfterGenerate");
            }
            if (_entriesList == null || _entriesList.serializedProperty != _propEntries)
                BuildEntriesList();

            // Référence de script (lecture seule).
            SerializedProperty scriptProp = serializedObject.FindProperty("m_Script");
            if (scriptProp != null)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.PropertyField(scriptProp, true);
                EditorGUI.EndDisabledGroup();
            }

            // ---- ENTRIES (liste custom style CounterUI) ----
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("=== ENTRIES ===", EditorStyles.boldLabel);
            _entriesList.DoLayoutList();

            EditorGUILayout.PropertyField(_propRemoveAfterGenerate, true);

            EditorGUILayout.Space(8);

            EditorGUI.BeginDisabledGroup(
                !EditorSceneManager.GetActiveScene().IsValid() ||
                EditorApplication.isPlayingOrWillChangePlaymode);
            if (GUILayout.Button("Bake Item Pool", GUILayout.Height(30)))
            {
                BakePool();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "BAKE MANUEL avant upload : cliquez sur « Bake Item Pool » pour instancier " +
                "les préfabs dans la scène (la génération n'est PLUS automatique au build). " +
                "Un re-bake SUPPRIME d'abord le pool précédent (instances « ... (Pool N) ») " +
                "avant de le régénérer. À l'entrée en Play Mode (ClientSim / Build & Test), " +
                "la duplication reste AUTOMATIQUE. Tant que le pool n'est pas baké, la scène " +
                "reste PROPRE en édition : aucune instance de pool dans la hiérarchie.",
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        // =============================================
        //  REORDERABLE LIST CALLBACKS
        // =============================================

        /// <summary>Header : "# | Prefab | Count" (réserve la place des boutons +/-).</summary>
        private void DrawEntriesHeader(Rect rect)
        {
            const float countW = 55f;
            const float buttonsW = 45f;

            float usable = rect.width - buttonsW;

            EditorGUI.LabelField(new Rect(rect.x, rect.y, 30, rect.height), "#", EditorStyles.miniLabel);
            EditorGUI.LabelField(new Rect(rect.x + 30, rect.y, usable - 30 - countW - 5, rect.height), "Prefab", EditorStyles.miniLabel);
            EditorGUI.LabelField(new Rect(rect.x + usable - countW, rect.y, countW, rect.height), "Count", EditorStyles.miniLabel);
        }

        /// <summary>Ligne : index | Prefab | Count.</summary>
        private void DrawEntriesElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty entry = _propEntries.GetArrayElementAtIndex(index);
            SerializedProperty prefabProp = entry.FindPropertyRelative("Prefab");
            SerializedProperty countProp = entry.FindPropertyRelative("Count");

            const float countW = 55f;

            EditorGUI.LabelField(new Rect(rect.x, rect.y, 25, rect.height), index.ToString());
            EditorGUI.PropertyField(new Rect(rect.x + 30, rect.y, rect.width - 30 - countW - 5, rect.height), prefabProp, GUIContent.none);
            EditorGUI.PropertyField(new Rect(rect.x + rect.width - countW, rect.y, countW, rect.height), countProp, GUIContent.none);
        }

        private void OnAddEntry(ReorderableList list)
        {
            if (_propEntries == null) return;

            int idx = _propEntries.arraySize;
            _propEntries.arraySize++;

            SerializedProperty entry = _propEntries.GetArrayElementAtIndex(idx);
            entry.FindPropertyRelative("Prefab").objectReferenceValue = null;
            entry.FindPropertyRelative("Count").intValue = 1;
        }

        private void OnRemoveEntry(ReorderableList list)
        {
            if (_propEntries == null) return;

            int idx = list.index;
            if (idx >= 0 && idx < _propEntries.arraySize)
                _propEntries.DeleteArrayElementAtIndex(idx);
        }

        private void OnReorderEntries(ReorderableList list, int oldIndex, int newIndex)
        {
            if (_propEntries == null) return;
            _propEntries.MoveArrayElement(oldIndex, newIndex);
        }

        // =============================================
        //  BAKE MANUEL DU POOL
        // =============================================

        /// <summary>
        /// Bake manuel : matérialise le pool dans la scène (instances + sync
        /// Udon + tailles de registres du Manager), puis sauvegarde la scène.
        /// ⚠ DelayCall : la génération peut DÉTRUIRE ce marqueur
        /// (RemoveAfterGenerate) pendant le rendu de l'inspector — on sort donc
        /// d'abord du OnInspectorGUI avant de générer.
        /// </summary>
        private void BakePool()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[ItemSpawner] Aucune scène active : bake du pool impossible.");
                return;
            }

            EditorApplication.delayCall += () =>
            {
                M2922_ItemSpawnerGenerator.GenerateAndUpdate(scene);
                EditorSceneManager.SaveScene(scene);
            };
        }
    }
}
