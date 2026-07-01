using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BoneFollower = M2922.Component.Player.M2922_BoneFollower;

namespace M2922.Component.Player
{
    [AddComponentMenu("M2922/Player/Armature Bone Setup")]
    public class M2922_ArmatureBoneSetup : MonoBehaviour
    {
        [Header("=== BONE SETUP ===")]
        [Tooltip("Si coché, écrase les M2922_BoneFollower existants. Sinon, les conserve.")]
        public bool overwriteExisting = true;

        [Tooltip("Si coché, active le suivi de rotation sur tous les os.")]
        public bool defaultTrackRotation = true;

        [Tooltip("Si coché, active le suivi de position sur tous les os.")]
        public bool defaultTrackPosition = true;

        [Header("=== DEBUG (Éditeur) ===")]
        [Tooltip("Affiche les logs dans la console lors du setup.")]
        public bool verboseLogging = true;

        private static readonly Dictionary<string, HumanBodyBones> BoneNameMapping = new Dictionary<string, HumanBodyBones>
        {
            // Tronc
            { "hips", HumanBodyBones.Hips },
            { "pelvis", HumanBodyBones.Hips },
            { "spine", HumanBodyBones.Spine },
            { "spine1", HumanBodyBones.Spine },
            { "chest", HumanBodyBones.Chest },
            { "spine2", HumanBodyBones.Chest },
            { "upperspine", HumanBodyBones.UpperChest },
            { "upperchest", HumanBodyBones.UpperChest },
            { "neck", HumanBodyBones.Neck },
            { "head", HumanBodyBones.Head },

            // Bras gauche
            { "leftshoulder", HumanBodyBones.LeftShoulder },
            { "lshoulder", HumanBodyBones.LeftShoulder },
            { "shoulder_l", HumanBodyBones.LeftShoulder },
            { "leftupperarm", HumanBodyBones.LeftUpperArm },
            { "lupperarm", HumanBodyBones.LeftUpperArm },
            { "upperarm_l", HumanBodyBones.LeftUpperArm },
            { "leftarm", HumanBodyBones.LeftUpperArm },
            { "larm", HumanBodyBones.LeftUpperArm },
            { "leftlowerarm", HumanBodyBones.LeftLowerArm },
            { "llowerarm", HumanBodyBones.LeftLowerArm },
            { "lowerarm_l", HumanBodyBones.LeftLowerArm },
            { "leftforearm", HumanBodyBones.LeftLowerArm },
            { "leftelbow", HumanBodyBones.LeftLowerArm },
            { "lefthand", HumanBodyBones.LeftHand },
            { "lhand", HumanBodyBones.LeftHand },
            { "hand_l", HumanBodyBones.LeftHand },

            // Bras droit
            { "rightshoulder", HumanBodyBones.RightShoulder },
            { "rshoulder", HumanBodyBones.RightShoulder },
            { "shoulder_r", HumanBodyBones.RightShoulder },
            { "rightupperarm", HumanBodyBones.RightUpperArm },
            { "rupperarm", HumanBodyBones.RightUpperArm },
            { "upperarm_r", HumanBodyBones.RightUpperArm },
            { "rightarm", HumanBodyBones.RightUpperArm },
            { "rarm", HumanBodyBones.RightUpperArm },
            { "rightlowerarm", HumanBodyBones.RightLowerArm },
            { "rlowerarm", HumanBodyBones.RightLowerArm },
            { "lowerarm_r", HumanBodyBones.RightLowerArm },
            { "rightforearm", HumanBodyBones.RightLowerArm },
            { "rightelbow", HumanBodyBones.RightLowerArm },
            { "righthand", HumanBodyBones.RightHand },
            { "rhand", HumanBodyBones.RightHand },
            { "hand_r", HumanBodyBones.RightHand },

            // Jambe gauche
            { "leftupperleg", HumanBodyBones.LeftUpperLeg },
            { "lupperleg", HumanBodyBones.LeftUpperLeg },
            { "upperleg_l", HumanBodyBones.LeftUpperLeg },
            { "leftupleg", HumanBodyBones.LeftUpperLeg },
            { "leftthigh", HumanBodyBones.LeftUpperLeg },
            { "lthigh", HumanBodyBones.LeftUpperLeg },
            { "leftlowerleg", HumanBodyBones.LeftLowerLeg },
            { "llowerleg", HumanBodyBones.LeftLowerLeg },
            { "lowerleg_l", HumanBodyBones.LeftLowerLeg },
            { "leftleg", HumanBodyBones.LeftLowerLeg },
            { "leftcalf", HumanBodyBones.LeftLowerLeg },
            { "leftshin", HumanBodyBones.LeftLowerLeg },
            { "leftfoot", HumanBodyBones.LeftFoot },
            { "lfoot", HumanBodyBones.LeftFoot },
            { "foot_l", HumanBodyBones.LeftFoot },
            { "lefttoes", HumanBodyBones.LeftToes },
            { "ltoes", HumanBodyBones.LeftToes },
            { "lefttoebase", HumanBodyBones.LeftToes },

            // Jambe droite
            { "rightupperleg", HumanBodyBones.RightUpperLeg },
            { "rupperleg", HumanBodyBones.RightUpperLeg },
            { "upperleg_r", HumanBodyBones.RightUpperLeg },
            { "rightupleg", HumanBodyBones.RightUpperLeg },
            { "rightthigh", HumanBodyBones.RightUpperLeg },
            { "rthigh", HumanBodyBones.RightUpperLeg },
            { "rightlowerleg", HumanBodyBones.RightLowerLeg },
            { "rlowerleg", HumanBodyBones.RightLowerLeg },
            { "lowerleg_r", HumanBodyBones.RightLowerLeg },
            { "rightleg", HumanBodyBones.RightLowerLeg },
            { "rightcalf", HumanBodyBones.RightLowerLeg },
            { "rightshin", HumanBodyBones.RightLowerLeg },
            { "rightfoot", HumanBodyBones.RightFoot },
            { "rfoot", HumanBodyBones.RightFoot },
            { "foot_r", HumanBodyBones.RightFoot },
            { "righttoes", HumanBodyBones.RightToes },
            { "rtoes", HumanBodyBones.RightToes },
            { "righttoebase", HumanBodyBones.RightToes },

            // Doigts main gauche (noms standard Unity/Mixamo)
            { "leftthumbproximal", HumanBodyBones.LeftThumbProximal },
            { "leftthumbintermediate", HumanBodyBones.LeftThumbIntermediate },
            { "leftthumbdistal", HumanBodyBones.LeftThumbDistal },
            { "lefthandthumb1", HumanBodyBones.LeftThumbProximal },
            { "lefthandthumb2", HumanBodyBones.LeftThumbIntermediate },
            { "lefthandthumb3", HumanBodyBones.LeftThumbDistal },
            { "leftindexproximal", HumanBodyBones.LeftIndexProximal },
            { "leftindexintermediate", HumanBodyBones.LeftIndexIntermediate },
            { "leftindexdistal", HumanBodyBones.LeftIndexDistal },
            { "lefthandindex1", HumanBodyBones.LeftIndexProximal },
            { "lefthandindex2", HumanBodyBones.LeftIndexIntermediate },
            { "lefthandindex3", HumanBodyBones.LeftIndexDistal },
            { "leftmiddleproximal", HumanBodyBones.LeftMiddleProximal },
            { "leftmiddleintermediate", HumanBodyBones.LeftMiddleIntermediate },
            { "leftmiddledistal", HumanBodyBones.LeftMiddleDistal },
            { "lefthandmiddle1", HumanBodyBones.LeftMiddleProximal },
            { "lefthandmiddle2", HumanBodyBones.LeftMiddleIntermediate },
            { "lefthandmiddle3", HumanBodyBones.LeftMiddleDistal },
            { "leftringproximal", HumanBodyBones.LeftRingProximal },
            { "leftringintermediate", HumanBodyBones.LeftRingIntermediate },
            { "leftringdistal", HumanBodyBones.LeftRingDistal },
            { "lefthandring1", HumanBodyBones.LeftRingProximal },
            { "lefthandring2", HumanBodyBones.LeftRingIntermediate },
            { "lefthandring3", HumanBodyBones.LeftRingDistal },
            { "leftlittleproximal", HumanBodyBones.LeftLittleProximal },
            { "leftlittleintermediate", HumanBodyBones.LeftLittleIntermediate },
            { "leftlittledistal", HumanBodyBones.LeftLittleDistal },
            { "lefthandpinky1", HumanBodyBones.LeftLittleProximal },
            { "lefthandpinky2", HumanBodyBones.LeftLittleIntermediate },
            { "lefthandpinky3", HumanBodyBones.LeftLittleDistal },

            // Doigts main droite (noms standard Unity/Mixamo)
            { "rightthumbproximal", HumanBodyBones.RightThumbProximal },
            { "rightthumbintermediate", HumanBodyBones.RightThumbIntermediate },
            { "rightthumbdistal", HumanBodyBones.RightThumbDistal },
            { "righthandthumb1", HumanBodyBones.RightThumbProximal },
            { "righthandthumb2", HumanBodyBones.RightThumbIntermediate },
            { "righthandthumb3", HumanBodyBones.RightThumbDistal },
            { "rightindexproximal", HumanBodyBones.RightIndexProximal },
            { "rightindexintermediate", HumanBodyBones.RightIndexIntermediate },
            { "rightindexdistal", HumanBodyBones.RightIndexDistal },
            { "righthandindex1", HumanBodyBones.RightIndexProximal },
            { "righthandindex2", HumanBodyBones.RightIndexIntermediate },
            { "righthandindex3", HumanBodyBones.RightIndexDistal },
            { "rightmiddleproximal", HumanBodyBones.RightMiddleProximal },
            { "rightmiddleintermediate", HumanBodyBones.RightMiddleIntermediate },
            { "rightmiddledistal", HumanBodyBones.RightMiddleDistal },
            { "righthandmiddle1", HumanBodyBones.RightMiddleProximal },
            { "righthandmiddle2", HumanBodyBones.RightMiddleIntermediate },
            { "righthandmiddle3", HumanBodyBones.RightMiddleDistal },
            { "rightringproximal", HumanBodyBones.RightRingProximal },
            { "rightringintermediate", HumanBodyBones.RightRingIntermediate },
            { "rightringdistal", HumanBodyBones.RightRingDistal },
            { "righthandring1", HumanBodyBones.RightRingProximal },
            { "righthandring2", HumanBodyBones.RightRingIntermediate },
            { "righthandring3", HumanBodyBones.RightRingDistal },
            { "rightlittleproximal", HumanBodyBones.RightLittleProximal },
            { "rightlittleintermediate", HumanBodyBones.RightLittleIntermediate },
            { "rightlittledistal", HumanBodyBones.RightLittleDistal },
            { "righthandpinky1", HumanBodyBones.RightLittleProximal },
            { "righthandpinky2", HumanBodyBones.RightLittleIntermediate },
            { "righthandpinky3", HumanBodyBones.RightLittleDistal },

            // Yeux / Mâchoire
            { "lefteye", HumanBodyBones.LeftEye },
            { "righteye", HumanBodyBones.RightEye },
            { "jaw", HumanBodyBones.Jaw },
        };
        public static bool TryGetBoneFromName(string transformName, out HumanBodyBones bone)
        {
            string cleaned = transformName.ToLowerInvariant()
                .Replace(" ", "")
                .Replace("_", "")
                .Replace("-", "");

            if (BoneNameMapping.TryGetValue(cleaned, out bone))
                return true;

            // Fallback : essayer de parser directement le nom comme enum HumanBodyBones
            if (System.Enum.TryParse(transformName, true, out bone))
                return true;

            return false;
        }
        [ContextMenu("Setup All Bones")]
        public void SetupAllBones()
        {
#if UNITY_EDITOR
            Transform[] allChildren = GetComponentsInChildren<Transform>();
            int matched = 0;
            int skipped = 0;
            int total = allChildren.Length;

            if (verboseLogging)
                Debug.Log($"[M2922_ArmatureBoneSetup] Scan de {total} transforms dans l'armature <b>{gameObject.name}</b>...");

            foreach (Transform child in allChildren)
            {
                // Ignorer le root lui-même (sauf s'il correspond à un os)
                if (child == transform && !TryGetBoneFromName(child.name, out _))
                    continue;

                if (TryGetBoneFromName(child.name, out HumanBodyBones bone))
                {
                    // Vérifier si un M2922_BoneFollower existe déjà
                    var existing = child.GetComponent<BoneFollower>();

                    if (existing != null && !overwriteExisting)
                    {
                        if (verboseLogging)
                            Debug.Log($"  → <color=grey>[SKIP]</color> {child.name} a déjà un BoneFollower (trackedBone={existing.trackedBone})");
                        skipped++;
                        continue;
                    }

                    if (existing != null)
                    {
                        // Écraser
                        UnityEditor.Undo.DestroyObjectImmediate(existing);
                    }

                    var follower = UnityEditor.Undo.AddComponent<BoneFollower>(child.gameObject);
                    follower.trackedBone = bone;
                    follower.trackRotation = defaultTrackRotation;
                    follower.trackPosition = defaultTrackPosition;

                    if (verboseLogging)
                        Debug.Log($"  → <color=green>[OK]</color> {child.name} → HumanBodyBones.{bone}");
                    matched++;
                }
                else
                {
                    if (verboseLogging)
                        Debug.Log($"  → <color=orange>[?]</color> {child.name} : pas de correspondance HumanBodyBones trouvée");
                    skipped++;
                }
            }

            Debug.Log($"[M2922_ArmatureBoneSetup] <b>Terminé !</b> {matched} os configurés, {skipped} ignorés sur {total} transforms.");
#endif
        }
        [ContextMenu("Remove All Bone Followers")]
        public void RemoveAllBoneFollowers()
        {
#if UNITY_EDITOR
            var followers = GetComponentsInChildren<BoneFollower>();
            int count = followers.Length;

            foreach (var follower in followers)
            {
                UnityEditor.Undo.DestroyObjectImmediate(follower);
            }

            Debug.Log($"[M2922_ArmatureBoneSetup] <b>{count} BoneFollower</b> retirés de l'armature <b>{gameObject.name}</b>.");
#endif
        }
    }
}
