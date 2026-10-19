using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDKBase;
using Yamadev.YamaStream;

namespace AppleteaSystems.StageSyncSystem
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SSSYamaBridge : Listener
    {
        [SerializeField] private Controller _controller;
        [SerializeField] private SSSCore _sssCore;

        public Controller Controller
        {
            get => _controller;
            set
            {
                _controller = value;
                RegisterListener();
            }
        }

        public SSSCore SSSCore
        {
            get => _sssCore;
            set => _sssCore = value;
        }

        private void Start()
        {
            RegisterListener();
        }

        public void RegisterListener()
        {
            if (_controller != null)
            {
                _controller.AddListener(this);
            }
        }

        public override void OnVideoReady()
        {
            if (_sssCore != null) _sssCore.OnVideoReady();
        }

        public override void OnVideoStart()
        {
            if (_sssCore != null) _sssCore.OnVideoStart();
        }

        public override void OnVideoEnd()
        {
            if (_sssCore != null) _sssCore.OnVideoEnd();
        }

        public override void OnVideoLoop()
        {
            if (_sssCore != null) _sssCore.OnVideoLoop();
        }

        public override void OnVideoError(VideoError videoError)
        {
            if (_sssCore != null) _sssCore.OnVideoError(videoError);
        }

        public override void OnVideoPlay()
        {
            if (_sssCore != null) _sssCore.OnVideoPlay();
        }

        public override void OnVideoPause()
        {
            if (_sssCore != null) _sssCore.OnVideoPause();
        }

        public override void OnVideoStop()
        {
            if (_sssCore != null) _sssCore.OnVideoStop();
        }

        public override void OnSetTime(float time)
        {
            if (_sssCore != null) _sssCore.OnVideoSetTime(time);
        }
    }
}

