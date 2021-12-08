
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class WatlGameMode : UdonSharpBehaviour
{
    private int MAX_AXE_COUNT = 10;

    // Set by parent
    [NonSerialized]
    public AxeThrowingGame Game;

    // Part of standard GameMode
    public string DisplayName = "Standard Target (WATL Rules)";

    public SphereCollider[] ScoreZones;

    public SphereCollider LeftKillshotZone;

    public SphereCollider RightKillshotZone;

    public TextMeshPro AxeCountTxt;

    public TextMeshPro KsDisplayTxt;

    public TextMeshPro ScoreTxt;

    public Button KillshotButton;

    public GameObject[] ChildObjects;

    // Part of standard GameMode
    [UdonSynced]
    public bool PlayerOpening;

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

    [UdonSynced]
    public int KillshotsRemaining;

    public void Start()
    {
        if (ScoreZones.Length != 6)
        {
            Debug.LogError("Must have 6 score zones");
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
        PlayerName = null;
        PlayerOpening = true;
        Score = 0;
        AxeCount = MAX_AXE_COUNT;
        KillshotCalled = false;
        KillshotsRemaining = 2;
        LeftKillshotActive = true;
        RightKillshotActive = true;
    }

    public void _CallKillshot()
    {
        if (KillshotsRemaining > 0 && AxeCount > 0 && !KillshotCalled)
        {
            KillshotCalled = true;
        }
    }

    // Part of standard GameMode
    public void _ScoreAxe()
    {
        if (KillshotCalled)
        {
            if (LeftKillshotActive)
            {
                if (Game.IsAxeInSphereScoreZone(LeftKillshotZone))
                {
                    LeftKillshotActive = false;
                    AddScore(8);
                    return;
                }
            }
            if (RightKillshotActive)
            {
                if (Game.IsAxeInSphereScoreZone(RightKillshotZone))
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
                if (Game.IsAxeInSphereScoreZone(ScoreZones[i]))
                {
                    AddScore(6 - i);
                    return;
                }
            }
        }
    }

    private void AddScore(int score)
    {
        Score += score;
        OwnerUpdateState();
    }

    // Part of standard GameMode
    public void _ConsumeAxe()
    {
        PlayerName = Networking.LocalPlayer.displayName;
        PlayerOpening = false;

        if (AxeCount > 0)
        {
            if (KillshotCalled)
            {
                KillshotsRemaining--;
            }

            AxeCount--;
        }

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
        if (AxeCount > 0)
        {
            KillshotCalled = false;
            Game.Axe._Reset();
        }

        OwnerUpdateState();
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
        Game.SetTitle(string.IsNullOrWhiteSpace(PlayerName) ? "AXE THROWING" : PlayerName);

        Game.SetMenuTitle("Player: " + (string.IsNullOrWhiteSpace(PlayerName) ? "(unoccupied)" : PlayerName));

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

        Game.SetMenuStatusText(
            "Playing with Standard WATL rules.\n" +
            $"Score: {Score}\n" +
            $"Axes Remaining:{AxeCount}/{MAX_AXE_COUNT}\n" + 
            $"Killshot Attempts Remaining: {KillshotsRemaining}\nAllowed killshots: {allowedKillshots}\n" + 
            $"{(KillshotCalled ? $"<b>KILLSHOT CALLED! Hit the {allowedKillshots} blue target.</b>" : "Killshot inactive.")}");
    }
}
