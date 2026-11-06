using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDKBase;
using HoshinoLabs.IwaSync3.Udon;

namespace AppleteaSystems.StageSyncSystem
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SSSIwaSyncBridge : VideoCoreEventListener
    {
        [SerializeField] private VideoCore _iwasyncCore;
        [SerializeField] private SSSCore _sssCore;

        private VideoCore _registeredCore;

        public VideoCore IwasyncCore
        {
            get => _iwasyncCore;
            set
            {
                _iwasyncCore = value;
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
            if (_iwasyncCore != null && _registeredCore != _iwasyncCore)
            {
                _registeredCore = _iwasyncCore;
                _iwasyncCore.AddListener(this);
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

        public override void OnPlayerPlay()
        {
            if (_sssCore != null) _sssCore.OnVideoPlay();
        }

        public override void OnPlayerPause()
        {
            if (_sssCore != null) _sssCore.OnVideoPause();
        }

        public override void OnPlayerStop()
        {
            if (_sssCore != null) _sssCore.OnVideoStop();
        }
    }
}

