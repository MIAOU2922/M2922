using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Component.Physics
{
    /// <summary>
    /// Déplacement de base : walk, swim, sprint.
    /// </summary>
    [AddComponentMenu("M2922/Physics/Movement Component")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_MovementComponent : M2922_Base
    {
        [Header("=== MOVEMENT ===")]
        [SerializeField] private float _walkSpeed = 4f;
        [SerializeField] private float _sprintSpeed = 7f;
        [SerializeField] private float _acceleration = 10f;
        [SerializeField] private float _jumpForce = 5f;

        private Rigidbody _rb;
        private M2922_GravityComponent _gravity;
        private bool _isGrounded = true;

        public float WalkSpeed => _walkSpeed;
        public float SprintSpeed => _sprintSpeed;
        public float JumpForce => _jumpForce;
        public Vector3 MoveDirection { get; set; }
        public bool WantsJump { get; set; }
        public bool WantsSprint { get; set; }

        protected override void Start()
        {
            base.Start();
            _rb = GetComponent<Rigidbody>();
            _gravity = GetComponent<M2922_GravityComponent>();
        }

        protected override void Update()
        {
            base.Update();
        }

        private void FixedUpdate()
        {
            // Gravité custom
            if (_gravity != null)
                _rb.AddForce(_gravity.GetGravity(), ForceMode.Acceleration);

            // Mouvement
            float targetSpeed = WantsSprint ? _sprintSpeed : _walkSpeed;
            Vector3 targetVelocity = MoveDirection * targetSpeed;
            targetVelocity.y = _rb.velocity.y;

            _rb.velocity = Vector3.Lerp(_rb.velocity, targetVelocity, _acceleration * Time.fixedDeltaTime);

            // Saut
            if (WantsJump && _isGrounded)
            {
                _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
                _isGrounded = false;
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            // Vérifie si on touche le sol
            foreach (ContactPoint contact in collision.contacts)
            {
                if (Vector3.Dot(contact.normal, Vector3.up) > 0.5f)
                    _isGrounded = true;
            }
        }

        private void OnCollisionExit()
        {
            _isGrounded = false;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override M2922_GizmoDisplayInfo[] GetGizmoValues()
        {
            return new M2922_GizmoDisplayInfo[]
            {
                new M2922_GizmoDisplayInfo("Walk", $"{_walkSpeed:F1}"),
                new M2922_GizmoDisplayInfo("Sprint", $"{_sprintSpeed:F1}"),
                new M2922_GizmoDisplayInfo("Jump", $"{_jumpForce:F1}"),
            };
        }
#endif
    }
}
