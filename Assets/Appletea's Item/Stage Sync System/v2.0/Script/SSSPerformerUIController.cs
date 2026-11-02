using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using UdonSharp;

namespace AppleteaSystems.StageSyncSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VRC_Pickup))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SSSPerformerUIController : UdonSharpBehaviour
    {
        [Header("Core & System References")]
        [SerializeField]
        [Tooltip("連携先のSSSCore")]
        private SSSCore _sssCore;

        [SerializeField]
        [Tooltip("連携先のメインUIコントローラ (SSSUIController)")]
        private SSSUIController _mainUIController;

        [SerializeField]
        [Tooltip("自動帰還トリガー子機 (SSSPerformerTrigger)")]
        private SSSPerformerTrigger _performerTrigger;

        [SerializeField]
        [Tooltip("近接検知チェッカー (SSSPerformerProximityChecker)")]
        private SSSPerformerProximityChecker _proximityChecker;

        [SerializeField]
        [Tooltip("親機自身のVRC_Pickup")]
        private VRC_Pickup _parentPickup;

        [SerializeField]
        [Tooltip("親機自身のPickup用Collider")]
        private Collider _parentPickupCollider;

        [SerializeField]
        [Tooltip("親機自身のRigidbody")]
        private Rigidbody _parentRigidbody;

        [Header("Canvas & State Panels")]
        [SerializeField]
        [Tooltip("親機UIのルートCanvas")]
        private Canvas _targetCanvas;

        [SerializeField]
        [Tooltip("1. Idle (待機中) 表示パネル")]
        private GameObject _standbyPanel;

        [SerializeField]
        [Tooltip("2. WaitingForReady (ロード中) 表示パネル")]
        private GameObject _loadingPanel;

        [SerializeField]
        [Tooltip("3. StagePlaying (上演中) 表示パネル")]
        private GameObject _playingPanel;

        [Header("Standby Panel UI")]
        [SerializeField]
        [Tooltip("演出予約選択用UIボタン配列 (Editor拡張により自動設定)")]
        private Button[] _directionSWs;

        [SerializeField]
        [Tooltip("現在予約中の演出名を表示するText (Next Cue)")]
        private Text _nextCueText;

        [SerializeField]
        [Tooltip("全員読み込めていない（閾値未達）場合の上演停止を無効化するToggle（メインUI連動）")]
        private Toggle _ignoreThresholdToggle;

        [SerializeField]
        [Tooltip("演者が読み込めていない場合の上演停止を無効化するToggle（メインUI連動）")]
        private Toggle _ignorePerformerErrorToggle;

        [SerializeField]
        [Tooltip("親機の配置固定（Position Lock）切り替えToggle")]
        private Toggle _positionLockToggle;

        [SerializeField]
        [Tooltip("親機ロック中に表示または色変更するインジケータ（任意）")]
        private GameObject _lockVisualIndicator;

        [Header("Loading Panel UI")]
        [SerializeField]
        [Tooltip("ロード中の演出名を表示するText")]
        private Text _loadingDirectionText;

        [SerializeField]
        [Tooltip("ロード進捗 (Loaded: X/Y) を表示するText")]
        private Text _loadingProgressText;

        [SerializeField]
        [Tooltip("壇上からの即時強制スタート (Force Start Now) ボタン")]
        private Button _forceStartNowButton;

        [SerializeField]
        [Tooltip("ロード待機キャンセル (Cancel / Abort) ボタン")]
        private Button _cancelLoadingButton;

        [Header("Playing Panel UI")]
        [SerializeField]
        [Tooltip("現在上演中の演出名を表示するText")]
        private Text _playingDirectionText;

        [SerializeField]
        [Tooltip("緊急停止ボタンの2ステップ安全ロック解除Toggle")]
        private Toggle _unlockEmergencyStopToggle;

        [SerializeField]
        [Tooltip("緊急停止実行 (EMERGENCY STOP) ボタン")]
        private Button _emergencyStopButton;

        // Local state (完全イベント駆動・UdonSynced変数ゼロ)
        private int _standbyObjectIndex = 0;
        private bool _hasUserSelectedStandby = false;
        private bool _isPositionLocked = false;
        private bool _isPlayerNear = false;
        private bool _isStopSafetyUnlocked = false;
        private bool _isUpdatingUI = false;
        private bool _initialized = false;
        private Collider[] _canvasColliders;

        private readonly Color _selectedButtonColor = new Color(0.28f, 0.22f, 0.78f, 1f);
        private readonly Color _normalButtonColor = new Color(0.18f, 0.20f, 0.26f, 1f);
        private readonly Color _selectedTextColor = new Color(1f, 1f, 1f, 1f);
        private readonly Color _normalTextColor = new Color(0.90f, 0.92f, 0.96f, 1f);

        #region Accessors

        public SSSCore SSSCore
        {
            get => _sssCore;
            set => _sssCore = value;
        }

        public SSSUIController MainUIController
        {
            get => _mainUIController;
            set => _mainUIController = value;
        }

        public Button[] DirectionSWs
        {
            get => _directionSWs;
            set => _directionSWs = value;
        }

        public int StandbyObjectIndex => _standbyObjectIndex;
        public bool IsPositionLocked => _isPositionLocked;
        public bool IsPlayerNear => _isPlayerNear;

        #endregion

        void Start()
        {
            InitializeIfNeeded();
            SyncWithMainUI();
            RefreshPlaylistVisuals();
            RefreshViewState();
            SendCustomEventDelayedFrames(nameof(RefreshViewState), 1);
        }

        private void InitializeIfNeeded()
        {
            if (_initialized) return;
            _initialized = true;

            if (_sssCore == null && transform.parent != null)
            {
                _sssCore = transform.parent.GetComponentInChildren<SSSCore>(true);
            }
            if (_sssCore != null && _sssCore.PerformerUIController == null)
            {
                _sssCore.PerformerUIController = this;
            }

            if (_mainUIController == null && transform.parent != null)
            {
                _mainUIController = transform.parent.GetComponentInChildren<SSSUIController>(true);
            }
            if (_mainUIController != null && _mainUIController.PerformerUIController == null)
            {
                _mainUIController.PerformerUIController = this;
            }

            if (_performerTrigger == null)
            {
                _performerTrigger = GetComponentInChildren<SSSPerformerTrigger>(true);
            }
            if (_performerTrigger != null)
            {
                _performerTrigger.PerformerUIController = this;
            }

            if (_proximityChecker == null)
            {
                _proximityChecker = GetComponentInChildren<SSSPerformerProximityChecker>(true);
            }
            if (_proximityChecker != null)
            {
                _proximityChecker.PerformerUIController = this;
            }

            if (_parentPickup == null)
            {
                _parentPickup = (VRC_Pickup)GetComponent(typeof(VRC_Pickup));
            }
            if (_parentPickupCollider == null)
            {
                _parentPickupCollider = GetComponent<Collider>();
            }
            if (_parentRigidbody == null)
            {
                _parentRigidbody = GetComponent<Rigidbody>();
            }
            if (_parentRigidbody != null)
            {
                _parentRigidbody.isKinematic = true;
                _parentRigidbody.useGravity = false;
            }

            if (_targetCanvas == null)
            {
                _targetCanvas = GetComponentInChildren<Canvas>(true);
            }
            if (_targetCanvas != null)
            {
                _canvasColliders = _targetCanvas.GetComponentsInChildren<Collider>(true);
            }

            if (_ignoreThresholdToggle != null && _ignoreThresholdToggle.group != null)
            {
                _ignoreThresholdToggle.group = null;
            }
            if (_ignorePerformerErrorToggle != null && _ignorePerformerErrorToggle.group != null)
            {
                _ignorePerformerErrorToggle.group = null;
            }
            if (_positionLockToggle != null && _positionLockToggle.group != null)
            {
                _positionLockToggle.group = null;
            }
            if (_unlockEmergencyStopToggle != null && _unlockEmergencyStopToggle.group != null)
            {
                _unlockEmergencyStopToggle.group = null;
            }

            EnsureValidStandbyIndex();
        }

        public override void OnDrop()
        {
            if (_parentRigidbody != null)
            {
                _parentRigidbody.isKinematic = true;
                _parentRigidbody.useGravity = false;
            }
        }

        #region Proximity & Performer Role State Control

        public void SetPlayerProximity(bool isNear)
        {
            InitializeIfNeeded();
            if (_isPlayerNear == isNear)
            {
                RefreshViewState();
                return;
            }

            _isPlayerNear = isNear;
            if (!isNear)
            {
                // 演者が親機から離れた際は2ステップ緊急停止のロックを自動的にリセットする
                ResetEmergencyStopSafety();
            }

            RefreshViewState();
        }

        public void OnPerformerRoleChanged(bool isPerformer)
        {
            InitializeIfNeeded();

            if (!isPerformer)
            {
                // 演者エリアから退出して観客に戻った瞬間、親機・子機ともに強制Dropし子機はホルスターへ自動帰還させる
                if (_parentPickup != null && _parentPickup.IsHeld)
                {
                    _parentPickup.Drop();
                }
                if (_performerTrigger != null)
                {
                    _performerTrigger.SetPerformerPickupable(false);
                }
                ResetEmergencyStopSafety();
            }

            RefreshViewState();
        }

        public void RefreshViewState()
        {
            InitializeIfNeeded();

            bool isPerformer = _sssCore != null && _sssCore.IsPerformer;
            bool isWaiting = _sssCore != null && _sssCore.IsWaitingForReady;
            bool isPlaying = _sssCore != null && _sssCore.IsStagePlaying && !isWaiting;
            bool isIdle = !isWaiting && !isPlaying;

            // 1. 親機 Pickup の有効/無効制御（演者 かつ PositionLock解除時のみ把持可能）
            bool canPickupParent = isPerformer && !_isPositionLocked;
            if (!canPickupParent && _parentPickup != null && _parentPickup.IsHeld)
            {
                _parentPickup.Drop();
            }
            if (_parentPickup != null)
            {
                _parentPickup.pickupable = canPickupParent;
            }
            if (_parentPickupCollider != null)
            {
                _parentPickupCollider.enabled = canPickupParent;
            }
            if (_lockVisualIndicator != null && _lockVisualIndicator.activeSelf != _isPositionLocked)
            {
                _lockVisualIndicator.SetActive(_isPositionLocked);
            }

            // 2. 子機 (PerformerTrigger) の有効/無効制御
            if (_performerTrigger != null)
            {
                if (!isPerformer)
                {
                    _performerTrigger.SetPerformerPickupable(false);
                }
                else
                {
                    _performerTrigger.SetPerformerPickupable(true);
                    if (!isIdle)
                    {
                        // ロード開始時・上演開始時はホルスターへ帰還済みとする
                        _performerTrigger.ReturnToHolster();
                    }
                }
            }

            // 3. 親機 Canvas と状態別パネルの表示・Collider制御
            bool showCanvas = isPerformer && _isPlayerNear;
            if (!showCanvas)
            {
                ResetEmergencyStopSafety();
            }

            SetCanvasCollidersEnabled(showCanvas);

            if (_targetCanvas != null)
            {
                if (_targetCanvas.gameObject.activeSelf != showCanvas)
                {
                    _targetCanvas.gameObject.SetActive(showCanvas);
                }
            }

            if (showCanvas)
            {
                if (_standbyPanel != null && _standbyPanel.activeSelf != isIdle)
                {
                    _standbyPanel.SetActive(isIdle);
                }
                if (_loadingPanel != null && _loadingPanel.activeSelf != isWaiting)
                {
                    _loadingPanel.SetActive(isWaiting);
                }
                if (_playingPanel != null && _playingPanel.activeSelf != isPlaying)
                {
                    _playingPanel.SetActive(isPlaying);
                }

                if (isPlaying)
                {
                    SyncEmergencyStopUI();
                }
            }
        }

        private void SetCanvasCollidersEnabled(bool enabledState)
        {
            if (_targetCanvas == null) return;
            if (_canvasColliders == null)
            {
                _canvasColliders = _targetCanvas.GetComponentsInChildren<Collider>(true);
            }

            Collider proxCol = _proximityChecker != null ? _proximityChecker.GetComponent<Collider>() : null;
            for (int i = 0; i < _canvasColliders.Length; i++)
            {
                Collider col = _canvasColliders[i];
                if (col == null || col == _parentPickupCollider || col == proxCol) continue;
                col.enabled = enabledState;
            }
        }

        #endregion

        #region Smart Queue (Standby Selection) & Conditional Auto-Advance

        public void OnButtonClicked()
        {
            InitializeIfNeeded();
            int sender = FindSender();
            if (sender < 0) return;

            OnSelectStandbyDirection(sender);
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

        public void OnSelectStandbyDirection(int objectIndex)
        {
            InitializeIfNeeded();
            int dirCount = GetDirectionCount();
            if (objectIndex < 0 || objectIndex >= dirCount) return;

            _standbyObjectIndex = objectIndex;
            _hasUserSelectedStandby = true;
            RefreshPlaylistVisuals();
        }

        public bool TryFireStandbyDirection()
        {
            InitializeIfNeeded();
            if (_sssCore == null) return false;
            if (!_sssCore.IsPerformer) return false;
            if (_sssCore.IsWaitingForReady || _sssCore.IsStagePlaying) return false;

            EnsureValidStandbyIndex();
            int dirCount = GetDirectionCount();
            if (_standbyObjectIndex < 0 || _standbyObjectIndex >= dirCount) return false;

            // メインUIのForce Launch状態をそのまま反映してSSSCore Ownerへ開始リクエストを送信
            bool ignoreThreshold = _mainUIController != null && _mainUIController.IgnoreThresholdError;
            bool ignorePerformer = _mainUIController != null && _mainUIController.IgnorePerformerError;

            _sssCore.SendCustomNetworkEvent(
                NetworkEventTarget.Owner,
                nameof(SSSCore.NetRequestStartFromPerformer),
                _standbyObjectIndex,
                ignoreThreshold,
                ignorePerformer
            );
            return true;
        }

        private void EnsureValidStandbyIndex()
        {
            int dirCount = GetDirectionCount();
            if (dirCount <= 0)
            {
                _standbyObjectIndex = 0;
                return;
            }
            if (_standbyObjectIndex < 0 || _standbyObjectIndex >= dirCount)
            {
                _standbyObjectIndex = GetFirstVisualDirectionIndex();
            }
        }

        private int GetFirstVisualDirectionIndex()
        {
            if (_mainUIController != null)
            {
                int firstFromMain = _mainUIController.GetFirstSortedDirectionIndex();
                if (firstFromMain >= 0) return firstFromMain;
            }

            if (_directionSWs != null)
            {
                int bestIndex = 0;
                int bestSibling = int.MaxValue;
                for (int i = 0; i < _directionSWs.Length; i++)
                {
                    if (_directionSWs[i] == null || _directionSWs[i].transform.parent == null) continue;
                    int sib = _directionSWs[i].transform.parent.GetSiblingIndex();
                    if (sib < bestSibling)
                    {
                        bestSibling = sib;
                        bestIndex = i;
                    }
                }
                return bestIndex;
            }

            return 0;
        }

        private int GetNextVisualDirectionIndex(int currentIndex)
        {
            if (_mainUIController != null)
            {
                int nextFromMain = _mainUIController.GetNextSortedDirectionIndex(currentIndex);
                if (nextFromMain >= 0) return nextFromMain;
            }

            if (_directionSWs == null || _directionSWs.Length <= 0) return 0;
            if (currentIndex < 0 || currentIndex >= _directionSWs.Length || _directionSWs[currentIndex] == null)
            {
                return GetFirstVisualDirectionIndex();
            }

            Transform currentItem = _directionSWs[currentIndex].transform.parent;
            int currentSibling = currentItem != null ? currentItem.GetSiblingIndex() : -1;

            int nextIndex = -1;
            int minHigherSibling = int.MaxValue;
            int firstIndex = 0;
            int minOverallSibling = int.MaxValue;

            for (int i = 0; i < _directionSWs.Length; i++)
            {
                if (_directionSWs[i] == null || _directionSWs[i].transform.parent == null) continue;
                int sib = _directionSWs[i].transform.parent.GetSiblingIndex();
                if (sib < minOverallSibling)
                {
                    minOverallSibling = sib;
                    firstIndex = i;
                }
                if (sib > currentSibling && sib < minHigherSibling)
                {
                    minHigherSibling = sib;
                    nextIndex = i;
                }
            }

            return nextIndex >= 0 ? nextIndex : firstIndex;
        }

        public void RefreshPlaylistVisuals()
        {
            InitializeIfNeeded();
            EnsureValidStandbyIndex();

            string standbyName = GetDirectionName(_standbyObjectIndex);
            if (_nextCueText != null)
            {
                _nextCueText.text = $"Next Cue: {standbyName}";
            }

            if (_directionSWs != null)
            {
                for (int i = 0; i < _directionSWs.Length; i++)
                {
                    Button btn = _directionSWs[i];
                    if (btn == null) continue;

                    bool isSelected = (i == _standbyObjectIndex);
                    Image btnImg = btn.GetComponent<Image>();
                    if (btnImg != null)
                    {
                        btnImg.color = isSelected ? _selectedButtonColor : _normalButtonColor;
                    }

                    Text btnText = btn.GetComponentInChildren<Text>(true);
                    if (btnText != null)
                    {
                        btnText.color = isSelected ? _selectedTextColor : _normalTextColor;
                    }
                }
            }
        }

        #endregion

        #region Lifecycle Notifications from SSSCore & SSSUIController

        public void OnStageLoadingBegan(int objectIndex, int sessionId)
        {
            InitializeIfNeeded();
            ResetEmergencyStopSafety();

            if (_loadingDirectionText != null)
            {
                _loadingDirectionText.text = $"Loading: {GetDirectionName(objectIndex)}";
            }

            if (_performerTrigger != null)
            {
                _performerTrigger.ReturnToHolster();
            }

            RefreshViewState();
        }

        public void OnLoadProgressUpdated(int readyCount, int totalCount)
        {
            InitializeIfNeeded();
            if (_loadingProgressText != null)
            {
                if (totalCount <= 0)
                {
                    _loadingProgressText.text = "Loaded: 0/0";
                }
                else
                {
                    _loadingProgressText.text = $"Loaded: {readyCount}/{totalCount}";
                }
            }
        }

        public void OnStagePlaybackStarted(int objectIndex, int sessionId)
        {
            InitializeIfNeeded();
            ResetEmergencyStopSafety();

            if (_playingDirectionText != null)
            {
                _playingDirectionText.text = $"Playing: {GetDirectionName(objectIndex)}";
            }

            if (_performerTrigger != null)
            {
                _performerTrigger.ReturnToHolster();
            }

            RefreshViewState();
        }

        public void OnStagePlaybackAborted(int sessionId)
        {
            InitializeIfNeeded();
            // エラー中止・手動停止時は _standbyObjectIndex を変更せず同じ演出を維持する（即時リトライ用）
            ResetEmergencyStopSafety();
            RefreshPlaylistVisuals();
            RefreshViewState();
        }

        public void OnStagePlaybackCompleted(int completedObjectIndex)
        {
            InitializeIfNeeded();
            ResetEmergencyStopSafety();

            // 最後まで正常に再生完了した場合のみ、現在のソート順（SiblingIndex順）における次の演出へ自動予約を進める
            if (completedObjectIndex == _standbyObjectIndex)
            {
                _standbyObjectIndex = GetNextVisualDirectionIndex(completedObjectIndex);
            }

            RefreshPlaylistVisuals();
            RefreshViewState();
        }

        public void ApplySortOrderFromMainUI(int[] sortedIndices, int count)
        {
            InitializeIfNeeded();
            if (sortedIndices == null || count <= 0 || _directionSWs == null) return;

            for (int order = 0; order < count; order++)
            {
                int dirIdx = sortedIndices[order];
                if (dirIdx >= 0 && dirIdx < _directionSWs.Length && _directionSWs[dirIdx] != null)
                {
                    Transform itemTransform = _directionSWs[dirIdx].transform.parent;
                    if (itemTransform != null)
                    {
                        itemTransform.SetSiblingIndex(order + 1);
                    }
                }
            }

            // ユーザーがまだ手動選択していない初期状態では、ソート後の先頭曲を予約対象にセットする
            if (!_hasUserSelectedStandby && count > 0)
            {
                _standbyObjectIndex = sortedIndices[0];
            }

            RefreshPlaylistVisuals();
        }

        public void SyncForceLaunchFromMainUI(bool ignoreThreshold, bool ignorePerformerError, bool isLocked)
        {
            InitializeIfNeeded();
            _isUpdatingUI = true;

            if (_ignoreThresholdToggle != null)
            {
                _ignoreThresholdToggle.isOn = ignoreThreshold;
                _ignoreThresholdToggle.interactable = !isLocked;
                UpdateSlideSwitchVisual(_ignoreThresholdToggle, ignoreThreshold);
            }

            if (_ignorePerformerErrorToggle != null)
            {
                _ignorePerformerErrorToggle.isOn = ignorePerformerError;
                _ignorePerformerErrorToggle.interactable = !isLocked;
                UpdateSlideSwitchVisual(_ignorePerformerErrorToggle, ignorePerformerError);
            }

            _isUpdatingUI = false;
        }

        public void SyncWithMainUI()
        {
            if (_mainUIController == null) return;
            SyncForceLaunchFromMainUI(
                _mainUIController.IgnoreThresholdError,
                _mainUIController.IgnorePerformerError,
                _mainUIController.IsSettingsLocked
            );
            OnLoadProgressUpdated(_mainUIController.SyncedReadyCount, _mainUIController.SyncedTotalCount);
        }

        #endregion

        #region Standby Panel Controls (Force Launch & Position Lock)

        public void OnForceLaunchToggleChanged()
        {
            if (_isUpdatingUI) return;
            InitializeIfNeeded();

            if (_mainUIController == null) return;
            if (_mainUIController.IsSettingsLocked)
            {
                SyncWithMainUI();
                return;
            }

            bool ignoreThreshold = _ignoreThresholdToggle != null && _ignoreThresholdToggle.isOn;
            bool ignorePerformer = _ignorePerformerErrorToggle != null && _ignorePerformerErrorToggle.isOn;

            UpdateSlideSwitchVisual(_ignoreThresholdToggle, ignoreThreshold);
            UpdateSlideSwitchVisual(_ignorePerformerErrorToggle, ignorePerformer);

            // メインUIへ操作指示を送信し、メインUIを直接操作した場合と完全に同等の処理を実行する
            _mainUIController.RequestSetForceLaunchFromPerformer(ignoreThreshold, ignorePerformer);
        }

        public void OnPositionLockToggleChanged()
        {
            if (_isUpdatingUI) return;
            InitializeIfNeeded();

            _isPositionLocked = _positionLockToggle != null && _positionLockToggle.isOn;
            UpdateSlideSwitchVisual(_positionLockToggle, _isPositionLocked);
            RefreshViewState();
        }

        #endregion

        #region Loading & Playing Panel Controls (Force Start Now & 2-Step Emergency Stop)

        public void OnForceStartNowClicked()
        {
            InitializeIfNeeded();
            if (_sssCore == null || !_sssCore.IsPerformer || !_sssCore.IsWaitingForReady) return;

            _sssCore.SendCustomNetworkEvent(
                NetworkEventTarget.Owner,
                nameof(SSSCore.NetRequestForceLaunchNow),
                _sssCore.PlaybackSessionId
            );
        }

        public void OnCancelLoadingClicked()
        {
            InitializeIfNeeded();
            if (_sssCore == null || !_sssCore.IsPerformer || !_sssCore.IsWaitingForReady) return;

            _sssCore.SendCustomNetworkEvent(
                NetworkEventTarget.Owner,
                nameof(SSSCore.NetRequestStopFromPerformer),
                _sssCore.PlaybackSessionId
            );
        }

        public void OnUnlockEmergencyStopToggleChanged()
        {
            if (_isUpdatingUI) return;
            InitializeIfNeeded();

            _isStopSafetyUnlocked = _unlockEmergencyStopToggle != null && _unlockEmergencyStopToggle.isOn;
            SyncEmergencyStopUI();
        }

        public void OnEmergencyStopClicked()
        {
            InitializeIfNeeded();
            if (_sssCore == null || !_sssCore.IsPerformer) return;
            if (!_isStopSafetyUnlocked) return;
            if (!_sssCore.IsStagePlaying && !_sssCore.IsWaitingForReady) return;

            ResetEmergencyStopSafety();
            _sssCore.SendCustomNetworkEvent(
                NetworkEventTarget.Owner,
                nameof(SSSCore.NetRequestStopFromPerformer),
                _sssCore.PlaybackSessionId
            );
        }

        private void ResetEmergencyStopSafety()
        {
            _isStopSafetyUnlocked = false;
            SyncEmergencyStopUI();
        }

        private void SyncEmergencyStopUI()
        {
            _isUpdatingUI = true;
            if (_unlockEmergencyStopToggle != null)
            {
                _unlockEmergencyStopToggle.isOn = _isStopSafetyUnlocked;
                UpdateSlideSwitchVisual(_unlockEmergencyStopToggle, _isStopSafetyUnlocked);
            }
            if (_emergencyStopButton != null)
            {
                _emergencyStopButton.interactable = _isStopSafetyUnlocked;
            }
            _isUpdatingUI = false;
        }

        #endregion

        #region Helpers

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

        private string GetDirectionName(int objectIndex)
        {
            if (_sssCore != null && _sssCore.Directions != null &&
                objectIndex >= 0 && objectIndex < _sssCore.Directions.Length)
            {
                DirectionObject dir = _sssCore.Directions[objectIndex];
                if (dir != null && !string.IsNullOrEmpty(dir.directionName))
                {
                    return dir.directionName;
                }
            }
            return $"Direction #{objectIndex + 1}";
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
    }
}
