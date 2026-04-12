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

        // validation dans l'editeur
        protected virtual void OnValidate()
        {
            if (Manager == null) TryFindManager();
            SetDebugFlags();
        }
#endif
        protected virtual void Start()
        {
            if (Manager == null) TryFindManager();
            SetDebugFlags();
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
            // Ne pas copier les flags si on est le Manager lui-même
            if (Manager == null || Manager.gameObject == gameObject) return;
            DEBUG = Manager.DEBUG;
            VERBOSE_DEBUG = Manager.VERBOSE_DEBUG;
        }
    }
}