
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class AxeThrowingGame : UdonSharpBehaviour
{
    private int MAX_AXE_COUNT = 10;

    public Collider AxeStickZone;

    public Collider AxeHoldZone;

    public NoGoZone NoGoZoneScript;

    public SphereCollider[] ScoreZones;

    public SphereCollider LeftKillshotZone;

    public SphereCollider RightKillshotZone;

    public TextMeshPro AxeCountTxt;

    public TextMeshPro KsDisplayTxt;

    public TextMeshPro TargetTitleTxt;

    public TextMeshPro ScoreTxt;

    public TextMeshProUGUI MenuPlayerNameTxtugui;

    public TextMeshProUGUI MenuStatusTxtugui;

    public TextMeshProUGUI MenuPlayerOwnedTxtugui;

    public GameObject MenuLocked;

    public GameObject MenuUnlocked;

    public GameObject ForceTakeMenu;

    public Button KillshotButton;

    public ThrowingAxe Axe;

    public AxeSpawner AxeHolster;

    [SerializeField]
    private string AdminName;

    [UdonSynced]
    public string PlayerName;

    [UdonSynced]
    public int Score;

    [UdonSynced]
    public int AxeCount;

    [UdonSynced]
    public bool LeftKillshotActive;

    [UdonSynced]
    public bool RightKillshotActive;

    [UdonSynced]
    public bool KillshotCalled;
    private bool killshotActiveForScore;

    [UdonSynced]
    public int KillshotsRemaining;

    [UdonSynced]
    public int AxeState;

    [UdonSynced]
    public long lockDecayTime;

    void Start()
    {
        if (ScoreZones.Length != 6)
        {
            Debug.LogError("Must have 6 score zones");
            var behavior = (UdonBehaviour)this.GetComponent(typeof(UdonBehaviour));
            behavior.enabled = false;
        }

        DisplayGameState();
        SendCustomEventDelayedSeconds(nameof(_Initialize), 2.0f, VRC.Udon.Common.Enums.EventTiming.Update);
    }

    private void SetDefaults()
    {
        lockDecayTime = Networking.GetNetworkDateTime().Ticks;

        PlayerName = null;
        Score = 0;
        AxeCount = MAX_AXE_COUNT;
        KillshotCalled = false;
        KillshotsRemaining = 2;
        LeftKillshotActive = true;
        RightKillshotActive = true;
    }

    public void ScoreAxe(Collider axeScoreableCollider)
    {
        if (killshotActiveForScore)
        {
            if (LeftKillshotActive)
            {
                if (isAxeInScoreZone(axeScoreableCollider, LeftKillshotZone))
                {
                    LeftKillshotActive = false;
                    AddScore(8);
                    return;
                }
            }
            if (RightKillshotActive)
            {
                if (isAxeInScoreZone(axeScoreableCollider, RightKillshotZone))
                {
                    RightKillshotActive = false;
                    AddScore(8);
                    return;
                }
            }
            return;
        }
        else
        {
            // assume the score zones are ordered 6 points to 1 point
            // score is highest touching score value.
            for (int i = 0; i < 6; i++)
            {
                if (isAxeInScoreZone(axeScoreableCollider, ScoreZones[i]))
                {
                    AddScore(6 - i);
                    return;
                }
            }
        }
    }

    private bool isAxeInScoreZone(Collider axeScoreableCollider, SphereCollider sphereCollider)
    {
        var collisions = Physics.OverlapSphere(sphereCollider.transform.position + sphereCollider.center, sphereCollider.radius, ~0, QueryTriggerInteraction.Collide);
        foreach (var collider in collisions)
        {
            if (axeScoreableCollider == collider)
            {
                return true;
            }
        }

        return false;
    }

    private void AddScore(int score)
    {
        Score += score;

        LockBoard(40);
        UpdateGameState();
    }

    public void _AxeTaken()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        PlayerName = Networking.LocalPlayer.displayName;
        killshotActiveForScore = false;

        LockBoard(40);
        UpdateGameState();
    }

    private float lockNerf = 1;
    private float lockoutTime = 0;
    private void LockBoard(int seconds)
    {
        // prevents a player from taking all the axes from every board and trolling a whole public lobby
        var lastLockedBoard = Networking.LocalPlayer.GetPlayerTag("AxeThrowingGameId");
        var currentBoard = Networking.GetUniqueName(gameObject);
        Networking.LocalPlayer.SetPlayerTag("AxeThrowingGameId", currentBoard);

        if (string.IsNullOrEmpty(lastLockedBoard) || lastLockedBoard == currentBoard)
        {
            if (lockoutTime < Time.time)
            {
                Debug.Log("Resetting lock nerf after timeout.");
                lockNerf = 1;
            }
        }
        else
        {
            // accumulate stacking lockout nerf if you keep switching boards
            lockoutTime = Time.time + 120f; // reset lock nerf in 2 minutes
            lockNerf = lockNerf*2;
        }

        Debug.Log($"Locking board at nerf level {lockNerf} {Networking.GetUniqueName(gameObject)}");
        lockDecayTime = Networking.GetNetworkDateTime().AddSeconds(seconds / lockNerf).Ticks;
    }

    public void _ConsumeAxe()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);

        if (AxeCount > 0)
        {
            if (KillshotCalled)
            {
                KillshotsRemaining--;
                KillshotCalled = false;
                killshotActiveForScore = true;
            }

            AxeCount--;
            UpdateGameState();
        }
    }

    public void _Initialize()
    {
        if (Networking.IsMaster && Networking.IsOwner(gameObject))
        {
            Debug.Log("Initializing game");
            SetDefaults();
            UpdateGameState();
            Axe._Reset();
            Debug.Log("Game initialized.");
        }
    }

    public void _Reset()
    {
        Debug.Log("Resetting game");
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        SetDefaults();
        UpdateGameState();
        Axe._Reset();
    }


    public void _RespawnAxe()
    {
        if (AxeCount > 0)
        {
            Debug.Log("respawning axe");

            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            Networking.SetOwner(Networking.LocalPlayer, Axe.gameObject);
            Axe._Reset();
        }
    }

    public void _CallKillshot()
    {
        Debug.Log("Player calling killshot.");
        Networking.SetOwner(Networking.LocalPlayer, gameObject);

        if (!KillshotCalled && KillshotsRemaining > 0)
        {
            KillshotCalled = true;
        }

        UpdateGameState();
    }

    public void SetAxeState(int state)
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        AxeState = state;
        RequestSerialization();
        LocalUpdateStates();
    }

    private void UpdateGameState()
    {
        Networking.SetOwner(Networking.LocalPlayer, Axe.gameObject);
        Networking.SetOwner(Networking.LocalPlayer, AxeHolster.gameObject);
        RequestSerialization();
        LocalUpdateStates();
    }

    public override void OnDeserialization()
    {
        LocalUpdateStates();
    }

    private void LocalUpdateStates()
    {
        Axe.TransitionState(AxeState);
        DisplayGameState();
        UpdateLockStatus();
    }

    public void _PlayerInZoneSnailUpdate()
    {
        UpdateLockStatus();
    }

    private void DisplayGameState()
    {
        Debug.Log("Displaying game state");
        TargetTitleTxt.text = string.IsNullOrWhiteSpace(PlayerName) ? "AXE THROWING" : PlayerName;
        ScoreTxt.text = Score.ToString();

        AxeCountTxt.text = new string('\u25cf', MAX_AXE_COUNT - AxeCount) + new string('\u25cb', AxeCount);

        KillshotButton.interactable = !KillshotCalled && KillshotsRemaining > 0 && AxeCount > 0;

        var allowedKillshots = "Left and Right";
        if (KillshotsRemaining <= 0)
        {
            KsDisplayTxt.text = "KS: 0";
            allowedKillshots = "None";
        }
        else if (KillshotsRemaining == 1)
        {
            if (LeftKillshotActive && RightKillshotActive)
            {
                KsDisplayTxt.text = "KS: 1";
            }
            else if (LeftKillshotActive)
            {
                KsDisplayTxt.text = "KS: L";
                allowedKillshots = "Left";
            }
            else
            {
                KsDisplayTxt.text = "KS: R";
                allowedKillshots = "Right";
            }
        }
        else
        {
            KsDisplayTxt.text = "KS: 2";
        }

        MenuPlayerNameTxtugui.text = string.IsNullOrWhiteSpace(PlayerName) ? "(unoccupied)" : PlayerName;
        MenuPlayerOwnedTxtugui.text = $"(locked by {MenuPlayerNameTxtugui.text})";
        MenuStatusTxtugui.text = $"Score: {Score}\nAxes Remaining:{AxeCount}/{MAX_AXE_COUNT}\nKillshot Attempts Remaining: {KillshotsRemaining}\nAllowed killshots: {allowedKillshots}\n{(KillshotCalled ? $"<b>KILLSHOT CALLED! Hit the {allowedKillshots} blue target.</b>" : "Killshot inactive.")}";
        Debug.Log("Game state saved.");
    }

    public void UpdateLockStatus()
    {
        var localAdmin = Networking.LocalPlayer.displayName == AdminName;
        var inuseByAdmin = PlayerName == AdminName;
        var localSuper = Networking.LocalPlayer.isInstanceOwner || Networking.LocalPlayer.isMaster;

        var locked = !Networking.IsOwner(gameObject) && !string.IsNullOrEmpty(PlayerName);
        var powerToUnlock = localAdmin || (localSuper && !inuseByAdmin) || Networking.GetNetworkDateTime().Ticks > lockDecayTime;

        MenuLocked.SetActive(locked);
        MenuUnlocked.SetActive(!locked);
        ForceTakeMenu.SetActive(powerToUnlock);


        Axe.SetOwnerLocked(locked);
        AxeHolster.SetOwnerLocked(locked);
    }
}
