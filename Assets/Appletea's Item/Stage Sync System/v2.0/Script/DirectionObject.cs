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
        [Tooltip("演出名")]
        private string DirectionName = "Default";

        [SerializeField]
        [Tooltip("演出用動画URL")]
        private VRCUrl VideoURL;

        //Getter
        public string directionName => DirectionName;
        public PlayableDirector playableDirector => GetComponent<PlayableDirector>();
        public VRCUrl videoURL => VideoURL;
    }
}