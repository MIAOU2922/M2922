using UnityEditor;
using UnityEngine;
using UdonSharpEditor;
using M2922.World;

namespace M2922.Editor
{
    [CustomEditor(typeof(M2922_Teleporter))]
    public class M2922_TeleporterEditor : M2922_BaseEditor
    {
        // ── Destination ───────────────────────────────────────────────────────
        private SerializedProperty _destinationProp;

        // ── Activation ────────────────────────────────────────────────────────
        private SerializedProperty _activationModeProp;
        private SerializedProperty _isEnabledProp;

        // ── Filter ────────────────────────────────────────────────────────────
        private SerializedProperty _filterProp;

        // ── Options ───────────────────────────────────────────────────────────
        private SerializedProperty _teleportDelayProp;
        private SerializedProperty _cooldownProp;
        private SerializedProperty _alignRotationProp;
        private SerializedProperty _oneShotProp;

        // ── VFX / SFX ─────────────────────────────────────────────────────────
        private SerializedProperty _departEffectProp;
        private SerializedProperty _arrivalEffectProp;
        private SerializedProperty _teleportAudioProp;

        // ── UI state ──────────────────────────────────────────────────────────
        private bool _optionsOpen = true;
        private bool _vfxOpen     = false;

        // ─────────────────────────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            _destinationProp    = serializedObject.FindProperty("_destination");
            _activationModeProp = serializedObject.FindProperty("_activationMode");
            _isEnabledProp      = serializedObject.FindProperty("_isEnabled");
            _filterProp         = serializedObject.FindProperty("_filter");
            _teleportDelayProp  = serializedObject.FindProperty("_teleportDelay");
            _cooldownProp       = serializedObject.FindProperty("_cooldown");
            _alignRotationProp  = serializedObject.FindProperty("_alignRotation");
            _oneShotProp        = serializedObject.FindProperty("_oneShot");
            _departEffectProp   = serializedObject.FindProperty("_departEffect");
            _arrivalEffectProp  = serializedObject.FindProperty("_arrivalEffect");
            _teleportAudioProp  = serializedObject.FindProperty("_teleportAudio");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            serializedObject.Update();
            M2922_Teleporter tp = (M2922_Teleporter)target;

            // =================================================================
            // DESTINATION
            // =================================================================
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("DESTINATION", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_destinationProp, new GUIContent("Destination Transform"));

            if (_destinationProp.objectReferenceValue == null)
                EditorGUILayout.HelpBox("Aucune destination assignée ! Le téléporteur ne fonctionnera pas.", MessageType.Error);
            else
            {
                Transform dest = (Transform)_destinationProp.objectReferenceValue;
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.Vector3Field("Position arrivée", dest.position);
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Ping destination", GUILayout.Width(130)))
                    EditorGUIUtility.PingObject(dest.gameObject);
                if (GUILayout.Button("Sélectionner"))
                    Selection.activeGameObject = dest.gameObject;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(4);

            // =================================================================
            // ACTIVATION
            // =================================================================
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("ACTIVATION", EditorStyles.boldLabel);

            // Enabled toggle avec couleur
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = _isEnabledProp.boolValue ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.4f, 0.4f);
            EditorGUILayout.PropertyField(_isEnabledProp, new GUIContent("Enabled"));
            GUI.backgroundColor = prevBg;

            EditorGUILayout.PropertyField(_activationModeProp, new GUIContent("Mode d'activation"));

            TeleporterActivation mode = (TeleporterActivation)_activationModeProp.enumValueIndex;

            if (mode == TeleporterActivation.TriggerZone || mode == TeleporterActivation.Both)
            {
                bool hasCollider = tp.GetComponent<Collider>() != null;
                if (!hasCollider)
                    EditorGUILayout.HelpBox(
                        "TriggerZone : ajoutez un Collider (Is Trigger) sur ce GameObject.",
                        MessageType.Warning);
                else
                {
                    Collider col = tp.GetComponent<Collider>();
                    if (!col.isTrigger)
                        EditorGUILayout.HelpBox(
                            "Le Collider doit être en mode 'Is Trigger'.",
                            MessageType.Warning);
                }
            }

