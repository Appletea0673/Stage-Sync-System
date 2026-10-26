using System;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using UdonSharp;

namespace AppleteaSystems.StageSyncSystem
{
    public enum DirectionSortMode
    {
        Hierarchy = 0,
        ByNameAsc = 1,
        Manual = 2,
        ByNameDesc = 3,
        ByGameObjectNameAsc = 4,
        ByGameObjectNameDesc = 5
    }

    [DisallowMultipleComponent]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class SSSUIController : UdonSharpBehaviour
    {
        [Header("Core Reference")]
        [SerializeField]
        [Tooltip("連携先のSSSCore")]
        SSSCore _sssCore;

        [Header("Playlist Switches")]
        [SerializeField]
        [Tooltip("演出再生用UIボタン配列 (Editor拡張により自動設定)")]
        Button[] _directionSWs;

        [Header("Status UI - Loaded Player Count")]
        [SerializeField]
        [Tooltip("[読み込めた人数]/[全体の人数] を表示するText")]
        Text _loadedPlayerCountText;

        [SerializeField]
        [Tooltip("未読み込み時（初期待機状態）の表示テキスト")]
        string _initialLoadedCountText = "0/0";

        [SerializeField]
        [Tooltip("全員読み込めた場合の文字色 (カラーコード指定)")]
        string _loadedAllColorCode = "#4D4D4D";

        [SerializeField]
        [Tooltip("一部読み込めていない場合の文字色 (カラーコード指定)")]
        string _loadedPartialColorCode = "#E67E22";

        [SerializeField]
        [Tooltip("設定した閾値を下回っている場合の文字色 (カラーコード指定)")]
        string _loadedBelowThresholdColorCode = "#E74C3C";

        [Header("Status UI - Performer Status")]
        [SerializeField]
        [Tooltip("演者の読み込み状態を表示するText")]
        Text _performerStatusText;

        [SerializeField]
        [Tooltip("演者が読み込めた場合に有効化するオブジェクト（青ランプ等）")]
        GameObject _performerLoadedObject;

        [SerializeField]
        [Tooltip("演者が読み込み失敗した場合に有効化するオブジェクト（警告ランプ等）")]
        GameObject _performerErrorObject;

        [SerializeField]
        string _performerStandbyText = "Standby";

        [SerializeField]
        string _performerNoPerformerText = "No Performer";

        [SerializeField]
        string _performerLoadingText = "Loading...";

        [SerializeField]
        string _performerLoadedText = "Loaded";

        [SerializeField]
        string _performerErrorText = "Error";

        [Header("Status UI - Delay Calculation Status")]
        [SerializeField]
        [Tooltip("遅延計算のステータスを表示するText")]
        Text _delayStatusText;

        [SerializeField]
        string _delayOperationalText = "Operational";

        [SerializeField]
        string _delayFallbackText = "Fallback";

        [Header("Direction Sort UI")]
        [SerializeField]
        [Tooltip("Hierarchy順ソートのラジオボタンToggle")]
        Toggle _sortHierarchyToggle;

        [SerializeField]
        [Tooltip("演出名（昇順）ソートのラジオボタンToggle")]
        Toggle _sortByNameToggle;

        [SerializeField]
        [Tooltip("手動（Manual）ソートのラジオボタンToggle")]
        Toggle _sortManualToggle;

        [SerializeField]
        [Tooltip("演出名（降順）ソートのラジオボタンToggle（任意）")]
        Toggle _sortByNameDescToggle;

        [SerializeField]
        [Tooltip("GameObject名（昇順）ソートのラジオボタンToggle（任意）")]
        Toggle _sortByGameObjectNameAscToggle;

        [SerializeField]
        [Tooltip("GameObject名（降順）ソートのラジオボタンToggle（任意）")]
        Toggle _sortByGameObjectNameDescToggle;

        [Header("Error Detection (Force Launch) UI")]
        [SerializeField]
        [Tooltip("全員読み込めていない（閾値未達）場合の上演停止を無効化するToggle")]
        Toggle _ignoreThresholdToggle;

        [SerializeField]
        [Tooltip("演者が読み込めていない場合の上演停止を無効化するToggle")]
        Toggle _ignorePerformerErrorToggle;

        [SerializeField]
        [Tooltip("Force Launchの有効期間設定フォームのGameObject")]
        GameObject _forceLaunchDurationForm;

        [SerializeField]
        [Tooltip("「次の1回だけ有効」のラジオボタンToggle")]
        Toggle _durationOnceToggle;

        [SerializeField]
        [Tooltip("「常時無効化」のラジオボタンToggle")]
        Toggle _durationAlwaysToggle;

        [Header("Delay Calculation Settings UI")]
        [SerializeField]
        [Tooltip("デフォルトの遅延計算を使用するラジオボタンToggle")]
        Toggle _delayDefaultToggle;

        [SerializeField]
        [Tooltip("Fallbackの定数を使用するラジオボタンToggle")]
        Toggle _delayFallbackToggle;

        // Status constants
        public const int LOAD_COLOR_DEFAULT = 0;
        public const int LOAD_COLOR_WARNING = 1;
        public const int LOAD_COLOR_ERROR = 2;

        public const int PERFORMER_STATUS_STANDBY = 0;
        public const int PERFORMER_STATUS_NONE = 1;
        public const int PERFORMER_STATUS_LOADING = 2;
        public const int PERFORMER_STATUS_LOADED = 3;
        public const int PERFORMER_STATUS_ERROR = 4;

        // Synced UI & Settings State
        [UdonSynced]
        int _syncedReadyCount = 0;

        [UdonSynced]
        int _syncedTotalCount = 0;

        [UdonSynced]
        int _syncedLoadColorState = LOAD_COLOR_DEFAULT;

        [UdonSynced]
        int _syncedPerformerStatus = PERFORMER_STATUS_STANDBY;

        [UdonSynced]
        int _syncedSortMode = (int)DirectionSortMode.Hierarchy;

        [UdonSynced]
        bool _syncedIgnoreThreshold = false;

        [UdonSynced]
        bool _syncedIgnorePerformerError = false;

        [UdonSynced]
        bool _syncedForceLaunchAlways = false;

        [UdonSynced]
        bool _syncedForceFallbackDelay = false;

        [UdonSynced]
        bool _syncedIsFallbackDelay = false;

        // Local state
        bool _isUpdatingUI = false;
        bool _initialized = false;
        bool _canOperateUI = true;
        bool _isSettingsLocked = false;
        int _lockedSortMode = (int)DirectionSortMode.Hierarchy;
        bool _lockedIgnoreThreshold = false;
        bool _lockedIgnorePerformerError = false;
        bool _lockedForceLaunchAlways = false;
        bool _lockedForceFallbackDelay = false;
        Color _fallbackDefaultTextColor = new Color(0.3019608f, 0.3019608f, 0.3019608f, 1f);
        int[] _currentSortedIndices;
        int[] _manualOrderIndices;

        #region Accessors

        public SSSCore SSSCore
        {
            get => _sssCore;
            set => _sssCore = value;
        }

        public Button[] DirectionSWs
        {
            get => _directionSWs;
            set => _directionSWs = value;
        }

        public bool IgnoreThresholdError => _syncedIgnoreThreshold;
        public bool IgnorePerformerError => _syncedIgnorePerformerError;
        public bool ForceLaunchAlways => _syncedForceLaunchAlways;
        public bool UseFallbackDelay => _syncedForceFallbackDelay;
        public DirectionSortMode CurrentSortMode => (DirectionSortMode)_syncedSortMode;
        public bool IsManualSortMode => _syncedSortMode == (int)DirectionSortMode.Manual;
        public bool IsSettingsLocked => _isSettingsLocked || (_sssCore != null && _sssCore.IsLoadingOrPlaying);

        #endregion

        void Start()
        {
            InitializeIfNeeded();
            ApplyAllToUIAndCore();
        }

        private void InitializeIfNeeded()
        {
            if (_initialized) return;
            _initialized = true;

            if (_sssCore == null && transform.parent != null)
            {
                _sssCore = transform.parent.GetComponentInChildren<SSSCore>(true);
            }

            if (_sssCore != null && _sssCore.UIController == null)
            {
                _sssCore.UIController = this;
            }

            if (_loadedPlayerCountText != null)
            {
                _fallbackDefaultTextColor = _loadedPlayerCountText.color;
            }

            // 有効期間フォーム自体は常に表示状態（SetActive(true)）を維持し、Toggle.interactableのみで操作制限を行う
            if (_forceLaunchDurationForm != null && !_forceLaunchDurationForm.activeSelf)
            {
                _forceLaunchDurationForm.SetActive(true);
            }

            // Force Launch用の2つのToggleは独立したON/OFFスイッチとして動作させるためToggleGroupから切り離す
            if (_ignoreThresholdToggle != null && _ignoreThresholdToggle.group != null)
            {
                _ignoreThresholdToggle.group = null;
            }
            if (_ignorePerformerErrorToggle != null && _ignorePerformerErrorToggle.group != null)
            {
                _ignorePerformerErrorToggle.group = null;
            }

            // ラジオボタン群のToggleGroupは全解除不可（allowSwitchOff = false）を保証
            if (_durationOnceToggle != null && _durationOnceToggle.group != null)
            {
                _durationOnceToggle.group.allowSwitchOff = false;
            }
            if (_sortHierarchyToggle != null && _sortHierarchyToggle.group != null)
            {
                _sortHierarchyToggle.group.allowSwitchOff = false;
            }
            if (_delayDefaultToggle != null && _delayDefaultToggle.group != null)
            {
                _delayDefaultToggle.group.allowSwitchOff = false;
            }

            InitializeSortArrays();
        }

        private void InitializeSortArrays()
        {
            int count = GetDirectionCount();
            if (count <= 0)
            {
                _currentSortedIndices = new int[0];
                _manualOrderIndices = new int[0];
                return;
            }

            if (_manualOrderIndices == null || _manualOrderIndices.Length != count)
            {
                _manualOrderIndices = new int[count];
                for (int i = 0; i < count; i++)
                {
                    _manualOrderIndices[i] = i;
                }
            }

            if (_currentSortedIndices == null || _currentSortedIndices.Length != count)
            {
                _currentSortedIndices = new int[count];
                for (int i = 0; i < count; i++)
                {
                    _currentSortedIndices[i] = i;
                }
            }
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player) && !player.isLocal && Networking.IsOwner(gameObject))
            {
                RequestSerialization();
            }
        }

        public override void OnDeserialization()
        {
            InitializeIfNeeded();

            // 演出の読み込み中・上演中は設定項目の変更をロックし、同時に入った設定変更操作を無効化して読み込みルーチンを優先する
            if (IsSettingsLocked)
            {
                _syncedSortMode = _lockedSortMode;
                _syncedIgnoreThreshold = _lockedIgnoreThreshold;
                _syncedIgnorePerformerError = _lockedIgnorePerformerError;
                _syncedForceLaunchAlways = _lockedForceLaunchAlways;
                _syncedForceFallbackDelay = _lockedForceFallbackDelay;
            }

            ApplyAllToUIAndCore();
        }

        public void SetOperatorInteractable(bool canOperate)
        {
            _canOperateUI = canOperate;
            RefreshSettingsInteractableState();
        }

        public void SetSettingsLocked(bool locked)
        {
            InitializeIfNeeded();
            if (locked && !_isSettingsLocked)
            {
                _lockedSortMode = _syncedSortMode;
                _lockedIgnoreThreshold = _syncedIgnoreThreshold;
                _lockedIgnorePerformerError = _syncedIgnorePerformerError;
                _lockedForceLaunchAlways = _syncedForceLaunchAlways;
                _lockedForceFallbackDelay = _syncedForceFallbackDelay;
            }
            _isSettingsLocked = locked;
            RefreshSettingsInteractableState();
        }

        public void RefreshSettingsInteractableState()
        {
            bool canEditSettings = _canOperateUI && !IsSettingsLocked;

            if (_sortHierarchyToggle != null) _sortHierarchyToggle.interactable = canEditSettings;
            if (_sortByNameToggle != null) _sortByNameToggle.interactable = canEditSettings;
            if (_sortManualToggle != null) _sortManualToggle.interactable = canEditSettings;
            if (_sortByNameDescToggle != null) _sortByNameDescToggle.interactable = canEditSettings;
            if (_sortByGameObjectNameAscToggle != null) _sortByGameObjectNameAscToggle.interactable = canEditSettings;
            if (_sortByGameObjectNameDescToggle != null) _sortByGameObjectNameDescToggle.interactable = canEditSettings;

            if (_ignoreThresholdToggle != null) _ignoreThresholdToggle.interactable = canEditSettings;
            if (_ignorePerformerErrorToggle != null) _ignorePerformerErrorToggle.interactable = canEditSettings;

            bool isDurationEnabled = canEditSettings && (_syncedIgnoreThreshold || _syncedIgnorePerformerError);
            if (_durationOnceToggle != null) _durationOnceToggle.interactable = isDurationEnabled;
            if (_durationAlwaysToggle != null) _durationAlwaysToggle.interactable = isDurationEnabled;

            if (_delayDefaultToggle != null) _delayDefaultToggle.interactable = canEditSettings;
            if (_delayFallbackToggle != null) _delayFallbackToggle.interactable = canEditSettings;
        }

        #region Playlist Switch Handling (Migrated from DirectionController)

        public void OnButtonClicked()
        {
            InitializeIfNeeded();
            int sender = FindSender();
            if (sender < 0 || _sssCore == null) return;

            _sssCore.StartHandling(sender);
        }

        private int FindSender()
        {
            if (_directionSWs != null)
            {
                for (int i = 0; i < _directionSWs.Length; i++)
                {
                    if (_directionSWs[i] != null && !_directionSWs[i].enabled)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        #endregion

        #region Status Updates from SSSCore

        public void ResetStatusToStandby(bool hasActivePerformer)
        {
            InitializeIfNeeded();
            TakeOwnership();

            _syncedReadyCount = 0;
            _syncedTotalCount = 0;
            _syncedLoadColorState = LOAD_COLOR_DEFAULT;
            _syncedPerformerStatus = hasActivePerformer ? PERFORMER_STATUS_STANDBY : PERFORMER_STATUS_NONE;
            _syncedIsFallbackDelay = _syncedForceFallbackDelay || (_sssCore != null && _sssCore.IsCurrentlyUsingFallbackDelay());

            RefreshStatusUI();
            RequestSerialization();
        }

        public void UpdateStandbyPerformerStatus(bool hasActivePerformer)
        {
            InitializeIfNeeded();
            if (_syncedTotalCount > 0 && (_syncedPerformerStatus == PERFORMER_STATUS_LOADING || _syncedPerformerStatus == PERFORMER_STATUS_LOADED || _syncedPerformerStatus == PERFORMER_STATUS_ERROR))
            {
                RefreshDelayStatusUI();
                return;
            }

            _syncedPerformerStatus = hasActivePerformer ? PERFORMER_STATUS_STANDBY : PERFORMER_STATUS_NONE;
            _syncedIsFallbackDelay = _syncedForceFallbackDelay || (_sssCore != null && _sssCore.IsCurrentlyUsingFallbackDelay());
            RefreshStatusUI();
        }

        public void UpdateLoadProgress(
            int readyCount,
            int totalExpected,
            int failedCount,
            float errorAbortThresholdPercent,
            bool hasPerformer,
            bool allPerformersReady,
            bool anyPerformerFailed)
        {
            InitializeIfNeeded();
            TakeOwnership();

            _syncedReadyCount = readyCount;
            _syncedTotalCount = totalExpected;

            // 人数・エラー率に応じたテキスト色判定
            if (totalExpected <= 0 || readyCount >= totalExpected)
            {
                _syncedLoadColorState = LOAD_COLOR_DEFAULT;
            }
            else
            {
                float failureRate = (float)failedCount / totalExpected;
                float thresholdRatio = Mathf.Clamp01(errorAbortThresholdPercent / 100f);
                if (failedCount > 0 && failureRate >= thresholdRatio)
                {
                    _syncedLoadColorState = LOAD_COLOR_ERROR;
                }
                else
                {
                    _syncedLoadColorState = LOAD_COLOR_WARNING;
                }
            }

            // Performer Status 判定
            if (!hasPerformer)
            {
                _syncedPerformerStatus = PERFORMER_STATUS_NONE;
            }
            else if (anyPerformerFailed)
            {
                _syncedPerformerStatus = PERFORMER_STATUS_ERROR;
            }
            else if (allPerformersReady)
            {
                _syncedPerformerStatus = PERFORMER_STATUS_LOADED;
            }
            else
            {
                _syncedPerformerStatus = PERFORMER_STATUS_LOADING;
            }

            _syncedIsFallbackDelay = _syncedForceFallbackDelay || (_sssCore != null && _sssCore.IsCurrentlyUsingFallbackDelay());

            RefreshStatusUI();
            RequestSerialization();
        }

        public void RefreshStatusUI()
        {
            InitializeIfNeeded();

            // 1. [読み込めた人数]/[全体の人数] と文字色
            if (_loadedPlayerCountText != null)
            {
                if (_syncedTotalCount <= 0)
                {
                    _loadedPlayerCountText.text = _initialLoadedCountText;
                }
                else
                {
                    _loadedPlayerCountText.text = $"{_syncedReadyCount}/{_syncedTotalCount}";
                }

                Color defaultColor = ParseHexColor(_loadedAllColorCode, _fallbackDefaultTextColor);
                if (_syncedLoadColorState == LOAD_COLOR_ERROR)
                {
                    _loadedPlayerCountText.color = ParseHexColor(_loadedBelowThresholdColorCode, new Color(0.905f, 0.298f, 0.235f, 1f));
                }
                else if (_syncedLoadColorState == LOAD_COLOR_WARNING)
                {
                    _loadedPlayerCountText.color = ParseHexColor(_loadedPartialColorCode, new Color(0.901f, 0.494f, 0.133f, 1f));
                }
                else
                {
                    _loadedPlayerCountText.color = defaultColor;
                }
            }

            // 2. Performer Status テキスト＆ランプオブジェクトのトグル
            if (_performerStatusText != null)
            {
                switch (_syncedPerformerStatus)
                {
                    case PERFORMER_STATUS_NONE:
                        _performerStatusText.text = _performerNoPerformerText;
                        break;
                    case PERFORMER_STATUS_LOADING:
                        _performerStatusText.text = _performerLoadingText;
                        break;
                    case PERFORMER_STATUS_LOADED:
                        _performerStatusText.text = _performerLoadedText;
                        break;
                    case PERFORMER_STATUS_ERROR:
                        _performerStatusText.text = _performerErrorText;
                        break;
                    default:
                        _performerStatusText.text = _performerStandbyText;
                        break;
                }
            }

            bool showBlueLamp = (_syncedPerformerStatus == PERFORMER_STATUS_LOADED);
            bool showErrorLamp = (_syncedPerformerStatus == PERFORMER_STATUS_ERROR);

            if (_performerLoadedObject != null && _performerLoadedObject.activeSelf != showBlueLamp)
            {
                _performerLoadedObject.SetActive(showBlueLamp);
            }
            if (_performerErrorObject != null && _performerErrorObject.activeSelf != showErrorLamp)
            {
                _performerErrorObject.SetActive(showErrorLamp);
            }

            // 3. 遅延計算ステータス (Operational / Fallback)
            RefreshDelayStatusUI();
        }

        public void RefreshDelayStatusUI()
        {
            if (_delayStatusText == null) return;

            bool isFallback = _syncedForceFallbackDelay || _syncedIsFallbackDelay;
            if (_sssCore != null)
            {
                isFallback = _syncedForceFallbackDelay || _sssCore.IsCurrentlyUsingFallbackDelay();
                _syncedIsFallbackDelay = isFallback;
            }

            _delayStatusText.text = isFallback ? _delayFallbackText : _delayOperationalText;
        }

        #endregion

        #region Direction Sort Control

        public void OnSortToggleChanged()
        {
            if (_isUpdatingUI) return;
            InitializeIfNeeded();

            if (IsSettingsLocked)
            {
                SyncUITogglesFromState();
                RefreshSettingsInteractableState();
                return;
            }

            int newSortMode = _syncedSortMode;
            if (_sortHierarchyToggle != null && _sortHierarchyToggle.isOn)
            {
                newSortMode = (int)DirectionSortMode.Hierarchy;
            }
            else if (_sortByNameToggle != null && _sortByNameToggle.isOn)
            {
                newSortMode = (int)DirectionSortMode.ByNameAsc;
            }
            else if (_sortManualToggle != null && _sortManualToggle.isOn)
            {
                newSortMode = (int)DirectionSortMode.Manual;
            }
            else if (_sortByNameDescToggle != null && _sortByNameDescToggle.isOn)
            {
                newSortMode = (int)DirectionSortMode.ByNameDesc;
            }
            else if (_sortByGameObjectNameAscToggle != null && _sortByGameObjectNameAscToggle.isOn)
            {
                newSortMode = (int)DirectionSortMode.ByGameObjectNameAsc;
            }
            else if (_sortByGameObjectNameDescToggle != null && _sortByGameObjectNameDescToggle.isOn)
            {
                newSortMode = (int)DirectionSortMode.ByGameObjectNameDesc;
            }

            if (newSortMode == _syncedSortMode) return;

            TakeOwnership();
            _syncedSortMode = newSortMode;
            ApplySortOrderToPlaylist();
            RequestSerialization();
        }

        public void OnSortHierarchySelected()
        {
            if (_isUpdatingUI) return;
            if (_sortHierarchyToggle != null && !_sortHierarchyToggle.isOn) return;
            SetSortMode(DirectionSortMode.Hierarchy);
        }

        public void OnSortByNameSelected()
        {
            if (_isUpdatingUI) return;
            if (_sortByNameToggle != null && !_sortByNameToggle.isOn) return;
            SetSortMode(DirectionSortMode.ByNameAsc);
        }

        public void OnSortManualSelected()
        {
            if (_isUpdatingUI) return;
            if (_sortManualToggle != null && !_sortManualToggle.isOn) return;
            SetSortMode(DirectionSortMode.Manual);
        }

        public void SetSortMode(DirectionSortMode mode)
        {
            InitializeIfNeeded();
            if (IsSettingsLocked)
            {
                SyncUITogglesFromState();
                RefreshSettingsInteractableState();
                return;
            }
            if (_syncedSortMode == (int)mode) return;

            TakeOwnership();
            _syncedSortMode = (int)mode;
            ApplySortOrderToPlaylist();
            SyncUITogglesFromState();
            RequestSerialization();
        }

        public void ApplySortOrderToPlaylist()
        {
            InitializeIfNeeded();
            int count = GetDirectionCount();
            if (count <= 0 || _directionSWs == null) return;

            InitializeSortArrays();
            DirectionObject[] dirs = _sssCore != null ? _sssCore.Directions : null;

            if (_syncedSortMode == (int)DirectionSortMode.Manual)
            {
                for (int i = 0; i < count; i++)
                {
                    _currentSortedIndices[i] = _manualOrderIndices[i];
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    _currentSortedIndices[i] = i;
                }

                // Insertion Sort (UdonSharp対応・アロケーションフリー)
                for (int i = 1; i < count; i++)
                {
                    int keyIdx = _currentSortedIndices[i];
                    int j = i - 1;
                    while (j >= 0 && CompareDirections(dirs, _currentSortedIndices[j], keyIdx, _syncedSortMode) > 0)
                    {
                        _currentSortedIndices[j + 1] = _currentSortedIndices[j];
                        j--;
                    }
                    _currentSortedIndices[j + 1] = keyIdx;
                }
            }

            // UIボタンの親Transform (DirectionSW) のSiblingIndexを並び替えてリスト表示順を更新
            // ※Index 0 は非アクティブのテンプレート DirectionSW のため +1 から配置
            for (int order = 0; order < count; order++)
            {
                int dirIdx = _currentSortedIndices[order];
                if (dirIdx >= 0 && dirIdx < _directionSWs.Length && _directionSWs[dirIdx] != null)
                {
                    Transform itemTransform = _directionSWs[dirIdx].transform.parent;
                    if (itemTransform != null)
                    {
                        itemTransform.SetSiblingIndex(order + 1);
                    }
                }
            }
        }

        private int CompareDirections(DirectionObject[] dirs, int indexA, int indexB, int sortMode)
        {
            if (indexA == indexB) return 0;
            if (dirs == null || indexA < 0 || indexA >= dirs.Length || indexB < 0 || indexB >= dirs.Length)
            {
                return indexA.CompareTo(indexB);
            }

            DirectionObject a = dirs[indexA];
            DirectionObject b = dirs[indexB];
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            if (sortMode == (int)DirectionSortMode.ByNameAsc)
            {
                int cmp = string.CompareOrdinal(a.directionName, b.directionName);
                return cmp != 0 ? cmp : CompareTransformHierarchy(a.transform, b.transform);
            }
            if (sortMode == (int)DirectionSortMode.ByNameDesc)
            {
                int cmp = string.CompareOrdinal(b.directionName, a.directionName);
                return cmp != 0 ? cmp : CompareTransformHierarchy(b.transform, a.transform);
            }
            if (sortMode == (int)DirectionSortMode.ByGameObjectNameAsc)
            {
                int cmp = string.CompareOrdinal(a.gameObject.name, b.gameObject.name);
                return cmp != 0 ? cmp : CompareTransformHierarchy(a.transform, b.transform);
            }
            if (sortMode == (int)DirectionSortMode.ByGameObjectNameDesc)
            {
                int cmp = string.CompareOrdinal(b.gameObject.name, a.gameObject.name);
                return cmp != 0 ? cmp : CompareTransformHierarchy(b.transform, a.transform);
            }

            // Default: Hierarchy Order
            return CompareTransformHierarchy(a.transform, b.transform);
        }

        private int CompareTransformHierarchy(Transform a, Transform b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int depthA = GetTransformDepth(a);
            int depthB = GetTransformDepth(b);
            int minDepth = depthA < depthB ? depthA : depthB;

            for (int level = 0; level < minDepth; level++)
            {
                Transform ancA = GetAncestorAtDepth(a, depthA, level);
                Transform ancB = GetAncestorAtDepth(b, depthB, level);
                if (ancA != ancB)
                {
                    return ancA.GetSiblingIndex().CompareTo(ancB.GetSiblingIndex());
                }
            }

            return depthA.CompareTo(depthB);
        }

        private int GetTransformDepth(Transform t)
        {
            int depth = 0;
            Transform cur = t;
            while (cur != null)
            {
                depth++;
                cur = cur.parent;
            }
            return depth;
        }

        private Transform GetAncestorAtDepth(Transform t, int totalDepth, int targetLevel)
        {
            int stepsUp = totalDepth - 1 - targetLevel;
            Transform cur = t;
            for (int i = 0; i < stepsUp && cur != null; i++)
            {
                cur = cur.parent;
            }
            return cur;
        }

        private int GetDirectionCount()
        {
            if (_sssCore != null && _sssCore.Directions != null)
            {
                return _sssCore.Directions.Length;
            }
            if (_directionSWs != null)
            {
                return _directionSWs.Length;
            }
            return 0;
        }

        #endregion

        #region Error Detection (Force Launch) Control

        public void OnForceLaunchToggleChanged()
        {
            if (_isUpdatingUI) return;
            InitializeIfNeeded();

            if (IsSettingsLocked)
            {
                SyncUITogglesFromState();
                ApplyForceLaunchStateToCoreAndUI();
                return;
            }

            bool ignoreThreshold = _ignoreThresholdToggle != null && _ignoreThresholdToggle.isOn;
            bool ignorePerformer = _ignorePerformerErrorToggle != null && _ignorePerformerErrorToggle.isOn;

            if (_syncedIgnoreThreshold == ignoreThreshold && _syncedIgnorePerformerError == ignorePerformer) return;

            TakeOwnership();
            _syncedIgnoreThreshold = ignoreThreshold;
            _syncedIgnorePerformerError = ignorePerformer;

            ApplyForceLaunchStateToCoreAndUI();
            RequestSerialization();
        }

        public void OnForceLaunchDurationToggleChanged()
        {
            if (_isUpdatingUI) return;
            InitializeIfNeeded();

            if (IsSettingsLocked)
            {
                SyncUITogglesFromState();
                ApplyForceLaunchStateToCoreAndUI();
                return;
            }

            bool always = _durationAlwaysToggle != null && _durationAlwaysToggle.isOn;
            if (_durationOnceToggle != null && _durationOnceToggle.isOn)
            {
                always = false;
            }

            if (_syncedForceLaunchAlways == always) return;

            TakeOwnership();
            _syncedForceLaunchAlways = always;
            ApplyForceLaunchStateToCoreAndUI();
            RequestSerialization();
        }

        /// <summary>
        /// 次回上演の開始判定完了時に SSSCore から呼び出され、
        /// 「次の1回のみ」が選択されている場合は Force Launch スイッチを自動的に OFF に戻す。
        /// </summary>
        public void ConsumeForceLaunchIfOnce()
        {
            InitializeIfNeeded();
            if (!_syncedIgnoreThreshold && !_syncedIgnorePerformerError) return;
            if (_syncedForceLaunchAlways) return;

            TakeOwnership();
            _syncedIgnoreThreshold = false;
            _syncedIgnorePerformerError = false;
            _lockedIgnoreThreshold = false;
            _lockedIgnorePerformerError = false;

            ApplyForceLaunchStateToCoreAndUI();
            SyncUITogglesFromState();
            RequestSerialization();
        }

        private void ApplyForceLaunchStateToCoreAndUI()
        {
            if (_sssCore != null)
            {
                _sssCore.ApplyForceLaunchSettingsFromUI(_syncedIgnoreThreshold, _syncedIgnorePerformerError);
            }

            UpdateSlideSwitchVisual(_ignoreThresholdToggle, _syncedIgnoreThreshold);
            UpdateSlideSwitchVisual(_ignorePerformerErrorToggle, _syncedIgnorePerformerError);

            // 有効期間フォームの操作有効/無効は Toggle.interactable のみで制御（レイアウト崩れ防止のため SetActive は変更しない）
            RefreshSettingsInteractableState();
        }

        private void UpdateSlideSwitchVisual(Toggle toggle, bool isOn)
        {
            if (toggle == null) return;

            Transform slideBg = toggle.transform.Find("Toggle");
            if (slideBg == null) return;

            Image bgImage = slideBg.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.color = isOn
                    ? new Color(0.31764707f, 0.1882353f, 0.81960785f, 1f)
                    : new Color(0.8f, 0.8f, 0.8f, 1f);
            }

            Transform knob = slideBg.Find("Button");
            if (knob != null)
            {
                RectTransform knobRect = knob.GetComponent<RectTransform>();
                if (knobRect != null)
                {
                    Vector2 pos = knobRect.anchoredPosition;
                    pos.x = isOn ? 23.8f : -23.8f;
                    knobRect.anchoredPosition = pos;
                }

                Image knobImage = knob.GetComponent<Image>();
                if (knobImage != null)
                {
                    knobImage.color = isOn
                        ? new Color(1f, 1f, 1f, 1f)
                        : new Color(0.34117648f, 0.34117648f, 0.34117648f, 1f);
                }
            }
        }

        #endregion

        #region Delay Calculation Settings Control

        public void OnDelayCalculationToggleChanged()
        {
            if (_isUpdatingUI) return;
            InitializeIfNeeded();

            if (IsSettingsLocked)
            {
                SyncUITogglesFromState();
                RefreshSettingsInteractableState();
                return;
            }

            bool useFallback = _delayFallbackToggle != null && _delayFallbackToggle.isOn;
            if (_delayDefaultToggle != null && _delayDefaultToggle.isOn)
            {
                useFallback = false;
            }

            if (_syncedForceFallbackDelay == useFallback) return;

            TakeOwnership();
            _syncedForceFallbackDelay = useFallback;
            ApplyDelayCalculationSettingToCoreAndUI();
            RequestSerialization();
        }

        private void ApplyDelayCalculationSettingToCoreAndUI()
        {
            if (_sssCore != null)
            {
                _sssCore.UseFallbackDelay = _syncedForceFallbackDelay;
            }
            RefreshDelayStatusUI();
        }

        #endregion

        #region Internal UI Sync & Helpers

        private void ApplyAllToUIAndCore()
        {
            SyncUITogglesFromState();
            ApplyForceLaunchStateToCoreAndUI();
            ApplyDelayCalculationSettingToCoreAndUI();
            ApplySortOrderToPlaylist();
            RefreshStatusUI();
        }

        private void SyncUITogglesFromState()
        {
            _isUpdatingUI = true;

            // Sort Toggles (ToggleGroup(allowSwitchOff = false)対策として先にON側をtrueにしてからOFF側をfalseにする)
            if (_syncedSortMode == (int)DirectionSortMode.Hierarchy && _sortHierarchyToggle != null) _sortHierarchyToggle.isOn = true;
            else if (_syncedSortMode == (int)DirectionSortMode.ByNameAsc && _sortByNameToggle != null) _sortByNameToggle.isOn = true;
            else if (_syncedSortMode == (int)DirectionSortMode.Manual && _sortManualToggle != null) _sortManualToggle.isOn = true;
            else if (_syncedSortMode == (int)DirectionSortMode.ByNameDesc && _sortByNameDescToggle != null) _sortByNameDescToggle.isOn = true;
            else if (_syncedSortMode == (int)DirectionSortMode.ByGameObjectNameAsc && _sortByGameObjectNameAscToggle != null) _sortByGameObjectNameAscToggle.isOn = true;
            else if (_syncedSortMode == (int)DirectionSortMode.ByGameObjectNameDesc && _sortByGameObjectNameDescToggle != null) _sortByGameObjectNameDescToggle.isOn = true;

            if (_sortHierarchyToggle != null && _syncedSortMode != (int)DirectionSortMode.Hierarchy) _sortHierarchyToggle.isOn = false;
            if (_sortByNameToggle != null && _syncedSortMode != (int)DirectionSortMode.ByNameAsc) _sortByNameToggle.isOn = false;
            if (_sortManualToggle != null && _syncedSortMode != (int)DirectionSortMode.Manual) _sortManualToggle.isOn = false;
            if (_sortByNameDescToggle != null && _syncedSortMode != (int)DirectionSortMode.ByNameDesc) _sortByNameDescToggle.isOn = false;
            if (_sortByGameObjectNameAscToggle != null && _syncedSortMode != (int)DirectionSortMode.ByGameObjectNameAsc) _sortByGameObjectNameAscToggle.isOn = false;
            if (_sortByGameObjectNameDescToggle != null && _syncedSortMode != (int)DirectionSortMode.ByGameObjectNameDesc) _sortByGameObjectNameDescToggle.isOn = false;

            // Force Launch Toggles
            if (_ignoreThresholdToggle != null)
                _ignoreThresholdToggle.isOn = _syncedIgnoreThreshold;
            if (_ignorePerformerErrorToggle != null)
                _ignorePerformerErrorToggle.isOn = _syncedIgnorePerformerError;

            if (_syncedForceLaunchAlways)
            {
                if (_durationAlwaysToggle != null) _durationAlwaysToggle.isOn = true;
                if (_durationOnceToggle != null) _durationOnceToggle.isOn = false;
            }
            else
            {
                if (_durationOnceToggle != null) _durationOnceToggle.isOn = true;
                if (_durationAlwaysToggle != null) _durationAlwaysToggle.isOn = false;
            }

            // Delay Calculation Toggles
            if (_syncedForceFallbackDelay)
            {
                if (_delayFallbackToggle != null) _delayFallbackToggle.isOn = true;
                if (_delayDefaultToggle != null) _delayDefaultToggle.isOn = false;
            }
            else
            {
                if (_delayDefaultToggle != null) _delayDefaultToggle.isOn = true;
                if (_delayFallbackToggle != null) _delayFallbackToggle.isOn = false;
            }

            _isUpdatingUI = false;
        }

        private void TakeOwnership()
        {
            if (Utilities.IsValid(Networking.LocalPlayer) && !Networking.IsOwner(Networking.LocalPlayer, gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }
        }

        private Color ParseHexColor(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;

            string clean = hex.Trim();
            if (clean.StartsWith("#"))
            {
                clean = clean.Substring(1);
            }

            if (clean.Length != 6 && clean.Length != 8) return fallback;

            int r = ParseHexByte(clean, 0);
            int g = ParseHexByte(clean, 2);
            int b = ParseHexByte(clean, 4);
            int a = clean.Length == 8 ? ParseHexByte(clean, 6) : 255;

            if (r < 0 || g < 0 || b < 0 || a < 0) return fallback;

            return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }

        private int ParseHexByte(string str, int startIndex)
        {
            int hi = HexCharToValue(str[startIndex]);
            int lo = HexCharToValue(str[startIndex + 1]);
            if (hi < 0 || lo < 0) return -1;
            return (hi << 4) | lo;
        }

        private int HexCharToValue(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        #endregion
    }
}
