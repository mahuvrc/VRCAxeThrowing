
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class NoGoZone : UdonSharpBehaviour
{
    [NonSerialized]
    public bool LocalPlayerInZone;

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player.isLocal)
        {
            Debug.Log("Player Enters no go zone.");
            LocalPlayerInZone = true;
        }
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (player.isLocal)
        {
            Debug.Log("Player exits no go zone.");
            LocalPlayerInZone = false;
        }
    }
}
