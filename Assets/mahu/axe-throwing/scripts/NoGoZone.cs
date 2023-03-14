using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace mahu.AxeThrowing
{
    public class NoGoZone : UdonSharpBehaviour
    {
        [NonSerialized]
        public bool LocalPlayerInZone;

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (player.isLocal)
            {
                LocalPlayerInZone = true;
            }
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            if (player.isLocal)
            {
                LocalPlayerInZone = false;
            }
        }
    }
}