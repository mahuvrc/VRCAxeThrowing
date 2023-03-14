using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace mahu.AxeThrowing
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class AxeSpawner : UdonSharpBehaviour
    {
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private VRC_Pickup pickup;

        public AxeThrowingGame game;
        public TextMeshProUGUI LockUnlockBtnTxt;
        public GameObject Handle;

        [UdonSynced]
        public Vector3 Position;

        [UdonSynced]
        public Quaternion Rotation;

        [UdonSynced]
        public int Discontinuity;
        private int localDiscontinuity;

        [UdonSynced]
        public bool locked;

        private bool ownerLocked;

        void Start()
        {
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            pickup = (VRC_Pickup)GetComponent(typeof(VRC_Pickup));

            if (!Networking.LocalPlayer.IsUserInVR())
            {
                pickup.proximity = 1.5f;
            }
        }

        // hack for exit trigger
        private bool inPlayableZone;
        private bool pickupStay;
        private bool interpolating;

        public void OnTriggerEnter(Collider other)
        {
            if (Networking.IsOwner(gameObject))
            {
                if (other == game.AxeHoldZone)
                {
                    inPlayableZone = true;
                }
            }
        }

        public void OnTriggerExit(Collider other)
        {
            if (Networking.IsOwner(gameObject))
            {
                if (other == game.AxeHoldZone)
                {
                    inPlayableZone = false;
                    SendCustomEventDelayedSeconds(nameof(_TriggerExitDelay), 0.2f);
                }
            }
        }

        public void _TriggerExitDelay()
        {
            if (!inPlayableZone && Networking.IsOwner(gameObject))
            {
                _Reset();
            }
        }

        public void _Reset()
        {
            pickup.Drop();
            transform.position = initialPosition;
            transform.rotation = initialRotation;
            Discontinuity++;
            localDiscontinuity = Discontinuity;
            locked = false;
            RequestSerialization();
            UpdateState();
        }

        public override void OnPickup()
        {
            pickupStay = true;
            RequestSerialization();

            _UpdatePosition();
        }

        public override void OnDrop()
        {
            pickupStay = false;
            RequestSerialization();
        }

        public override void OnPreSerialization()
        {
            Position = transform.position;
            Rotation = transform.rotation;
        }

        public override void OnDeserialization()
        {
            if (Discontinuity != localDiscontinuity)
            {
                localDiscontinuity = Discontinuity;
                transform.SetPositionAndRotation(Position, Rotation);
            }
            else
            {
                if (!interpolating)
                    _Interpolate();
            }

            UpdateState();
        }

        public void _UpdatePosition()
        {
            if (!Networking.IsOwner(gameObject) || !pickupStay)
            {
                return;
            }

            if (!Mathf.Approximately((transform.position - Position).sqrMagnitude, 0)
                || !Mathf.Approximately((transform.rotation.eulerAngles - Rotation.eulerAngles).sqrMagnitude, 0))
            {
                RequestSerialization();
            }

            SendCustomEventDelayedSeconds(nameof(_UpdatePosition), 0.2f);
        }

        public void _Interpolate()
        {
            if (Networking.IsOwner(gameObject))
            {
                interpolating = false;
                return;
            }

            transform.position = Vector3.Lerp(transform.position, Position, Time.deltaTime / 0.25f);
            transform.rotation = Quaternion.Lerp(transform.rotation, Rotation, Time.deltaTime / 0.25f);

            if (Mathf.Approximately((transform.position - Position).sqrMagnitude, 0)
                && Mathf.Approximately((transform.rotation.eulerAngles - Rotation.eulerAngles).sqrMagnitude, 0))
            {
                interpolating = false;
                return;
            }
            else
            {
                interpolating = true;
                SendCustomEventDelayedFrames(nameof(_Interpolate), 1);
            }
        }

        public void _ToggleLock()
        {
            locked = !locked;
            RequestSerialization();
            UpdateState();
        }

        private void UpdateState()
        {
            var lockStatus = !locked && !ownerLocked;
            pickup.pickupable = lockStatus;
            Handle.SetActive(lockStatus);
            LockUnlockBtnTxt.text = locked ? "Unlock Axe Holder" : "Lock Axe Holder";
        }

        public void SetOwnerLocked(bool locked)
        {
            ownerLocked = locked;
            UpdateState();
        }
    }
}