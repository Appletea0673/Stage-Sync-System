using UnityEngine;
using UdonSharp;

namespace AppleteaSystems.StageSyncSystem
{
    [DisallowMultipleComponent]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DirectionController : UdonSharpBehaviour
    {
        [Header("Base")]
        [SerializeField]
        SSSCore SSS_core;

        [SerializeField]
        SSSUIController _uiController;

        [SerializeField]
        SSSPerformerUIController _performerUIController;

        public SSSCore SSSCore
        {
            get => SSS_core;
            set => SSS_core = value;
        }

        public SSSUIController UIController
        {
            get => _uiController;
            set => _uiController = value;
        }

        public SSSPerformerUIController PerformerUIController
        {
            get => _performerUIController;
            set => _performerUIController = value;
        }

        // 既存UIボタンイベントからの互換転送用（実処理はSSSUIControllerへ移行済み）
        public void OnButtonClicked()
        {
            if (_uiController == null)
            {
                _uiController = GetComponentInChildren<SSSUIController>(true);
            }
            if (_uiController != null)
            {
                _uiController.OnButtonClicked();
            }
        }
    }
}
