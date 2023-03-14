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
    public class BeerAxeSingleGameMode : AxeThrowingGameMode
    {
        public override string DisplayName { get { return "Beer Axe (1 player per board)"; } }

        private const int CUPS_ALL_HIT = 0b111111;

        public TextMeshPro WinText;

        public SphereCollider[] Player1CupColliders;

        public GameObject[] Player1CupIndicator;

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
                var behavior = (UdonBehaviour)GetComponent(typeof(UdonBehaviour));
                behavior.enabled = false;
            }
        }

        public override void _Reset()
        {
            Player1Name = null;
            PlayerOpening = true;

            Player1CupStatus = 0;
            OwnerUpdateState();
        }

        public override void _ScoreAxe()
        {
            for (int i = 0; i < Player1CupColliders.Length; i++)
            {
                if (IsAxeInSphereScoreZone(Player1CupColliders[i])
                    && !IsCupHit(Player1CupStatus, i))
                {
                    Player1CupStatus = FlagCupAsHit(Player1CupStatus, i);
                    break;
                }
            }

            OwnerUpdateState();
        }

        private bool IsCupHit(int cupFlags, int cupIndex)
        {
            return (cupFlags & 1 << cupIndex) > 0;
        }

        private int FlagCupAsHit(int cupFlags, int cupIndex)
        {
            return cupFlags | 1 << cupIndex;
        }

        public override void _ConsumeAxe()
        {
            OwnerUpdateState();
            PlayerOpening = false;
            Player1Name = Networking.LocalPlayer.displayName;
        }

        public override void _AxeTaken()
        {
            OwnerUpdateState();
        }

        public override void _NextRound()
        {
            if (Player1CupStatus < CUPS_ALL_HIT)
            {
                OwnerUpdateState();
                game.Axe._Reset();
            }
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

            var player1Display = string.IsNullOrWhiteSpace(Player1Name) ? "(unoccupied)" : Player1Name;


            game.SetTitle(string.IsNullOrWhiteSpace(Player1Name) ? "PLAY BEER AXE" : Player1Name);
            game.SetMenuTitle($"Beer Axe! {player1Display}");


            var player1cupremaining = 6;

            for (int i = 0; i < 6; i++)
            {
                if (IsCupHit(Player1CupStatus, i))
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


            game.SetMenuStatusText(
                "Playing BEER AXE!.\n" +
                $"Opponent has {player1cupremaining} cups remaining\n");

        }
    }
}