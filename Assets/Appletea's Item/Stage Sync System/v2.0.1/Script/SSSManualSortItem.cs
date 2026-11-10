using UnityEngine;
using UnityEngine.UI;
using UdonSharp;

namespace AppleteaSystems.StageSyncSystem
{
    [DisallowMultipleComponent]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SSSManualSortItem : UdonSharpBehaviour
    {
        [Header("References")]
        [SerializeField] private SSSRuntimeManualSorter _sorter;
        [SerializeField] private int _objectIndex;
        [SerializeField] private RectTransform _rectTransform;
        [SerializeField] private Image _bgImage;
        [SerializeField] private Text _orderNumberText;
        [SerializeField] private Text _directionTitleText;
        [SerializeField] private Button _moveUpButton;
        [SerializeField] private Button _moveDownButton;
        [SerializeField] private Image _dragHandleImage;

        private readonly string[] _twoDigitRankTable = new string[]
        {
            "00", "01", "02", "03", "04", "05", "06", "07", "08", "09",
            "10", "11", "12", "13", "14", "15", "16", "17", "18", "19",
            "20", "21", "22", "23", "24", "25", "26", "27", "28", "29",
            "30", "31", "32", "33", "34", "35", "36", "37", "38", "39",
            "40", "41", "42", "43", "44", "45", "46", "47", "48", "49",
            "50", "51", "52", "53", "54", "55", "56", "57", "58", "59",
            "60", "61", "62", "63", "64"
        };

        private bool _initialized = false;

        public SSSRuntimeManualSorter Sorter
        {
            get => _sorter;
            set => _sorter = value;
        }

        public int ObjectIndex
        {
            get => _objectIndex;
            set => _objectIndex = value;
        }

        public RectTransform ItemRect
        {
            get
            {
                if (_rectTransform == null)
                {
                    _rectTransform = (RectTransform)transform;
                }
                return _rectTransform;
            }
            set => _rectTransform = value;
        }

        public void InitializeIfNeeded()
        {
            if (_initialized) return;
            _initialized = true;

            if (_rectTransform == null)
            {
                _rectTransform = (RectTransform)transform;
            }
            if (_sorter == null)
            {
                _sorter = GetComponentInParent<SSSRuntimeManualSorter>(true);
            }
            if (_bgImage == null)
            {
                _bgImage = GetComponent<Image>();
            }
            if (_orderNumberText == null)
            {
                Transform t = transform.Find("OrderNumberText");
                if (t != null) _orderNumberText = t.GetComponent<Text>();
            }
            if (_directionTitleText == null)
            {
                Transform t = transform.Find("DirectionTitleText");
                if (t != null) _directionTitleText = t.GetComponent<Text>();
            }
            if (_moveUpButton == null)
            {
                Transform t = transform.Find("MoveUpButton");
                if (t != null) _moveUpButton = t.GetComponent<Button>();
            }
            if (_moveDownButton == null)
            {
                Transform t = transform.Find("MoveDownButton");
                if (t != null) _moveDownButton = t.GetComponent<Button>();
            }
            if (_dragHandleImage == null)
            {
                Transform t = transform.Find("DragHandleIcon");
                if (t != null) _dragHandleImage = t.GetComponent<Image>();
            }
        }

        public void SetDirectionTitle(string title)
        {
            InitializeIfNeeded();
            if (_directionTitleText != null)
            {
                _directionTitleText.text = title != null ? title : "";
            }
        }

        public void SetSlotNumber(int oneBasedRank, bool canMoveUp, bool canMoveDown, bool interactable)
        {
            InitializeIfNeeded();
            if (_orderNumberText != null)
            {
                if (oneBasedRank >= 0 && oneBasedRank < _twoDigitRankTable.Length)
                {
                    _orderNumberText.text = _twoDigitRankTable[oneBasedRank];
                }
                else
                {
                    _orderNumberText.text = oneBasedRank < 10 ? $"0{oneBasedRank}" : oneBasedRank.ToString();
                }
            }

            if (_moveUpButton != null)
            {
                _moveUpButton.interactable = interactable && canMoveUp;
            }
            if (_moveDownButton != null)
            {
                _moveDownButton.interactable = interactable && canMoveDown;
            }
        }

        public void SetHighlightState(bool isDragged, bool interactable)
        {
            InitializeIfNeeded();
            if (_bgImage != null)
            {
                if (isDragged)
                {
                    _bgImage.color = new Color(0.31764707f, 0.1882353f, 0.81960785f, 0.96f);
                }
                else
                {
                    _bgImage.color = interactable
                        ? new Color(0.3019608f, 0.3019608f, 0.3019608f, 1f)
                        : new Color(0.3019608f, 0.3019608f, 0.3019608f, 0.55f);
                }
            }

            RectTransform rect = ItemRect;
            if (rect != null)
            {
                rect.localScale = isDragged ? new Vector3(1.03f, 1.03f, 1f) : Vector3.one;
            }

            if (_dragHandleImage != null)
            {
                Color c = _dragHandleImage.color;
                c.a = interactable ? (isDragged ? 1f : 0.75f) : 0.3f;
                _dragHandleImage.color = c;
            }
        }

        public void OnMoveUpClicked()
        {
            InitializeIfNeeded();
            if (_sorter != null)
            {
                _sorter.OnItemMoveUpClicked(_objectIndex);
            }
        }

        public void OnMoveDownClicked()
        {
            InitializeIfNeeded();
            if (_sorter != null)
            {
                _sorter.OnItemMoveDownClicked(_objectIndex);
            }
        }
    }
}
