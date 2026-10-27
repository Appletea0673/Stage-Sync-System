using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Data;
using VRC.SDKBase;

namespace AppleteaSystems.StageSyncSystem
{
    /// <summary>
    /// 遅延時間補正検証用デバッグモニター（テスト①・テスト③用）
    /// SSSCoreに対して片方向参照のみで動作し、検証完了後は本スクリプトとシーン上のGameObjectを削除するだけで完全に除去可能。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SSSDelayDebugMonitor : UdonSharpBehaviour
    {
        [SerializeField] private SSSCore _sssCore;
        [SerializeField] private Text _debugText;
        [SerializeField] private float _uiUpdateInterval = 0.1f;
        [SerializeField] private float _periodicLogInterval = 5.0f;

        private float _lastUiUpdateTime = 0f;
        private bool _wasStageActive = false;
        private float _stageActiveStartTime = 0f;
        private bool _loggedAt02s = false;
        private bool _loggedAt15s = false;
        private float _lastPeriodicLogTime = 0f;
        private int _lastLoggedNetworkDelay = -99999;
        private int _lastKnownPairCount = -1;

        public SSSCore Core
        {
            get => _sssCore;
            set => _sssCore = value;
        }

        public Text DebugText
        {
            get => _debugText;
            set => _debugText = value;
        }

        void Update()
        {
            if (_sssCore == null) return;

            bool isStageActive = _sssCore.IsStagePlaying && !_sssCore.IsWaitingForReady;
            if (isStageActive && !_wasStageActive)
            {
                _wasStageActive = true;
                _stageActiveStartTime = Time.time;
                _loggedAt02s = false;
                _loggedAt15s = false;
                _lastPeriodicLogTime = Time.time;
                WriteTelemetryLog("StagePlaybackStart");
            }
            else if (!isStageActive && _wasStageActive)
            {
                _wasStageActive = false;
                WriteTelemetryLog("StagePlaybackEnd");
            }

            if (isStageActive)
            {
                float elapsed = Time.time - _stageActiveStartTime;
                if (!_loggedAt02s && elapsed >= 0.2f)
                {
                    _loggedAt02s = true;
                    WriteTelemetryLog("PostInitialSeek_0.2s");
                }
                if (!_loggedAt15s && elapsed >= 1.5f)
                {
                    _loggedAt15s = true;
                    WriteTelemetryLog("PostDelayedForceSync_1.5s");
                }
                if (Time.time - _lastPeriodicLogTime >= _periodicLogInterval)
                {
                    _lastPeriodicLogTime = Time.time;
                    WriteTelemetryLog("Periodic_5.0s");
                }
            }

            DataDictionary pairDelays = _sssCore.UndirectedPairDelays;
            int currentPairCount = pairDelays != null ? pairDelays.Count : 0;
            int currentNetDelay = Mathf.RoundToInt(_sssCore.NetworkDelay);
            if (currentNetDelay != _lastLoggedNetworkDelay || currentPairCount != _lastKnownPairCount)
            {
                _lastLoggedNetworkDelay = currentNetDelay;
                _lastKnownPairCount = currentPairCount;
                WriteTelemetryLog("LatencyUpdated");
            }

            if (Time.time - _lastUiUpdateTime >= _uiUpdateInterval)
            {
                _lastUiUpdateTime = Time.time;
                RefreshOverlayText();
            }
        }

        private float ComputeCircularDiffSeconds(float a, float b, float duration, bool isLooping)
        {
            float diff = a - b;
            if (isLooping && duration > 0.5f && !float.IsInfinity(duration))
            {
                float half = duration * 0.5f;
                while (diff > half) diff -= duration;
                while (diff < -half) diff += duration;
            }
            return diff;
        }

        private void RefreshOverlayText()
        {
            if (_debugText == null || _sssCore == null) return;

            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            string localInfo = Utilities.IsValid(localPlayer)
                ? $"{localPlayer.playerId}:{localPlayer.displayName}"
                : "Editor/None";

            string roleStr = _sssCore.IsPerformer ? "<color=#00FFFF>Performer</color>" : "<color=#AAFFAA>Audience</color>";
            string modeStr = _sssCore.IsCurrentlyUsingFallbackDelay()
                ? "<color=#FFAA00>Fallback</color>"
                : "<color=#00FF88>Operational</color>";

            int totalPlayers = VRCPlayerApi.GetPlayerCount();
            DataList perfList = _sssCore.PerformerPlayerIds;
            int perfCount = perfList != null ? perfList.Count : 0;

            int netDelayMs = Mathf.RoundToInt(_sssCore.NetworkDelay);
            int baseSysMs = _sssCore.BaseSystemDelayMs;
            int fallbackMs = _sssCore.AudienceDelaysMs;
            float appliedOffsetMs = _sssCore.DebugAppliedOffsetSeconds * 1000f;

            bool isStageActive = _sssCore.IsStagePlaying && !_sssCore.IsWaitingForReady;
            string stageStateStr = _sssCore.IsWaitingForReady
                ? "<color=#FFFF00>Loading/Waiting</color>"
                : (isStageActive ? "<color=#00FF88>Playing</color>" : "<color=#AAAAAA>Idle</color>");

            float videoTime = _sssCore.DebugCurrentVideoTime;
            float targetTime = _sssCore.DebugTargetStageVideoTime;
            float unshiftedTime = _sssCore.DebugUnshiftedStageVideoTime;
            float timelineTime = _sssCore.DebugCurrentTimelineTime;
            float duration = _sssCore.DebugVideoDuration;
            bool isLooping = _sssCore.DebugIsVideoLooping;

            float videoShiftMs = isStageActive
                ? ComputeCircularDiffSeconds(videoTime, unshiftedTime, duration, isLooping) * 1000f
                : 0f;
            float videoSeekErrMs = isStageActive
                ? ComputeCircularDiffSeconds(videoTime, targetTime, duration, isLooping) * 1000f
                : 0f;
            float timelineDiffMs = isStageActive
                ? ComputeCircularDiffSeconds(timelineTime, videoTime, duration, isLooping) * 1000f
                : 0f;

            string seekErrColor = Mathf.Abs(videoSeekErrMs) <= 20f ? "#00FF88" : (Mathf.Abs(videoSeekErrMs) <= 50f ? "#FFAA00" : "#FF5555");
            string tlDiffColor = Mathf.Abs(timelineDiffMs) <= 20f ? "#00FF88" : (Mathf.Abs(timelineDiffMs) <= 50f ? "#FFAA00" : "#FF5555");

            string text = "<b>[SSS Delay Telemetry - Test 1 & Test 3]</b>\n";
            text += $"Local: {localInfo} | Role: {roleStr} | Mode: {modeStr} | Players: {totalPlayers} (Perf: {perfCount})\n";
            text += "--------------------------------------------------------------------------------\n";
            text += "<b>[Test 1: Internal Delay & Video/Timeline Sync]</b>\n";
            text += $"CalcNetDelay: <b>{netDelayMs} ms</b> | BaseSysDelay: <b>{baseSysMs} ms</b> (TotalCalc: {netDelayMs + baseSysMs} ms | FallbackConst: {fallbackMs} ms)\n";
            text += $"AppliedOffset: <b>{appliedOffsetMs:+0.0;-0.0;0.0} ms</b> | State: {stageStateStr}\n";
            text += $"VideoTime: {videoTime:F3}s | TargetTime: {targetTime:F3}s | BaseStageTime: {unshiftedTime:F3}s | TLTime: {timelineTime:F3}s\n";
            text += $"VideoShift(vs Base): <b>{videoShiftMs:+0.0;-0.0;0.0} ms</b> | ";
            text += $"SeekErr(vs Target): <color={seekErrColor}><b>{videoSeekErrMs:+0.0;-0.0;0.0} ms</b></color> | ";
            text += $"TimelineDiff(TL-Video): <color={tlDiffColor}><b>{timelineDiffMs:+0.0;-0.0;0.0} ms</b></color>\n";
            text += "--------------------------------------------------------------------------------\n";
            text += "<b>[Test 3: Pairwise Bidirectional Delay d(X->Y), d(Y->X) & Clock Skew δ]</b>\n";
            text += BuildPairwiseSummaryString(true);

            _debugText.text = text;
        }

        private string BuildPairwiseSummaryString(bool richText)
        {
            DataList knownPlayers = _sssCore.KnownPlayers;
            DataDictionary directedMin = _sssCore.DirectedPairDelays;
            DataDictionary directedRaw = _sssCore.DirectedPairRawDelays;
            DataDictionary undirected = _sssCore.UndirectedPairDelays;
            DataList perfList = _sssCore.PerformerPlayerIds;

            if (knownPlayers == null || knownPlayers.Count < 2)
            {
                return "  (Waiting for 2+ players to measure pairwise latency...)\n";
            }

            string result = "";
            int count = knownPlayers.Count;
            int pairShown = 0;

            for (int i = 0; i < count; i++)
            {
                int idA = knownPlayers[i].Int;
                for (int j = i + 1; j < count; j++)
                {
                    int idB = knownPlayers[j].Int;
                    int x = idA < idB ? idA : idB;
                    int y = idA < idB ? idB : idA;

                    string keyXY = $"{x}_{y}";
                    string keyYX = $"{y}_{x}";

                    DataToken minXYToken = default;
                    DataToken minYXToken = default;
                    DataToken rawXYToken = default;
                    DataToken rawYXToken = default;
                    DataToken undirToken = default;

                    bool hasXY = directedMin != null && directedMin.TryGetValue(keyXY, TokenType.Int, out minXYToken);
                    bool hasYX = directedMin != null && directedMin.TryGetValue(keyYX, TokenType.Int, out minYXToken);
                    bool hasRawXY = directedRaw != null && directedRaw.TryGetValue(keyXY, TokenType.Int, out rawXYToken);
                    bool hasRawYX = directedRaw != null && directedRaw.TryGetValue(keyYX, TokenType.Int, out rawYXToken);
                    bool hasUndir = undirected != null && undirected.TryGetValue(keyXY, TokenType.Int, out undirToken);

                    if (!hasXY && !hasYX && !hasUndir) continue;

                    string strXY = hasXY ? $"{minXYToken.Int}ms" : "--";
                    if (hasRawXY) strXY += $"(raw:{rawXYToken.Int})";

                    string strYX = hasYX ? $"{minYXToken.Int}ms" : "--";
                    if (hasRawYX) strYX += $"(raw:{rawYXToken.Int})";

                    string strBiAvg = hasUndir ? $"{undirToken.Int}ms" : "--";
                    string strSkew = "--";
                    if (hasXY && hasYX)
                    {
                        float skew = (minXYToken.Int - minYXToken.Int) * 0.5f;
                        strSkew = $"{skew:+0.0;-0.0;0.0}ms";
                    }

                    bool xIsPerf = perfList != null && perfList.Contains(x);
                    bool yIsPerf = perfList != null && perfList.Contains(y);
                    string tagX = xIsPerf ? $"P{x}[Perf]" : $"P{x}";
                    string tagY = yIsPerf ? $"P{y}[Perf]" : $"P{y}";

                    if (richText)
                    {
                        result += $"  {tagX}<->{tagY}: d({x}->{y})={strXY} | d({y}->{x})={strYX} | <b>BiAvg={strBiAvg}</b> | <b>Skew(δ)={strSkew}</b>\n";
                    }
                    else
                    {
                        result += $"{tagX}<->{tagY}: d({x}->{y})={strXY}, d({y}->{x})={strYX}, BiAvg={strBiAvg}, Skew={strSkew} ; ";
                    }

                    pairShown++;
                    if (richText && pairShown >= 6)
                    {
                        result += "  ...(see output_log.txt for full list)\n";
                        return result;
                    }
                }
            }

            if (pairShown == 0)
            {
                return "  (No pairwise latency packets recorded yet)\n";
            }

            return result;
        }

        public void WriteTelemetryLog(string triggerReason)
        {
            if (_sssCore == null) return;

            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            string localInfo = Utilities.IsValid(localPlayer)
                ? $"{localPlayer.playerId}:{localPlayer.displayName}"
                : "Editor/None";

            bool isStageActive = _sssCore.IsStagePlaying && !_sssCore.IsWaitingForReady;
            float videoTime = _sssCore.DebugCurrentVideoTime;
            float targetTime = _sssCore.DebugTargetStageVideoTime;
            float unshiftedTime = _sssCore.DebugUnshiftedStageVideoTime;
            float timelineTime = _sssCore.DebugCurrentTimelineTime;
            float duration = _sssCore.DebugVideoDuration;
            bool isLooping = _sssCore.DebugIsVideoLooping;

            float appliedOffsetMs = _sssCore.DebugAppliedOffsetSeconds * 1000f;
            float videoShiftMs = isStageActive
                ? ComputeCircularDiffSeconds(videoTime, unshiftedTime, duration, isLooping) * 1000f
                : 0f;
            float videoSeekErrMs = isStageActive
                ? ComputeCircularDiffSeconds(videoTime, targetTime, duration, isLooping) * 1000f
                : 0f;
            float timelineDiffMs = isStageActive
                ? ComputeCircularDiffSeconds(timelineTime, videoTime, duration, isLooping) * 1000f
                : 0f;

            string modeStr = _sssCore.IsCurrentlyUsingFallbackDelay() ? "Fallback" : "Operational";
            string roleStr = _sssCore.IsPerformer ? "Performer" : "Audience";
            int netDelayMs = Mathf.RoundToInt(_sssCore.NetworkDelay);

            Debug.Log(
                $"[SSS-Debug-Test1] Trigger={triggerReason} | Local={localInfo} | Role={roleStr} | Mode={modeStr} | " +
                $"CalcNetDelay={netDelayMs}ms | BaseSysDelay={_sssCore.BaseSystemDelayMs}ms | FallbackConst={_sssCore.AudienceDelaysMs}ms | " +
                $"AppliedOffset={appliedOffsetMs:F1}ms | VideoShift={videoShiftMs:F1}ms | SeekErr={videoSeekErrMs:F1}ms | TimelineDiff={timelineDiffMs:F1}ms | " +
                $"VideoTime={videoTime:F3}s | TargetTime={targetTime:F3}s | TLTime={timelineTime:F3}s"
            );

            Debug.Log(
                $"[SSS-Debug-Test3] Trigger={triggerReason} | Local={localInfo} | Pairs: {BuildPairwiseSummaryString(false)}"
            );
        }
    }
}
