using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDKBase;
#if SSS_IWASYNC
using HoshinoLabs.IwaSync3.Udon;
#endif

namespace AppleteaSystems.StageSyncSystem
{
    // iwaSync3 (HoshinoLabs.IwaSync3.Udon) が導入されている場合のみ SSS_IWASYNC が定義され、VideoCoreEventListener として動作する。
    // 未導入環境ではクラス自体は残し（Prefab/Scene上の参照を壊さないため）、何もしない空のブリッジとしてコンパイルされる。
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
#if SSS_IWASYNC
    public class SSSIwaSyncBridge : VideoCoreEventListener
#else
    public class SSSIwaSyncBridge : UdonSharpBehaviour
#endif
    {
#if SSS_IWASYNC
        [SerializeField] private VideoCore _iwasyncCore;
#endif
        [SerializeField] private SSSCore _sssCore;

#if SSS_IWASYNC
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
#if SSS_IWASYNC
            if (_iwasyncCore != null && _registeredCore != _iwasyncCore)
            {
                _registeredCore = _iwasyncCore;
                _iwasyncCore.AddListener(this);
            }
#endif
        }

#if SSS_IWASYNC
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
#endif
    }
}
