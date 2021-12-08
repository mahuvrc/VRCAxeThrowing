
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class BeerAxeSingleGameMode : UdonSharpBehaviour
{
    // Set by parent
    [NonSerialized]
    public AxeThrowingGame Game;

    // Part of standard GameMode
    public string DisplayName = "Beer Axe (1 player per board)";

    public TextMeshPro WinText;

    public SphereCollider[] Player1CupColliders;

    public GameObject[] Player1CupIndicator;

    public GameObject[] ChildObjects;

    // Part of standard GameMode
    [UdonSynced]
    public bool PlayerOpening;

    [UdonSynced]
    public string Player1Name;

    [UdonSynced]
    int Player1CupStatus;

    public void Start()
    {
        if (Player1CupColliders.Length != 6
            || Player1CupIndicator.Length != 6)
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
        PlayerOpening = true;

        Player1CupStatus = 0;
    }

    public void _ScoreAxe()
    {
        for (int i = 0; i < Player1CupColliders.Length; i++)
        {
            if (Game.IsAxeInSphereScoreZone(Player1CupColliders[i])
                && !IsCupDisabled(Player1CupStatus, i))
            {
                Player1CupStatus = DisableCup(Player1CupStatus, i);
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

    // Part of standard GameMode
    public void _ConsumeAxe()
    {
        OwnerUpdateState();
        PlayerOpening = false;
        Player1Name = Networking.LocalPlayer.displayName;
    }

    // Part of standard GameMode
    public void _AxeTaken()
    {
        OwnerUpdateState();
    }

    // Part of standard GameMode
    public void _NextRound()
    {
        if (Player1CupStatus < 0b111111)
        {
            OwnerUpdateState();
            Game.Axe._Reset();
        }
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

        Game.SetTitle(string.IsNullOrWhiteSpace(Player1Name) ? "PLAY BEER AXE" : Player1Name);
        Game.SetMenuTitle($"Beer Axe! {player1Display}");

        var player1cupremaining = 6;

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
        }

        WinText.text = player1cupremaining <= 0 ? "YOU WIN" : "";
        Game.SetMenuStatusText(
            "Playing BEER AXE!.\n" +
            $"Opponent has {player1cupremaining} cups remaining\n");


    }
}
