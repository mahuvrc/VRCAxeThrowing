﻿
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public abstract class ScoreCallback : UdonSharpBehaviour
{
    public abstract void _OnPlayerScore(int score);
}
