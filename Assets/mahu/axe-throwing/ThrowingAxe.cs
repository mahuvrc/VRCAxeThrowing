
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

public class ThrowingAxe : UdonSharpBehaviour
{
    const int MOTIONSMOOTHFRAMES = 5;

    const int STATE_WAITING = 0;
    const int STATE_TAKEN = 1;
    const int STATE_USED = 2;

    public AxeThrowingGame Game;
    public Transform edgePosition;
    public Collider scoreCollider;
    public AudioSource audio;
    public AudioClip woodChop;
    public AudioClip wompWomp;

    public bool useSmoothVelocity;
    public bool useAssist;

    public float velMult;
    public float angVelMult;

    private VRCPickup pickup;
    private VRCObjectSync objectsync;
    private Rigidbody rb;

    private bool pickupStay;
    private bool foul;
    private float triggerHeldPeak;
    private int heldFrames;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private float[] frameTimes = new float[MOTIONSMOOTHFRAMES];
    private Vector3[] heldVelocities = new Vector3[MOTIONSMOOTHFRAMES];
    private Vector3[] heldAngularVelocities = new Vector3[MOTIONSMOOTHFRAMES];


    private Vector3 smoothVelocity, smoothAngularVelocity;

    public void Start()
    {
        pickup = (VRCPickup)GetComponent(typeof(VRCPickup));
        objectsync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
        rb = GetComponent<Rigidbody>();
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
            pickup.Drop();

            if (!foul)
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "ChopSound");
                Game.ScoreAxe(scoreCollider);
            }
            else
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "ErrorSound");
            }
        }
    }

    public void ChopSound()
    {
        audio.PlayOneShot(woodChop);
    }

    public void ErrorSound()
    {
        audio.PlayOneShot(wompWomp);
    }

    // hack for exit trigger
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
                SendCustomEventDelayedSeconds(nameof(_ResetDelay), 2f);
                Game._ConsumeAxe();

                if (pickup.IsHeld)
                {
                    pickup.Drop();
                }
            }
        }
    }

    public void SetState(int state)
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        Game.SetAxeState(state);
    }

    public void TransitionState(int state)
    {
        Debug.Log("Axe transitioning to state " + state);
        switch (state)
        {
            case STATE_WAITING:
                if (Networking.IsOwner(gameObject))
                {
                    objectsync.SetKinematic(true);
                }
                pickup.pickupable = true;
                break;
            case STATE_TAKEN:
                if (Networking.IsOwner(gameObject))
                {
                    objectsync.SetKinematic(false);
                }
                pickup.pickupable = Networking.IsOwner(gameObject);
                break;
            case STATE_USED:
                pickup.pickupable = false;
                break;
            default:
                break;
        }
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
    }

    public void Update()
    {
        if (pickupStay)
        {
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

            if (heldFrames > 0)
            {
                heldVelocities[heldFrames % MOTIONSMOOTHFRAMES] = (transform.position - lastPosition) / Time.deltaTime;

                float angleDegrees;
                Vector3 rotationAxis;

                (transform.rotation * Quaternion.Inverse(lastRotation)).ToAngleAxis(out angleDegrees, out rotationAxis);

                heldAngularVelocities[heldFrames % MOTIONSMOOTHFRAMES] = (rotationAxis * angleDegrees * Mathf.Deg2Rad) / Time.deltaTime;
            }

            frameTimes[heldFrames % MOTIONSMOOTHFRAMES] = Time.deltaTime;

            lastPosition = transform.position;
            lastRotation = transform.rotation;

            if (pickup.IsHeld)
            {
                heldFrames++;
            }
            else
            {
                pickupStay = false;
                Throw();
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
    }

    // Custom Throw function so that it takes place at the right time when the pickup is dropped
    // and after computing the velocities for the frame
    private void Throw()
    {
        Debug.Log("Player throwing axe.");

        if (heldFrames < MOTIONSMOOTHFRAMES || !inPlayableZone)
        {
            // do nothing
        }
        else
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
                // an axe thrown from 3 meters at this speed will hit the target in 0.37 seconds, turning 1 rotation
                const float OPTIMAL_VELOCITY = 7.5f;
                const float OPTIMAL_ANGVELOCITY = 17.6f;

                if (Networking.LocalPlayer.IsUserInVR())
                {
                    // Increasing difficulty value makes the assist weaker and the throwing more dependant on skill
                    const float DIFFICULTY = 1.75f;
                    const float ANG_DIFFICULTY = 0.75f;

                    Debug.Log($"RB velocity before assist: {rb.velocity} ({rb.velocity.magnitude}) {rb.angularVelocity} ({rb.angularVelocity.magnitude})");
                    rb.velocity = computeAssist(rb.velocity, OPTIMAL_VELOCITY, DIFFICULTY);
                    rb.angularVelocity = computeAssist(rb.angularVelocity, OPTIMAL_ANGVELOCITY, ANG_DIFFICULTY);
                    Debug.Log($"RB velocity after assist: {rb.velocity} ({rb.velocity.magnitude}) {rb.angularVelocity} ({rb.angularVelocity.magnitude})");
                }
                else
                {
                    // Give desktop users a stupid random aimbot, lol
                    var playerLook = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                    
                    var randomThrow = playerLook.rotation * Vector3.forward * UnityEngine.Random.Range(OPTIMAL_VELOCITY * 0.9f, OPTIMAL_VELOCITY * 1.1f);
                    var randomSpin = playerLook.rotation * Vector3.right * UnityEngine.Random.Range(OPTIMAL_ANGVELOCITY * 0.9f, OPTIMAL_ANGVELOCITY * 1.1f);

                    rb.velocity = computeAssist(randomThrow, OPTIMAL_VELOCITY, 2);
                    rb.angularVelocity = computeAssist(randomSpin, OPTIMAL_ANGVELOCITY, 1.5f);
                }
            }
        }
    }

    private Vector3 computeAssist(Vector3 vec, float optimal, float difficulty)
    {
        // exaggerate this distance highly and lerp towards the optimal velocity, more closer = more power
        var relativeDiffToOptimal = Mathf.Abs(optimal - vec.magnitude) / optimal;

        // higher values = more assist. at relative diff = 0 this returns 0;
        var assistPower = Mathf.Pow(1 - Mathf.Clamp(relativeDiffToOptimal, 0, 1), difficulty);
        Debug.Log($"Assist amount: off by {relativeDiffToOptimal} => {assistPower} assistPower");
        return Vector3.Lerp(vec, optimal * vec.normalized, assistPower);
    }

    public void _ResetDelay()
    {
        if (Game.AxeCount > 0)
        {
            _Reset();
        }
    }

    public void _Reset()
    {
        Debug.Log("Resetting Thrown Axe");
        SetState(STATE_WAITING);
        pickup.Drop();
        objectsync.Respawn();
    }

    private void ComputeVelocityRegression()
    {
        var velocities = new Vector3[MOTIONSMOOTHFRAMES];
        var angularVelocities = new Vector3[MOTIONSMOOTHFRAMES];
        var times = new float[MOTIONSMOOTHFRAMES];

        for (int i = 0; i < MOTIONSMOOTHFRAMES; i++)
        {
            // array wraps around so we can just add
            var curFrameIdx = (heldFrames + 1 + i) % MOTIONSMOOTHFRAMES;
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
        // this is called linear regression or something
        // it's better than averaging the velocity frame to frame especially at low frame rates
        // and will smooth out noise caused by fluctuations frame to frame

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
