using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using HoshinoLabs.IwaSync3.Udon;

namespace AppleteaSystems.StageSyncSystem
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SSSIwaSyncBridge : VideoCoreEventListener
    {
        [SerializeField] private VideoCore _iwasyncCore;
        [SerializeField] private SSSCore _sssCore;

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
            if (_iwasyncCore != null)
            {
                _iwasyncCore.AddListener(this);
            }
        }

        public override void OnVideoReady()
        {
            if (_sssCore != null) _sssCore.SendCustomEvent("OnVideoReady");
        }

        public override void OnVideoStart()
        {
            if (_sssCore != null) _sssCore.SendCustomEvent("OnVideoStart");
        }

        public override void OnVideoEnd()
        {
            if (_sssCore != null) _sssCore.SendCustomEvent("OnVideoEnd");
        }

        public override void OnPlayerPlay()
        {
            if (_sssCore != null) _sssCore.SendCustomEvent("OnVideoPlay");
        }

        public override void OnPlayerPause()
        {
            if (_sssCore != null) _sssCore.SendCustomEvent("OnVideoPause");
        }

        public override void OnPlayerStop()
        {
            if (_sssCore != null) _sssCore.SendCustomEvent("OnVideoStop");
        }
    }
}
