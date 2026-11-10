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

        [SerializeField]
        [Tooltip("演者にも演出を再生するか（デフォルト: OFF＝負荷軽減のため演者には演出を再生しない）")]
        private bool PlayForPerformer = false;

        //Getter
        public string directionName => DirectionName;
        public PlayableDirector playableDirector => GetComponent<PlayableDirector>();
        public VRCUrl videoURL => VideoURL;
        public bool playForPerformer => PlayForPerformer;
    }
}