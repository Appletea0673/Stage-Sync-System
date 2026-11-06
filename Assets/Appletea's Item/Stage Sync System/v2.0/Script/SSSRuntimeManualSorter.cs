using UnityEngine;
using UnityEngine.UI;
using UdonSharp;

namespace AppleteaSystems.StageSyncSystem
{
    [DisallowMultipleComponent]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SSSRuntimeManualSorter : UdonSharpBehaviour
    {
        [Header("Core & UI References")]
        [SerializeField] private SSSCore _sssCore;
        [SerializeField] private SSSUIController _uiController;

        [Header("Drawer & Scroll Layout")]
        [SerializeField] private RectTransform _sortViewportRect;
        [SerializeField] private RectTransform _sortContentRect;
        [SerializeField] private RectTransform _placeholderBarRect;
        [SerializeField] private Slider _dragOverlaySlider;
        [SerializeField] private Scrollbar _verticalScrollbar;

        [Header("Sort Item Instances (Indexed by ObjectIndex)")]
        [SerializeField] private SSSManualSortItem[] _itemsByObjectIndex;

        [Header("Layout & Animation Parameters")]
        [SerializeField] private float _itemHeight = 68f;
        [SerializeField] private float _itemSpacing = 8f;
        [SerializeField] private float _lerpSpeed = 18f;
        [SerializeField] private float _autoScrollSpeed = 350f;

        // Pre-allocated internal working arrays (Zero GC during drag)
        private int[] _workingOrder;
        private float[] _targetYPositions;
        private bool _initialized = false;

        // State control
        private bool _isDragging = false;
        private bool _isAnimating = false;
        private int _draggingObjectIndex = -1;
        private int _draggingCurrentSlot = -1;
        private float _currentScrollOffset = 0f;
        private bool _isInteractable = true;
        private bool _isUpdatingScrollbar = false;

        public SSSManualSortItem[] ItemsByObjectIndex
        {
            get => _itemsByObjectIndex;
            set => _itemsByObjectIndex = value;
        }

        public float ItemHeight => _itemHeight;
        public float ItemSpacing => _itemSpacing;

        private void Start()
        {
            InitializeArraysIfNeeded();
            // 初回アクティブ化直後に開始されたドラッグ/アニメーションを Start で止めないようにする
            if (!_isDragging && !_isAnimating)
            {
                this.enabled = false;
            }
        }

        private void InitializeArraysIfNeeded()
        {
            if (_uiController == null)
            {
                _uiController = GetComponentInParent<SSSUIController>(true);
            }
            if (_sssCore == null && _uiController != null)
            {
                _sssCore = _uiController.SSSCore;
            }
            if (_sortViewportRect == null)
            {
                Transform t = transform.Find("SortScrollView/Viewport");
                if (t != null) _sortViewportRect = (RectTransform)t;
            }
            if (_sortContentRect == null)
            {
                Transform t = transform.Find("SortScrollView/Viewport/Content");
                if (t != null) _sortContentRect = (RectTransform)t;
            }
            if (_placeholderBarRect == null && _sortContentRect != null)
            {
                Transform t = _sortContentRect.Find("PlaceholderBar");
                if (t != null) _placeholderBarRect = (RectTransform)t;
            }
            if (_dragOverlaySlider == null)
            {
                Transform t = transform.Find("SortScrollView/DragInputOverlaySlider");
                if (t != null) _dragOverlaySlider = t.GetComponent<Slider>();
            }
            if (_verticalScrollbar == null)
            {
                Transform t = transform.Find("SortScrollView/VerticalScrollbar");
                if (t != null) _verticalScrollbar = t.GetComponent<Scrollbar>();
            }
            if ((_itemsByObjectIndex == null || _itemsByObjectIndex.Length == 0) && _sortContentRect != null)
            {
                SSSManualSortItem[] foundItems = _sortContentRect.GetComponentsInChildren<SSSManualSortItem>(true);
                if (foundItems != null && foundItems.Length > 0)
                {
                    _itemsByObjectIndex = foundItems;
                    for (int i = 0; i < _itemsByObjectIndex.Length; i++)
                    {
                        if (_itemsByObjectIndex[i] != null)
                        {
                            _itemsByObjectIndex[i].Sorter = this;
                            _itemsByObjectIndex[i].ObjectIndex = i;
                            _itemsByObjectIndex[i].InitializeIfNeeded();
                        }
                    }
                }
            }

            int count = _itemsByObjectIndex != null ? _itemsByObjectIndex.Length : 0;
            if (count <= 0)
            {
                _workingOrder = new int[0];
                _targetYPositions = new float[0];
                _initialized = true;
                return;
            }

            if (!_initialized || _workingOrder == null || _workingOrder.Length != count)
            {
                _workingOrder = new int[count];
                _targetYPositions = new float[count];
                for (int i = 0; i < count; i++)
                {
                    _workingOrder[i] = i;
                }
                _initialized = true;
            }

            RefreshDirectionTitlesFromCore();
        }

        public void RefreshDirectionTitlesFromCore()
        {
            if (_sssCore == null || _sssCore.Directions == null || _itemsByObjectIndex == null) return;
            DirectionObject[] dirs = _sssCore.Directions;
            int len = Mathf.Min(dirs.Length, _itemsByObjectIndex.Length);
            for (int i = 0; i < len; i++)
            {
                if (_itemsByObjectIndex[i] != null && dirs[i] != null)
                {
                    _itemsByObjectIndex[i].SetDirectionTitle(dirs[i].directionName);
                }
            }
        }

        public void OpenDrawerWithOrder(int[] currentOrder)
        {
            InitializeArraysIfNeeded();
            int count = _workingOrder != null ? _workingOrder.Length : 0;
            if (currentOrder != null && currentOrder.Length == count)
            {
                for (int i = 0; i < count; i++)
                {
                    _workingOrder[i] = currentOrder[i];
                }
            }

            _currentScrollOffset = 0f;
            UpdateContentHeightAndScrollPosition();

            if (_placeholderBarRect != null)
            {
                _placeholderBarRect.gameObject.SetActive(false);
            }

            RecalculateTargetPositionsAndLabels(immediateSnap: true);
            SyncScrollbarVisual();

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            _isDragging = false;
            _isAnimating = false;
            _draggingObjectIndex = -1;
            _draggingCurrentSlot = -1;
            this.enabled = false;
        }

        public void CloseDrawer()
        {
            CancelDragAndDisable();
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        public void SyncOrderFromNetwork(int[] syncedOrder)
        {
            InitializeArraysIfNeeded();
            int count = _workingOrder != null ? _workingOrder.Length : 0;
            if (syncedOrder == null || syncedOrder.Length != count) return;

            for (int i = 0; i < count; i++)
            {
                _workingOrder[i] = syncedOrder[i];
            }

            if (!_isDragging)
            {
                if (gameObject.activeInHierarchy)
                {
                    RecalculateTargetPositionsAndLabels(immediateSnap: false);
                    _isAnimating = true;
                    this.enabled = true;
                }
                else
                {
                    RecalculateTargetPositionsAndLabels(immediateSnap: true);
                }
            }
        }

        public void SetInteractable(bool interactable)
        {
            InitializeArraysIfNeeded();
            _isInteractable = interactable;

            if (_dragOverlaySlider != null)
            {
                _dragOverlaySlider.interactable = interactable;
            }
            if (_verticalScrollbar != null)
            {
                _verticalScrollbar.interactable = interactable;
            }

            if (!interactable)
            {
                CancelDragAndDisable();
            }

            RefreshItemLabelsAndButtons();
        }

        public void CancelDragAndDisable()
        {
            if (_isDragging && _itemsByObjectIndex != null &&
                _draggingObjectIndex >= 0 && _draggingObjectIndex < _itemsByObjectIndex.Length &&
                _itemsByObjectIndex[_draggingObjectIndex] != null)
            {
                _itemsByObjectIndex[_draggingObjectIndex].SetHighlightState(false, _isInteractable);
            }

            _isDragging = false;
            _isAnimating = false;
            _draggingObjectIndex = -1;
            _draggingCurrentSlot = -1;

            if (_placeholderBarRect != null)
            {
                _placeholderBarRect.gameObject.SetActive(false);
            }

            if (_workingOrder != null && _workingOrder.Length > 0)
            {
                RecalculateTargetPositionsAndLabels(immediateSnap: true);
            }

            this.enabled = false;
        }

        private void Update()
        {
            // Safety check: immediately abort drag & disable Update if loading or playing starts
            if (_sssCore != null && (_sssCore.IsWaitingForReady || _sssCore.IsStagePlaying))
            {
                CancelDragAndDisable();
                return;
            }

            if (!_isInteractable)
            {
                CancelDragAndDisable();
                return;
            }

            int count = _workingOrder != null ? _workingOrder.Length : 0;
            if (count <= 0)
            {
                this.enabled = false;
                return;
            }

            float dt = Time.deltaTime;
            float viewportHeight = GetViewportHeight();
            float stride = _itemHeight + _itemSpacing;

            // 1. Live drag tracking, edge auto-scroll, and slot reflow
            if (_isDragging && _dragOverlaySlider != null)
            {
                float normY = _dragOverlaySlider.value; // 0.0 (Top) .. 1.0 (Bottom)

                // Edge Auto-Scroll when pointer is near top (0..0.15) or bottom (0.85..1.0)
                float maxScroll = Mathf.Max(0f, GetTotalContentHeight() - viewportHeight);
                if (maxScroll > 0f)
                {
                    if (normY < 0.15f)
                    {
                        float scrollFactor = (0.15f - normY) / 0.15f;
                        _currentScrollOffset = Mathf.Max(0f, _currentScrollOffset - (_autoScrollSpeed * scrollFactor * dt));
                        ApplyScrollOffsetToContent();
                        SyncScrollbarVisual();
                    }
                    else if (normY > 0.85f)
                    {
                        float scrollFactor = (normY - 0.85f) / 0.15f;
                        _currentScrollOffset = Mathf.Min(maxScroll, _currentScrollOffset + (_autoScrollSpeed * scrollFactor * dt));
                        ApplyScrollOffsetToContent();
                        SyncScrollbarVisual();
                    }
                }

                float pointerYInContent = (normY * viewportHeight) + _currentScrollOffset;
                int targetSlot = Mathf.Clamp(Mathf.FloorToInt(pointerYInContent / stride), 0, count - 1);

                if (targetSlot != _draggingCurrentSlot)
                {
                    ShiftWorkingOrder(_draggingCurrentSlot, targetSlot);
                    _draggingCurrentSlot = targetSlot;
                    RecalculateTargetPositionsAndLabels(immediateSnap: false);

                    if (_placeholderBarRect != null)
                    {
                        _placeholderBarRect.anchoredPosition = new Vector2(0f, -targetSlot * stride);
                    }

                    // Live-update local playlist sibling order
                    if (_uiController != null)
                    {
                        _uiController.ApplyManualOrderLocal(_workingOrder);
                    }
                }

                // Move dragged item smoothly along pointer Y
                if (_draggingObjectIndex >= 0 && _draggingObjectIndex < _itemsByObjectIndex.Length)
                {
                    SSSManualSortItem draggedItem = _itemsByObjectIndex[_draggingObjectIndex];
                    if (draggedItem != null)
                    {
                        RectTransform dragRect = draggedItem.ItemRect;
                        float maxDragY = Mathf.Max(0f, (count - 1) * stride);
                        float dragY = -Mathf.Clamp(pointerYInContent - (_itemHeight * 0.5f), 0f, maxDragY);
                        Vector2 pos = dragRect.anchoredPosition;
                        pos.y = Mathf.Lerp(pos.y, dragY, Mathf.Clamp01(dt * 28f));
                        dragRect.anchoredPosition = pos;
                    }
                }
            }

            // 2. Smooth Lerp animation of non-dragged items toward their slot target Y
            bool anyMoving = false;
            for (int slot = 0; slot < count; slot++)
            {
                int objIdx = _workingOrder[slot];
                if (_isDragging && objIdx == _draggingObjectIndex) continue;
                if (objIdx < 0 || objIdx >= _itemsByObjectIndex.Length) continue;

                SSSManualSortItem item = _itemsByObjectIndex[objIdx];
                if (item == null) continue;

                RectTransform itemRect = item.ItemRect;
                Vector2 pos = itemRect.anchoredPosition;
                float targetY = _targetYPositions[slot];
                float diff = Mathf.Abs(pos.y - targetY);

                if (diff > 0.5f)
                {
                    pos.y = Mathf.Lerp(pos.y, targetY, Mathf.Clamp01(dt * _lerpSpeed));
                    itemRect.anchoredPosition = pos;
                    anyMoving = true;
                }
                else if (diff > 0.001f)
                {
                    pos.y = targetY;
                    itemRect.anchoredPosition = pos;
                }
            }

            // 3. Auto-disable Update when drag is finished and all items have snapped into place
            if (!_isDragging && !anyMoving)
            {
                _isAnimating = false;
                this.enabled = false;
            }
        }

        #region Event Callbacks (Overlay Slider, Scrollbar, & ▲/▼ Buttons)

        public void OnDragSliderPointerDown()
        {
            if (!_isInteractable) return;
            if (_sssCore != null && (_sssCore.IsWaitingForReady || _sssCore.IsStagePlaying)) return;

            InitializeArraysIfNeeded();
            int count = _workingOrder != null ? _workingOrder.Length : 0;
            if (count <= 1 || _dragOverlaySlider == null) return;

            float viewportHeight = GetViewportHeight();
            float stride = _itemHeight + _itemSpacing;
            float pointerYInContent = (_dragOverlaySlider.value * viewportHeight) + _currentScrollOffset;

            int hoveredSlot = Mathf.FloorToInt(pointerYInContent / stride);
            if (hoveredSlot < 0 || hoveredSlot >= count) return;

            _draggingCurrentSlot = hoveredSlot;
            _draggingObjectIndex = _workingOrder[hoveredSlot];
            _isDragging = true;
            _isAnimating = true;

            if (_placeholderBarRect != null)
            {
                _placeholderBarRect.anchoredPosition = new Vector2(0f, -hoveredSlot * stride);
                _placeholderBarRect.gameObject.SetActive(true);
                _placeholderBarRect.SetAsFirstSibling();
            }

            if (_draggingObjectIndex >= 0 && _draggingObjectIndex < _itemsByObjectIndex.Length)
            {
                SSSManualSortItem draggedItem = _itemsByObjectIndex[_draggingObjectIndex];
                if (draggedItem != null)
                {
                    draggedItem.SetHighlightState(true, _isInteractable);
                    draggedItem.transform.SetAsLastSibling();
                }
            }

            this.enabled = true;
        }

        public void OnDragSliderValueChanged()
        {
            if (!_isDragging) return;
            this.enabled = true;
        }

        public void OnDragSliderPointerUp()
        {
            if (!_isDragging) return;

            _isDragging = false;
            if (_placeholderBarRect != null)
            {
                _placeholderBarRect.gameObject.SetActive(false);
            }

            if (_draggingObjectIndex >= 0 && _draggingObjectIndex < _itemsByObjectIndex.Length)
            {
                SSSManualSortItem draggedItem = _itemsByObjectIndex[_draggingObjectIndex];
                if (draggedItem != null)
                {
                    draggedItem.SetHighlightState(false, _isInteractable);
                }
            }

            _draggingObjectIndex = -1;
            _draggingCurrentSlot = -1;

            RecalculateTargetPositionsAndLabels(immediateSnap: false);
            _isAnimating = true;
            this.enabled = true;

            if (_uiController != null)
            {
                _uiController.CommitManualOrderAndSync(_workingOrder);
            }
        }

        public void OnItemMoveUpClicked(int objectIndex)
        {
            if (!_isInteractable || _isDragging) return;
            if (_sssCore != null && (_sssCore.IsWaitingForReady || _sssCore.IsStagePlaying)) return;

            InitializeArraysIfNeeded();
            int currentSlot = FindSlotOfObjectIndex(objectIndex);
            if (currentSlot <= 0) return;

            ShiftWorkingOrder(currentSlot, currentSlot - 1);
            RecalculateTargetPositionsAndLabels(immediateSnap: false);
            _isAnimating = true;
            this.enabled = true;

            if (_uiController != null)
            {
                _uiController.CommitManualOrderAndSync(_workingOrder);
            }
        }

        public void OnItemMoveDownClicked(int objectIndex)
        {
            if (!_isInteractable || _isDragging) return;
            if (_sssCore != null && (_sssCore.IsWaitingForReady || _sssCore.IsStagePlaying)) return;

            InitializeArraysIfNeeded();
            int count = _workingOrder != null ? _workingOrder.Length : 0;
            int currentSlot = FindSlotOfObjectIndex(objectIndex);
            if (currentSlot < 0 || currentSlot >= count - 1) return;

            ShiftWorkingOrder(currentSlot, currentSlot + 1);
            RecalculateTargetPositionsAndLabels(immediateSnap: false);
            _isAnimating = true;
            this.enabled = true;

            if (_uiController != null)
            {
                _uiController.CommitManualOrderAndSync(_workingOrder);
            }
        }

        public void OnVerticalScrollbarValueChanged()
        {
            if (_isUpdatingScrollbar || _verticalScrollbar == null) return;

            float viewportHeight = GetViewportHeight();
            float maxScroll = Mathf.Max(0f, GetTotalContentHeight() - viewportHeight);
            if (maxScroll <= 0f)
            {
                _currentScrollOffset = 0f;
            }
            else
            {
                float normalizedTopToBottom = _verticalScrollbar.direction == Scrollbar.Direction.BottomToTop
                    ? (1f - _verticalScrollbar.value)
                    : _verticalScrollbar.value;
                _currentScrollOffset = Mathf.Clamp(normalizedTopToBottom * maxScroll, 0f, maxScroll);
            }

            ApplyScrollOffsetToContent();
        }

        #endregion

        #region Internal Helpers

        private int FindSlotOfObjectIndex(int objectIndex)
        {
            if (_workingOrder == null) return -1;
            for (int i = 0; i < _workingOrder.Length; i++)
            {
                if (_workingOrder[i] == objectIndex) return i;
            }
            return -1;
        }

        private void ShiftWorkingOrder(int fromSlot, int toSlot)
        {
            if (_workingOrder == null || fromSlot == toSlot) return;
            int count = _workingOrder.Length;
            if (fromSlot < 0 || fromSlot >= count || toSlot < 0 || toSlot >= count) return;

            int movedObjIdx = _workingOrder[fromSlot];
            if (fromSlot < toSlot)
            {
                for (int i = fromSlot; i < toSlot; i++)
                {
                    _workingOrder[i] = _workingOrder[i + 1];
                }
            }
            else
            {
                for (int i = fromSlot; i > toSlot; i--)
                {
                    _workingOrder[i] = _workingOrder[i - 1];
                }
            }
            _workingOrder[toSlot] = movedObjIdx;
        }

        private void RecalculateTargetPositionsAndLabels(bool immediateSnap)
        {
            if (_workingOrder == null || _itemsByObjectIndex == null) return;
            float stride = _itemHeight + _itemSpacing;
            int count = _workingOrder.Length;

            for (int slot = 0; slot < count; slot++)
            {
                _targetYPositions[slot] = -slot * stride;
                int objIdx = _workingOrder[slot];
                if (objIdx < 0 || objIdx >= _itemsByObjectIndex.Length) continue;

                SSSManualSortItem item = _itemsByObjectIndex[objIdx];
                if (item == null) continue;

                item.SetSlotNumber(slot + 1, slot > 0, slot < count - 1, _isInteractable);
                if (!(_isDragging && objIdx == _draggingObjectIndex))
                {
                    item.SetHighlightState(false, _isInteractable);
                }

                if (immediateSnap && item.ItemRect != null)
                {
                    item.ItemRect.anchoredPosition = new Vector2(0f, _targetYPositions[slot]);
                }
            }
        }

        private void RefreshItemLabelsAndButtons()
        {
            if (_workingOrder == null || _itemsByObjectIndex == null) return;
            int count = _workingOrder.Length;
            for (int slot = 0; slot < count; slot++)
            {
                int objIdx = _workingOrder[slot];
                if (objIdx < 0 || objIdx >= _itemsByObjectIndex.Length) continue;

                SSSManualSortItem item = _itemsByObjectIndex[objIdx];
                if (item != null)
                {
                    item.SetSlotNumber(slot + 1, slot > 0, slot < count - 1, _isInteractable);
                    item.SetHighlightState(_isDragging && objIdx == _draggingObjectIndex, _isInteractable);
                }
            }
        }

        private float GetViewportHeight()
        {
            if (_sortViewportRect == null) return 0f;
            float h = _sortViewportRect.rect.height;
            if (h <= 0f)
            {
                h = _sortViewportRect.sizeDelta.y;
            }
            return Mathf.Max(0f, h);
        }

        private float GetTotalContentHeight()
        {
            int count = _workingOrder != null ? _workingOrder.Length : (_itemsByObjectIndex != null ? _itemsByObjectIndex.Length : 0);
            if (count <= 0) return 0f;
            return (count * _itemHeight) + (Mathf.Max(0, count - 1) * _itemSpacing);
        }

        private void UpdateContentHeightAndScrollPosition()
        {
            if (_sortContentRect == null) return;
            float totalHeight = GetTotalContentHeight();
            Vector2 sd = _sortContentRect.sizeDelta;
            sd.y = totalHeight;
            _sortContentRect.sizeDelta = sd;
            ApplyScrollOffsetToContent();
        }

        private void ApplyScrollOffsetToContent()
        {
            if (_sortContentRect == null) return;
            Vector2 pos = _sortContentRect.anchoredPosition;
            pos.y = _currentScrollOffset;
            _sortContentRect.anchoredPosition = pos;
        }

        private void SyncScrollbarVisual()
        {
            if (_verticalScrollbar == null) return;
            _isUpdatingScrollbar = true;

            float viewportHeight = GetViewportHeight();
            float totalHeight = GetTotalContentHeight();
            float maxScroll = Mathf.Max(0f, totalHeight - viewportHeight);

            if (totalHeight <= 0f || maxScroll <= 0f)
            {
                _verticalScrollbar.size = 1f;
                _verticalScrollbar.value = _verticalScrollbar.direction == Scrollbar.Direction.BottomToTop ? 1f : 0f;
            }
            else
            {
                _verticalScrollbar.size = Mathf.Clamp(viewportHeight / totalHeight, 0.1f, 1f);
                float normalizedTopToBottom = Mathf.Clamp01(_currentScrollOffset / maxScroll);
                _verticalScrollbar.value = _verticalScrollbar.direction == Scrollbar.Direction.BottomToTop
                    ? (1f - normalizedTopToBottom)
                    : normalizedTopToBottom;
            }

            _isUpdatingScrollbar = false;
        }

        #endregion
    }
}
