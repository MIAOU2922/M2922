using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace M2922.Core
{
    /// <summary>
    /// Central system orchestrator — entry point that initializes all core subsystems.
    /// </summary>
    [AddComponentMenu("M2922/Core/System")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class M2922_System : M2922_Base
    {
        protected override void Start()
        {
            base.Start();
            this.Log("M2922 System initialized.");
        }
    }
}
