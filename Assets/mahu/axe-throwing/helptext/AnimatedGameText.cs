using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace mahu.AxeThrowing
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class AnimatedGameText : UdonSharpBehaviour
    {
        [SerializeField]
        private TextMeshPro tmpText;

        [SerializeField]
        private Animator animator;

        public Color defaultColor = Color.white;

        public Color helpColor = Color.white;

        public Color goodColor = Color.white;

        public Color badColor = Color.white;

        
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

        public void _PlayText(string text, Color color, float speed)
        {
            tmpText.text = text;
            tmpText.color = color;

            animator.speed = speed;
            animator.SetTrigger("play");
        }
    }
}