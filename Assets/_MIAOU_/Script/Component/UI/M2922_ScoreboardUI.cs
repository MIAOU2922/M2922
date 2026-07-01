using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;
using M2922.Component.Game;

namespace M2922.Component.UI
{
    /// <summary>
    /// Affichage du tableau des scores.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_ScoreboardUI : M2922_Base
    {
        [Header("=== SCOREBOARD ===")]
        [SerializeField] private GameObject _scoreboardPanel;
        [SerializeField] private KeyCode _toggleKey = KeyCode.Tab;

        [Header("=== REFERENCES ===")]
        [SerializeField] private M2922_ScoreManager _scoreManager;
        [SerializeField] private M2922_TeamManager _teamManager;

        private bool _isOpen = false;

        protected override void Start()
        {
            base.Start();
            if (_scoreManager == null)
            {
                GameObject go = GameObject.Find("[M2922_ScoreManager]");
                if (go != null) _scoreManager = go.GetComponent<M2922_ScoreManager>();
            }
            if (_teamManager == null)
            {
                GameObject go = GameObject.Find("[M2922_TeamManager]");
                if (go != null) _teamManager = go.GetComponent<M2922_TeamManager>();
            }
            if (_scoreboardPanel != null) _scoreboardPanel.SetActive(false);
        }

        protected override void Update()
        {
            base.Update();

            if (Input.GetKeyDown(_toggleKey))
            {
                _isOpen = !_isOpen;
                if (_scoreboardPanel != null) _scoreboardPanel.SetActive(_isOpen);
            }

            if (_isOpen) RefreshScoreboard();
        }

        private void RefreshScoreboard()
        {
            // TODO: populate scoreboard UI with player stats
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Open", _isOpen ? "YES" : "NO", _isOpen ? Color.green : Color.gray),
                new M2922_GizmoDisplayInfo("Toggle", _toggleKey.ToString()),
            };
        }
#endif
    }
}
