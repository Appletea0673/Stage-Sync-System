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

        [SerializeField, HideInInspector]
        SSSUIController _uiController;

        [Header("General Settings")]
        [SerializeField]
        DirectionObject[] _directions;

        [SerializeField]
        VideoPlayerType _videoPlayerType = VideoPlayerType.YamaPlayer;

        [Header("Performer Object Toggle")]
        [SerializeField]
        [Tooltip("演者エリア内でのみ有効化するオブジェクト群（演者時 ON / 観客時 OFF）")]
        GameObject[] _performerEnableObjects;

        [SerializeField]
        [Tooltip("演者エリア内でのみ無効化するオブジェクト群（演者時 OFF / 観客時 ON）")]
        GameObject[] _performerDisableObjects;

        [Header("Manual Parameters")]
        [SerializeField]
        [Tooltip("Milliseconds (Fallback delay when fewer than 3 players are in the instance)")]
        int AudienceDelays = 600;

        [SerializeField]
        [Tooltip("Timeline sync drift threshold in seconds")]
        float SyncAccuracy = 0.1f;

        [SerializeField]
        [Tooltip("Maximum seconds to wait for all players to finish loading the video")]
        float ReadyTimeout = 15.0f;

        [SerializeField, Range(0f, 100f)]
        [Tooltip("Percentage of players failing to load required to abort stage playback (0-100%)")]
        float ErrorAbortThreshold = 50.0f;

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
        int _localErrorSessionId = -1;
        int _localStartedSessionId = -1;
        float _loadingStartTime = 0f;

        // Ready & Error Tracking (Owner / All)
        DataList _expectedPlayerIds = new DataList();
        DataList _readyPlayerIds = new DataList();
        DataList _failedPlayerIds = new DataList();
        DataList _performerPlayerIds = new DataList();

        // Latency Calculation (YamaPlayer triangulation method via NetworkCallable)
        DataDictionary _pairDelays = new DataDictionary();
        DataList _knownPlayers = new DataList();
        float _networkDelay = 0f;
        bool _isStageOffsetLocked = false;
        bool _hasPendingLatencyUpdate = false;

        // UI / Force Launch / Fallback Settings
        bool _ignoreThresholdError = false;
        bool _ignorePerformerError = false;
        bool _useFallbackDelay = false;

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

        public SSSUIController UIController
        {
            get => _uiController;
            set => _uiController = value;
        }

        public VideoPlayerType PlayerType
        {
            get => _videoPlayerType;
            set => _videoPlayerType = value;
        }

        public float NetworkDelay => _networkDelay;

        public bool IgnoreThresholdError
        {
            get => _ignoreThresholdError;
            set => _ignoreThresholdError = value;
        }

        public bool IgnorePerformerError
        {
            get => _ignorePerformerError;
            set => _ignorePerformerError = value;
        }

        public bool UseFallbackDelay
        {
            get => _useFallbackDelay;
            set
            {
                if (_useFallbackDelay == value) return;
                _useFallbackDelay = value;
                if (_isStagePlaying && !_isWaitingForReady && _isStageOffsetLocked)
                {
                    _hasPendingLatencyUpdate = true;
                }
                else
                {
                    ApplyOffset();
                    if (!_useFallbackDelay && HasActivePerformer() && !_isStagePlaying)
                    {
                        TriggerBeaconLatencyPing();
                    }
                }
            }
        }

        public bool IsCurrentlyUsingFallbackDelay()
        {
            return _useFallbackDelay || (HasActivePerformer() && _networkDelay <= 0f && _isStagePlaying);
        }

        public GameObject[] PerformerEnableObjects
        {
            get => _performerEnableObjects;
            set => _performerEnableObjects = value;
        }

        public GameObject[] PerformerDisableObjects
        {
            get => _performerDisableObjects;
            set => _performerDisableObjects = value;
        }

        public bool IsPerformer
        {
            get => _isPerformer;
            set
            {
                _isPerformer = value;
                ApplyOffset();
                ApplyPerformerObjects();
                SyncLocalPerformerState();
                if (_isStagePlaying && !_isWaitingForReady && IsVideoPlayerPlaying())
                {
                    SyncActiveDirection(true);
                }
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
            ApplyPerformerObjects();

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
            if (Utilities.IsValid(player) && !player.isLocal)
            {
                if (_isStagePlaying && !_isWaitingForReady)
                {
                    // 上演中に新規プレイヤーが入室した場合は既存プレイヤーのオフセット更新を保留し、上演終了後に再計算する
                    _hasPendingLatencyUpdate = true;
                }

                if (Networking.IsOwner(gameObject))
                {
                    RequestSerialization();
                    // 上演中に動画プレイヤー側の RequestSerialization を呼ぶと既存全員に強制シーク(DoSync)が走るため非再生時のみ実行
                    if (!_isStagePlaying)
                    {
                        if (IsUsingIwaSync() && Iwasync_core.isOwner)
                        {
                            Iwasync_core.RequestSerialization();
                        }
                        else if (IsUsingYamaPlayer() && Networking.IsOwner(YamaPlayer_controller.gameObject))
                        {
                            YamaPlayer_controller.RequestSerialization();
                        }
                    }
                }

                if (_isPerformer)
                {
                    SyncLocalPerformerState();
                }
            }

            if (Utilities.IsValid(player) && player.isLocal)
            {
                ApplyOffset();
                SendCustomEventDelayedSeconds(nameof(RequestLatencyUpdate), 1.5f);
            }
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            bool hadPerformer = HasActivePerformer();
            if (Utilities.IsValid(player))
            {
                int leftId = player.playerId;
                RemoveFromList(_knownPlayers, leftId);
                RemoveFromList(_performerPlayerIds, leftId);
                RemoveFromList(_expectedPlayerIds, leftId);
                RemoveFromList(_readyPlayerIds, leftId);
                RemoveFromList(_failedPlayerIds, leftId);
            }

            if (hadPerformer != HasActivePerformer())
            {
                if (_isStagePlaying && !_isWaitingForReady && _isStageOffsetLocked)
                {
                    _hasPendingLatencyUpdate = true;
                }
                else
                {
                    ApplyOffset();
                }
            }

            if (Networking.IsOwner(gameObject) && _isWaitingForReady)
            {
                EvaluatePlayersLoadState(false);
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
                // 既に現在のセッションを再生中の既存プレイヤーは、途中参加者入室時のシリアライズで再同期しないように保護
                if (_localStartedSessionId != _playbackSessionId)
                {
                    ApplyOffset();
                    if (IsVideoPlayerPlaying())
                    {
                        _localStartedSessionId = _playbackSessionId;
                        SyncActiveDirection(true);
                    }
                }
            }
            else
            {
                RestoreVideoPlayerVolume();
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
                    EvaluatePlayersLoadState(true);
                }
                return;
            }

            if (_isStagePlaying && IsVideoPlayerPlaying())
            {
                if (_playingObjectIndex < 0 || _playingObjectIndex >= _directions.Length) return;
                DirectionObject currentDir = _directions[_playingObjectIndex];
                if (currentDir == null) return;

                if (!ShouldPlayDirection(currentDir))
                {
                    if (currentDir.gameObject.activeSelf || _activeDirectionIndex != -1)
                    {
                        SyncActiveDirection(false);
                    }
                    return;
                }

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

            if (Utilities.IsValid(Networking.LocalPlayer))
            {
                UpdatePerformerIdList(Networking.LocalPlayer.playerId, _isPerformer);
            }

            _playbackSessionId++;
            PlayingObjectIndex = objectIndex;
            _isWaitingForReady = true;
            _isStagePlaying = false;
            _stageStartServerTime = 0;
            _loadingStartTime = Time.time;

            RecordExpectedPlayers();
            if (_uiController != null)
            {
                _uiController.UpdateLoadProgress(
                    0,
                    _expectedPlayerIds.Count,
                    0,
                    ErrorAbortThreshold,
                    _performerPlayerIds.Count > 0,
                    false,
                    false
                );
            }
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

            RestoreVideoPlayerVolume();
            _isStagePlaying = false;
            StopAllDirections();
            OnStagePlaybackEnded();
        }

        public override void OnVideoEnd()
        {
            _isWaitingForReady = false;
            _isStagePlaying = false;
            RestoreVideoPlayerVolume();
            StopAllDirections();
            OnStagePlaybackEnded();

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
            // 読み込みエラー発生時は失敗ステータスと演者フラグをOwnerへ通知する
            if (_isWaitingForReady)
            {
                ReportLocalPlayerError();
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

        #region Synchronized Ready, Error Check & Start (NetworkCallable)

        private bool HasActivePerformer()
        {
            return _isPerformer || _performerPlayerIds.Count > 0;
        }

        private void SyncLocalPerformerState()
        {
            if (!Utilities.IsValid(Networking.LocalPlayer)) return;
            int localId = Networking.LocalPlayer.playerId;
            UpdatePerformerIdList(localId, _isPerformer);
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(NetUpdatePerformerState), localId, _isPerformer);
        }

        [NetworkCallable(100)]
        public void NetUpdatePerformerState(int playerId, bool isPerformer)
        {
            UpdatePerformerIdList(playerId, isPerformer);
        }

        private void UpdatePerformerIdList(int playerId, bool isPerformer)
        {
            bool hadPerformer = _performerPlayerIds.Count > 0;

            if (isPerformer)
            {
                if (!_performerPlayerIds.Contains(playerId))
                {
                    _performerPlayerIds.Add(playerId);
                }
            }
            else
            {
                RemoveFromList(_performerPlayerIds, playerId);
            }

            bool hasPerformer = _performerPlayerIds.Count > 0;
            if (!hadPerformer && hasPerformer)
            {
                // 演者が0人→1人以上になったタイミングで初めて遅延計測を実行
                TriggerBeaconLatencyPing();
            }

            if (hadPerformer != hasPerformer)
            {
                if (_isStagePlaying && !_isWaitingForReady && _isStageOffsetLocked)
                {
                    _hasPendingLatencyUpdate = true;
                }
                else
                {
                    ApplyOffset();
                }
            }
        }

        private void RemoveFromList(DataList list, int value)
        {
            int idx = list.IndexOf(value);
            if (idx >= 0)
            {
                list.RemoveAt(idx);
            }
        }

        [NetworkCallable(100)]
        public void NetOnBeginLoading(int sessionId, int objectIndex)
        {
            _playbackSessionId = sessionId;
            _playingObjectIndex = objectIndex;
            _isWaitingForReady = true;
            _isStagePlaying = false;
            _localReadySessionId = -1;
            _localErrorSessionId = -1;
            _localStartedSessionId = -1;
            _isStageOffsetLocked = false;
            _hasPendingLatencyUpdate = false;
            _loadingStartTime = Time.time;

            if (_isPerformer)
            {
                SyncLocalPerformerState();
            }

            MuteVideoPlayerForLoading();
            StopAllDirections();

            // 演者が存在する場合のみ動画ロード時間を利用してインスタンス内の通信遅延を計測
            if (HasActivePerformer())
            {
                TriggerBeaconLatencyPing();
            }
        }

        private void ReportLocalPlayerReady()
        {
            if (_localReadySessionId == _playbackSessionId) return;
            _localReadySessionId = _playbackSessionId;

            int localId = Utilities.IsValid(Networking.LocalPlayer) ? Networking.LocalPlayer.playerId : 1;
            SendCustomNetworkEvent(
                NetworkEventTarget.Owner,
                nameof(NetNotifyPlayerLoadStatus),
                _playbackSessionId,
                localId,
                true,
                _isPerformer
            );
        }

        private void ReportLocalPlayerError()
        {
            if (_localReadySessionId == _playbackSessionId) return;
            if (_localErrorSessionId == _playbackSessionId) return;
            _localErrorSessionId = _playbackSessionId;

            int localId = Utilities.IsValid(Networking.LocalPlayer) ? Networking.LocalPlayer.playerId : 1;
            SendCustomNetworkEvent(
                NetworkEventTarget.Owner,
                nameof(NetNotifyPlayerLoadStatus),
                _playbackSessionId,
                localId,
                false,
                _isPerformer
            );
        }

        [NetworkCallable(100)]
        public void NetNotifyPlayerLoadStatus(int sessionId, int playerId, bool isSuccess, bool isPerformer)
        {
            if (!Networking.IsOwner(gameObject)) return;
            if (!_isWaitingForReady || sessionId != _playbackSessionId) return;

            UpdatePerformerIdList(playerId, isPerformer);

            if (isSuccess)
            {
                RemoveFromList(_failedPlayerIds, playerId);
                if (!_readyPlayerIds.Contains(playerId))
                {
                    _readyPlayerIds.Add(playerId);
                }
            }
            else
            {
                if (!_readyPlayerIds.Contains(playerId) && !_failedPlayerIds.Contains(playerId))
                {
                    _failedPlayerIds.Add(playerId);
                }
            }

            EvaluatePlayersLoadState(false);
        }

        private void RecordExpectedPlayers()
        {
            _expectedPlayerIds.Clear();
            _readyPlayerIds.Clear();
            _failedPlayerIds.Clear();

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

        private void EvaluatePlayersLoadState(bool isTimeout)
        {
            if (!Networking.IsOwner(gameObject) || !_isWaitingForReady) return;

            int totalExpected = 0;
            int readyCount = 0;
            int failedCount = 0;
            int totalPerformers = 0;
            int readyPerformers = 0;
            bool performerFailed = false;

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

                    totalExpected++;
                    bool isReady = _readyPlayerIds.Contains(pid);
                    bool isFailed = _failedPlayerIds.Contains(pid) || (isTimeout && !isReady);
                    bool isPerf = _performerPlayerIds.Contains(pid);

                    if (isPerf)
                    {
                        totalPerformers++;
                    }

                    if (isReady)
                    {
                        readyCount++;
                        if (isPerf)
                        {
                            readyPerformers++;
                        }
                    }
                    else if (isFailed)
                    {
                        failedCount++;
                        if (isPerf)
                        {
                            performerFailed = true;
                        }
                    }
                }
            }

            if (_uiController != null)
            {
                bool hasPerformer = totalPerformers > 0;
                bool allPerformersReady = hasPerformer && readyPerformers >= totalPerformers && !performerFailed;
                _uiController.UpdateLoadProgress(
                    readyCount,
                    totalExpected,
                    failedCount,
                    ErrorAbortThreshold,
                    hasPerformer,
                    allPerformersReady,
                    performerFailed
                );
            }

            // 1. 演者が1人でも読み込み失敗（またはタイムアウト未完了）している場合は割合に関係なく再生中止（Force Launchで無効化されていない場合）
            if (performerFailed && !_ignorePerformerError)
            {
                if (_uiController != null) _uiController.ConsumeForceLaunchIfOnce();
                AbortSynchronizedStagePlayback();
                return;
            }

            // 2. 失敗（またはタイムアウト未完了）したプレイヤーの割合が閾値（デフォルト50%）以上の場合は再生中止（Force Launchで無効化されていない場合）
            if (totalExpected > 0 && failedCount > 0 && !_ignoreThresholdError)
            {
                float failureRate = (float)failedCount / totalExpected;
                float thresholdRatio = Mathf.Clamp01(ErrorAbortThreshold / 100f);
                if (failureRate >= thresholdRatio)
                {
                    if (_uiController != null) _uiController.ConsumeForceLaunchIfOnce();
                    AbortSynchronizedStagePlayback();
                    return;
                }
            }

            // 3. 全員の応答が揃った場合、またはタイムアウト時点で中止条件に該当しなかった場合は再生開始
            if (isTimeout || (readyCount + failedCount >= totalExpected))
            {
                if (_uiController != null) _uiController.ConsumeForceLaunchIfOnce();
                BeginSynchronizedStagePlayback();
            }
        }

        public void CheckReadyTimeout()
        {
            if (!Networking.IsOwner(gameObject) || !_isWaitingForReady) return;
            if (Time.time - _loadingStartTime >= ReadyTimeout - 0.1f)
            {
                EvaluatePlayersLoadState(true);
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

        private void AbortSynchronizedStagePlayback()
        {
            if (!Networking.IsOwner(gameObject) || !_isWaitingForReady) return;

            _isWaitingForReady = false;
            _isStagePlaying = false;
            _playingObjectIndex = -1;
            _stageStartServerTime = 0;
            RequestSerialization();

            StopVideoOnPlayer();

            SendCustomNetworkEvent(
                NetworkEventTarget.All,
                nameof(NetAbortStagePlayback),
                _playbackSessionId
            );
        }

        [NetworkCallable(100)]
        public void NetAbortStagePlayback(int sessionId)
        {
            if (sessionId != _playbackSessionId) return;

            _isWaitingForReady = false;
            _isStagePlaying = false;
            _playingObjectIndex = -1;
            _stageStartServerTime = 0;

            StopLocalVideoPlayer();
            RestoreVideoPlayerVolume();
            StopAllDirections();
            OnStagePlaybackEnded();
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
            _isStageOffsetLocked = true;

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
            if (!HasActivePerformer() || _useFallbackDelay) return;
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
            if (!HasActivePerformer() || _useFallbackDelay) return;
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
            // 上演中かつ既に自分のオフセットが確定している既存プレイヤーは、上演終了まで遅延再計算・オフセット更新を保留する
            if (_isStagePlaying && !_isWaitingForReady && _isStageOffsetLocked)
            {
                _hasPendingLatencyUpdate = true;
                return;
            }

            float prevDelay = _networkDelay;
            _networkDelay = Mathf.Clamp(GetServerDelaySeconds(), 0f, 1f);
            if (_isStagePlaying && !_isWaitingForReady)
            {
                // 上演中に途中参加したプレイヤー本人のみ、自身の初回遅延計測が完了した時点で一度だけオフセットを適用しロックする
                if (_networkDelay > 0f)
                {
                    _isStageOffsetLocked = true;
                    if (Mathf.Abs(_networkDelay - prevDelay) > 0.001f)
                    {
                        ApplyOffset();
                    }
                }
            }

            if (_uiController != null)
            {
                _uiController.RefreshDelayStatusUI();
            }
        }

        private void OnStagePlaybackEnded()
        {
            _isStageOffsetLocked = false;
            if (_hasPendingLatencyUpdate)
            {
                _hasPendingLatencyUpdate = false;
                UpdateNetworkDelay();
                ApplyOffset();
                if (HasActivePerformer())
                {
                    TriggerBeaconLatencyPing();
                }
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

            if (_uiController == null && transform.parent != null)
            {
                _uiController = transform.parent.GetComponentInChildren<SSSUIController>(true);
            }
            if (_uiController != null)
            {
                _uiController.SSSCore = this;
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

        private void ApplyPerformerObjects()
        {
            if (_performerEnableObjects != null)
            {
                for (int i = 0; i < _performerEnableObjects.Length; i++)
                {
                    GameObject obj = _performerEnableObjects[i];
                    if (obj == null || IsProtectedSystemObject(obj)) continue;
                    if (obj.activeSelf != _isPerformer)
                    {
                        obj.SetActive(_isPerformer);
                    }
                }
            }

            if (_performerDisableObjects != null)
            {
                bool activeForCurrentRole = !_isPerformer;
                for (int i = 0; i < _performerDisableObjects.Length; i++)
                {
                    GameObject obj = _performerDisableObjects[i];
                    if (obj == null || IsProtectedSystemObject(obj)) continue;
                    if (obj.activeSelf != activeForCurrentRole)
                    {
                        obj.SetActive(activeForCurrentRole);
                    }
                }
            }
        }

        private bool IsProtectedSystemObject(GameObject obj)
        {
            if (obj == null) return true;
            Transform t = obj.transform;

            // 1. SSSCore自身・子階層（Bridge等）・親階層（Stage Sync Systemルート等）の保護
            if (t == transform || transform.IsChildOf(t) || t.IsChildOf(transform)) return true;

            // 2. SSSの主要コンポーネントおよび動画プレイヤーを含むオブジェクト（またはその親階層）の保護
            if (obj.GetComponentInChildren<SSSCore>(true) != null) return true;
            if (obj.GetComponentInChildren<DirectionController>(true) != null) return true;
            if (obj.GetComponentInChildren<SSSUIController>(true) != null) return true;
            if (obj.GetComponentInChildren<DirectionObject>(true) != null) return true;
            if (obj.GetComponentInChildren<DirectionSwitch>(true) != null) return true;
            if (obj.GetComponentInChildren<PerformerChecker>(true) != null) return true;
            if (obj.GetComponentInChildren<UIOperatorChecker>(true) != null) return true;
            if (obj.GetComponentInChildren<Controller>(true) != null) return true;
            if (obj.GetComponentInChildren<VideoCore>(true) != null) return true;

            // 3. 動画プレイヤー・演出オブジェクトの子階層の保護
            if (YamaPlayer_controller != null && t.IsChildOf(YamaPlayer_controller.transform)) return true;
            if (Iwasync_core != null && t.IsChildOf(Iwasync_core.transform)) return true;
            if (_directions != null)
            {
                for (int i = 0; i < _directions.Length; i++)
                {
                    if (_directions[i] != null && t.IsChildOf(_directions[i].transform)) return true;
                }
            }

            // 4. Stage Sync System の UI 階層の保護
            if (transform.parent != null)
            {
                Transform uiRoot = transform.parent.Find("UI");
                if (uiRoot != null && (t == uiRoot || uiRoot.IsChildOf(t) || t.IsChildOf(uiRoot))) return true;
            }

            return false;
        }

        private bool ShouldPlayDirection(DirectionObject dirObj)
        {
            if (dirObj == null) return false;
            if (_isPerformer && !dirObj.playForPerformer) return false;
            return true;
        }

        private void SyncActiveDirection(bool play)
        {
            if (_directions == null) return;

            bool anyActivated = false;
            for (int i = 0; i < _directions.Length; i++)
            {
                if (_directions[i] == null) continue;
                bool shouldBeActive = play && (i == _playingObjectIndex) && ShouldPlayDirection(_directions[i]);
                if (shouldBeActive)
                {
                    DirectionSetting(_directions[i], true);
                    _activeDirectionIndex = i;
                    anyActivated = true;
                }
                else if (_directions[i].gameObject.activeSelf)
                {
                    DirectionSetting(_directions[i], false);
                }
            }

            if (!anyActivated)
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

        private void StopVideoOnPlayer()
        {
            if (IsUsingYamaPlayer())
            {
                if (!Networking.IsOwner(YamaPlayer_controller.gameObject))
                {
                    Networking.SetOwner(Networking.LocalPlayer, YamaPlayer_controller.gameObject);
                }
                YamaPlayer_controller.Stopped = true;
                YamaPlayer_controller.RequestSerialization();
            }
            else if (IsUsingIwaSync())
            {
                Iwasync_core.TakeOwnership();
                Iwasync_core.Stop();
                Iwasync_core.RequestSerialization();
            }
        }

        private void StopLocalVideoPlayer()
        {
            if (IsUsingYamaPlayer())
            {
                if (YamaPlayer_controller.VideoPlayerHandle != null)
                {
                    YamaPlayer_controller.VideoPlayerHandle.Stop();
                }
            }
            else if (IsUsingIwaSync())
            {
                Iwasync_core.Stop();
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
        // 演者が不在の場合: 全員 0秒 (遅延計測・オフセット補正を省略)
        // Forced Fallback 有効時: 演者 +AudienceDelays / 観客 0秒 (定数フォールバック)
        // Default 時: 演者 +NetworkDelay (未計測時は AudienceDelays) / 観客 -NetworkDelay
        private float GetOffsetSeconds()
        {
            if (!HasActivePerformer())
            {
                return 0f;
            }
            if (_useFallbackDelay)
            {
                return _isPerformer ? (AudienceDelays / 1000f) : 0f;
            }
            if (_isPerformer)
            {
                return (_networkDelay > 0f) ? _networkDelay : (AudienceDelays / 1000f);
            }
            return -_networkDelay;
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
                float targetDelay = GetOffsetSeconds();
                if (Mathf.Abs(YamaPlayer_controller.LocalDelay - targetDelay) > 0.0005f)
                {
                    YamaPlayer_controller.LocalDelay = targetDelay;
                }
            }
            else if (IsUsingIwaSync())
            {
                int targetOffset = GetOffsetTime();
                if (Iwasync_core.offsetTime != targetOffset)
                {
                    Iwasync_core.offsetTime = targetOffset;
                }
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