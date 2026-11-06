using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDKBase;
#if SSS_YAMAPLAYER
using Yamadev.YamaStream;
#endif

namespace AppleteaSystems.StageSyncSystem
{
    // YamaPlayer (net.kwxxw.yama-stream) が導入されている場合のみ SSS_YAMAPLAYER が定義され、Listener として動作する。
    // 未導入環境ではクラス自体は残し（Prefab/Scene上の参照を壊さないため）、何もしない空のブリッジとしてコンパイルされる。
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
#if SSS_YAMAPLAYER
    public class SSSYamaBridge : Listener
#else
    public class SSSYamaBridge : UdonSharpBehaviour
#endif
    {
#if SSS_YAMAPLAYER
        [SerializeField] private Controller _controller;
#endif
        [SerializeField] private SSSCore _sssCore;

#if SSS_YAMAPLAYER
        public Controller Controller
        {
            get => _controller;
            set
            {
                _controller = value;
                RegisterListener();
            }
        }
#endif

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
#if SSS_YAMAPLAYER
            if (_controller != null)
            {
                _controller.AddListener(this);
            }
#endif
        }

#if SSS_YAMAPLAYER
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
#endif
    }
}
