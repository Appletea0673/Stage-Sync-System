using UnityEngine;
using VRC.SDKBase;
using UnityEngine.Playables;
using UdonSharp;

namespace AppleteaSystems.StageSyncSystem
{
    [RequireComponent(typeof(PlayableDirector))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DirectionObject : UdonSharpBehaviour
    {
        [SerializeField]
        [Tooltip("‰‰o–¼")]
        private string DirectionName = "Default";

        [SerializeField]
        [Tooltip("‰‰o—p“®‰æURL")]
        private VRCUrl VideoURL;

        //Getter
        public string directionName => DirectionName;
        public PlayableDirector playableDirector => GetComponent<PlayableDirector>();
        public VRCUrl videoURL => VideoURL;
    }
}