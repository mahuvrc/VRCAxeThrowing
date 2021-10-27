
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class AxeStickTrigger : UdonSharpBehaviour
{

    public ThrowingAxe Axe;

    private void OnTriggerEnter(Collider other)
    {
        if (!Networking.IsOwner(Axe.gameObject))
        {
            return;
        }

        Axe._OnStickTrigger(other);
    }
}
