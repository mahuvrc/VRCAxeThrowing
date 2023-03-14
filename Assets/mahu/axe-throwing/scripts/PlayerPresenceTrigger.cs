using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace mahu.AxeThrowing
{
    public class PlayerPresenceTrigger : UdonSharpBehaviour
    {
        public AxeThrowingGame Game;
        public Animator UIAnimator;

        private float snailUpdateTime;

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (player.isLocal)
            {
                UIAnimator.SetBool("visible", true);
            }
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            if (player.isLocal)
            {
                UIAnimator.SetBool("visible", false);
            }
        }

        public override void OnPlayerTriggerStay(VRCPlayerApi player)
        {
            if (player.isLocal)
            {
                if (Time.time > snailUpdateTime)
                {
                    snailUpdateTime = Time.time + Random.Range(.5f, 1f);
                    Game._PlayerInZoneSnailUpdate();
                }
            }
        }
    }
}