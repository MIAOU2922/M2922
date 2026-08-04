using UnityEditor;

namespace M2922.Component.UI.Editor
{
    /// <summary>
    /// Custom editor pour M2922_PlayerInfoUI.
    /// Hérite de M2922_CounterUIEditor pour hériter de la
    /// ReorderableList COLOR THRESHOLDS.
    /// </summary>
    [CustomEditor(typeof(M2922_PlayerInfoUI))]
    [CanEditMultipleObjects]
    public class M2922_PlayerInfoUIEditor : M2922_CounterUIEditor
    {
    }
}
