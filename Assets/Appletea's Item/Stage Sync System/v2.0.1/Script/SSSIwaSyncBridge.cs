using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;
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
        private BaseVRCVideoPlayer _unityVideoPlayer;
        private BaseVRCVideoPlayer _avProVideoPlayer;

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
                FindVideoPlayers();
            }
#endif
        }

#if SSS_IWASYNC
        public void FindVideoPlayers()
        {
            if (_iwasyncCore == null) return;
            BaseVRCVideoPlayer[] players = _iwasyncCore.GetComponentsInChildren<BaseVRCVideoPlayer>(true);
            if (players != null)
            {
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i] == null) continue;
                    string n = players[i].gameObject.name;
                    if (n.Contains("Unity"))
                    {
                        _unityVideoPlayer = players[i];
                    }
                    else if (n.Contains("AVPro"))
                    {
                        _avProVideoPlayer = players[i];
                    }
                }
                if (_unityVideoPlayer == null && players.Length > 0) _unityVideoPlayer = players[0];
                if (_avProVideoPlayer == null && players.Length > 1) _avProVideoPlayer = players[1];
            }
        }

        public BaseVRCVideoPlayer GetActiveVideoPlayer()
        {
            if (_iwasyncCore == null) return null;
            if (_unityVideoPlayer == null && _avProVideoPlayer == null)
            {
                FindVideoPlayers();
            }
            if (_iwasyncCore.isModeStream)
            {
                return _avProVideoPlayer != null ? _avProVideoPlayer : _unityVideoPlayer;
            }
            return _unityVideoPlayer != null ? _unityVideoPlayer : _avProVideoPlayer;
        }

        public void SeekDirect(float targetTime)
        {
            BaseVRCVideoPlayer player = GetActiveVideoPlayer();
            if (player != null)
            {
                player.SetTime(targetTime);
            }
        }

        public void StopDirect()
        {
            if (_unityVideoPlayer == null && _avProVideoPlayer == null)
            {
                FindVideoPlayers();
            }
            if (_unityVideoPlayer != null)
            {
                _unityVideoPlayer.Stop();
            }
            if (_avProVideoPlayer != null)
            {
                _avProVideoPlayer.Stop();
            }
        }
#else
        public void SeekDirect(float targetTime)
        {
        }

        public void StopDirect()
        {
        }
#endif

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
