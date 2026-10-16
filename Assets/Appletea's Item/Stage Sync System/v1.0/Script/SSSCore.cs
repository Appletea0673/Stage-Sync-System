using System;
using UnityEngine;
using UnityEngine.Playables;
using VRC.SDKBase;
using VRC.SDK3.Data;
using VRC.SDK3.Components.Video;
using VRC.SDK3.UdonNetworkCalling;
using VRC.Udon.Common.Interfaces;
using Yamadev.YamaStream;
using HoshinoLabs.IwaSync3.Udon;
using UdonSharp;

namespace AppleteaSystems.StageSyncSystem
{
    public enum VideoPlayerType
    {
        YamaPlayer,
        IwaSync
    }

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

        [SerializeField, HideInInspector]
        SSSYamaBridge _yamaBridge;

        [SerializeField, HideInInspector]
        SSSIwaSyncBridge _iwaSyncBridge;

        [Header("General Settings")]
        [SerializeField]
        DirectionObject[] _directions;

        [SerializeField]
        VideoPlayerType _videoPlayerType = VideoPlayerType.YamaPlayer;

        [Header("Manual Parameters")]
        [SerializeField]
        [Tooltip("Milliseconds (Fallback delay when fewer than 3 players are in the instance)")]
        int AudienceDelays = 600;

        [SerializeField]
        [Tooltip("Milliseconds (Manual fine-tuning offset)")]
        int VideoOffset = 0;

        [SerializeField]
        [Tooltip("Timeline sync drift threshold in seconds")]
        float SyncAccuracy = 0.1f;

        [SerializeField]
        [Tooltip("Maximum seconds to wait for all players to finish loading the video")]
        float ReadyTimeout = 15.0f;

        // Synced Variables
        [UdonSynced, FieldChangeCallback(nameof(PlayingObjectIndex))]
        int _playingObjectIndex = -1;

        [UdonSynced]
        bool _isWaitingForReady = false;

        [UdonSynced]
        bool _isStagePlaying = false;

        [UdonSynced]
        int _playbackSessionId = 0;

        [UdonSynced]
        int _stageStartServerTime = 0;

        // Local Variables
        bool _isPerformer = false;
        const uint _MODE_VIDEO = 0x00000010;
        float _volumeTemp = 0.5f;
        bool _isMutedBySSS = false;
        int _activeDirectionIndex = -1;
        int _localReadySessionId = -1;
        int _localStartedSessionId = -1;
        float _loadingStartTime = 0f;

        // Ready Tracking (Owner)
        DataList _expectedPlayerIds = new DataList();
        DataList _readyPlayerIds = new DataList();

        // Latency Calculation (YamaPlayer triangulation method via NetworkCallable)
        DataDictionary _pairDelays = new DataDictionary();
        DataList _knownPlayers = new DataList();
        float _networkDelay = 0f;

        #region Accessor

        public DirectionObject[] Directions
        {
            get => _directions;
            set => _directions = value;
        }

        public Controller YamaPlayerController
        {
            get => YamaPlayer_controller;
            set => YamaPlayer_controller = value;
        }

        public VideoCore IwasyncCore
        {
            get => Iwasync_core;
            set => Iwasync_core = value;
        }

        public SSSYamaBridge YamaBridge
        {
            get => _yamaBridge;
            set => _yamaBridge = value;
        }

        public SSSIwaSyncBridge IwaSyncBridge
        {
            get => _iwaSyncBridge;
            set => _iwaSyncBridge = value;
        }

        public VideoPlayerType PlayerType
        {
            get => _videoPlayerType;
            set => _videoPlayerType = value;
        }

        public float NetworkDelay => _networkDelay;

        public bool IsPerformer
        {
            get => _isPerformer;
            set
            {
                _isPerformer = value;
                ApplyOffset();
            }
        }

        public int PlayingObjectIndex
        {
            get => _playingObjectIndex;
            set => _playingObjectIndex = value;
        }

        #endregion

