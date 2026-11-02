using UnityEngine;
using VRC.SDKBase;
using UdonSharp;

namespace AppleteaSystems.StageSyncSystem
{
    [RequireComponent(typeof(VRC_Pickup))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SSSPerformerTrigger : UdonSharpBehaviour
    {
        [SerializeField]
        [Tooltip("連携先の演者用親機UIコントローラ")]
        private SSSPerformerUIController _performerUIController;

        [SerializeField]
        [Tooltip("子機の自動帰還先となる親機内のホルスター基準Transform")]
        private Transform _holsterTransform;

        [SerializeField]
        private VRC_Pickup _pickup;

        [SerializeField]
        private Rigidbody _rigidbody;

        [SerializeField]
        private Collider _pickupCollider;

        public SSSPerformerUIController PerformerUIController
        {
            get => _performerUIController;
            set => _performerUIController = value;
        }

        public Transform HolsterTransform
        {
            get => _holsterTransform;
            set => _holsterTransform = value;
        }

        void Start()
        {
            InitializeComponents();
            ReturnToHolster();
        }

        private void InitializeComponents()
        {
            if (_pickup == null)
            {
                _pickup = (VRC_Pickup)GetComponent(typeof(VRC_Pickup));
            }
            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
            }
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                _rigidbody.useGravity = false;
            }
            if (_pickupCollider == null)
            {
                _pickupCollider = GetComponent<Collider>();
            }
            if (_performerUIController == null)
            {
                _performerUIController = GetComponentInParent<SSSPerformerUIController>(true);
            }
        }

        public override void OnPickupUseDown()
        {
            if (_performerUIController == null) return;

            // 親機コントローラ経由で現在の予約演出の発火をリクエスト
            bool fired = _performerUIController.TryFireStandbyDirection();
            if (fired)
            {
                // 発火成功と同時に手から離して親機のホルスターへ自動帰還
                ReturnToHolster();
            }
        }

        public override void OnDrop()
        {
            ReturnToHolster();
        }

        public void ReturnToHolster()
        {
            InitializeComponents();

            if (_pickup != null && _pickup.IsHeld)
            {
                _pickup.Drop();
            }

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                _rigidbody.useGravity = false;
            }

            if (_holsterTransform != null)
            {
                transform.SetPositionAndRotation(_holsterTransform.position, _holsterTransform.rotation);
            }
            else
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
        }

        public void SetPerformerPickupable(bool canPickup)
        {
            InitializeComponents();

            if (!canPickup)
            {
                ReturnToHolster();
            }

            if (_pickup != null)
            {
                _pickup.pickupable = canPickup;
            }
            if (_pickupCollider != null)
            {
                _pickupCollider.enabled = canPickup;
            }
        }
    }
}
