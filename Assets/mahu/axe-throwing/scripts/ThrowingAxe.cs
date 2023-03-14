using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace mahu.AxeThrowing
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class ThrowingAxe : UdonSharpBehaviour
    {
        const int STATE_WAITING = 0;
        const int STATE_TAKEN = 1;
        const int STATE_USED = 2;

        public AxeThrowingGame game;
        public AxeThrowingDebugLog debugLog;

        public AnimatedGameText gameText;

        public Transform edgePosition;
        public Transform handlePosition;
        public Transform headPosition;
        public Collider scoreCollider;
        public AudioSource audio;
        public AudioClip woodChop;
        public AudioClip errorSound;
        public AudioClip clangSound;
        public GameObject debugStickIndicatorPrefab;
        public PhysicMaterial axeMetal;

        public LayerMask throwCheckLayermask;

        public float velMult;
        public float angVelMult;

        [NonSerialized]
        public bool stuck;

        [NonSerialized]
        public bool hitBoard;

        private VRCPickup pickup;
        private Rigidbody rb;
        private ParentConstraint parentConstraint;

        private Collider playableZone;
        private bool inPlayableZone;
        private bool pickupStay;
        private bool shouldSyncHandOffset;
        private bool foul;
        private float triggerHeldPeak;
        private int heldFrames;
        private Vector3 lastPosition;
        private Quaternion lastRotation;

        private float[] frameTimes = new float[5];
        private Vector3[] heldVelocities = new Vector3[5];
        private Vector3[] heldAngularVelocities = new Vector3[5];

        private Vector3 smoothVelocity, smoothAngularVelocity;

        private Vector3 initialPosition;
        private Quaternion initialRotation;

        private bool ownerLocked;

        private const byte handState_idle = 0;
        private const byte handState_throwing = 1;
        private const byte handState_in_left = 2;
        private const byte handState_in_right = 3;


        [UdonSynced, NonSerialized]
        byte handState;

        [UdonSynced, NonSerialized]
        Vector3 syncPosition;

        [UdonSynced, NonSerialized]
        Quaternion syncRotation;

        [UdonSynced, NonSerialized]
        Vector3 simulatedThrowVel;

        [UdonSynced, NonSerialized]
        Vector3 simulatedThrowAngVel;

        [UdonSynced, NonSerialized]
        bool simulatedStick;

        [UdonSynced, NonSerialized]
        float simulatedStickTime;

        [UdonSynced, NonSerialized]
        Vector3 simulatedStickPosition;

        [UdonSynced, NonSerialized]
        Quaternion simulatedStickRotation;

        public void Start()
        {
            pickup = (VRCPickup)GetComponent(typeof(VRCPickup));
            rb = GetComponent<Rigidbody>();
            parentConstraint = GetComponent<ParentConstraint>();

            initialPosition = transform.position;
            initialRotation = transform.rotation;

            pickup.proximity = Networking.LocalPlayer.IsUserInVR() ? 0.05f : 0.5f;

            // Center of mass of the axe is forced to zero so that the calcualations are simplified for simulation
            // When creating custom models set the pivot of the object to the center of mass
            if (rb.centerOfMass.magnitude > 1e-3)
            {
                debugLog._Error($"Axe center of mass offset error: {rb.centerOfMass.ToString("0.000000")}");
            }

            rb.centerOfMass = Vector3.zero;
            playableZone = game.AxeHoldZone;
        }

        public void OnTriggerEnter(Collider other)
        {
            _EvaluateAxePlayableZone();
        }

        public void OnTriggerExit(Collider other)
        {
            _EvaluateAxePlayableZone();
        }

        // There are many colliders on this gameobject and OnTriggerEnter/Exit are called for each. So this will keep track and only check once per frame
        int lastFrameEvaluateAxePlayableZone;
        public void _EvaluateAxePlayableZone()
        {
            if (lastFrameEvaluateAxePlayableZone == Time.frameCount)
                return;

            lastFrameEvaluateAxePlayableZone = Time.frameCount;

            if (Networking.IsOwner(gameObject))
            {
                var closestPointInPlayableZone = playableZone.ClosestPoint(transform.position);
                inPlayableZone = (transform.position - closestPointInPlayableZone).sqrMagnitude < Vector3.kEpsilon;

                if (!inPlayableZone)
                {
                    if (game.AxeState == STATE_TAKEN)
                    {
                        debugLog._Info($"Axe will be consumed and respawned.");
                        SetState(STATE_USED);
                        game._ConsumeAxe();

                        if (pickup.IsHeld)
                        {
                            pickup.Drop();
                        }
                    }
                }
            }
        }

        private float clangTime = 0;
        public void OnCollisionEnter(Collision collision)
        {
            var contact = collision.contacts[0];
            if (Time.time > clangTime
                && contact.thisCollider != null
                && contact.otherCollider != null
                && contact.thisCollider.sharedMaterial == axeMetal
                && contact.otherCollider.sharedMaterial == axeMetal)
            {
                if (Networking.GetUniqueName(gameObject).CompareTo(
                    Networking.GetUniqueName(collision.rigidbody.gameObject)) < 0)
                {
                    clangTime = Time.time + UnityEngine.Random.Range(0.1f, 0.5f);
                    audio.PlayOneShot(clangSound, UnityEngine.Random.Range(0.25f, 0.65f));
                }
            }
        }

        #region Network Events
        public void ChopSound()
        {
            audio.PlayOneShot(woodChop);
        }

        public void ErrorSound()
        {
            audio.PlayOneShot(errorSound, 0.75f);
        }
        #endregion

        #region State Management
        public void SetState(int state)
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            game.SetAxeState(state);
        }

        private int currentState = -1;
        public void TransitionState(int state)
        {
            if (currentState == state)
            {
                return;
            }
            currentState = state;

            debugLog._Info("Axe transitioning to state " + state);
            switch (state)
            {
                case STATE_WAITING:
                    parentConstraint.constraintActive = true;

                    if (Networking.IsOwner(gameObject))
                    {
                        rb.isKinematic = true;
                        pickup.proximity = Networking.LocalPlayer.IsUserInVR() ? 0.05f : 0.5f;
                        handState = handState_idle;
                        RequestSerialization();
                    }
                    break;
                case STATE_TAKEN:
                    if (Networking.IsOwner(gameObject))
                    {
                        rb.isKinematic = false;
                    }
                    pickup.proximity = 2f;

                    parentConstraint.constraintActive = false;
                    break;
                case STATE_USED:
                    parentConstraint.constraintActive = false;
                    break;
                default:
                    break;
            }

            SetPickupable(state);
        }

        private void SetPickupable(int state)
        {
            pickup.pickupable = !ownerLocked
                && (state == STATE_WAITING
                    || state == STATE_TAKEN && Networking.IsOwner(gameObject));
        }

        public void SetOwnerLocked(bool locked)
        {
            ownerLocked = locked;
            SetPickupable(game.AxeState);
        }

        public void _Reset()
        {
            debugLog._Info("Resetting Thrown Axe");
            SetState(STATE_WAITING);
            pickup.Drop();
        }
        #endregion

        public override void OnPickup()
        {
            if (!Networking.LocalPlayer.IsUserInVR())
            {
                pickup.orientation = VRC_Pickup.PickupOrientation.Gun;
            }

            SetState(STATE_TAKEN);
            game._AxeTaken();

            foul = false;
            heldFrames = 0;
            lastPosition = transform.position;
            lastRotation = transform.rotation;
            triggerHeldPeak = 0;
            pickupStay = true;
            currentHand = pickup.currentHand;
            stuck = false;
            hitBoard = false;

            // Number of frames used for velocity regression on the throw based on framerate and platform
#if UNITY_ANDROID
            var optimalSmoothingFrames = Mathf.RoundToInt(Mathf.Clamp(0.28f / Time.smoothDeltaTime, 5, 50));
#else
            var optimalSmoothingFrames = Mathf.RoundToInt(Mathf.Clamp(0.15f / Time.smoothDeltaTime, 5, 50));
#endif

            frameTimes = new float[optimalSmoothingFrames];
            heldVelocities = new Vector3[optimalSmoothingFrames];
            heldAngularVelocities = new Vector3[optimalSmoothingFrames];

            _ShouldSyncHandOffset();

            // sync it again after picking up since it moves in hand over some fractions of a second
            SendCustomEventDelayedSeconds(nameof(_ShouldSyncHandOffset), 0.5f);
        }

        public void _ShouldSyncHandOffset()
        {
            shouldSyncHandOffset = true;
        }

        private void SyncHandOffset()
        {
            shouldSyncHandOffset = false;

            if (!pickupStay)
                return;

            handState = currentHand == VRC_Pickup.PickupHand.Left ? handState_in_left : handState_in_right;
            var bone = handState == handState_in_left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
            var boneInverseRotation = Quaternion.Inverse(Networking.LocalPlayer.GetBoneRotation(bone));
            syncPosition = boneInverseRotation * (transform.position - Networking.LocalPlayer.GetBonePosition(bone));
            syncRotation = boneInverseRotation * transform.rotation;
            RequestSerialization();
        }

        public override void OnDeserialization()
        {
            if (handState == handState_idle)
            {
                transform.position = syncPosition;
                transform.rotation = syncRotation;
                rb.isKinematic = true;
            }
            else if (handState == handState_throwing)
            {
                transform.position = syncPosition;
                transform.rotation = syncRotation;
                rb.isKinematic = false;
                rb.velocity = simulatedThrowVel;
                rb.angularVelocity = simulatedThrowAngVel;
                Physics.SyncTransforms();

                debugLog._Info("Throwing remote axe");

                if (simulatedStick)
                    SendCustomEventDelayedSeconds(nameof(_StickAtResultRemote), simulatedStickTime - Mathf.Max(Time.smoothDeltaTime, Time.fixedDeltaTime));
            }
        }


        VRC_Pickup.PickupHand currentHand;

        public override void PostLateUpdate()
        {
            if (pickupStay)
            {
                if (pickup.IsHeld)
                {
                    currentHand = pickup.currentHand;

                    if (shouldSyncHandOffset)
                    {
                        SyncHandOffset();
                    }
                }

                var frametotal = frameTimes.Length;

                // When you hold and release using the trigger, we can throw the axe with better precision
                // than the vrchat grip inputs
                float triggerHeldStrength;
                if (pickup.currentHand == VRC_Pickup.PickupHand.Left)
                {
                    triggerHeldStrength = Input.GetAxisRaw("Oculus_CrossPlatform_PrimaryIndexTrigger");
                    triggerHeldPeak = Mathf.Max(triggerHeldPeak, triggerHeldStrength);
                }
                else
                {
                    triggerHeldStrength = Input.GetAxisRaw("Oculus_CrossPlatform_SecondaryIndexTrigger");
                    triggerHeldPeak = Mathf.Max(triggerHeldPeak, triggerHeldStrength);
                }

                // drop after its been clearly pressed and now we started to release
                if (triggerHeldPeak > 0.5f && triggerHeldStrength < triggerHeldPeak * 0.8f)
                {
                    pickup.Drop();
                }

                var throwPosition = rb.worldCenterOfMass;

                if (heldFrames > 0)
                {
                    heldVelocities[heldFrames % frametotal] = (throwPosition - lastPosition) / Time.deltaTime;

                    float angleDegrees;
                    Vector3 rotationAxis;

                    (transform.rotation * Quaternion.Inverse(lastRotation)).ToAngleAxis(out angleDegrees, out rotationAxis);

                    heldAngularVelocities[heldFrames % frametotal] = rotationAxis * angleDegrees * Mathf.Deg2Rad / Time.deltaTime;
                }

                frameTimes[heldFrames % frametotal] = Time.deltaTime;

                lastPosition = throwPosition;
                lastRotation = transform.rotation;

                if (pickup.IsHeld)
                {
                    heldFrames++;
                }
                else
                {
                    pickupStay = false;

                    if (game.AxeState == STATE_TAKEN)
                    {
                        Throw();
                    }
                }
            }
            else
            {
                if (Networking.IsOwner(gameObject))
                {
                    if (handState == handState_in_left || handState == handState_in_right)
                    {
                        handState = handState_idle;
                        RequestSerialization();
                    }
                }
                else
                {
                    if (handState == handState_in_left || handState == handState_in_right)
                    {
                        var owner = Networking.GetOwner(gameObject);
                        var bone = handState == handState_in_left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
                        var boneRot = owner.GetBoneRotation(bone);
                        var bonePos = owner.GetBonePosition(bone);
                        transform.SetPositionAndRotation(bonePos + boneRot * syncPosition, boneRot * syncRotation);
                    }
                }
            }
        }

        public override void OnDrop()
        {
            if (game.NoGoZoneScript.LocalPlayerInZone)
            {
                debugLog._Info("Player was in the no go zone. Call foul.");
                foul = true;
            }

            var sqrDistanceFromHoldZone = (playableZone.ClosestPoint(transform.position) - transform.position).sqrMagnitude;
            if (sqrDistanceFromHoldZone > 1e-10)
            {
                debugLog._Info("Player released axe outside playable area. Call foul.");
                foul = true;
            }
        }

        #region Throwing and Simulation
        // Custom Throw function so that it takes place at the right time when the pickup is dropped
        // and after computing the velocities for the frame
        private void Throw()
        {
            handState = handState_throwing;
            syncPosition = transform.position;
            syncRotation = transform.rotation;
            simulatedThrowVel = Vector3.zero;
            simulatedThrowAngVel = Vector3.zero;
            simulatedStick = false;

            // an axe thrown from 3 meters at this speed will hit the target in 0.37 seconds, turning 1 rotation
            const float OPTIMAL_VELOCITY = 7.5f;
            const float OPTIMAL_ANGVELOCITY = 16.6f;
            
            debugLog._Info("Player throwing axe.");
            if (heldFrames < frameTimes.Length || !inPlayableZone)
            {
                // do nothing
            }
            else
            {
                if (Networking.LocalPlayer.IsUserInVR())
                {
                    debugLog._Info($"RB velocity before smoothing: {rb.velocity} {rb.angularVelocity}");

                    ComputeVelocityRegression();

                    rb.velocity = smoothVelocity * velMult;
                    rb.angularVelocity = smoothAngularVelocity * angVelMult;

                    // Note after physics simulation physics update the PCVR and Quest difficulty are the same
                    // The difficulty was originally reduced due to poor physics calculation
                    // Increasing difficulty value makes the assist weaker and the throwing more dependant on skill
                    const float DIFFICULTY = 2.275f;
                    const float ANG_DIFFICULTY = 2.275f;
                    const float ANG_MULT = 1.676f;

                    debugLog._Info($"RB velocity before assist: {rb.velocity} ({rb.velocity.magnitude}) {rb.angularVelocity} ({rb.angularVelocity.magnitude})");
                    rb.velocity = ComputeAssist(rb.velocity, OPTIMAL_VELOCITY, DIFFICULTY, 1);
                    // Compute a target velocity based on the adjusted velocity. The effect is that ths will try to match the spin closer to 1 rotation
                    var optimalComputedAngularVel = OPTIMAL_ANGVELOCITY / OPTIMAL_VELOCITY * rb.velocity.magnitude;
                    rb.angularVelocity = ComputeAssist(rb.angularVelocity, optimalComputedAngularVel, ANG_DIFFICULTY, ANG_MULT);
                    debugLog._Info($"RB velocity after assist: {rb.velocity} ({rb.velocity.magnitude}) {rb.angularVelocity} ({rb.angularVelocity.magnitude})");
                }
                else
                {
                    // Give desktop users a stupid random aimbot, lol
                    var playerLook = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);

                    var randomThrow = playerLook.rotation * Vector3.forward * UnityEngine.Random.Range(OPTIMAL_VELOCITY * 0.9f, OPTIMAL_VELOCITY * 1.1f);
                    var randomSpin = playerLook.rotation * Vector3.right * UnityEngine.Random.Range(OPTIMAL_ANGVELOCITY * 0.95f, OPTIMAL_ANGVELOCITY * 1.05f);

                    rb.velocity = ComputeAssist(randomThrow, OPTIMAL_VELOCITY, 2, 1);
                    rb.angularVelocity = ComputeAssist(randomSpin, OPTIMAL_ANGVELOCITY, 1.5f, 1);
                }
            }

            simulatedThrowVel = rb.velocity;
            simulatedThrowAngVel = rb.angularVelocity;

            if (ThrowSimulationV2(out var simulationTime, out _simulationResultPos, out _simulationResultRot))
            {
                simulatedStick = true;
                simulatedStickPosition = _simulationResultPos;
                simulatedStickRotation = _simulationResultRot;
                simulatedStickTime = simulationTime;

                SendCustomEventDelayedSeconds(nameof(_StickAtResult), simulationTime - Mathf.Max(Time.smoothDeltaTime, Time.fixedDeltaTime));
            }

            RequestSerialization();
        }

        private Vector3 _simulationResultPos;
        private Quaternion _simulationResultRot;
        public void _StickAtResult()
        {
            rb.isKinematic = true;
            pickup.Drop();
            stuck = true;

            transform.position = _simulationResultPos;
            transform.rotation = _simulationResultRot;

            // required to sync the transform since scoring the axe uses the physics engine
            // to check for collider overlap
            Physics.SyncTransforms();

            if (!foul)
            {
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ChopSound));
                game.ScoreAxe();
            }
            else
            {
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ErrorSound));
                gameText._PlayText("FOUL! Over line", 0.5f);
            }
        }

        public void _StickAtResultRemote()
        {
            debugLog._Info("Sticking remote axe");
            rb.isKinematic = true;
            transform.position = simulatedStickPosition;
            transform.rotation = simulatedStickRotation;
            Physics.SyncTransforms();
        }

        private void ComputeVelocityRegression()
        {
            var framecount = frameTimes.Length;

            var velocities = new Vector3[framecount];
            var angularVelocities = new Vector3[framecount];
            var times = new float[framecount];

            for (int i = 0; i < framecount; i++)
            {
                // array wraps around so we can just add
                var curFrameIdx = (heldFrames + 1 + i) % framecount;
                velocities[i] = heldVelocities[curFrameIdx];
                angularVelocities[i] = heldAngularVelocities[curFrameIdx];
                times[i] = frameTimes[curFrameIdx] + (i == 0 ? 0.0f : times[i - 1]);
                //debugLog._Info($"Historical velocities[{i}] ({times[i]}):  {velocities[i]} {angularVelocities[i]}");
            }

            var resultVelocity = ComputeRegression(velocities, times);
            var resultAngularVelocity = ComputeRegression(angularVelocities, times);

            debugLog._Info($"Smoothed RB velocity: {resultVelocity} {resultAngularVelocity}");

            smoothVelocity = resultVelocity;
            smoothAngularVelocity = resultAngularVelocity;
        }

        private Vector3 ComputeRegression(Vector3[] y, float[] x)
        {
            // this is linear regression via the least squares method. it's loads better
            // than averaging the velocity frame to frame especially at low frame rates
            // and will smooth out noise caused by fluctuations frame to frame and the
            // tendency for players to sharply flick their wrist when throwing

            float sumx = 0;                      /* sum of x     */
            float sumx2 = 0;                     /* sum of x**2  */
            Vector3 sumxy = Vector3.zero;                     /* sum of x * y */
            Vector3 sumy = Vector3.zero;                      /* sum of y     */
            Vector3 sumy2 = Vector3.zero;                     /* sum of y**2  */
            int n = x.Length;

            for (int i = 0; i < n; i++)
            {
                var xi = x[i];
                var yi = y[i];
                sumx += xi;
                sumx2 += xi * xi;
                sumxy += xi * yi;
                sumy += yi;
                sumy2 += Vector3.Scale(yi, yi);
            }

            float denom = n * sumx2 - sumx * sumx;
            if (denom == 0)
            {
                // singular matrix. can't solve the problem.
                debugLog._Warning("Unable to compute regression for throw.");
                return y[n - 1];
            }

            Vector3 m = (n * sumxy - sumx * sumy) / denom;
            Vector3 b = (sumy * sumx2 - sumx * sumxy) / denom;

            return m * x[n - 1] + b;
        }

        private Vector3 ComputeAssist(Vector3 vec, float optimal, float difficulty, float mult)
        {
            // exaggerate this distance highly and lerp towards the optimal velocity, more closer = more power
            var relativeDiffToOptimal = Mathf.Abs(optimal - vec.magnitude) / optimal;

            // higher values = more assist. at relative diff = 0 this returns 0;
            var assistPower = Mathf.Pow(1 - Mathf.Clamp(relativeDiffToOptimal / mult, 0, 1), difficulty);
            debugLog._Info($"Assist amount: off by {relativeDiffToOptimal} => {assistPower} assistPower");
            return Vector3.Lerp(vec, optimal * vec.normalized, assistPower);
        }

        /// <summary>
        /// Runs a simulation to determine if the axe sticks to the board. The simulation could also
        /// be used to show the axe fly through the air but it looks pretty much identical to unity physics so
        /// instead the axe is just allowed to fly through the air and then some code sticks it to the board
        /// after the computed delay.
        /// </summary>
        private bool ThrowSimulationV2(out float finalTime, out Vector3 finalPosition, out Quaternion finalRotation)
        {
            finalTime = 0;
            finalPosition = Vector3.zero;
            finalRotation = Quaternion.identity;

            var debugEnabled = AxeThrowingGame.IsDebugEnabled();
            var planeCollider = game.AxeStickPlane;
            if (planeCollider == null)
                return false;

            var intoWoodForward = -planeCollider.gameObject.transform.forward;

            var velocity = rb.velocity;
            var angularVelocityRad = rb.angularVelocity;
            var angularVelocityDeg = rb.angularVelocity * Mathf.Rad2Deg;
            var velocityIntoWood = Vector3.Project(rb.velocity, intoWoodForward);
            var intersectionPlane = new Plane(planeCollider.gameObject.transform.forward, planeCollider.transform.position);

            var worldToLocal = transform.worldToLocalMatrix;
            var startPos = transform.position;
            var startRot = transform.rotation;

            var edgePosLocal = worldToLocal.MultiplyPoint3x4(edgePosition.position);
            var edgeRotationLocal = worldToLocal.rotation * edgePosition.rotation;

            var handlePosLocal = worldToLocal.MultiplyPoint3x4(handlePosition.position);
            var headPosLocal = worldToLocal.MultiplyPoint3x4(headPosition.position);

            // assume handle is furthest from center of mass
            var radius = handlePosLocal.magnitude;

            // assumes the board is mounted upright on a wall and straight up and down
            // estimate initial collision as ballistic trajectory towards plane
            var distToIntersection = intersectionPlane.GetDistanceToPoint(transform.position) - radius;
            var timeToIntersection = distToIntersection / velocityIntoWood.magnitude;

            var intersectionTR = SimulatedTR(timeToIntersection, startPos, startRot, velocity, angularVelocityDeg);
            var adjIntersectionTR = intersectionTR;
            var adjIntersectionTime = timeToIntersection;

            var posBeforeAdj = intersectionTR.MultiplyPoint3x4(Vector3.zero);
            var closestBoardIntersection = planeCollider.ClosestPoint(posBeforeAdj);
            if ((closestBoardIntersection - posBeforeAdj).magnitude > radius * 2f)
            {
                debugLog._Info("Simulated Axe does not intersect board.");
                return false;
            }


            // use an approximation of the maximum speed the axe edge could be heading into the wood for simulation adjustment
            var adjustmentSpeed = velocity.magnitude + angularVelocityRad.magnitude * edgePosLocal.magnitude;
            bool convergence = false;

            // adjust time until edge position intersects closely the plane
            for (int i = 0; i < 10; i++)
            {
                var edgePos = adjIntersectionTR.MultiplyPoint3x4(edgePosLocal);
                var errorDist = intersectionPlane.GetDistanceToPoint(edgePos);
                var errorTime = errorDist / adjustmentSpeed;

                if (debugEnabled)
                    debugLog._Info($"{i}th adjustment: {edgePos} errorDist {errorDist}, errorTime {errorTime}, adjIntersectionTime {adjIntersectionTime}");

                if (Mathf.Abs(errorDist) < 1e-3)
                {
                    if (debugEnabled)
                        debugLog._Info($"Convergence after {i}th adjustment: {edgePos} errorDist {errorDist}, errorTime {errorTime}, adjIntersectionTime {adjIntersectionTime}");

                    convergence = true;
                    break;
                }

                if (Mathf.Abs(errorDist) > radius * 2)
                {
                    debugLog._Warning($"Divergence error. Axe likely moving away from board or moving way too fast.");
                    return false;
                }

                adjIntersectionTime = adjIntersectionTime + errorTime;
                adjIntersectionTR = SimulatedTR(adjIntersectionTime, startPos, startRot, velocity, angularVelocityDeg);
            }

            if (!convergence)
            {
                debugLog._Error("Simulation adjustment did not converge on edge slicing board.");
                return false;
            }

            // Calculate a path and raycast along it to prevent collisions
            var lastPos = Vector3.zero;
            for (int i = 1; i < 5; i++)
            {
                var pct = i / 5f;
                var t = adjIntersectionTime * pct;
                var simulated = SimulatedTR(t, startPos, startRot, velocity, angularVelocityDeg);
                var simPos = simulated.MultiplyPoint3x4(Vector3.zero);

                if (debugEnabled)
                {
                    var indicator = Instantiate(debugStickIndicatorPrefab, simPos, simulated.rotation);
                    Destroy(indicator, 4);
                }

                if (i > 1)
                {
                    if (Physics.Linecast(lastPos, simPos, out var hit, throwCheckLayermask, QueryTriggerInteraction.Ignore))
                    {
                        var hitName = hit.rigidbody != null ? hit.rigidbody.gameObject.name : hit.collider.gameObject.name;
                        debugLog._Info($"Axe intersects with checked geometry {hitName} along path at step {i}. Axe does not stick.");
                        return false;
                    }
                }

                lastPos = simPos;
            }

            var resultObjectRotation = adjIntersectionTR.rotation;
            var resultObjectPosition = adjIntersectionTR.MultiplyPoint3x4(Vector3.zero);

            GameObject finalIndicator = null;
            if (debugEnabled)
            {
                finalIndicator = Instantiate(debugStickIndicatorPrefab, resultObjectPosition, resultObjectRotation);
                Destroy(finalIndicator, 8);
            }

            var resultEdgePos = adjIntersectionTR.MultiplyPoint3x4(edgePosLocal);
            var resultEdgeRot = adjIntersectionTR.rotation * edgeRotationLocal;

            var closestEdgeBoardIntersection = planeCollider.ClosestPoint(resultEdgePos);
            if ((closestEdgeBoardIntersection - resultEdgePos).magnitude > 1e-2f)
            {
                debugLog._Info("Simulated Axe does not intersect board.");
                return false;
            }

            // determine if axe head sticks
            var edgeForward = resultEdgeRot * Vector3.forward;
            var edgeRight = resultEdgeRot * Vector3.right;

            // angle between the edge's "cutting action" and the board plane
            var alignedHeadAngle = Vector3.SignedAngle(intoWoodForward, edgeForward, edgeRight);

            // angle between the cutting plane of the axe and the board plane
            var orthogonalHeadAngle = Vector3.Angle(intoWoodForward, edgeRight);

            bool sticks = orthogonalHeadAngle > 45
                && orthogonalHeadAngle < 135;


            // angles that would cause the axe to dig in early or the handle bounce off
            const float maxCutAngle = 70;
            const float minCutAngle = -12;

            // 20 degrees of bounce tolerance: the axe or the tip of the axe's head can dig in a pretty hard
            // angle and bounce the position back. The 20 degree tolerance value is not exactly unrealistic given
            // high speed footage of axe throwing. And it is forgiving enough for fun VR axe throwing gameplay.
            const float bounceTolerance = 20;
            var clampedAngle = Mathf.Clamp(alignedHeadAngle, minCutAngle, maxCutAngle);
            var cutError = clampedAngle - alignedHeadAngle;

            debugLog._Info($"Angle between axe and wood along cutting action: {alignedHeadAngle} deg"
                    + $"\r\nAngle between axe and wood orthogonal to cutting action: {orthogonalHeadAngle} deg"
                    + $"\r\nCut error to try to correct: {cutError.ToString("0.000")} deg");

            if (sticks && Mathf.Abs(cutError) > 1e-5)
            {
                if (Mathf.Abs(cutError) < bounceTolerance)
                {
                    debugLog._Info("Simulated Axe intersects board by small amount and result will be adjusted for a small bounce.");
                    var bounceResult = Quaternion.AngleAxis(cutError, edgeRight);

                    resultObjectRotation = bounceResult * resultObjectRotation;
                    resultEdgeRot = bounceResult * resultEdgeRot;

                    // move result based on edge intersection, this approximates a handle that pivoted through the edge not the center of mass but that's OK
                    // since the edge should be much closer to the center of mass than the handle is.
                    resultObjectPosition = resultObjectRotation * -edgePosLocal + resultEdgePos;

                    if (debugEnabled && finalIndicator != null)
                    {
                        // destroy old final indicator early like its a path piece
                        Destroy(finalIndicator, 4);

                        // create adjusted indicator
                        finalIndicator = Instantiate(debugStickIndicatorPrefab, resultObjectPosition, resultObjectRotation);
                        Destroy(finalIndicator, 8);
                    }
                }
                else
                {
                    debugLog._Info("cut error too high to adjust final position. axe does not stick");
                    sticks = false;
                }
            }

            if (sticks)
            {
                debugLog._Info("Simulated Axe stuck to board");

                finalTime = adjIntersectionTime;
                finalPosition = resultObjectPosition;
                finalRotation = resultObjectRotation;

                return true;
            }
            else
            {
                debugLog._Info("Simulated Axe does not stick to board");

                return false;
            }
        }

        private static Matrix4x4 SimulatedTR(float t, Vector3 initialPosition, Quaternion initialRotation, Vector3 velocity, Vector3 angularVelocityDeg)
        {
            var dropFromTime = 0.5f * Physics.gravity * t * t;
            var rotationAmountFromTime = angularVelocityDeg * t;
            var rotationFromTime = Quaternion.AngleAxis(rotationAmountFromTime.magnitude, rotationAmountFromTime.normalized);

            var translationFromTime = dropFromTime + velocity * t;
            return Matrix4x4.TRS(translationFromTime + initialPosition, rotationFromTime * initialRotation, Vector3.one);
        }

        #endregion
    }
}