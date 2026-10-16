using UdonSharp;
using UnityEngine;
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

        public override void OnVideoPlay()
        {
            if (_sssCore != null) _sssCore.SendCustomEvent("OnVideoPlay");
        }

        public override void OnVideoPause()
        {
            if (_sssCore != null) _sssCore.SendCustomEvent("OnVideoPause");
        }

        public override void OnVideoStop()
        {
            if (_sssCore != null) _sssCore.SendCustomEvent("OnVideoStop");
        }
    }
}
