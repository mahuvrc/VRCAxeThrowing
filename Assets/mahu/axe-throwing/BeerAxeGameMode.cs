
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class BeerAxeGameMode : AxeThrowingGameMode
{
    public override string DisplayName { get { return "Beer Axe (2 players per board)"; } }

    public TextMeshPro Player1NameTxt;

    public TextMeshPro Player2NameTxt;

    public TextMeshPro WinText;

    public SphereCollider[] Player1CupColliders;

    public SphereCollider[] Player2CupColliders;

    public GameObject[] Player1CupIndicator;

    public GameObject[] Player2CupIndicator;

    [UdonSynced]
    public int Player1Id;

    [UdonSynced]
    public int Player2Id;

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
    public void _SetDefaults()
    {
        Player1Id = 0;
        Player2Id = 0;
        PlayerOpening = true;

        Player1CupStatus = 0;
        Player2CupStatus = 0;
    }

    public override void _ScoreAxe()
    {
        for (int i = 0; i < Player1CupColliders.Length; i++)
        {
            if (IsAxeInSphereScoreZone(Player1CupColliders[i])
                && !IsCupDisabled(Player1CupStatus, i))
            {
                Player1CupStatus = DisableCup(Player1CupStatus, i);
                ActivatePlayer(1);
                break;
            }

            if (IsAxeInSphereScoreZone(Player2CupColliders[i])
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

    private void ActivatePlayer(int playerIndex)
    {
        var localPlayerId = Networking.LocalPlayer.playerId;

        // when local player is not on the board
        if (Player1Id != localPlayerId && Player2Id != localPlayerId)
        {
            if (playerIndex == 0 && Player1Id == 0)
            {
                Player1Id = localPlayerId;
                CurrentPlayer = 0;
            }
            
            if (playerIndex == 1 && Player2Id == 0)
            {
                Player2Id = localPlayerId;
                CurrentPlayer = 1;
            }
        }

        if (Player1Id == 0 || Player2Id == 0)
        {
            PlayerOpening = true;
        }
        else
        {
            PlayerOpening = false;
        }
    }

    // Part of standard GameMode
    public override void _ConsumeAxe()
    {
        OwnerUpdateState();
    }

    // Part of standard GameMode
    public override void _AxeTaken()
    {
        OwnerUpdateState();
    }

    // Part of standard GameMode
    public override void _NextRound()
    {
        if (Player1CupStatus < 0b111111 && Player2CupStatus < 0b111111)
        {
            if (Networking.LocalPlayer.playerId == Player1Id)
            {
                CurrentPlayer = 1;
            }
            else
            {
                CurrentPlayer = 0;
            }

            OwnerUpdateState();

            // Hack to swap players at a later time to stop race conditions
            SendCustomEventDelayedSeconds(nameof(_ChangeToPlayer), 0.5f);
        }
    }

    public void _ChangeToPlayer()
    {
        game._ChangePlayer(CurrentPlayer == 0 ? Player1Id : Player2Id);
        game.Axe._Reset();
    }

    public override void _Reset()
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
        if (!IsActiveGamemode())
            return;

        var player1 = VRCPlayerApi.GetPlayerById(Player1Id);
        var player2 = VRCPlayerApi.GetPlayerById(Player2Id);
        var player1Display = (Utilities.IsValid(player1) ? player1.displayName : "(unoccupied)");
        var player2Display = (Utilities.IsValid(player2) ? player2.displayName : "(unoccupied)");


        game.SetTitle("BEER AXE");
        game.SetMenuTitle($"Beer Axe! {player1Display} vs {player2Display}");

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


        game.SetMenuStatusText(
            "Playing BEER AXE!.\n" +
            $"{player1Display}: {player1cupremaining} cups remaining\n" +
            $"{player2Display}: {player2cupremaining} cups remaining\n{playerwinstext}");
    }
}
