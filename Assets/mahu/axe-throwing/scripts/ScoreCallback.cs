using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace mahu.AxeThrowing
{
    public abstract class ScoreCallback : UdonSharpBehaviour
    {
        public abstract void _OnPlayerScore(int score);
    }
}