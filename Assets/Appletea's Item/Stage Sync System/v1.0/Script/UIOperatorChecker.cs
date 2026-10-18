using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using UdonSharp;

namespace AppleteaSystems.StageSyncSystem
{
    [RequireComponent(typeof(BoxCollider))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class UIOperatorChecker : UdonSharpBehaviour
    {
        [SerializeField]
        [Tooltip("操作制限を行う対象のUI Canvas（未指定時はルート階層から自動取得）")]
        Canvas _targetCanvas;

        BoxCollider _operatorCollider;
        Collider[] _canvasColliders;
        Button[] _buttons;
        Scrollbar[] _scrollbars;
        ScrollRect[] _scrollRects;
        Slider[] _sliders;
        Toggle[] _toggles;

        void Start()
        {
            _operatorCollider = GetComponent<BoxCollider>();
            if (_operatorCollider != null)
            {
                _operatorCollider.isTrigger = true;
            }

            if (_targetCanvas == null && transform.root != null)
            {
                _targetCanvas = transform.root.GetComponentInChildren<Canvas>(true);
            }

            CacheUIComponents();
            RefreshUIState();

            // VRCUiShapeによる初期化後のCollider有効化を上書きするため1フレーム後にも再適用
            SendCustomEventDelayedFrames(nameof(RefreshUIState), 1);
        }

        public void RefreshUIState()
        {
            CacheUIComponents();
            SetUIInteractable(IsLocalPlayerInsideCollider());
        }

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || !player.isLocal) return;
            SetUIInteractable(true);
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || !player.isLocal) return;
            SetUIInteractable(false);
        }

        public override void OnPlayerRespawn(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || !player.isLocal) return;
            SendCustomEventDelayedFrames(nameof(RefreshUIState), 1);
        }

        private void CacheUIComponents()
        {
            if (_targetCanvas == null) return;

            _canvasColliders = _targetCanvas.GetComponentsInChildren<Collider>(true);
            _buttons = _targetCanvas.GetComponentsInChildren<Button>(true);
            _scrollbars = _targetCanvas.GetComponentsInChildren<Scrollbar>(true);
            _scrollRects = _targetCanvas.GetComponentsInChildren<ScrollRect>(true);
            _sliders = _targetCanvas.GetComponentsInChildren<Slider>(true);
            _toggles = _targetCanvas.GetComponentsInChildren<Toggle>(true);
        }

        private bool IsLocalPlayerInsideCollider()
        {
            if (!Utilities.IsValid(Networking.LocalPlayer)) return false;
            if (_operatorCollider == null || !_operatorCollider.enabled || !gameObject.activeInHierarchy) return false;

            Vector3 playerPos = Networking.LocalPlayer.GetPosition();
            Vector3 closest = _operatorCollider.ClosestPoint(playerPos);
            return Vector3.Distance(closest, playerPos) < 0.01f;
        }

        private void SetUIInteractable(bool canOperate)
        {
            if (_targetCanvas == null) return;

            // 1. 外部からUIへのRay（VRCUiShape用Colliderへのヒット）を遮断・許可
            if (_canvasColliders != null)
            {
                for (int i = 0; i < _canvasColliders.Length; i++)
                {
                    if (_canvasColliders[i] != null && _canvasColliders[i] != _operatorCollider)
                    {
                        _canvasColliders[i].enabled = canOperate;
                    }
                }
            }

            // 2. UIコンポーネント群の操作を無効化・有効化
            if (_buttons != null)
            {
                for (int i = 0; i < _buttons.Length; i++)
                {
                    if (_buttons[i] != null) _buttons[i].interactable = canOperate;
                }
            }

            if (_scrollbars != null)
            {
                for (int i = 0; i < _scrollbars.Length; i++)
                {
                    if (_scrollbars[i] != null) _scrollbars[i].interactable = canOperate;
                }
            }

            if (_scrollRects != null)
            {
                for (int i = 0; i < _scrollRects.Length; i++)
                {
                    if (_scrollRects[i] != null) _scrollRects[i].enabled = canOperate;
                }
            }

            if (_sliders != null)
            {
                for (int i = 0; i < _sliders.Length; i++)
                {
                    if (_sliders[i] != null) _sliders[i].interactable = canOperate;
                }
            }

            if (_toggles != null)
            {
                for (int i = 0; i < _toggles.Length; i++)
                {
                    if (_toggles[i] != null) _toggles[i].interactable = canOperate;
                }
            }
        }
    }
}
