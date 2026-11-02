using UnityEngine;
using VRC.SDKBase;
using UdonSharp;

namespace AppleteaSystems.StageSyncSystem
{
    [RequireComponent(typeof(SphereCollider))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SSSPerformerProximityChecker : UdonSharpBehaviour
    {
        [SerializeField]
        [Tooltip("連携先の演者用親機UIコントローラ")]
        private SSSPerformerUIController _performerUIController;

        [SerializeField]
        [Tooltip("近接判定用のSphereCollider (isTrigger = true)")]
        private SphereCollider _proximityCollider;

        public SSSPerformerUIController PerformerUIController
        {
            get => _performerUIController;
            set => _performerUIController = value;
        }

        void Start()
        {
            if (_proximityCollider == null)
            {
                _proximityCollider = GetComponent<SphereCollider>();
            }
            if (_proximityCollider != null)
            {
                _proximityCollider.isTrigger = true;
            }

            if (_performerUIController == null)
            {
                _performerUIController = GetComponentInParent<SSSPerformerUIController>(true);
            }

            RefreshProximityState();
            // VRCUiShapeの初期化によるCollider有効化を上書きするため1フレーム後にも再適用
            SendCustomEventDelayedFrames(nameof(RefreshProximityState), 1);
        }

        public void RefreshProximityState()
        {
            if (_performerUIController == null) return;
            _performerUIController.SetPlayerProximity(IsLocalPlayerInsideCollider());
        }

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || !player.isLocal) return;
            if (_performerUIController != null)
            {
                _performerUIController.SetPlayerProximity(true);
            }
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || !player.isLocal) return;
            if (_performerUIController != null)
            {
                _performerUIController.SetPlayerProximity(false);
            }
        }

        public override void OnPlayerRespawn(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || !player.isLocal) return;
            SendCustomEventDelayedFrames(nameof(RefreshProximityState), 1);
        }

        private bool IsLocalPlayerInsideCollider()
        {
            if (!Utilities.IsValid(Networking.LocalPlayer)) return false;
            if (_proximityCollider == null || !_proximityCollider.enabled || !gameObject.activeInHierarchy) return false;

            Vector3 playerPos = Networking.LocalPlayer.GetPosition();
            Vector3 closest = _proximityCollider.ClosestPoint(playerPos);
            return Vector3.Distance(closest, playerPos) < 0.01f;
        }
    }
}
