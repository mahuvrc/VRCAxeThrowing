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
    public class AxeThrowingGame : UdonSharpBehaviour
    {
        private int MAX_AXE_COUNT = 10;
        private const string TAG_ENABLE_DEBUG_SETTING = "mahu_AxeGame_EnableDebug";

        public BoxCollider AxeStickPlane;

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

        public AnimatedGameText gameText;

        public GameObject helpTextEnableBtn;

        public GameObject helpTextDisableBtn;

        public AxeThrowingDebugLog debugLog;

        public GameObject[] GameModeButtons;

        public AxeThrowingGameMode[] GameModes;

        public AxeThrowingGameMode ActiveGameMode;

        [UdonSynced]
        public int ActiveGameModeId;

        [UdonSynced]
        public int AxeState;

        [UdonSynced]
        public long lockDecayTime;

        [UdonSynced]
        public bool InProgress;

        private bool startingNextRound;

        void Start()
        {
            for (var i = 0; i < GameModeButtons.Length; i++)
            {
                if (i < GameModes.Length)
                {
                    var gameMode = GameModes[i];
                    gameMode.game = this;
                    gameMode.gameObject.SetActive(true);

                    var name = gameMode.DisplayName;
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

        public void ScoreAxe()
        {
            ActiveGameMode._ScoreAxe();
        }

        public void _AxeTaken()
        {
            SetOwner(Networking.LocalPlayer);
            ActiveGameMode._AxeTaken();

            InProgress = true;
            LockBoard(40);
            OwnerUpdateState();
        }

        public void _TakeBoardLock()
        {
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
                    //Debug.Log("Resetting lock nerf after timeout.");
                    lockNerf = 1;
                }
            }
            else
            {
                // accumulate stacking lockout nerf if you keep switching boards
                lockoutTime = Time.time + 120f; // reset lock nerf in 2 minutes
                lockNerf = lockNerf * 2;
            }

            //Debug.Log($"Locking board at nerf level {lockNerf} {Networking.GetUniqueName(gameObject)}");
            lockDecayTime = Networking.GetNetworkDateTime().AddSeconds(seconds / lockNerf).Ticks;
        }

        public void _ChangePlayer(int playerId)
        {
            var player = VRCPlayerApi.GetPlayerById(playerId);
            if (Utilities.IsValid(player))
            {
                SetOwner(player);
            }
        }

        public void _ConsumeAxe()
        {
            ActiveGameMode._ConsumeAxe();
            startingNextRound = true;
            SendCustomEventDelayedSeconds(nameof(_NextRound), 2.0f);
        }

        public void _NextRound()
        {
            if (!startingNextRound)
                return;

            startingNextRound = false;
            ActiveGameMode._NextRound();
        }

        public void _Initialize()
        {
            if (Networking.IsMaster && Networking.IsOwner(gameObject))
            {
                debugLog._Info("Initializing game");
                _Reset();
                debugLog._Info("Game initialized.");
            }
        }

        public void _Reset()
        {
            Debug.Log("Resetting game");
            startingNextRound = false;
            lockDecayTime = Networking.GetNetworkDateTime().Ticks;
            InProgress = false;
            ActiveGameMode._Reset();
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
            if (!Networking.IsOwner(gameObject))
                Networking.SetOwner(player, gameObject);

            if (!Networking.IsOwner(Axe.gameObject))
                Networking.SetOwner(player, Axe.gameObject);

            if (!Networking.IsOwner(AxeHolster.gameObject))
                Networking.SetOwner(player, AxeHolster.gameObject);

            foreach (var gameMode in GameModes)
            {
                if (!Networking.IsOwner(gameMode.gameObject))
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
                    gameMode._Hide();
                else
                    gameMode._Show();
            }
        }

        public void _PlayerInZoneSnailUpdate()
        {
            DisplayLockStatus();
            DisplayDebugStatus();
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
            return ActiveGameMode.PlayerOpening;
        }

        public void DisplayLockStatus()
        {
            var localSuper = Networking.LocalPlayer.isInstanceOwner || Networking.LocalPlayer.isMaster;

            var locked = !Networking.IsOwner(gameObject) && !ActiveGameModeHasOpening();
            var powerToUnlock = localSuper || Networking.GetNetworkDateTime().Ticks > lockDecayTime;

            MenuPlayerOwnedTxtugui.text = $"(locked by {Networking.GetOwner(gameObject).displayName})";

            MenuLocked.SetActive(locked);
            MenuUnlocked.SetActive(!locked);
            ForceTakeMenu.SetActive(powerToUnlock);

            Axe.SetOwnerLocked(locked);
            AxeHolster.SetOwnerLocked(locked);
        }

        public void DisplayDebugStatus()
        {
            var enabled = IsDebugEnabled();
            if (debugLog != null)
            {
                debugLog.gameObject.SetActive(enabled);
            }
        }

        public static bool IsDebugEnabled()
        {
#if UNITY_EDITOR
            return true;
#endif

            var debugEnabledStr = Networking.LocalPlayer.GetPlayerTag(TAG_ENABLE_DEBUG_SETTING);
            if (debugEnabledStr == "true")
            {
                return true;
            }

            return false;
        }

        public static void SetDebugEnabled(bool value)
        {
            Networking.LocalPlayer.SetPlayerTag(TAG_ENABLE_DEBUG_SETTING, value.ToString().ToLower());
        }

        public void _ForceTakeBoard()
        {
            // set owner on all things and reset everything completely upon receipt
            // this isn't technically necessary but it helps prevent confusion if players
            // are moving the axe holster around

            _Reset();
            AxeHolster._Reset();
            _TakeBoardLock();
        }
    }
}