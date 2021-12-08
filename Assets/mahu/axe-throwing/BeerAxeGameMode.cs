
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class BeerAxeGameMode : UdonSharpBehaviour
{
    // Set by parent
    [NonSerialized]
    public AxeThrowingGame Game;

    // Part of standard GameMode
    public string DisplayName = "Beer Axe (2 players per board)";

    public TextMeshPro Player1NameTxt;

    public TextMeshPro Player2NameTxt;

    public TextMeshPro WinText;

    public SphereCollider[] Player1CupColliders;

    public SphereCollider[] Player2CupColliders;

    public GameObject[] Player1CupIndicator;

    public GameObject[] Player2CupIndicator;

    public GameObject[] ChildObjects;

    // Part of standard GameMode
    [UdonSynced]
    public bool PlayerOpening;

    [UdonSynced]
    public string Player1Name;

    [UdonSynced]
    public string Player2Name;

    [UdonSynced]
    public int CurrentPlayer;

    [UdonSynced]
    int Player1CupStatus;

    [UdonSynced]
    int Player2CupStatus;

    public void Start()
    {
        if (Player1CupColliders.Length != 6
            || Player2CupColliders.Length != 6
            || Player1CupIndicator.Length != 6
            || Player2CupIndicator.Length != 6)
        {
            Debug.LogError("Must have 6 score zones and indicators per player");
            var behavior = (UdonBehaviour)this.GetComponent(typeof(UdonBehaviour));
            behavior.enabled = false;
        }
    }

    // Part of standard GameMode
    public void _Show()
    {
        foreach (var item in ChildObjects)
        {
            item.SetActive(true);
        }
    }

    // Part of standard GameMode
    public void _Hide()
    {
        foreach (var item in ChildObjects)
        {
            item.SetActive(false);
        }
    }

    // Part of standard GameMode
    public void _SetDefaults()
    {
        Player1Name = null;
        Player2Name = null;
        PlayerOpening = true;

        Player1CupStatus = 0;
        Player2CupStatus = 0;
    }

    public void _ScoreAxe()
    {
        for (int i = 0; i < Player1CupColliders.Length; i++)
        {
            if (Game.IsAxeInSphereScoreZone(Player1CupColliders[i])
                && !IsCupDisabled(Player1CupStatus, i))
            {
                Player1CupStatus = DisableCup(Player1CupStatus, i);
                ActivatePlayer(1);
                break;
            }

            if (Game.IsAxeInSphereScoreZone(Player2CupColliders[i])
                && !IsCupDisabled(Player2CupStatus, i))
            {
                Player2CupStatus = DisableCup(Player2CupStatus, i);
                ActivatePlayer(0);
                break;
            }
        }

        OwnerUpdateState();
    }

    private bool IsCupDisabled(int playerCuprepr, int i)
    {
        return (playerCuprepr & (1 << i)) > 0;
    }

    private int DisableCup(int playerCuprepr, int i)
    {
        return playerCuprepr | (1 << i);
    }

    private void ActivatePlayer(int player)
    {
        var localPlayer = Networking.LocalPlayer.displayName;

        // when local player is not on the board
        if (Player1Name != localPlayer && Player2Name != localPlayer)
        {
            if (player == 0 && string.IsNullOrEmpty(Player1Name))
            {
                Player1Name = localPlayer;
                CurrentPlayer = 0;
            }
            
            if (player == 1 && string.IsNullOrEmpty(Player2Name))
            {
                Player2Name = localPlayer;
                CurrentPlayer = 1;
            }
        }

        if (string.IsNullOrEmpty(Player1Name) || string.IsNullOrEmpty(Player2Name))
        {
            PlayerOpening = true;
        }
        else
        {
            PlayerOpening = false;
        }
    }

    // Part of standard GameMode
    public void _ConsumeAxe()
    {
        OwnerUpdateState();
    }

    // Part of standard GameMode
    public void _AxeTaken()
    {
        OwnerUpdateState();
    }

    // Part of standard GameMode
    public void _NextRound()
    {
        if (Player1CupStatus < 0b111111 && Player2CupStatus < 0b111111)
        {
            if (Networking.LocalPlayer.displayName == Player1Name)
            {
                CurrentPlayer = 1;
            }
            else
            {
                CurrentPlayer = 0;
            }

            OwnerUpdateState();
            Game.Axe._Reset();

            // Hack to swap players at a later time to stop race conditions
            SendCustomEventDelayedSeconds("_ChangeToPlayer", 0.5f);
        }
    }

    public void _ChangeToPlayer()
    {
        Game.ChangePlayer(CurrentPlayer == 0 ? Player1Name : Player2Name);
    }

    // Part of standard GameMode
    public void _Reset()
    {
        _SetDefaults();
        OwnerUpdateState();
    }

    private void OwnerUpdateState()
    {
        RequestSerialization();
        DisplayGameState();
    }

    public override void OnDeserialization()
    {
        DisplayGameState();
    }

    private void DisplayGameState()
    {
        var player1Display = (string.IsNullOrWhiteSpace(Player1Name) ? "(unoccupied)" : Player1Name);
        var player2Display = (string.IsNullOrWhiteSpace(Player2Name) ? "(unoccupied)" : Player2Name);

        Game.SetTitle("BEER AXE");
        Game.SetMenuTitle($"Beer Axe! {player1Display} vs {player2Display}");

        Player1NameTxt.text = player1Display;
        Player2NameTxt.text = player2Display;

        var player1cupremaining = 6;
        var player2cupremaining = 6;

        for (int i = 0; i < 6; i++)
        {
            if (IsCupDisabled(Player1CupStatus, i))
            {
                Player1CupIndicator[i].SetActive(true);
                player1cupremaining--;
            }
            else
            {
                Player1CupIndicator[i].SetActive(false);
            }

            if (IsCupDisabled(Player2CupStatus, i))
            {
                Player2CupIndicator[i].SetActive(true);
                player2cupremaining--;
            }
            else
            {
                Player2CupIndicator[i].SetActive(false);
            }
        }

        var playerwinstext = "";
        if (player1cupremaining <= 0)
        {
            playerwinstext = player2Display + " Wins!";
        }
        if (player2cupremaining <= 0)
        {
            playerwinstext = player1Display + " Wins!";
        }

        WinText.text = playerwinstext;

        Game.SetMenuStatusText(
            "Playing BEER AXE!.\n" +
            $"{player1Display}: {player1cupremaining} cups remaining\n" +
            $"{player2Display}: {player2cupremaining} cups remaining\n{playerwinstext}");

        
    }
}