        void Start()
        {
            InitializeBridges();
            InitializeVolumeTemp();

            if (_directions != null)
            {
                foreach (DirectionObject dir in _directions)
                {
                    if (dir == null) continue;
                    DirectionSetting(dir, false);
                    dir.playableDirector.playOnAwake = true;
                }
            }
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            if (Networking.IsOwner(gameObject) && Utilities.IsValid(player) && !player.isLocal)
            {
                RequestSerialization();
                if (IsUsingIwaSync() && Iwasync_core.isOwner)
                {
                    Iwasync_core.RequestSerialization();
                }
                else if (IsUsingYamaPlayer() && Networking.IsOwner(YamaPlayer_controller.gameObject))
                {
                    YamaPlayer_controller.RequestSerialization();
                }
            }

            if (Utilities.IsValid(player) && player.isLocal)
            {
                ApplyOffset();
                SendCustomEventDelayedSeconds(nameof(RequestLatencyUpdate), 1.0f);
            }
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player))
            {
                int leftId = player.playerId;
                int knownIdx = _knownPlayers.IndexOf(leftId);
                if (knownIdx >= 0) _knownPlayers.RemoveAt(knownIdx);
            }

            if (Networking.IsOwner(gameObject) && _isWaitingForReady)
            {
                EvaluateAllPlayersReady();
            }
        }

        public override void OnDeserialization()
        {
            if (_isWaitingForReady)
            {
                MuteVideoPlayerForLoading();
                StopAllDirections();
            }
            else if (_isStagePlaying)
            {
                RestoreVideoPlayerVolume();
                ApplyOffset();
                if (IsVideoPlayerPlaying())
                {
                    SyncActiveDirection(true);
                }
            }
            else
            {
                StopAllDirections();
            }
        }

        void Update()
        {
            if (_directions == null || _directions.Length == 0) return;

            if (_isWaitingForReady)
            {
                if (IsVideoPlayerPlaying() && !IsVideoPlayerPaused())
                {
                    PauseLocalVideoPlayerAtZero();
                }

                if (Networking.IsOwner(gameObject) && Time.time - _loadingStartTime >= ReadyTimeout)
                {
                    BeginSynchronizedStagePlayback();
                }
                return;
            }

            if (_isStagePlaying && IsVideoPlayerPlaying())
            {
                if (_playingObjectIndex < 0 || _playingObjectIndex >= _directions.Length) return;
                DirectionObject currentDir = _directions[_playingObjectIndex];
                if (currentDir == null) return;

                if (_activeDirectionIndex != _playingObjectIndex || !currentDir.gameObject.activeSelf)
                {
                    SyncActiveDirection(true);
                }
                else if (!IsVideoPlayerPaused())
                {
                    float videoTime = GetCurrentVideoTime();
                    float timelineTime = (float)currentDir.playableDirector.time;
                    if (Mathf.Abs(timelineTime - videoTime) >= SyncAccuracy)
                    {
                        currentDir.playableDirector.time = videoTime;
                    }
                }
            }
        }

        #region External Control & Video Lifecycle

        // 外部呼出し用
        public void StartHandling(int objectIndex)
        {
            if (_directions == null || objectIndex < 0 || objectIndex >= _directions.Length) return;
            if (_directions[objectIndex] == null) return;

            // 起動させた人物がOwnerを取得
            TakeOwnership();

            _playbackSessionId++;
            PlayingObjectIndex = objectIndex;
            _isWaitingForReady = true;
            _isStagePlaying = false;
            _stageStartServerTime = 0;
            _loadingStartTime = Time.time;

            RecordExpectedPlayers();
            RequestSerialization();

            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(NetOnBeginLoading), _playbackSessionId, objectIndex);

            // 動画ロード開始（一時停止状態で待機）
            LoadVideoOnPlayer(objectIndex);

            // タイムアウト監視を開始
            SendCustomEventDelayedSeconds(nameof(CheckReadyTimeout), ReadyTimeout);
        }

        public override void OnVideoReady()
        {
            if (_isWaitingForReady)
            {
                MuteVideoPlayerForLoading();
                PauseLocalVideoPlayerAtZero();
                ReportLocalPlayerReady();
            }
        }

        public override void OnVideoStart()
        {
            if (_isWaitingForReady)
            {
                MuteVideoPlayerForLoading();
                PauseLocalVideoPlayerAtZero();
                ReportLocalPlayerReady();
                return;
            }

            if (_isStagePlaying)
            {
                RestoreVideoPlayerVolume();
                ApplyOffset();
                SyncActiveDirection(true);
                SendCustomEventDelayedSeconds(nameof(DelayedForceSync), 1.0f);
            }
        }

        public override void OnVideoPlay()
        {
            if (_isWaitingForReady)
            {
                PauseLocalVideoPlayerAtZero();
                return;
            }

            if (_isStagePlaying && _playingObjectIndex >= 0 && _playingObjectIndex < _directions.Length)
            {
                DirectionObject currentDir = _directions[_playingObjectIndex];
                if (currentDir != null && currentDir.gameObject.activeSelf)
                {
                    currentDir.playableDirector.time = GetCurrentVideoTime();
                    currentDir.playableDirector.Play();
                }
                SendCustomEventDelayedSeconds(nameof(DelayedForceSync), 1.0f);
            }
        }

        public override void OnVideoPause()
        {
            if (_isStagePlaying && _playingObjectIndex >= 0 && _playingObjectIndex < _directions.Length)
            {
                DirectionObject currentDir = _directions[_playingObjectIndex];
                if (currentDir != null && currentDir.gameObject.activeSelf)
                {
                    currentDir.playableDirector.Pause();
                }
            }
        }

        public void OnVideoStop()
        {
            // ロード開始直後の既存トラック停止イベントでは待機状態を解除しない
            if (_isWaitingForReady && Time.time - _loadingStartTime < 1.0f)
            {
                StopAllDirections();
                return;
            }

            float currentVol = GetVideoPlayerVolume();
            if (currentVol > 0.0f && !_isMutedBySSS)
            {
                _volumeTemp = currentVol;
            }

            _isStagePlaying = false;
            StopAllDirections();
        }

        public override void OnVideoEnd()
        {
            _isWaitingForReady = false;
            _isStagePlaying = false;
            StopAllDirections();

            if (Networking.IsOwner(gameObject))
            {
                RequestSerialization();
            }
        }

        public override void OnVideoLoop()
        {
            if (_isStagePlaying && _playingObjectIndex >= 0 && _playingObjectIndex < _directions.Length)
            {
                DirectionObject currentDir = _directions[_playingObjectIndex];
                if (currentDir != null && currentDir.gameObject.activeSelf)
                {
                    currentDir.playableDirector.time = GetCurrentVideoTime();
                    currentDir.playableDirector.Play();
                }
            }
        }

        public override void OnVideoError(VideoError videoError)
        {
            // 読み込みエラー発生時は自身のせいで他プレイヤーの再生開始が止まらないようReady扱いで通知する
            if (_isWaitingForReady)
            {
                ReportLocalPlayerReady();
            }
        }

        public void OnVideoSetTime(float time)
        {
            if (_isStagePlaying && _playingObjectIndex >= 0 && _playingObjectIndex < _directions.Length)
            {
                DirectionObject currentDir = _directions[_playingObjectIndex];
                if (currentDir != null && currentDir.gameObject.activeSelf)
                {
                    currentDir.playableDirector.time = time;
                }
            }
        }

        #endregion

        #region Synchronized Ready & Start (NetworkCallable)

        [NetworkCallable(100)]
        public void NetOnBeginLoading(int sessionId, int objectIndex)
        {
            _playbackSessionId = sessionId;
            _playingObjectIndex = objectIndex;
            _isWaitingForReady = true;
            _isStagePlaying = false;
            _localReadySessionId = -1;
            _localStartedSessionId = -1;
            _loadingStartTime = Time.time;

            MuteVideoPlayerForLoading();
            StopAllDirections();

            // 動画ロード時間を利用してインスタンス内の通信遅延を計測
            TriggerBeaconLatencyPing();
        }

        private void ReportLocalPlayerReady()
        {
            if (_localReadySessionId == _playbackSessionId) return;
            _localReadySessionId = _playbackSessionId;

            int localId = Utilities.IsValid(Networking.LocalPlayer) ? Networking.LocalPlayer.playerId : 1;
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(NetNotifyPlayerReady), _playbackSessionId, localId);
        }

        [NetworkCallable(100)]
        public void NetNotifyPlayerReady(int sessionId, int playerId)
        {
            if (!Networking.IsOwner(gameObject)) return;
            if (!_isWaitingForReady || sessionId != _playbackSessionId) return;

            if (!_readyPlayerIds.Contains(playerId))
            {
                _readyPlayerIds.Add(playerId);
            }

            EvaluateAllPlayersReady();
        }

        private void RecordExpectedPlayers()
        {
            _expectedPlayerIds.Clear();
            _readyPlayerIds.Clear();

            int count = VRCPlayerApi.GetPlayerCount();
            if (count <= 0) return;

            VRCPlayerApi[] players = new VRCPlayerApi[count];
            VRCPlayerApi.GetPlayers(players);
            for (int i = 0; i < players.Length; i++)
            {
                if (Utilities.IsValid(players[i]))
                {
                    _expectedPlayerIds.Add(players[i].playerId);
                }
            }
        }

        private void EvaluateAllPlayersReady()
        {
            if (!Networking.IsOwner(gameObject) || !_isWaitingForReady) return;

            int count = VRCPlayerApi.GetPlayerCount();
            if (count > 0)
            {
                VRCPlayerApi[] players = new VRCPlayerApi[count];
                VRCPlayerApi.GetPlayers(players);
                for (int i = 0; i < players.Length; i++)
                {
                    VRCPlayerApi player = players[i];
                    if (!Utilities.IsValid(player)) continue;

                    int pid = player.playerId;
                    if (_expectedPlayerIds.Count > 0 && !_expectedPlayerIds.Contains(pid))
                    {
                        continue;
                    }

                    if (!_readyPlayerIds.Contains(pid))
                    {
                        return;
                    }
                }
            }

            BeginSynchronizedStagePlayback();
        }

        public void CheckReadyTimeout()
        {
            if (!Networking.IsOwner(gameObject) || !_isWaitingForReady) return;
            if (Time.time - _loadingStartTime >= ReadyTimeout - 0.1f)
            {
                BeginSynchronizedStagePlayback();
            }
        }

        private void BeginSynchronizedStagePlayback()
        {
            if (!Networking.IsOwner(gameObject) || !_isWaitingForReady) return;

            _isWaitingForReady = false;
            _isStagePlaying = true;
            _stageStartServerTime = Networking.GetServerTimeInMilliseconds();
            RequestSerialization();

            if (IsUsingYamaPlayer())
            {
                if (!Networking.IsOwner(YamaPlayer_controller.gameObject))
                {
                    Networking.SetOwner(Networking.LocalPlayer, YamaPlayer_controller.gameObject);
                }
                YamaPlayer_controller.SetTime(0f);
                YamaPlayer_controller.Paused = false;
                YamaPlayer_controller.SyncTime = 0f;
                YamaPlayer_controller.RequestSerialization();
            }
            else if (IsUsingIwaSync())
            {
                Iwasync_core.TakeOwnership();
                Iwasync_core.clockTime = _stageStartServerTime;
                Iwasync_core.time = 0f;
                Iwasync_core.Play();
                Iwasync_core.RequestSerialization();
            }

            SendCustomNetworkEvent(
                NetworkEventTarget.All,
                nameof(NetStartStagePlayback),
                _playbackSessionId,
                _playingObjectIndex,
                _stageStartServerTime
            );
        }

        [NetworkCallable(100)]
        public void NetStartStagePlayback(int sessionId, int objectIndex, int startServerTime)
        {
            if (_localStartedSessionId == sessionId) return;
            _localStartedSessionId = sessionId;

            _playbackSessionId = sessionId;
            _playingObjectIndex = objectIndex;
            _stageStartServerTime = startServerTime;
            _isWaitingForReady = false;
            _isStagePlaying = true;

            RestoreVideoPlayerVolume();
            ResumeLocalVideoPlayer();
            ApplyOffset();
            SyncActiveDirection(true);

            SendCustomEventDelayedSeconds(nameof(DelayedForceSync), 1.0f);
        }

        public void DelayedForceSync()
        {
            if (!_isStagePlaying || _isWaitingForReady) return;
            ApplyOffset();
            if (IsUsingYamaPlayer())
            {
                YamaPlayer_controller.ForceSync();
            }
        }

        #endregion

        #region Latency Calculation (YamaPlayer Triangulation Method)

        public void RequestLatencyUpdate()
        {
            if (!Utilities.IsValid(Networking.LocalPlayer)) return;
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(NetRequestBeaconPings));
            SendLatencyPing();
        }

        [NetworkCallable(100)]
        public void NetRequestBeaconPings()
        {
            TriggerBeaconLatencyPing();
        }

        private void TriggerBeaconLatencyPing()
        {
            if (!Utilities.IsValid(Networking.LocalPlayer)) return;
            int beaconOrder = GetLocalBeaconIndex();
            if (beaconOrder >= 0 && beaconOrder < 3)
            {
                float delay = 0.05f + beaconOrder * 0.15f;
                SendCustomEventDelayedSeconds(nameof(SendLatencyPing), delay);
            }
        }

        public void SendLatencyPing()
        {
            if (!Utilities.IsValid(Networking.LocalPlayer)) return;
            SendCustomNetworkEvent(
                NetworkEventTarget.All,
                nameof(NetReceiveLatencyPing),
                Networking.LocalPlayer.playerId,
                Networking.GetServerTimeInMilliseconds()
            );
        }

        [NetworkCallable(100)]
        public void NetReceiveLatencyPing(int senderId, int sendServerTime)
        {
            if (!Utilities.IsValid(Networking.LocalPlayer)) return;
            int localId = Networking.LocalPlayer.playerId;
            if (localId == senderId) return;

            int delayMs = Networking.GetServerTimeInMilliseconds() - sendServerTime;
            if (delayMs < 0) delayMs = 0;

            RecordPairDelay(senderId, localId, delayMs);
            UpdateNetworkDelay();

            int beaconOrder = GetLocalBeaconIndex();
            if (beaconOrder >= 0 && beaconOrder < 3)
            {
                SendCustomNetworkEvent(
                    NetworkEventTarget.All,
                    nameof(NetReceiveLatencyRecord),
                    senderId,
                    localId,
                    delayMs
                );
            }
        }

        [NetworkCallable(100)]
        public void NetReceiveLatencyRecord(int player1, int player2, int delayMs)
        {
            RecordPairDelay(player1, player2, delayMs);
            UpdateNetworkDelay();
        }

        private int GetLocalBeaconIndex()
        {
            if (!Utilities.IsValid(Networking.LocalPlayer)) return -1;
            int localId = Networking.LocalPlayer.playerId;

            int count = VRCPlayerApi.GetPlayerCount();
            if (count <= 0) return 0;

            VRCPlayerApi[] players = new VRCPlayerApi[count];
            VRCPlayerApi.GetPlayers(players);

            int smallerCount = 0;
            for (int i = 0; i < players.Length; i++)
            {
                if (Utilities.IsValid(players[i]) && players[i].playerId < localId)
                {
                    smallerCount++;
                }
            }
            return smallerCount;
        }

        private string GetPairKey(int player1, int player2)
        {
            return player1 < player2 ? $"{player1}_{player2}" : $"{player2}_{player1}";
        }

        private void RecordPairDelay(int player1, int player2, int delayMs)
        {
            if (player1 == player2) return;
            if (!_knownPlayers.Contains(player1)) _knownPlayers.Add(player1);
            if (!_knownPlayers.Contains(player2)) _knownPlayers.Add(player2);

            string key = GetPairKey(player1, player2);
            _pairDelays.SetValue(key, delayMs);
        }

        public int GetDelayWithTwoPlayers(int player1, int player2)
        {
            if (player1 == player2) return -1;
            string key = GetPairKey(player1, player2);
            if (_pairDelays.TryGetValue(key, TokenType.Int, out DataToken token))
            {
                return token.Int;
            }
            return -1;
        }

        public DataDictionary GetMyLatencyRecords()
        {
            DataDictionary results = new DataDictionary();
            if (!Utilities.IsValid(Networking.LocalPlayer)) return results;

            int localId = Networking.LocalPlayer.playerId;
            for (int i = 0; i < _knownPlayers.Count; i++)
            {
                int otherId = _knownPlayers[i].Int;
                if (otherId == localId) continue;

                VRCPlayerApi otherPlayer = VRCPlayerApi.GetPlayerById(otherId);
                if (!Utilities.IsValid(otherPlayer)) continue;

                int record = GetDelayWithTwoPlayers(localId, otherId);
                if (record >= 0)
                {
                    results.SetValue(otherId, record);
                }
            }
            return results;
        }

        public float GetServerDelaySeconds()
        {
            DataDictionary records = GetMyLatencyRecords();
            if (records.Count < 2) return 0f;

            DataList keys = records.GetKeys();
            for (int x = keys.Count - 1; x >= 0; x--)
            {
                for (int y = keys.Count - 1; y >= 0; y--)
                {
                    if (x == y) continue;
                    int playerX = keys[x].Int;
                    int playerY = keys[y].Int;
                    int delayXY = GetDelayWithTwoPlayers(playerX, playerY);
                    if (delayXY >= 0)
                    {
                        int delayX = records[playerX].Int;
                        int delayY = records[playerY].Int;
                        return (delayX + delayY - delayXY) / 2f / 1000f;
                    }
                }
            }
            return 0f;
        }

        public void UpdateNetworkDelay()
        {
            float prevDelay = _networkDelay;
            _networkDelay = Mathf.Clamp(GetServerDelaySeconds(), 0f, 1f);
            if (_isStagePlaying && !_isWaitingForReady && Mathf.Abs(_networkDelay - prevDelay) > 0.001f)
            {
                ApplyOffset();
            }
        }

        #endregion

        #region Independent Private Methods

        private void InitializeBridges()
        {
            if (_yamaBridge == null) _yamaBridge = GetComponentInChildren<SSSYamaBridge>(true);
            if (_yamaBridge != null)
            {
                _yamaBridge.SSSCore = this;
                if (YamaPlayer_controller != null) _yamaBridge.Controller = YamaPlayer_controller;
            }

            if (_iwaSyncBridge == null) _iwaSyncBridge = GetComponentInChildren<SSSIwaSyncBridge>(true);
            if (_iwaSyncBridge != null)
            {
                _iwaSyncBridge.SSSCore = this;
                if (Iwasync_core != null) _iwaSyncBridge.IwasyncCore = Iwasync_core;
            }
        }

        private void InitializeVolumeTemp()
        {
            float initialVol = GetVideoPlayerVolume();
            if (initialVol > 0.0f)
            {
                _volumeTemp = initialVol;
            }
        }

        private void TakeOwnership()
        {
            if (Utilities.IsValid(Networking.LocalPlayer) && !Networking.IsOwner(Networking.LocalPlayer, gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }
        }

        private void SyncActiveDirection(bool play)
        {
            if (_directions == null) return;

            for (int i = 0; i < _directions.Length; i++)
            {
                if (_directions[i] == null) continue;
                bool shouldBeActive = play && (i == _playingObjectIndex);
                if (shouldBeActive)
                {
                    DirectionSetting(_directions[i], true);
                    _activeDirectionIndex = i;
                }
                else if (_directions[i].gameObject.activeSelf)
                {
                    DirectionSetting(_directions[i], false);
                }
            }

            if (!play)
            {
                _activeDirectionIndex = -1;
            }
        }

        private void StopAllDirections()
        {
            if (_directions == null) return;
            for (int i = 0; i < _directions.Length; i++)
            {
                if (_directions[i] != null)
                {
                    DirectionSetting(_directions[i], false);
                }
            }
            _activeDirectionIndex = -1;
        }

        private void DirectionSetting(DirectionObject dirObj, bool toggle)
        {
            if (dirObj == null) return;

            if (toggle)
            {
                if (!dirObj.gameObject.activeSelf)
                {
                    dirObj.gameObject.SetActive(true);
                }
                dirObj.playableDirector.time = GetCurrentVideoTime();
                dirObj.playableDirector.Play();
            }
            else
            {
                dirObj.playableDirector.Stop();
                dirObj.playableDirector.time = 0.0f;
                if (dirObj.gameObject.activeSelf)
                {
                    dirObj.gameObject.SetActive(false);
                }
            }
        }

        #endregion

        #region Video Player Dependent Methods

        private bool IsUsingYamaPlayer()
        {
            if (_videoPlayerType == VideoPlayerType.YamaPlayer && YamaPlayer_controller != null) return true;
            if (Iwasync_core == null && YamaPlayer_controller != null) return true;
            return false;
        }

        private bool IsUsingIwaSync()
        {
            if (_videoPlayerType == VideoPlayerType.IwaSync && Iwasync_core != null) return true;
            if (YamaPlayer_controller == null && Iwasync_core != null) return true;
            return false;
        }

        private void LoadVideoOnPlayer(int objectIndex)
        {
            DirectionObject dir = _directions[objectIndex];
            if (dir == null) return;

            if (IsUsingYamaPlayer())
            {
                if (!Networking.IsOwner(YamaPlayer_controller.gameObject))
                {
                    Networking.SetOwner(Networking.LocalPlayer, YamaPlayer_controller.gameObject);
                }
                YamaPlayer_controller.Paused = true;
                Track track = Track.New(
                    YamaPlayer_controller.VideoPlayerType,
                    dir.directionName,
                    dir.videoURL
                );
                YamaPlayer_controller.PlayTrack(track);
            }
            else if (IsUsingIwaSync())
            {
                Iwasync_core.TakeOwnership();
                Iwasync_core.PlayURL(_MODE_VIDEO, dir.videoURL);
                Iwasync_core.Pause();
                Iwasync_core.RequestSerialization();
            }
        }

        private bool IsVideoPlayerPlaying()
        {
            if (IsUsingYamaPlayer()) return YamaPlayer_controller.IsPlaying;
            if (IsUsingIwaSync()) return Iwasync_core.isPlaying;
            return false;
        }

        private bool IsVideoPlayerPaused()
        {
            if (IsUsingYamaPlayer()) return YamaPlayer_controller.Paused;
            if (IsUsingIwaSync()) return Iwasync_core.paused;
            return false;
        }

        private void PauseLocalVideoPlayerAtZero()
        {
            if (IsUsingYamaPlayer())
            {
                if (YamaPlayer_controller.VideoPlayerHandle != null)
                {
                    YamaPlayer_controller.VideoPlayerHandle.Pause();
                    YamaPlayer_controller.VideoPlayerHandle.VideoTime = 0f;
                }
            }
            else if (IsUsingIwaSync())
            {
                Iwasync_core.Pause();
                Iwasync_core.time = 0f;
            }
        }

        private void ResumeLocalVideoPlayer()
        {
            if (IsUsingYamaPlayer())
            {
                if (YamaPlayer_controller.VideoPlayerHandle != null)
                {
                    YamaPlayer_controller.VideoPlayerHandle.Play();
                }
            }
            else if (IsUsingIwaSync())
            {
                Iwasync_core.Play();
            }
        }

        private float GetVideoPlayerVolume()
        {
            if (IsUsingYamaPlayer()) return YamaPlayer_controller.Volume;
            if (IsUsingIwaSync()) return Iwasync_core.volume;
            return 0f;
        }

        private void SetVideoPlayerVolume(float vol)
        {
            if (IsUsingYamaPlayer())
            {
                YamaPlayer_controller.Volume = vol;
            }
            else if (IsUsingIwaSync())
            {
                Iwasync_core.volume = vol;
            }
        }

        private void MuteVideoPlayerForLoading()
        {
            if (!_isMutedBySSS)
            {
                float currentVol = GetVideoPlayerVolume();
                if (currentVol > 0.0f)
                {
                    _volumeTemp = currentVol;
                }
                _isMutedBySSS = true;
            }
            SetVideoPlayerVolume(0.0f);
        }

        private void RestoreVideoPlayerVolume()
        {
            if (_isMutedBySSS)
            {
                _isMutedBySSS = false;
                if (_volumeTemp > 0.0f)
                {
                    SetVideoPlayerVolume(_volumeTemp);
                }
            }
        }

        // Danceモード相当のオフセット計算 (秒単位)
        // 演者: +NetworkDelay (2人以下等でNetworkDelay未計測時は AudienceDelays をフォールバック使用)
        // 観客: -NetworkDelay
        private float GetOffsetSeconds()
        {
            float roleDelaySeconds;
            if (_isPerformer)
            {
                roleDelaySeconds = (_networkDelay > 0f) ? _networkDelay : (AudienceDelays / 1000f);
            }
            else
            {
                roleDelaySeconds = -_networkDelay;
            }
            return -(VideoOffset / 1000f) + roleDelaySeconds;
        }

        // ミリ秒単位のオフセット計算
        private int GetOffsetTime()
        {
            return Mathf.RoundToInt(GetOffsetSeconds() * 1000f);
        }

        private void ApplyOffset()
        {
            if (IsUsingYamaPlayer())
            {
                YamaPlayer_controller.LocalDelay = GetOffsetSeconds();
            }
            else if (IsUsingIwaSync())
            {
                Iwasync_core.offsetTime = GetOffsetTime();
            }
        }

        private float GetCurrentVideoTime()
        {
            if (IsUsingYamaPlayer() && YamaPlayer_controller.IsPlaying)
            {
                return YamaPlayer_controller.VideoTime;
            }
            if (IsUsingIwaSync() && Iwasync_core.isPlaying)
            {
                return Iwasync_core.time;
            }
            return 0f;
        }

        #endregion
    }
}