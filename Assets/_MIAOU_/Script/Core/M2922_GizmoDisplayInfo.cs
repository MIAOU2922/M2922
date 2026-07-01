using UnityEngine;

namespace M2922.Core
{
    /// <summary>
    /// Paire label/valeur affichée par le système d'auto-gizmo.
    /// Chaque composant peut override GetGizmoValues() pour exposer ses données importantes.
    /// </summary>
    [System.Serializable]
    public struct M2922_GizmoDisplayInfo
    {
        public string Label;
        public string Value;
        public Color Color;

        public M2922_GizmoDisplayInfo(string label, string value)
        {
            Label = label;
            Value = value;
            Color = Color.white;
        }

        public M2922_GizmoDisplayInfo(string label, string value, Color color)
        {
            Label = label;
            Value = value;
            Color = color;
        }
    }
}
