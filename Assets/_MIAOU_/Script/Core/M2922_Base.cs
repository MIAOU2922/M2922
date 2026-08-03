using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.StringLoading;
using VRC.Udon.Common.Interfaces;
using VRC.SDK3.Data;
using VRC.SDK3.Image;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using System.Buffers;

namespace M2922.Core
{
    public class M2922_Base : UdonSharpBehaviour
    {
        [Header("=== GLOBAL DEBUG ===")]
        public bool DEBUG = false;
        public bool VERBOSE_DEBUG = false;

        [Header("=== MANAGER REFERENCE ===")]
        public M2922_Manager Manager;

        [Header("=== SCRIPT IDENTITY ===")]
        [SerializeField] private bool _autoName = true;
        [SerializeField] private string _ScriptName = "";
        public string ScriptName => string.IsNullOrEmpty(_ScriptName) ? this.GetType().Name : _ScriptName;

        [Header("=== GIZMO ===")]
        [SerializeField] private bool _showGizmo = true;

        [Header("=== AUTO GIZMO ===")]
        [SerializeField] private bool _autoGizmo = true;
        [SerializeField] private float _gizmoOffsetY = 0.3f;
        [SerializeField] private float _gizmoHeaderScale = 12f;
        [SerializeField] private float _gizmoValueScale = 10f;
        [SerializeField] private Color _gizmoHeaderColor = new Color(0f, 1f, 0.8f);
        [SerializeField] private bool _gizmoOnlyWhenSelected = false;
        [HideInInspector] [SerializeField] private bool __gizmoColorInitialized = false;

        /// <summary>
        /// Surchargez pour auto-détecter les références (GetComponent) sur ce GameObject.
        /// Appelé depuis OnValidate() (éditeur) et Start() (runtime).
        /// </summary>
        protected virtual void AutoDetectReferences() { }

        //methodes
        // recherche le manager dans la scene
        private void TryFindManager()
        {
            GameObject _ManagerObj = GameObject.Find("Manager");
            if (_ManagerObj == null) return;
            Manager = _ManagerObj.GetComponent<M2922_Manager>();
            if (Manager == null) return;
        }
#if !COMPILER_UDONSHARP && UNITY_EDITOR

        /// <summary>
        /// Génère une couleur stable et distincte à partir d'un nom (hash du nom de classe).
        /// </summary>
        private static Color GetStableColor(string name)
        {
            int hash = name.GetHashCode();
            // Générer une teinte (H) bien répartie sur 0-1
            float h = ((hash & 0xFFFF) / 65535f);
            // Saturation et valeur élevées pour des couleurs vives et lisibles
            float s = 0.7f + ((hash >> 16) & 0xFF) / 255f * 0.3f;  // 0.7 - 1.0
            float v = 0.75f + ((hash >> 24) & 0xFF) / 255f * 0.25f; // 0.75 - 1.0
            return Color.HSVToRGB(h, s, v);
        }

        // validation dans l'editeur
        protected virtual void OnValidate()
        {
            if (_autoName)
                _ScriptName = this.GetType().Name;
            else if (string.IsNullOrEmpty(_ScriptName))
                _ScriptName = this.GetType().Name;

            // Auto-assigner une couleur de gizmo unique basée sur le nom de la classe
            if (!__gizmoColorInitialized)
            {
                _gizmoHeaderColor = GetStableColor(this.GetType().Name);
                __gizmoColorInitialized = true;
            }

            if (Manager == null) TryFindManager();
            SetDebugFlags();
            AutoDetectReferences();
        }

        protected virtual void OnDrawGizmos()
        {
            if (!_showGizmo) return;
            if (!_gizmoOnlyWhenSelected)
                DrawAutoGizmo();
        }

        protected virtual void OnDrawGizmosSelected()
        {
            if (!_showGizmo) return;
            if (_gizmoOnlyWhenSelected)
                DrawAutoGizmo();
        }

        /// <summary>
        /// Surchargez cette méthode pour afficher les valeurs importantes du composant dans le gizmo.
        /// Retourne un tableau de paires label/valeur.
        /// </summary>
        protected virtual M2922_GizmoDisplayInfo[] GetGizmoValues() { return null; }

        private void DrawAutoGizmo()
        {
            if (!_autoGizmo) return;

            // Récupérer tous les M2922_Base sur ce GameObject
            var allScripts = GetComponents<M2922_Base>();
            if (allScripts == null || allScripts.Length == 0) return;

            int total = allScripts.Length;
            int index = -1;
            for (int i = 0; i < total; i++)
            {
                if (allScripts[i] == this) { index = i; break; }
            }
            if (index < 0) return;

            // Offset vertical : on empile du haut vers le bas
            float yBase = transform.position.y + (total - 1) * _gizmoOffsetY * 0.5f;
            float yPos = yBase - index * _gizmoOffsetY;

            Vector3 labelPos = new Vector3(transform.position.x, yPos, transform.position.z);

            // Style header (nom du script)
            GUIStyle headerStyle = new GUIStyle();
            headerStyle.normal.textColor = _gizmoHeaderColor;
            headerStyle.fontSize = Mathf.RoundToInt(_gizmoHeaderScale);
            headerStyle.fontStyle = FontStyle.Bold;

            UnityEditor.Handles.Label(labelPos, ScriptName, headerStyle);

            // Valeurs importantes
            M2922_GizmoDisplayInfo[] values = GetGizmoValues();
            if (values != null && values.Length > 0)
            {
                GUIStyle valueStyle = new GUIStyle();
                valueStyle.fontSize = Mathf.RoundToInt(_gizmoValueScale);
                valueStyle.normal.textColor = Color.white;

                float valueOffsetY = _gizmoOffsetY * 0.35f;

                for (int i = 0; i < values.Length; i++)
                {
                    if (string.IsNullOrEmpty(values[i].Label)) continue;

                    Vector3 valPos = labelPos + Vector3.down * ((i + 1) * valueOffsetY);

                    valueStyle.normal.textColor = values[i].Color != default(Color)
                        ? values[i].Color
                        : Color.white;

                    string line = string.IsNullOrEmpty(values[i].Value)
                        ? values[i].Label
                        : $"{values[i].Label}: {values[i].Value}";

                    UnityEditor.Handles.Label(valPos, line, valueStyle);
                }
            }

            // Point de repère
            Gizmos.color = _gizmoHeaderColor;
            Gizmos.DrawSphere(transform.position, 0.05f);
        }
#endif
        protected virtual void Start()
        {
            if (_autoName)
            _ScriptName = this.GetType().Name;
            else if (string.IsNullOrEmpty(_ScriptName))
            _ScriptName = this.GetType().Name;
            if (Manager == null) TryFindManager();
            SetDebugFlags();
            AutoDetectReferences();
        }
        protected virtual void Update()
        {
            if (Manager == null) TryFindManager();
            if (Manager != null && (DEBUG != Manager.DEBUG || VERBOSE_DEBUG != Manager.VERBOSE_DEBUG))
            {
                SetDebugFlags();
            }
        }
        private void SetDebugFlags()
        {
            if (Manager == null || Manager == this.gameObject) return;
            DEBUG = Manager.DEBUG;
            VERBOSE_DEBUG = Manager.VERBOSE_DEBUG;
        }
    }
}