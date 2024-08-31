using UnityEngine;
using UdonSharp;

namespace AppleteaSystems.StageSyncSystem
{
    [RequireComponent(typeof(Collider))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class DirectionSwitch : UdonSharpBehaviour
    {
        [SerializeField]
        SSSCore SSS_core;

        [SerializeField]
        int DirectionIndex = 0;

        public override void Interact() 
        {
            SSS_core.StartHandling(DirectionIndex);
        }
    }
}