using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using M2922.Core;

namespace M2922.Core
{
    /// Classe de base pour tout système du jeu (armure, santé, etc).
    public class M2922_System : M2922_Base
    {


        
        
         // === METHODE ===
        protected override void Start()
        {
            base.Start();
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
        }
        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            if (!_showGizmo) return;
        }
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (!_showGizmo) return;
        }
#endif
    }
}