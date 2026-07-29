using UnityEngine;
using UnityEditor;

namespace M2922.Component.Weapon.Editor
{
    /// <summary>
    /// Inspector custom pour M2922_Weapon.
    /// Expose le champ _weaponDefinition (non-sérialisé) dans l'inspecteur.
    /// </summary>
    [CustomEditor(typeof(M2922_Weapon))]
    public class M2922_WeaponEditor : UnityEditor.Editor
    {
        private M2922_Weapon _target;

        private void OnEnable()
        {
            _target = (M2922_Weapon)target;
        }

        public override void OnInspectorGUI()
        {
            // Champ WeaponDefinition (non-sérialisé, géré par cet editor)
            WeaponDefinition prevDef = _target._weaponDefinition;

            _target._weaponDefinition = (WeaponDefinition)EditorGUILayout.ObjectField(
                "Weapon Definition (Source)",
                _target._weaponDefinition,
                typeof(WeaponDefinition),
                false
            );

            // Si changé → baker
            if (_target._weaponDefinition != prevDef)
            {
                _target.BakeWeaponData();
                EditorUtility.SetDirty(_target);
            }

            // Bouton bake manuel
            EditorGUILayout.Space();
            if (GUILayout.Button("Bake Weapon Data", GUILayout.Height(25)))
            {
                _target.BakeWeaponData();
                EditorUtility.SetDirty(_target);
            }

            // Afficher les données bakées
            if (!string.IsNullOrEmpty(_target.WeaponName))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("=== BAKED DATA ===", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Name", _target.WeaponName);
                EditorGUILayout.LabelField("Type", ((WeaponType)_target.WeaponTypeAsInt).ToString());
                EditorGUILayout.LabelField("Damage", ((DamageType)_target.DamageTypeAsInt).ToString());
                EditorGUILayout.LabelField("Slot", ((WeaponSlot)_target.SlotAsInt).ToString());
                EditorGUILayout.LabelField("Rarity", ((WeaponRarity)_target.RarityAsInt).ToString());
                EditorGUILayout.LabelField("Magazine", _target.Magazine.ToString());
                EditorGUILayout.LabelField("RPM", _target.RPM.ToString("F0"));
                EditorGUILayout.LabelField("Impact", _target.Impact.ToString("F1"));
                EditorGUILayout.LabelField("Range", _target.Range.ToString("F1"));
                EditorGUILayout.LabelField("Stability", _target.Stability.ToString("F1"));
                EditorGUILayout.LabelField("Handling", _target.Handling.ToString("F1"));
            }
            else
            {
                EditorGUILayout.HelpBox("Aucune donnée bakée. Glissez un WeaponDefinition et cliquez Bake.", MessageType.Warning);
            }
        }
    }
}
