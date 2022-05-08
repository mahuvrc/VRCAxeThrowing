
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class WatlGameMode : AxeThrowingGameMode
{
    private int MAX_AXE_COUNT = 10;

    public AnimatedGameText gameText;

    // Part of standard GameMode
    public override string DisplayName { get { return "Standard Target"; } }

    public SphereCollider[] ScoreZones;

    public SphereCollider LeftKillshotZone;

    public SphereCollider RightKillshotZone;

    public TextMeshPro AxeCountTxt;

    public TextMeshPro ScoreTxt;

    [Header("Optional Components")]
    [Tooltip("Use to set up some kind of scoreboard/currency/notification integration.")]
    public ScoreCallback scoreCallback;

    [UdonSynced, NonSerialized]
    public string PlayerName;

    [UdonSynced, NonSerialized]
    public int Score;

    [UdonSynced, NonSerialized]
    public int AxeCount;

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
    public void _SetDefaults()
    {
        PlayerName = null;
        PlayerOpening = true;
        Score = 0;
        AxeCount = MAX_AXE_COUNT;
    }

    //public void _CallKillshot()
    //{
    //    if (KillshotsRemaining > 0 && AxeCount > 0 && !KillshotCalled)
    //    {
    //        KillshotConsumed = false;
    //        KillshotCalled = true;

    //        string side;
    //        if (LeftKillshotActive && RightKillshotActive)
    //            side = "any";
    //        else if (LeftKillshotActive)
    //            side = "left";
    //        else
    //            side = "right";

    //        gameText._PlayText($"Killshot Active! Hit {side} blue killshot zone.", 0.25f);
    //        OwnerUpdateState();
    //    }
    //}

    // Part of standard GameMode
    public override void _ScoreAxe()
    {
        //if (KillshotCalled || KillshotConsumed)
        //{
        //    if (LeftKillshotActive)
        //    {
        if (IsAxeInSphereScoreZone(LeftKillshotZone))
        {
            //LeftKillshotActive = false;
            AddScore(8);
            gameText._PlayText($"+8 Killshot!", 0.75f);
            return;
        }
        //    }
        //    if (RightKillshotActive)
        //    {
        if (IsAxeInSphereScoreZone(RightKillshotZone))
        {
            //RightKillshotActive = false;
            AddScore(8);
            gameText._PlayText($"+8 Killshot!", 0.75f);
            return;
        }
        //    }

        //    gameText._PlayText($"Killshot Miss", 0.75f);
        //    return;
        //}
        //else
        //{

        // assume the score zones are ordered 6 points to 1 point
        // score is highest touching score value.
        for (int i = 0; i < 6; i++)
        {
            if (IsAxeInSphereScoreZone(ScoreZones[i]))
            {
                AddScore(6 - i);
                gameText._PlayText($"+{6-i}");
                return;
            }
        }
        gameText._PlayText($"Miss");
    }

    private void AddScore(int score)
    {
        Score += score;
        OwnerUpdateState();
    }

    // Part of standard GameMode
    public override void _ConsumeAxe()
    {
        PlayerName = Networking.LocalPlayer.displayName;
        PlayerOpening = false;

        if (AxeCount > 0)
        {
            //if (KillshotCalled)
            //{
            //    KillshotsRemaining--;
            //    KillshotCalled = false;
            //    KillshotConsumed = true;
            //}

            AxeCount--;
        }

        OwnerUpdateState();
    }

    public override void _AxeTaken()
    {
        OwnerUpdateState();
    }

    public override void _NextRound()
    {
        if (AxeCount > 0)
        {
            game.Axe._Reset();
            //KillshotConsumed = false;
        }
        else
        {
            if (scoreCallback != null)
            {
                scoreCallback._OnPlayerScore(Score);
            }

            gameText._PlayText($"Match Over", 0.25f);
        }

        OwnerUpdateState();
    }

    // Part of standard GameMode
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

        game.SetTitle(string.IsNullOrWhiteSpace(PlayerName) ? "AXE THROWING" : PlayerName);

        game.SetMenuTitle("Player: " + (string.IsNullOrWhiteSpace(PlayerName) ? "(unoccupied)" : PlayerName));

        ScoreTxt.text = Score.ToString();
        AxeCountTxt.text = new string('\u25cf', MAX_AXE_COUNT - AxeCount) + new string('\u25cb', AxeCount);

        //KillshotButton.interactable = !KillshotCalled && KillshotsRemaining > 0 && AxeCount > 0;

        //var allowedKillshots = "Left and Right";
        //if (KillshotsRemaining <= 0)
        //{
        //    KsDisplayTxt.text = "KS: 0";
        //    allowedKillshots = "None";
        //}
        //else if (KillshotsRemaining == 1)
        //{
        //    if (LeftKillshotActive && RightKillshotActive)
        //    {
        //        KsDisplayTxt.text = "KS: 1";
        //    }
        //    else if (LeftKillshotActive)
        //    {
        //        KsDisplayTxt.text = "KS: L";
        //        allowedKillshots = "Left";
        //    }
        //    else
        //    {
        //        KsDisplayTxt.text = "KS: R";
        //        allowedKillshots = "Right";
        //    }
        //}
        //else
        //{
        //    KsDisplayTxt.text = "KS: 2";
        //}

        game.SetMenuStatusText(
            "Playing with Standard rules.\n" +
            $"Score: {Score}\n" +
            $"Axes Remaining:{AxeCount}/{MAX_AXE_COUNT}");
            //$"Killshot Attempts Remaining: {KillshotsRemaining}\nAllowed killshots: {allowedKillshots}\n" +
            //$"{(KillshotCalled ? $"<b>KILLSHOT CALLED! Hit the {allowedKillshots} blue target.</b>" : "Killshot inactive.")}");
    }
}
