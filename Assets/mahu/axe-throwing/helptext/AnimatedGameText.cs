
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class AnimatedGameText : UdonSharpBehaviour
{
    private const string TAG_DISABLE_HELP_SETTING = "mahu_AxeGame_DisableHelpText";

    [SerializeField]
    private TextMeshPro tmpText;

    [SerializeField]
    private Animator animator;

    public Color defaultColor = Color.white;

    public Color helpColor = Color.white;

    public Color goodColor = Color.white;

    public Color badColor = Color.white;

    public bool _IsHelpTextDisabled()
    {
        var helpDisabledStr = Networking.LocalPlayer.GetPlayerTag(TAG_DISABLE_HELP_SETTING);
        if (helpDisabledStr == "true")
        {
            return true;
        }

        return false;
    }

    public void _DisableHelpText()
    {
        Networking.LocalPlayer.SetPlayerTag(TAG_DISABLE_HELP_SETTING, "true");
    }

    public void _EnableHelpText()
    {
        Networking.LocalPlayer.SetPlayerTag(TAG_DISABLE_HELP_SETTING, "false");
    }

    public void _PlayText(string text)
    {
        _PlayText(text, defaultColor, 1.0f);
    }

    public void _PlayText(string text, Color color)
    {
        _PlayText(text, color, 1.0f);
    }

    public void _PlayText(string text, float speed)
    {
        _PlayText(text, defaultColor, speed);
    }

    public void _PlayHelpText(string text)
    {
        _PlayHelpText(text, 0.5f);
    }

    public void _PlayHelpText(string text, float speed)
    {
        if (_IsHelpTextDisabled())
            return;

        _PlayText(text, helpColor, speed);
    }

    public void _PlayText(string text, Color color, float speed)
    {
        tmpText.text = text;
        tmpText.color = color;

        animator.speed = speed;
        animator.SetTrigger("play");
    }
}
