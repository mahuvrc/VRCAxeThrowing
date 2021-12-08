
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class AxeThrowingGame : UdonSharpBehaviour
{
    private int MAX_AXE_COUNT = 10;

    public Collider AxeStickZone;

    public Collider AxeHoldZone;

    public NoGoZone NoGoZoneScript;

    public TextMeshPro TargetTitleTxt;

    public TextMeshProUGUI MenuTitleTxtugui;

    public TextMeshProUGUI MenuStatusTxtugui;

    public TextMeshProUGUI MenuPlayerOwnedTxtugui;

    public GameObject MenuLocked;

    public GameObject MenuUnlocked;

    public GameObject ForceTakeMenu;

    public ThrowingAxe Axe;

    public AxeSpawner AxeHolster;

    public GameObject[] GameModeButtons;

    public UdonSharpBehaviour[] GameModes;

    public UdonSharpBehaviour ActiveGameMode;

    [UdonSynced]
    public int ActiveGameModeId;

    [UdonSynced]
    public int AxeState;

    [UdonSynced]
    public long lockDecayTime;

    [UdonSynced]
    public bool InProgress;

    void Start()
    {
        Axe.Game = this;
        AxeHolster.Game = this;

        for (var i = 0; i < GameModeButtons.Length; i++)
        {
            if (i < GameModes.Length)
            {
                var gameMode = GameModes[i];
                gameMode.SetProgramVariable("Game", this);
                gameMode.gameObject.SetActive(true);

                var name = (string)gameMode.GetProgramVariable("DisplayName");
                var btn = GameModeButtons[i];
                btn.SetActive(true);

                var btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
                btnText.text = name;
            }
            else
            {
                GameModeButtons[i].SetActive(false);
            }
        }

        ActiveGameMode = GameModes[ActiveGameModeId];
        ToggleGameModeVisibility();

        SendCustomEventDelayedSeconds(nameof(_Initialize), 2.0f, VRC.Udon.Common.Enums.EventTiming.Update);
    }

    private void SetDefaults()
    {
        lockDecayTime = Networking.GetNetworkDateTime().Ticks;
        InProgress = false;
    }

    public void ScoreAxe()
    {
        ActiveGameMode.SendCustomEvent("_ScoreAxe");
    }

    // Utility method used by multiple game modes
    public bool IsAxeInSphereScoreZone(SphereCollider sphereCollider)
    {

        var collisions = Physics.OverlapSphere(sphereCollider.transform.position + sphereCollider.center, sphereCollider.radius * sphereCollider.transform.lossyScale.x, ~0, QueryTriggerInteraction.Collide);
        foreach (var collider in collisions)
        {
            if (Axe.scoreCollider == collider)
            {
                return true;
            }
        }

        return false;
    }

    public void _AxeTaken()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        ActiveGameMode.SendCustomEvent("_AxeTaken");

        InProgress = true;
        LockBoard(40);
        OwnerUpdateState();
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

    public void ChangePlayer(string playerName)
    {
        VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        VRCPlayerApi.GetPlayers(players);

        foreach (var player in players)
        {
            if (player.IsValid() && player.displayName == playerName)
            {
                SetOwner(player);
            }
        }
    }

    public void _ConsumeAxe()
    {
        ActiveGameMode.SendCustomEvent("_ConsumeAxe");
        ActiveGameMode.SendCustomEventDelayedSeconds("_NextRound", 2.0f);
    }

    public void _Initialize()
    {
        if (Networking.IsMaster && Networking.IsOwner(gameObject))
        {
            Debug.Log("Initializing game");
            _Reset();
            Debug.Log("Game initialized.");
        }
    }

    public void _Reset()
    {
        Debug.Log("Resetting game");
        SetDefaults();
        ActiveGameMode.SendCustomEvent("_Reset");
        OwnerUpdateState();
        Axe._Reset();
    }

    public void _SetGameMode0()
    {
        SetGameMode(0);
    }

    public void _SetGameMode1()
    {
        SetGameMode(1);
    }

    public void _SetGameMode2()
    {
        SetGameMode(2);
    }

    public void _SetGameMode3()
    {
        SetGameMode(3);
    }

    public void SetGameMode(int gameMode)
    {
        SetOwner(Networking.LocalPlayer);
        ActiveGameMode = GameModes[gameMode];
        ActiveGameModeId = gameMode;
        _Reset();
    }

    public void SetAxeState(int state)
    {
        AxeState = state;
        OwnerUpdateState();
    }

    private void OwnerUpdateState()
    {
        SetOwner(Networking.LocalPlayer);

        RequestSerialization();
        DisplayState();
    }

    private void SetOwner(VRCPlayerApi player)
    {
        Networking.SetOwner(player, gameObject);
        Networking.SetOwner(player, Axe.gameObject);
        Networking.SetOwner(player, AxeHolster.gameObject);

        foreach (var gameMode in GameModes)
        {
            Networking.SetOwner(player, gameMode.gameObject);
        }
    }

    public override void OnDeserialization()
    {
        ActiveGameMode = GameModes[ActiveGameModeId];
        DisplayState();
    }

    private void DisplayState()
    {
        Axe.TransitionState(AxeState);

        ToggleGameModeVisibility();
        DisplayLockStatus();
    }

    private void ToggleGameModeVisibility()
    {
        foreach (var gameMode in GameModes)
        {
            if (gameMode != ActiveGameMode)
                gameMode.SendCustomEvent("_Hide");
            else
                gameMode.SendCustomEvent("_Show");
        }
    }

    public void _PlayerInZoneSnailUpdate()
    {
        DisplayLockStatus();
    }

    public void SetTitle(string title)
    {
        TargetTitleTxt.text = title;
    }

    public void SetMenuTitle(string title)
    {
        MenuTitleTxtugui.text = title;
    }

    public void SetMenuStatusText(string text)
    {
        MenuStatusTxtugui.text = text;
    }

    private bool ActiveGameModeHasOpening()
    {
        return (bool)ActiveGameMode.GetProgramVariable("PlayerOpening");
    }

    public void DisplayLockStatus()
    {
        var localSuper = Networking.LocalPlayer.isInstanceOwner || Networking.LocalPlayer.isMaster;

        var locked = !Networking.IsOwner(gameObject) && !ActiveGameModeHasOpening();
        var powerToUnlock =  localSuper || Networking.GetNetworkDateTime().Ticks > lockDecayTime;

        MenuPlayerOwnedTxtugui.text = $"(locked by {Networking.GetOwner(gameObject).displayName})";

        MenuLocked.SetActive(locked);
        MenuUnlocked.SetActive(!locked);
        ForceTakeMenu.SetActive(powerToUnlock);

        Axe.SetOwnerLocked(locked);
        AxeHolster.SetOwnerLocked(locked);
    }
}
