
using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

public class ThrowingAxe : UdonSharpBehaviour
{
    const int STATE_WAITING = 0;
    const int STATE_TAKEN = 1;
    const int STATE_USED = 2;

    // Set by parent
    [NonSerialized]
    public AxeThrowingGame Game;

    public Transform edgePosition;
    public Collider scoreCollider;
    public AudioSource audio;
    public AudioClip woodChop;
    public AudioClip errorSound;
    public AudioClip clangSound;

    public bool useSmoothVelocity;
    public bool useAssist;

    public float velMult;
    public float angVelMult;

    private VRCPickup pickup;
    private VRCObjectSync objectsync;
    private Rigidbody rb;
    private ParentConstraint parentConstraint;

    private bool pickupStay;
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

    public void Start()
    {
        pickup = (VRCPickup)GetComponent(typeof(VRCPickup));
        objectsync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
        rb = GetComponent<Rigidbody>();
        parentConstraint = GetComponent<ParentConstraint>();

        initialPosition = transform.position;
        initialRotation = transform.rotation;

        pickup.proximity = Networking.LocalPlayer.IsUserInVR() ? 0.05f : 0.5f;
    }

    public void _OnStickTrigger(Collider collider)
    {
        var stickCollider = Game.AxeStickZone;
        if (collider != stickCollider)
        {
            return;
        }

        // simple way to determine if it sticks: if the edge is traveling fast enough it cuts into the
        // wood. if that point is moving mostly into the wood, then it's going to stick. otherwise it
        // would bounce off.

        // assumes the front of the axe stick zone is facing towards local positive Z
        // assumes the edgePosition is rotated such that the z is towards cutting and the x is to the side

        var edgeVelocity = rb.GetPointVelocity(edgePosition.position);
        var intoWoodDir = -stickCollider.gameObject.transform.forward;
        var edgeVelocityIntoWood = Vector3.Project(edgeVelocity, intoWoodDir);
        var edgeVelocityIntoWoodOnPlaneOfEdge = Vector3.ProjectOnPlane(edgeVelocityIntoWood, edgePosition.right);

        Debug.Log($"Axe stick test: rb velocity: {rb.velocity.magnitude}, edgeVelocity: {edgeVelocity.magnitude}, edgeVelocityIntoWoodOnPlaneOfEdge: {edgeVelocityIntoWoodOnPlaneOfEdge.magnitude}");

        // random numbers chosen
        if (edgeVelocityIntoWoodOnPlaneOfEdge.magnitude > 0.75f // does the axe dig into the wood
            && edgeVelocityIntoWoodOnPlaneOfEdge.magnitude > edgeVelocity.magnitude * 0.5f) // is the axe able to stay stuck without bouncing
        {
            // axe is stuck
            objectsync.SetKinematic(true);
            //rb.isKinematic = true;
            pickup.Drop();

            if (!foul)
            {
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ChopSound));
                Game.ScoreAxe();
            }
            else
            {
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ErrorSound));
            }
        }
    }

    public void ChopSound()
    {
        audio.PlayOneShot(woodChop);
    }

    public void ErrorSound()
    {
        audio.PlayOneShot(errorSound, 0.75f);
    }

    // hack for exit trigger since the pickup experiences many trigger enter
    // and exit events when the axe is picked up for some reason.
    private bool inPlayableZone;
    public void OnTriggerEnter(Collider other)
    {
        if (Networking.IsOwner(gameObject))
        {
            if (other == Game.AxeHoldZone)
            {
                Debug.Log("Axe is entering playable area.");
                inPlayableZone = true;
            }
        }
    }

    public void OnTriggerExit(Collider other)
    {
        if (Networking.IsOwner(gameObject))
        {
            if (other == Game.AxeHoldZone)
            {
                Debug.Log("Axe is exiting playable area.");
                inPlayableZone = false;
                SendCustomEventDelayedSeconds(nameof(_TriggerExitDelay), 0.2f);
            }
        }
    }

    public void _TriggerExitDelay()
    {
        if (!inPlayableZone)
        {
            if (Game.AxeState == STATE_TAKEN)
            {
                Debug.Log($"Axe will be consumed and respawned.");
                SetState(STATE_USED);
                Game._ConsumeAxe();

                if (pickup.IsHeld)
                {
                    pickup.Drop();
                }
            }
        }
    }

    private float clangTime = 0;

    public void OnCollisionEnter(Collision collision)
    {
        //Debug.Log($"!! Collision '{collision.contacts[0].thisCollider.material.name}' on '{collision.contacts[0].otherCollider.material.name}')");

        if (Time.time > clangTime
            && collision.contacts[0].thisCollider.material.name.StartsWith("axemetal")
            && collision.contacts[0].otherCollider.material.name.StartsWith("axemetal"))
        {
            if (Networking.GetUniqueName(gameObject).CompareTo(
                Networking.GetUniqueName(collision.rigidbody.gameObject)) < 0)
            {
                clangTime = Time.time + UnityEngine.Random.Range(0.2f, 0.7f);
                //Debug.Log($"play clang {Networking.GetUniqueName(gameObject)} on {Networking.GetUniqueName(collision.rigidbody.gameObject)}");
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(Clang));
            }
        }
    }

    public void Clang()
    {
        audio.PlayOneShot(clangSound, UnityEngine.Random.Range(0.25f, 0.65f));
    }

    public void SetState(int state)
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        Game.SetAxeState(state);
    }

    private int currentState = -1;
    public void TransitionState(int state)
    {
        if (currentState == state)
        {
            return;
        }
        currentState = state;

        Debug.Log("Axe transitioning to state " + state);
        switch (state)
        {
            case STATE_WAITING:
                if (Networking.IsOwner(gameObject))
                {
                    objectsync.FlagDiscontinuity();
                    objectsync.SetKinematic(true);
                    pickup.proximity = Networking.LocalPlayer.IsUserInVR() ? 0.05f : 0.5f;
                }
                parentConstraint.constraintActive = true;
                break;
            case STATE_TAKEN:
                if (Networking.IsOwner(gameObject))
                {
                    objectsync.SetKinematic(false);
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
                || (state == STATE_TAKEN && Networking.IsOwner(gameObject)));
    }

    public void SetOwnerLocked(bool locked)
    {
        ownerLocked = locked;
        SetPickupable(Game.AxeState);
    }

    public override void OnPickup()
    {
        if (!Networking.LocalPlayer.IsUserInVR())
        {
            pickup.orientation = VRC_Pickup.PickupOrientation.Gun;
        }

        SetState(STATE_TAKEN);
        Game._AxeTaken();

        foul = false;
        heldFrames = 0;
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        triggerHeldPeak = 0;
        pickupStay = true;

#if UNITY_ANDROID
        var optimalSmoothingFrames = Mathf.RoundToInt(Mathf.Clamp(0.28f / Time.smoothDeltaTime, 5, 50));
#else
        var optimalSmoothingFrames = Mathf.RoundToInt(Mathf.Clamp(0.15f / Time.smoothDeltaTime, 5, 50));
#endif

        frameTimes = new float[optimalSmoothingFrames];
        heldVelocities = new Vector3[optimalSmoothingFrames];
        heldAngularVelocities = new Vector3[optimalSmoothingFrames];
    }


    VRC_Pickup.PickupHand currentHand;

    public void Update()
    {
        if (pickupStay)
        {
            if (pickup.IsHeld)
            {
                currentHand = pickup.currentHand;
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

                heldAngularVelocities[heldFrames % frametotal] = (rotationAxis * angleDegrees * Mathf.Deg2Rad) / Time.deltaTime;
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

                if (Game.AxeState == STATE_TAKEN)
                {
                    Throw();
                }
            }
        }
    }

    public override void OnDrop()
    {
        if (Game.NoGoZoneScript.LocalPlayerInZone)
        {
            Debug.Log("Player was in the no go zone. Call foul.");
            foul = true;
        }

        var sqrDistanceFromHoldZone = (Game.AxeHoldZone.ClosestPoint(transform.position) - transform.position).sqrMagnitude;
        if (sqrDistanceFromHoldZone > 1e-10)
        {
            Debug.Log("Player released axe outside playable area. Call foul.");
            foul = true;
        }
    }

    // Custom Throw function so that it takes place at the right time when the pickup is dropped
    // and after computing the velocities for the frame
    private void Throw()
    {
        // an axe thrown from 3 meters at this speed will hit the target in 0.37 seconds, turning 1 rotation
        const float OPTIMAL_VELOCITY = 7.5f;
        const float OPTIMAL_ANGVELOCITY = 16.6f;

        Debug.Log("Player throwing axe.");
        if (heldFrames < frameTimes.Length || !inPlayableZone)
        {
            // do nothing
        }
        else
        {
            if (Networking.LocalPlayer.IsUserInVR())
            {
                if (useSmoothVelocity)
                {
                    Debug.Log($"RB velocity before smoothing: {rb.velocity} {rb.angularVelocity}");

                    ComputeVelocityRegression();

                    rb.velocity = smoothVelocity * velMult;
                    rb.angularVelocity = smoothAngularVelocity * angVelMult;
                }

                if (useAssist)
                {
#if UNITY_ANDROID
                    // Increasing difficulty value makes the assist weaker and the throwing more dependant on skill
                    const float DIFFICULTY = 1.5f;
                    const float ANG_DIFFICULTY = 1.25f;
                    const float ANG_MULT = 4f;
#else
                    // Increasing difficulty value makes the assist weaker and the throwing more dependant on skill
                    const float DIFFICULTY = 2f;
                    const float ANG_DIFFICULTY = 2f;
                    const float ANG_MULT = 2f;
#endif

                    Debug.Log($"RB velocity before assist: {rb.velocity} ({rb.velocity.magnitude}) {rb.angularVelocity} ({rb.angularVelocity.magnitude})");
                    rb.velocity = computeAssist(rb.velocity, OPTIMAL_VELOCITY, DIFFICULTY, 1);
                    // Compute a target velocity based on the adjusted velocity. The effect is that ths will try to match the spin closer to 1 rotation
                    rb.angularVelocity = computeAssist(rb.angularVelocity, OPTIMAL_ANGVELOCITY / OPTIMAL_VELOCITY * rb.velocity.magnitude, ANG_DIFFICULTY, ANG_MULT);
                    Debug.Log($"RB velocity after assist: {rb.velocity} ({rb.velocity.magnitude}) {rb.angularVelocity} ({rb.angularVelocity.magnitude})");
                }
            }
            else
            {
                // Give desktop users a stupid random aimbot, lol
                var playerLook = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);

                var randomThrow = playerLook.rotation * Vector3.forward * UnityEngine.Random.Range(OPTIMAL_VELOCITY * 0.9f, OPTIMAL_VELOCITY * 1.1f);
                var randomSpin = playerLook.rotation * Vector3.right * UnityEngine.Random.Range(OPTIMAL_ANGVELOCITY * 0.9f, OPTIMAL_ANGVELOCITY * 1.1f);

                rb.velocity = computeAssist(randomThrow, OPTIMAL_VELOCITY, 2, 1);
                rb.angularVelocity = computeAssist(randomSpin, OPTIMAL_ANGVELOCITY, 1.5f, 1);
            }
        }
    }

    private Vector3 computeAssist(Vector3 vec, float optimal, float difficulty, float mult)
    {
        // exaggerate this distance highly and lerp towards the optimal velocity, more closer = more power
        var relativeDiffToOptimal = Mathf.Abs(optimal - vec.magnitude) / optimal;

        // higher values = more assist. at relative diff = 0 this returns 0;
        var assistPower = Mathf.Pow(1 - Mathf.Clamp(relativeDiffToOptimal / mult, 0, 1), difficulty);
        Debug.Log($"Assist amount: off by {relativeDiffToOptimal} => {assistPower} assistPower");
        return Vector3.Lerp(vec, optimal * vec.normalized, assistPower);
    }

    public void _Reset()
    {
        Debug.Log("Resetting Thrown Axe");
        SetState(STATE_WAITING);
        pickup.Drop();
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
            Debug.Log($"Historical velocities[{i}] ({times[i]}):  {velocities[i]} {angularVelocities[i]}");
        }

        var resultVelocity = ComputeRegression(velocities, times);
        var resultAngularVelocity = ComputeRegression(angularVelocities, times);

        Debug.Log($"Smoothed RB velocity: {resultVelocity} {resultAngularVelocity}");

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
            sumx += x[i];
            sumx2 += x[i] * x[i];
            sumxy += x[i] * y[i];
            sumy += y[i];
            sumy2 += new Vector3(y[i].x * y[i].x, y[i].y * y[i].y, y[i].z * y[i].z);
        }

        float denom = (n * sumx2 - (sumx * sumx));
        if (denom == 0)
        {
            // singular matrix. can't solve the problem.
            Debug.Log("Unable to compute regression for throw.");
            return y[n - 1];
        }

        Vector3 m = (n * sumxy - sumx * sumy) / denom;
        Vector3 b = (sumy * sumx2 - sumx * sumxy) / denom;

        return m * x[n - 1] + b;
    }
}
