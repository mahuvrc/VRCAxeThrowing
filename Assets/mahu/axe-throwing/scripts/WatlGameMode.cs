using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace mahu.AxeThrowing
{
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
                var behavior = (UdonBehaviour)GetComponent(typeof(UdonBehaviour));
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

        // Part of standard GameMode
        public override void _ScoreAxe()
        {
            if (IsAxeInSphereScoreZone(LeftKillshotZone))
            {
                AddScore(8);
                gameText._PlayText($"+8 Killshot!", 0.75f);
                return;
            }

            if (IsAxeInSphereScoreZone(RightKillshotZone))
            {
                AddScore(8);
                gameText._PlayText($"+8 Killshot!", 0.75f);
                return;
            }

            // assume the score zones are ordered 6 points to 1 point
            // score is highest touching score value.
            for (int i = 0; i < 6; i++)
            {
                if (IsAxeInSphereScoreZone(ScoreZones[i]))
                {
                    AddScore(6 - i);
                    gameText._PlayText($"+{6 - i}");
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

            game.SetMenuStatusText(
                "Playing with Standard rules.\n" +
                $"Score: {Score}\n" +
                $"Axes Remaining:{AxeCount}/{MAX_AXE_COUNT}");
        }
    }
}