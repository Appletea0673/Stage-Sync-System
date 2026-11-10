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

        [Header("Canvas, State Panels & Position Lock UI")]
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

        [SerializeField]
        [Tooltip("親機の配置固定（Pickup Lock）切り替えToggle (Canvas/UI Lock Toggle)")]
        private Toggle _positionLockToggle;

        [SerializeField]
        [Tooltip("Position Lock解除時に表示するUnlockアイコン (Canvas/UI Lock Toggle/Toggle/Button/Unlock)")]
        private GameObject _positionUnlockIcon;

        [SerializeField]
        [Tooltip("Position Lock有効時に表示するLockアイコン (Canvas/UI Lock Toggle/Toggle/Button/Lock)")]
        private GameObject _positionLockIcon;

        [SerializeField]
        [Tooltip("親機の把持バーオブジェクト (Pickup Bar)")]
        private GameObject _pickupBarObject;

        [SerializeField]
        [Tooltip("子機のホルスターリング親オブジェクト (TriggerHolster / Pedestal)")]
        private GameObject _triggerHolsterObject;

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

        [Header("Loading Panel UI")]
        [SerializeField]
        [Tooltip("ロード画面：[読み込めた人数]/[全体の人数] を表示するText")]
        private Text _loadingLoadedPlayerCountText;

        [SerializeField]
        [Tooltip("ロード画面：演者の読み込み状態を表示するText")]
        private Text _loadingPerformerStatusText;

        [SerializeField]
        [Tooltip("ロード画面：演者が読み込めた場合に有効化するオブジェクト（Readyランプ）")]
        private GameObject _loadingPerformerReadyObject;

        [SerializeField]
        [Tooltip("ロード画面：演者が読み込み失敗した場合に有効化するオブジェクト（Warningランプ）")]
        private GameObject _loadingPerformerWarningObject;

        [SerializeField]
        [Tooltip("ロード画面：遅延計算のステータスを表示するText")]
        private Text _loadingDelayStatusText;

        [SerializeField]
        [Tooltip("ロード中の演出名を表示するText (Loading Status/LoadingDirectionText)")]
        private Text _loadingDirectionText;

        [SerializeField]
        [Tooltip("ロード完了割合（%）を表示するText (Loading Status/Percentage)")]
        private Text _loadingPercentageText;

        [SerializeField]
        [Tooltip("ロード進捗プログレスバー (Loading Bar)")]
        private Slider _loadingProgressBar;

        [SerializeField]
        [Tooltip("旧ロード進捗テキスト（互換用・任意）")]
        private Text _loadingProgressText;

        [SerializeField]
        [Tooltip("壇上からの即時強制スタート (Force Start Now) ボタン")]
        private Button _forceStartNowButton;

        [SerializeField]
        [Tooltip("ロード待機キャンセル (Cancel / Abort) ボタン")]
        private Button _cancelLoadingButton;

        [Header("Playing Panel UI")]
        [SerializeField]
        [Tooltip("再生画面：[読み込めた人数]/[全体の人数] を表示するText")]
        private Text _playingLoadedPlayerCountText;

        [SerializeField]
        [Tooltip("再生画面：演者の読み込み状態を表示するText")]
        private Text _playingPerformerStatusText;

        [SerializeField]
        [Tooltip("再生画面：演者が読み込めた場合に有効化するオブジェクト（Readyランプ）")]
        private GameObject _playingPerformerReadyObject;

        [SerializeField]
        [Tooltip("再生画面：演者が読み込み失敗した場合に有効化するオブジェクト（Warningランプ）")]
        private GameObject _playingPerformerWarningObject;

        [SerializeField]
        [Tooltip("再生画面：遅延計算のステータスを表示するText")]
        private Text _playingDelayStatusText;

        [SerializeField]
        [Tooltip("現在上演中の演出名を表示するText (Video Player Status/PlayingDirectionText)")]
        private Text _playingDirectionText;

        [SerializeField]
        [Tooltip("動画の再生時間 (MM:SS/MM:SS) を表示するText (Video Player Status/Video Time)")]
        private Text _playingVideoTimeText;

        [SerializeField]
        [Tooltip("動画再生プログレスバー (PlayingPanel/Loading Bar)")]
        private Slider _playingProgressBar;

        [SerializeField]
        [Tooltip("緊急停止ボタンの2ステップ安全ロック解除Toggle")]
        private Toggle _unlockEmergencyStopToggle;

        [SerializeField]
        [Tooltip("緊急停止実行 (EMERGENCY STOP) ボタン")]
        private Button _emergencyStopButton;

        // Local state (完全イベント駆動・UdonSynced変数ゼロ・Updateメソッドゼロ)
        private const float _PLAYBACK_PROGRESS_UPDATE_INTERVAL = 0.2f;
        private int _standbyObjectIndex = 0;
        private bool _hasUserSelectedStandby = false;
        private bool _isPositionLocked = false;
        private bool _isPlayerNear = false;
        private bool _isStopSafetyUnlocked = false;
        private bool _isPlaybackProgressLoopScheduled = false;
        private bool _isUpdatingUI = false;
        private bool _initialized = false;
        private Collider[] _canvasColliders;

        // 待機選択中（Next Cue）はテンプレートの鮮やかな紫色(#5130D1)、非選択ボタンは暗めのグレー
        private readonly Color _selectedButtonColor = new Color(0.31805217f, 0.18970275f, 0.8207547f, 1f);
        private readonly Color _normalButtonColor = new Color(0.24f, 0.24f, 0.28f, 1f);
        private readonly Color _selectedTextColor = new Color(1f, 1f, 1f, 1f);
        private readonly Color _normalTextColor = new Color(0.85f, 0.85f, 0.88f, 1f);

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
            SyncPositionLockUI();
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

            AutoResolveHierarchyReferences();

            if (_targetCanvas != null)
            {
                EnsureCanvasCollidersConfigured();
            }

            if (_ignoreThresholdToggle != null && _ignoreThresholdToggle.group != null)
            {
                _ignoreThresholdToggle.group = null;
            }
            if (_ignorePerformerErrorToggle != null && _ignorePerformerErrorToggle.group != null)
            {
                _ignorePerformerErrorToggle.group = null;
            }
            if (_positionLockToggle != null)
            {
                if (_positionLockToggle.group != null)
                {
                    _positionLockToggle.group = null;
                }
                _isPositionLocked = _positionLockToggle.isOn;
            }
            if (_unlockEmergencyStopToggle != null && _unlockEmergencyStopToggle.group != null)
            {
                _unlockEmergencyStopToggle.group = null;
            }

            if (_loadingProgressBar != null)
            {
                _loadingProgressBar.interactable = false;
            }
            if (_playingProgressBar != null)
            {
                _playingProgressBar.interactable = false;
            }

            EnsureValidStandbyIndex();
        }

        private void AutoResolveHierarchyReferences()
        {
            if (_pickupBarObject == null)
            {
                Transform pb = transform.Find("Pickup Bar");
                if (pb != null) _pickupBarObject = pb.gameObject;
            }
            if (_triggerHolsterObject == null)
            {
                Transform th = transform.Find("TriggerHolster");
                if (th != null) _triggerHolsterObject = th.gameObject;
            }

            Transform canvasTrans = _targetCanvas != null ? _targetCanvas.transform : transform.Find("Canvas");
            if (canvasTrans == null) return;

            Transform panelRoot = canvasTrans.Find("Panel");
            if (panelRoot == null) panelRoot = canvasTrans;

            if (_standbyPanel == null)
            {
                Transform t = panelRoot.Find("StandbyPanel");
                if (t != null) _standbyPanel = t.gameObject;
            }
            if (_loadingPanel == null)
            {
                Transform t = panelRoot.Find("LoadingPanel");
                if (t != null) _loadingPanel = t.gameObject;
            }
            if (_playingPanel == null)
            {
                Transform t = panelRoot.Find("PlayingPanel");
                if (t != null) _playingPanel = t.gameObject;
            }

            if (_positionLockToggle == null)
            {
                Transform t = canvasTrans.Find("UI Lock Toggle");
                if (t == null) t = canvasTrans.Find("Toggle");
                if (t != null) _positionLockToggle = t.GetComponent<Toggle>();
            }
            if (_positionLockToggle != null)
            {
                if (_positionUnlockIcon == null)
                {
                    Transform u = _positionLockToggle.transform.Find("Toggle/Button/Unlock");
                    if (u != null) _positionUnlockIcon = u.gameObject;
                }
                if (_positionLockIcon == null)
                {
                    Transform l = _positionLockToggle.transform.Find("Toggle/Button/Lock");
                    if (l != null) _positionLockIcon = l.gameObject;
                }
            }

            if (_standbyPanel != null)
            {
                Transform sb = _standbyPanel.transform;
                if (_nextCueText == null)
                {
                    Transform t = sb.Find("NextCueBanner/NextCueText");
                    if (t != null) _nextCueText = t.GetComponent<Text>();
                }
                Transform bottom = sb.Find("BottomControls");
                if (bottom != null)
                {
                    if (_ignorePerformerErrorToggle == null && bottom.childCount > 0)
                    {
                        _ignorePerformerErrorToggle = bottom.GetChild(0).GetComponent<Toggle>();
                    }
                    if (_ignoreThresholdToggle == null && bottom.childCount > 1)
                    {
                        _ignoreThresholdToggle = bottom.GetChild(1).GetComponent<Toggle>();
                    }
                }
            }

            if (_loadingPanel != null)
            {
                Transform lp = _loadingPanel.transform;
                if (_loadingLoadedPlayerCountText == null)
                {
                    Transform t = lp.Find("Status/Loaded Player/Loaded Player num");
                    if (t != null) _loadingLoadedPlayerCountText = t.GetComponent<Text>();
                }
                if (_loadingPerformerStatusText == null)
                {
                    Transform t = lp.Find("Status/Performer Status/Performer Status");
                    if (t != null) _loadingPerformerStatusText = t.GetComponent<Text>();
                }
                if (_loadingPerformerReadyObject == null)
                {
                    Transform t = lp.Find("Status/Performer Status/Ready");
                    if (t != null) _loadingPerformerReadyObject = t.gameObject;
                }
                if (_loadingPerformerWarningObject == null)
                {
                    Transform t = lp.Find("Status/Performer Status/Warning");
                    if (t != null) _loadingPerformerWarningObject = t.gameObject;
                }
                if (_loadingDelayStatusText == null)
                {
                    Transform t = lp.Find("Status/Delay Status/Delay Status");
                    if (t != null) _loadingDelayStatusText = t.GetComponent<Text>();
                }
                if (_loadingDirectionText == null)
                {
                    Transform t = lp.Find("Loading Status/LoadingDirectionText");
                    if (t == null) t = lp.Find("LoadingDirectionText");
                    if (t != null) _loadingDirectionText = t.GetComponent<Text>();
                }
                if (_loadingPercentageText == null)
                {
                    Transform t = lp.Find("Loading Status/Percentage");
                    if (t != null) _loadingPercentageText = t.GetComponent<Text>();
                }
                if (_loadingProgressBar == null)
                {
                    Transform t = lp.Find("Loading Bar");
                    if (t != null) _loadingProgressBar = t.GetComponent<Slider>();
                }
                if (_forceStartNowButton == null)
                {
                    Transform t = lp.Find("ForceStartNowButton");
                    if (t != null) _forceStartNowButton = t.GetComponent<Button>();
                }
                if (_cancelLoadingButton == null)
                {
                    Transform t = lp.Find("CancelLoadingButton");
                    if (t != null) _cancelLoadingButton = t.GetComponent<Button>();
                }
            }

            if (_playingPanel != null)
            {
                Transform pp = _playingPanel.transform;
                if (_playingLoadedPlayerCountText == null)
                {
                    Transform t = pp.Find("Status/Loaded Player/Loaded Player num");
                    if (t != null) _playingLoadedPlayerCountText = t.GetComponent<Text>();
                }
                if (_playingPerformerStatusText == null)
                {
                    Transform t = pp.Find("Status/Performer Status/Performer Status");
                    if (t != null) _playingPerformerStatusText = t.GetComponent<Text>();
                }
                if (_playingPerformerReadyObject == null)
                {
                    Transform t = pp.Find("Status/Performer Status/Ready");
                    if (t != null) _playingPerformerReadyObject = t.gameObject;
                }
                if (_playingPerformerWarningObject == null)
                {
                    Transform t = pp.Find("Status/Performer Status/Warning");
                    if (t != null) _playingPerformerWarningObject = t.gameObject;
                }
                if (_playingDelayStatusText == null)
                {
                    Transform t = pp.Find("Status/Delay Status/Delay Status");
                    if (t != null) _playingDelayStatusText = t.GetComponent<Text>();
                }
                if (_playingDirectionText == null)
                {
                    Transform t = pp.Find("Video Player Status/PlayingDirectionText");
                    if (t == null) t = pp.Find("PlayingDirectionText");
                    if (t != null) _playingDirectionText = t.GetComponent<Text>();
                }
                if (_playingVideoTimeText == null)
                {
                    Transform t = pp.Find("Video Player Status/Video Time");
                    if (t != null) _playingVideoTimeText = t.GetComponent<Text>();
                }
                if (_playingProgressBar == null)
                {
                    Transform t = pp.Find("Loading Bar");
                    if (t != null) _playingProgressBar = t.GetComponent<Slider>();
                }
                if (_unlockEmergencyStopToggle == null)
                {
                    Transform t = pp.Find("UnlockEmergencyStopToggle");
                    if (t != null) _unlockEmergencyStopToggle = t.GetComponent<Toggle>();
                }
                if (_emergencyStopButton == null)
                {
                    Transform t = pp.Find("EmergencyStopButton");
                    if (t != null) _emergencyStopButton = t.GetComponent<Button>();
                }
            }
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
            bool showCanvas = isPerformer && _isPlayerNear;

            // 1. 親機 Pickup & Pickup Bar の有効/無効・表示制御
            // ・観客 (!isPerformer) または距離が離れている場合 (!_isPlayerNear) は非表示・把持不可
            // ・ロック時 (_isPositionLocked) は特別なロック表示は行わず Pickup Bar オブジェクト自体を非表示にする
            bool isHoldingParent = _parentPickup != null && _parentPickup.IsHeld;
            bool canPickupParent = isPerformer && !_isPositionLocked && (_isPlayerNear || isHoldingParent);
            if (!canPickupParent && isHoldingParent)
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
            if (_pickupBarObject != null && _pickupBarObject.activeSelf != canPickupParent)
            {
                _pickupBarObject.SetActive(canPickupParent);
            }

            SyncPositionLockUI();

            // 2. ホルスター (TriggerHolster / Pedestal) と 子機 (PerformerTrigger) の表示・有効制御
            // ・観客 (!isPerformer) からはホルスターもトリガーもすべて非表示・把持不可
            // ・演者 (isPerformer) は距離が離れてUIが消える場合でもホルスターのリング (Pedestal) とトリガーは表示・把持可能を維持
            if (_triggerHolsterObject != null && _triggerHolsterObject.activeSelf != isPerformer)
            {
                _triggerHolsterObject.SetActive(isPerformer);
            }

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
            if (!showCanvas)
            {
                ResetEmergencyStopSafety();
            }

            if (_targetCanvas != null)
            {
                if (_targetCanvas.gameObject.activeSelf != showCanvas)
                {
                    _targetCanvas.gameObject.SetActive(showCanvas);
                }
            }

            EnsureCanvasCollidersConfigured();
            SetCanvasCollidersEnabled(showCanvas);

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

                if (isWaiting || isPlaying)
                {
                    RefreshStatusFromMainUI();
                }

                if (isPlaying)
                {
                    SyncEmergencyStopUI();
                    EnsurePlaybackProgressLoopRunning();
                }
            }
        }

        private void EnsureCanvasCollidersConfigured()
        {
            if (_targetCanvas == null) return;

            BoxCollider[] boxCols = _targetCanvas.GetComponents<BoxCollider>();
            if (boxCols != null)
            {
                if (boxCols.Length >= 1 && boxCols[0] != null)
                {
                    boxCols[0].isTrigger = true;
                }
                if (boxCols.Length >= 2 && boxCols[1] != null)
                {
                    boxCols[1].isTrigger = true;
                    boxCols[1].center = new Vector3(560f, -391f, 0f);
                    boxCols[1].size = new Vector3(160f, 105f, 1f);
                }
            }

            _canvasColliders = _targetCanvas.GetComponentsInChildren<Collider>(true);
        }

        private void SetCanvasCollidersEnabled(bool enabledState)
        {
            if (_targetCanvas == null) return;
            if (_canvasColliders == null || _canvasColliders.Length == 0)
            {
                EnsureCanvasCollidersConfigured();
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

        #region Lifecycle Notifications & Status / Progress Bar Updates

        public void OnStageLoadingBegan(int objectIndex, int sessionId)
        {
            InitializeIfNeeded();
            ResetEmergencyStopSafety();

            if (_loadingDirectionText != null)
            {
                _loadingDirectionText.text = $"Loading : {GetDirectionName(objectIndex)}";
            }

            int initialTotal = VRCPlayerApi.GetPlayerCount();
            OnLoadProgressUpdated(0, initialTotal);
            RefreshStatusFromMainUI();

            if (_performerTrigger != null)
            {
                _performerTrigger.ReturnToHolster();
            }

            RefreshViewState();
        }

        public void OnLoadProgressUpdated(int readyCount, int totalCount)
        {
            InitializeIfNeeded();

            // 1. Loading Bar (0.0〜1.0) & Percentage (例: 75%)
            float ratio = 0f;
            if (totalCount > 0)
            {
                ratio = Mathf.Clamp01((float)readyCount / totalCount);
                int pct = Mathf.RoundToInt(ratio * 100f);
                if (_loadingPercentageText != null)
                {
                    _loadingPercentageText.text = $"{pct}%";
                }
            }
            else
            {
                bool isWaiting = _sssCore != null && _sssCore.IsWaitingForReady;
                if (_loadingPercentageText != null)
                {
                    _loadingPercentageText.text = isWaiting ? "0%" : "--%";
                }
            }

            if (_loadingProgressBar != null)
            {
                _loadingProgressBar.value = ratio;
            }

            // 2. Loaded Player num
            string countStr = totalCount <= 0 ? "0/0" : $"{readyCount}/{totalCount}";
            if (_loadingLoadedPlayerCountText != null)
            {
                _loadingLoadedPlayerCountText.text = countStr;
            }
            if (_playingLoadedPlayerCountText != null)
            {
                _playingLoadedPlayerCountText.text = countStr;
            }
            if (_loadingProgressText != null)
            {
                _loadingProgressText.text = $"Loaded: {countStr}";
            }
        }

        public void RefreshStatusFromMainUI()
        {
            InitializeIfNeeded();
            if (_mainUIController == null) return;

            int readyCount = _mainUIController.SyncedReadyCount;
            int totalCount = _mainUIController.SyncedTotalCount;
            OnLoadProgressUpdated(readyCount, totalCount);

            string countDisplay = _mainUIController.GetLoadedCountDisplayText();
            Color countColor = _mainUIController.GetLoadedCountDisplayColor();
            string perfStatusText = _mainUIController.GetPerformerStatusDisplayText();
            bool showReadyLamp = _mainUIController.GetShowPerformerReadyLamp();
            bool showErrorLamp = _mainUIController.GetShowPerformerErrorLamp();
            string delayText = _mainUIController.GetDelayStatusDisplayText();

            // LoadingPanel Status
            if (_loadingLoadedPlayerCountText != null)
            {
                _loadingLoadedPlayerCountText.text = countDisplay;
                _loadingLoadedPlayerCountText.color = countColor;
            }
            if (_loadingPerformerStatusText != null)
            {
                _loadingPerformerStatusText.text = perfStatusText;
            }
            if (_loadingPerformerReadyObject != null && _loadingPerformerReadyObject.activeSelf != showReadyLamp)
            {
                _loadingPerformerReadyObject.SetActive(showReadyLamp);
            }
            if (_loadingPerformerWarningObject != null && _loadingPerformerWarningObject.activeSelf != showErrorLamp)
            {
                _loadingPerformerWarningObject.SetActive(showErrorLamp);
            }
            if (_loadingDelayStatusText != null)
            {
                _loadingDelayStatusText.text = delayText;
            }

            // PlayingPanel Status
            if (_playingLoadedPlayerCountText != null)
            {
                _playingLoadedPlayerCountText.text = countDisplay;
                _playingLoadedPlayerCountText.color = countColor;
            }
            if (_playingPerformerStatusText != null)
            {
                _playingPerformerStatusText.text = perfStatusText;
            }
            if (_playingPerformerReadyObject != null && _playingPerformerReadyObject.activeSelf != showReadyLamp)
            {
                _playingPerformerReadyObject.SetActive(showReadyLamp);
            }
            if (_playingPerformerWarningObject != null && _playingPerformerWarningObject.activeSelf != showErrorLamp)
            {
                _playingPerformerWarningObject.SetActive(showErrorLamp);
            }
            if (_playingDelayStatusText != null)
            {
                _playingDelayStatusText.text = delayText;
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

            RefreshStatusFromMainUI();
            RefreshPlaybackProgressUI();

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
            ResetPlaybackProgressUI();
            RefreshPlaylistVisuals();
            RefreshViewState();
        }

        public void OnStagePlaybackCompleted(int completedObjectIndex)
        {
            InitializeIfNeeded();
            ResetEmergencyStopSafety();
            ResetPlaybackProgressUI();

            // 最後まで正常に再生完了した場合のみ、現在のソート順（SiblingIndex順）における次の演出へ自動予約を進める
            if (completedObjectIndex == _standbyObjectIndex)
            {
                _standbyObjectIndex = GetNextVisualDirectionIndex(completedObjectIndex);
            }

            RefreshPlaylistVisuals();
            RefreshViewState();
        }

        private void EnsurePlaybackProgressLoopRunning()
        {
            RefreshPlaybackProgressUI();
            if (!_isPlaybackProgressLoopScheduled)
            {
                _isPlaybackProgressLoopScheduled = true;
                SendCustomEventDelayedSeconds(nameof(OnPlaybackProgressTick), _PLAYBACK_PROGRESS_UPDATE_INTERVAL);
            }
        }

        public void OnPlaybackProgressTick()
        {
            _isPlaybackProgressLoopScheduled = false;

            bool isPerformer = _sssCore != null && _sssCore.IsPerformer;
            bool isWaiting = _sssCore != null && _sssCore.IsWaitingForReady;
            bool isPlaying = _sssCore != null && _sssCore.IsStagePlaying && !isWaiting;
            bool showCanvas = isPerformer && _isPlayerNear;

            if (!isPlaying || !showCanvas)
            {
                return;
            }

            RefreshPlaybackProgressUI();
            _isPlaybackProgressLoopScheduled = true;
            SendCustomEventDelayedSeconds(nameof(OnPlaybackProgressTick), _PLAYBACK_PROGRESS_UPDATE_INTERVAL);
        }

        public void RefreshPlaybackProgressUI()
        {
            if (_sssCore == null || !_sssCore.IsStagePlaying || _sssCore.IsWaitingForReady)
            {
                ResetPlaybackProgressUI();
                return;
            }

            int playingIdx = _sssCore.PlayingObjectIndex;
            if (_playingDirectionText != null && playingIdx >= 0)
            {
                _playingDirectionText.text = $"Playing: {GetDirectionName(playingIdx)}";
            }

            float currentTime = _sssCore.CurrentVideoTime;
            float duration = _sssCore.CurrentVideoDuration;

            if (duration > 0f && !float.IsInfinity(duration) && !float.IsNaN(duration))
            {
                float clampedCurrent = Mathf.Clamp(currentTime, 0f, duration);
                float ratio = Mathf.Clamp01(clampedCurrent / duration);

                if (_playingProgressBar != null)
                {
                    _playingProgressBar.value = ratio;
                }
                if (_playingVideoTimeText != null)
                {
                    _playingVideoTimeText.text = $"{FormatTimeMinSec(clampedCurrent)}/{FormatTimeMinSec(duration)}";
                }
            }
            else
            {
                if (_playingProgressBar != null)
                {
                    _playingProgressBar.value = 0f;
                }
                if (_playingVideoTimeText != null)
                {
                    if (currentTime > 0f && !float.IsInfinity(currentTime) && !float.IsNaN(currentTime))
                    {
                        _playingVideoTimeText.text = $"{FormatTimeMinSec(currentTime)}/--:--";
                    }
                    else
                    {
                        _playingVideoTimeText.text = "--:--/--:--";
                    }
                }
            }
        }

        private void ResetPlaybackProgressUI()
        {
            if (_playingProgressBar != null)
            {
                _playingProgressBar.value = 0f;
            }
            if (_playingVideoTimeText != null)
            {
                _playingVideoTimeText.text = "--:--/--:--";
            }
        }

        public void OnPlaylistOrderChanged(int[] sortedIndices)
        {
            if (sortedIndices == null) return;
            ApplySortOrderFromMainUI(sortedIndices, sortedIndices.Length);
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
            RefreshStatusFromMainUI();
        }

        #endregion

        #region Standby & Canvas Controls (Force Launch & Position Lock)

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
            SyncPositionLockUI();
            RefreshViewState();
        }

        private void SyncPositionLockUI()
        {
            _isUpdatingUI = true;
            if (_positionLockToggle != null)
            {
                _positionLockToggle.isOn = _isPositionLocked;
                UpdateSlideSwitchVisual(_positionLockToggle, _isPositionLocked);
            }
            if (_positionUnlockIcon != null && _positionUnlockIcon.activeSelf != !_isPositionLocked)
            {
                _positionUnlockIcon.SetActive(!_isPositionLocked);
            }
            if (_positionLockIcon != null && _positionLockIcon.activeSelf != _isPositionLocked)
            {
                _positionLockIcon.SetActive(_isPositionLocked);
            }
            _isUpdatingUI = false;
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

        private string FormatTimeMinSec(float seconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int minutes = totalSeconds / 60;
            int secs = totalSeconds % 60;
            return $"{minutes:D2}:{secs:D2}";
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