            if (mode == TeleporterActivation.Interaction || mode == TeleporterActivation.Both)
            {
                bool hasInteractable = tp.GetComponent<VRC.SDKBase.VRC_Interactable>() != null;
                if (!hasInteractable)
                    EditorGUILayout.HelpBox(
                        "Interaction : ajoutez un composant VRC_Interactable sur ce GameObject " +
                        "et configurez le texte d'interaction.",
                        MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(4);

            // =================================================================
            // FILTER
            // =================================================================
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("FILTER", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_filterProp, new GUIContent("Filtre"));

            TeleporterFilter filter = (TeleporterFilter)_filterProp.enumValueIndex;
            switch (filter)
            {
                case TeleporterFilter.PlayersOnly:
                    EditorGUILayout.HelpBox("Seuls les joueurs VRC seront téléportés.", MessageType.None);
                    break;
                case TeleporterFilter.EntitiesOnly:
                    EditorGUILayout.HelpBox(
                        "Seules les M2922_Entity (props, armes…) seront téléportées.\n" +
                        "Interaction ne peut téléporter que des joueurs — ce filtre la désactive.",
                        MessageType.None);
                    break;
                case TeleporterFilter.All:
                    EditorGUILayout.HelpBox("Joueurs VRC ET entités M2922 sont téléportés.", MessageType.None);
                    break;
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(4);

            // =================================================================
            // OPTIONS
            // =================================================================
            _optionsOpen = Section("OPTIONS", _optionsOpen, () =>
            {
                EditorGUILayout.PropertyField(_teleportDelayProp, new GUIContent("Délai (s)",
                    "Temps entre l'activation et la téléportation effective. 0 = instantané."));

                EditorGUILayout.PropertyField(_cooldownProp, new GUIContent("Cooldown (s)",
                    "Durée minimale entre deux utilisations successives."));

                EditorGUILayout.PropertyField(_alignRotationProp, new GUIContent("Aligner la rotation",
                    "Oriente le joueur/entité selon la rotation de la destination."));

                EditorGUILayout.PropertyField(_oneShotProp, new GUIContent("One Shot",
                    "Se désactive automatiquement après la première utilisation."));
            });

            // =================================================================
            // VFX / SFX
            // =================================================================
            _vfxOpen = Section("VFX / SFX", _vfxOpen, () =>
            {
                EditorGUILayout.PropertyField(_departEffectProp,  new GUIContent("Effet départ"));
                EditorGUILayout.PropertyField(_arrivalEffectProp, new GUIContent("Effet arrivée"));
                EditorGUILayout.PropertyField(_teleportAudioProp, new GUIContent("Son de téléport"));
            });

            // =================================================================
            // RUNTIME STATUS
            // =================================================================
            if (Application.isPlaying)
            {
                GUILayout.Space(4);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("RUNTIME", EditorStyles.boldLabel);

                Color c = GUI.contentColor;
                GUI.contentColor = tp.IsEnabled ? Color.green : Color.red;
                EditorGUILayout.LabelField(tp.IsEnabled ? "● ACTIF" : "○ DÉSACTIVÉ", EditorStyles.boldLabel);
                GUI.contentColor = c;

                GUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Activer"))    tp.Enable();
                if (GUILayout.Button("Désactiver")) tp.Disable();
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Téléporter joueur local"))
                    tp.TeleportLocalPlayer();

                EditorGUILayout.EndVertical();
            }

            DrawVisualDebug();
            serializedObject.ApplyModifiedProperties();
        }

        // =====================================================================
        // SCENE GUI
        // =====================================================================

        private void OnSceneGUI()
        {
            M2922_Teleporter tp = (M2922_Teleporter)target;
            if (!tp.HasDestination) return;

            Transform dest = (Transform)_destinationProp.objectReferenceValue;
            if (dest == null) return;

            // Flèche A → B
            Handles.color = tp.IsEnabled
                ? new Color(0f, 1f, 1f, 0.9f)
                : new Color(0.5f, 0.5f, 0.5f, 0.5f);

            Handles.DrawDottedLine(tp.transform.position, dest.position, 5f);

            // Flèche de direction à la destination
            float arrowSize = HandleUtility.GetHandleSize(dest.position) * 0.8f;
            Handles.ArrowHandleCap(0, dest.position, dest.rotation, arrowSize, EventType.Repaint);

            // Disque de sol à la destination
            Handles.color = new Color(0f, 1f, 1f, 0.15f);
            Handles.DrawSolidDisc(dest.position, dest.up, 0.5f);

            // Label
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.cyan;
            style.fontStyle        = FontStyle.Bold;
            Handles.Label(dest.position + Vector3.up * 0.6f, "⬇ Destination", style);
        }

    }
}
