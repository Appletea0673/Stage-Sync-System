using UnityEngine;
using VRC.SDKBase;
using Yamadev.YamaStream;
using HoshinoLabs.IwaSync3.Udon;
using UdonSharp;


namespace AppleteaSystems.StageSyncSystem
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class SSSCore : UdonSharpBehaviour
    {
        [Header("Video Player Core")]
        [SerializeField]
        [Tooltip("YamaPlayer")]
        Controller YamaPlayer_controller;

        [SerializeField]
        [Tooltip("IwaSync3")]
        VideoCore Iwasync_core;

        [Header("General Settings")]
        [SerializeField]
        DirectionObject[] _directions;
        [SerializeField]
        VideoPlayerType _videoPlayerType = VideoPlayerType.YamaPlayer;

        [Header("Manual Parameters")]
        [SerializeField]
        [Tooltip("Milliseconds")]
        int AudienceDelays = 600;

        [SerializeField]
        [Tooltip("Milliseconds")]
        int VideoOffset = 10000;

        [SerializeField]
        float SyncAccuracy = 0.1f;

        // Local Valiable
        bool _isPerformer = false;
        [FieldChangeCallback(nameof(PlayingObjectIndex)), UdonSynced]
        int _playingObjectIndex;
        const uint _MODE_VIDEO = 0x00000010;
        float _volumeTemp = 0.0f;

        #region Accessor

        public DirectionObject[] Directions
        {
            set { _directions = value; }
        }

        public Controller YamaPlayerController
        {
            set { YamaPlayer_controller = value; }
        }

        public VideoCore IwasyncCore
        {
            set { Iwasync_core = value; }
        }

        public bool IsPerformer
        {
            get => _isPerformer;
            set
            { 
                _isPerformer = value;
                Iwasync_core.offsetTime = GetOffsetTime();
            }

        }

        public int PlayingObjectIndex
        {
            get => _playingObjectIndex;
            set
            {
                _playingObjectIndex = value;
            }
        }
        #endregion

        enum VideoPlayerType
        {
            YamaPlayer,
            IwaSync
        }

        void Start()
        {
            //PlayerbleDirectorの初期設定
            foreach (DirectionObject _dir in _directions)
            {
                DirectionSetting(_dir, false);
                _dir.playableDirector.playOnAwake = true;
            }
        }

        public override void OnPlayerJoined(VRCPlayerApi player) 
        {
            if (Iwasync_core.isOwner)
            {
                Iwasync_core.RequestSerialization();
                RequestSerialization();
            }
            if (player == Networking.LocalPlayer) Iwasync_core.offsetTime = GetOffsetTime();
        }

        void Update()
        {
            if(_directions == null) return;
            if (Iwasync_core.isPlaying)
            {
                //同期完了確認
                bool _loadedFlag = isLoaded();
                if (_loadedFlag)
                {
                    Iwasync_core.volume = _volumeTemp;
                    DirectionSetting(_directions[_playingObjectIndex], true);
                }
                else
                {
                    Iwasync_core.volume = 0.0f;
                    //同期ズレが収束するまで同期を取る
                    //float offset = (float)Iwasync_core.GetProgramVariable("_time") + (Networking.GetServerTimeInMilliseconds() - Iwasync_core.clockTime) / 1000f * Iwasync_core.speed;
                    //float offsetLocal = Iwasync_core.offsetTime / 1000f;
                    //if (SyncAccuracy <= Mathf.Abs(Iwasync_core.time - offsetLocal - offset)) Iwasync_core.offsetTime = GetOffsetTime();
                    Iwasync_core.offsetTime = GetOffsetTime();
                }
            }
            else
            {
            }
        }

        //外部呼出し用
        public void StartHandling(int objectIndex)
        {
            //起動させた人物がOwnerを取得
            TakeOwnership();
            //再生中の曲設定を共有
            PlayingObjectIndex = objectIndex;
            RequestSerialization();

            //動画再生開始
            Iwasync_core.TakeOwnership();
            Iwasync_core.PlayURL(_MODE_VIDEO, _directions[_playingObjectIndex].videoURL);
            Iwasync_core.RequestSerialization();

            //遅延処理&強制同期処理
            //Iwasync_core.offsetTime = GetOffsetTime();
        }

        public void OnVideoReady()
        {
            
        }

        public void OnVideoStop()
        {
            //音量を一時保存
            if (Iwasync_core.volume != 0.0f) _volumeTemp = Iwasync_core.volume;
            Iwasync_core.volume = 0.0f;
            foreach (DirectionObject _dir in _directions)
            {
                DirectionSetting(_dir, false);
            }
        }
        
        #region Independent Private Methods

        private void TakeOwnership()
        {
            if (!Networking.IsOwner(Networking.LocalPlayer, gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }

        private void DirectionSetting(DirectionObject _do, bool _toggle)
        {
            if (_toggle)
            {
                _do.playableDirector.Play();
                _do.gameObject.SetActive(true);
                _do.playableDirector.time = GetCurrentVideoTime();
            }
            else
            {
                _do.playableDirector.Stop();
                _do.gameObject.SetActive(false);
                _do.playableDirector.time = 0.0f;
            }
        }

        #endregion

        #region Dependent Private Methods

        private int GetOffsetTime()
        {
            if (YamaPlayer_controller != null && _videoPlayerType == VideoPlayerType.YamaPlayer)
            {
                return -VideoOffset + (_isPerformer ? AudienceDelays : 0);
            }

            else return -VideoOffset + (_isPerformer ? AudienceDelays : 0);
        }

        private void ApplyOffset()
        {
            int offsetMs = GetOffsetTime();

            // YamaStream の場合 (秒に変換して代入)
            if (YamaPlayer_controller != null && _videoPlayerType == VideoPlayerType.YamaPlayer)
            {
                YamaPlayer_controller.LocalDelay = offsetMs / 1000f;
            }

            // IwaSync3 の場合 (ミリ秒で代入)
            if (Iwasync_core != null && _videoPlayerType == VideoPlayerType.IwaSync)
            {
                Iwasync_core.offsetTime = offsetMs;
            }
        }

        private float GetCurrentVideoTime()
        {
            // YamaStream が有効かつ再生中の場合
            if (YamaPlayer_controller != null && YamaPlayer_controller.IsPlaying)
            {
                return YamaPlayer_controller.VideoTime;
            }

            // IwaSync3 が有効かつ再生中の場合
            else if (Iwasync_core != null && Iwasync_core.isPlaying)
            {
                return Iwasync_core.time;
            }

            else return 0f;
        }

        #endregion
    }
}