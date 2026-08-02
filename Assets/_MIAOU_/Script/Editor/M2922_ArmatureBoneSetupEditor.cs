using UnityEditor;
using UnityEngine;

namespace M2922.Editor
{
    /// <summary>
    /// Custom editor pour M2922_ArmatureBoneSetup : ajoute des boutons dans l'inspecteur.
    /// </summary>
    [CustomEditor(typeof(M2922.Component.Player.M2922_ArmatureBoneSetup))]
    [CanEditMultipleObjects]
    public class M2922_ArmatureBoneSetupEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var setup = (M2922.Component.Player.M2922_ArmatureBoneSetup)target;

            GUILayout.Space(10);

            // === BOUTON SETUP ===
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.4f);
            if (GUILayout.Button("🔧 Setup All Bones", GUILayout.Height(40)))
            {
                setup.SetupAllBones();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(5);

            // === BOUTON REMOVE ===
            GUI.backgroundColor = new Color(0.9f, 0.35f, 0.3f);
            if (GUILayout.Button("🗑 Remove All Bone Followers", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog(
                    "Confirmer la suppression",
                    $"Retirer TOUS les M2922_BoneFollower de l'armature \"{setup.gameObject.name}\" et ses enfants ?",
                    "Oui, supprimer",
                    "Annuler"))
                {
                    setup.RemoveAllBoneFollowers();
                }
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(10);

            // === INFOS ===
            EditorGUILayout.HelpBox(
                "Placez ce script sur le root de l'armature.\n" +
                "Cliquez sur \"Setup All Bones\" pour ajouter automatiquement un M2922_BoneFollower " +
                "sur chaque os reconnu de l'armature.\n\n" +
                "Après le setup, vous pouvez retirer ce script — il ne sert qu'à la configuration.",
                MessageType.Info);
        }
    }
}
