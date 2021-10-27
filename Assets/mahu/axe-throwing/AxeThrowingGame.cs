
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class AxeThrowingGame : UdonSharpBehaviour
{
    public Collider AxeStickZone;

    public Collider AxeHoldZone;

    public NoGoZone NoGoZoneScript;

    public SphereCollider[] ScoreZones;

    public TextMeshPro AxeCountTxt;

    public TextMeshPro PlayerNameTxt;

    public TextMeshPro ScoreTxt;

    public ThrowingAxe Axe;

    [UdonSynced]
    public string PlayerName;

    [UdonSynced]
    public int Score;

    [UdonSynced]
    public int AxeCount;

    [UdonSynced]
    public int AxeState;

    void Start()
    {
        if (ScoreZones.Length != 6)
        {
            Debug.LogError("Must have 6 score zones");
            var behavior = (UdonBehaviour)this.GetComponent(typeof(UdonBehaviour));
            behavior.enabled = false;
        }

        DisplayGameState();
    }

    public void ScoreAxe(Collider axeScoreableCollider)
    {
        // assume the score zones are ordered 6 points to 1 point
        // score is highest touching score value.

        for (int i = 0; i < 6; i++)
        {
            var collisions = Physics.OverlapSphere(ScoreZones[i].transform.position + ScoreZones[i].center, ScoreZones[i].radius, ~0, QueryTriggerInteraction.Collide);
            foreach (var collider in collisions)
            {
                if (axeScoreableCollider == collider)
                {
                    AddScore(6 - i);
                    return;
                }
            }
        }
    }

    private void AddScore(int score)
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);

        PlayerName = Networking.LocalPlayer.displayName;
        Score += score;

        UpdateGameState();
    }

    public void _AxeTaken()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
    }

    public void _ConsumeAxe()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);

        if (AxeCount > 0)
        {
            AxeCount--;
            PlayerName = Networking.LocalPlayer.displayName;
            UpdateGameState();
        }
    }

    public void _Reset()
    {
        Debug.Log("Resetting game");
        Networking.SetOwner(Networking.LocalPlayer, gameObject);

        PlayerName = "AXE THROWING";
        Score = 0;
        AxeCount = 5;
        UpdateGameState();

        Axe._Reset();
    }

    public void SetAxeState(int state)
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        AxeState = state;
        RequestSerialization();

        Axe.TransitionState(state);
    }

    private void UpdateGameState()
    {
        RequestSerialization();
        DisplayGameState();
    }

    public override void OnDeserialization()
    {
        DisplayGameState();
        Axe.TransitionState(AxeState);
    }

    private void DisplayGameState()
    {
        PlayerNameTxt.text = PlayerName;
        ScoreTxt.text = Score.ToString();
        AxeCountTxt.text = AxeCount.ToString();
    }
}
