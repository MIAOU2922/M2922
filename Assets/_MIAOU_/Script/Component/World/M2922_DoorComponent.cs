using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.World
{
    public enum DoorState { Closed, Open, Opening, Closing }

    /// <summary>
    /// Porte : open/close, trigger, lock, synced.
    /// </summary>
    [AddComponentMenu("M2922/World/Door Component")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_DoorComponent : M2922_Base
    {
        [Header("=== DOOR ===")]
        [SerializeField] private DoorState _state = DoorState.Closed;
        [SerializeField] private bool _isLocked = false;
        [SerializeField] private KeyCode _interactKey = KeyCode.E;

        [Header("=== ANIMATION ===")]
        [SerializeField] private Transform _doorTransform;
        [SerializeField] private Vector3 _openOffset = new Vector3(0f, 0f, 2f);
        [SerializeField] private float _speed = 3f;

        [Header("=== TRIGGER ===")]
        [SerializeField] private bool _autoOpen = false;
        [SerializeField] private float _autoCloseDelay = 3f;

        private Vector3 _closedPosition;
        private Vector3 _openPosition;
        private float _lastTriggerTime = -999f;
        private bool _playerInTrigger = false;

        public DoorState State => _state;
        public bool IsLocked { get => _isLocked; set => _isLocked = value; }

        protected override void Start()
        {
            base.Start();
            if (_doorTransform != null)
            {
                _closedPosition = _doorTransform.localPosition;
                _openPosition = _closedPosition + _openOffset;
            }
        }

        protected override void Update()
        {
            base.Update();

            switch (_state)
            {
                case DoorState.Opening:
                    MoveToward(_openPosition);
                    break;
                case DoorState.Closing:
                    MoveToward(_closedPosition);
                    break;
            }

            // Auto-close
            if (_autoOpen && _state == DoorState.Open && !_playerInTrigger
                && Time.time - _lastTriggerTime > _autoCloseDelay)
            {
                Close();
            }
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            Color stateColor = _state == DoorState.Open ? Color.green : (_state == DoorState.Closed ? Color.red : Color.yellow);
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("State", _state.ToString(), stateColor),
                new M2922_GizmoDisplayInfo("Locked", _isLocked ? "YES" : "NO", _isLocked ? Color.red : Color.green),
            };
        }
#endif

        private void MoveToward(Vector3 target)
        {
            if (_doorTransform == null) return;
            _doorTransform.localPosition = Vector3.MoveTowards(
                _doorTransform.localPosition, target, _speed * Time.deltaTime);

            if (Vector3.Distance(_doorTransform.localPosition, target) < 0.01f)
            {
                _doorTransform.localPosition = target;
                _state = target == _openPosition ? DoorState.Open : DoorState.Closed;
            }
        }

        public void Toggle()
        {
            if (_isLocked) return;
            if (_state == DoorState.Closed || _state == DoorState.Closing)
                Open();
            else if (_state == DoorState.Open || _state == DoorState.Opening)
                Close();
        }

        public void Open()
        {
            if (_isLocked || _state == DoorState.Open || _state == DoorState.Opening) return;
            _state = DoorState.Opening;
        }

        public void Close()
        {
            if (_state == DoorState.Closed || _state == DoorState.Closing) return;
            _state = DoorState.Closing;
        }

        public void OnPlayerTriggerEnter() { _playerInTrigger = true; if (_autoOpen) Open(); }
        public void OnPlayerTriggerExit() { _playerInTrigger = false; _lastTriggerTime = Time.time; }
    }
}
