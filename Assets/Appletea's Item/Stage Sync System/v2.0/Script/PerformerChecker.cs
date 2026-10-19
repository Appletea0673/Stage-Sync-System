using UnityEngine;
using VRC.SDKBase;
using UdonSharp;

namespace AppleteaSystems.StageSyncSystem
{
    [RequireComponent(typeof(Collider))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PerformerChecker : UdonSharpBehaviour
    {
        [SerializeField]
        SSSCore SSS_core;

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (player != Networking.LocalPlayer) return;
            SSS_core.IsPerformer = true;
        }
        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            if (player != Networking.LocalPlayer) return;
            SSS_core.IsPerformer = false;
        }
    }
}