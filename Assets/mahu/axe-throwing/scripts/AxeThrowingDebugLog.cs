using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace mahu.AxeThrowing
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class AxeThrowingDebugLog : UdonSharpBehaviour
    {
        private const string LOG_PREFIX = "[mahu]";
        public const int MAX_LENGTH = 9999;
        public TextMeshProUGUI log;

        public void _Info(string text)
        {
            log.text += $"\r\n[info] {text}";
            Debug.Log($"{LOG_PREFIX} {text}");
            Trimlog();
        }

        public void _Warning(string text)
        {
            log.text += $"\r\n[warning] {text}";
            Debug.LogWarning($"{LOG_PREFIX} {text}");
            Trimlog();
        }

        public void _Error(string text)
        {
            log.text += $"\r\n[error] {text}";
            Debug.LogError($"{LOG_PREFIX} {text}");
            Trimlog();
        }

        private void Trimlog()
        {
            if (log.text.Length > MAX_LENGTH)
            {
                log.text = log.text.Substring(log.text.Length - MAX_LENGTH);
            }
        }
    }
}