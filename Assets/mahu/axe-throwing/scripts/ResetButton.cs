
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class ResetButton : UdonSharpBehaviour
{
    public AxeThrowingGame game;

    public override void Interact()
    {
        game._Reset();
    }
}
