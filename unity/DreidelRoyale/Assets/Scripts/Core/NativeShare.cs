using System;
using UnityEngine;

namespace DreidelRoyale.Core
{
    /// <summary>
    /// The system share sheet. Android goes through ACTION_SEND; iOS needs a small native
    /// shim (see Plugins/iOS/DreidelShare.mm), because UIActivityViewController has no
    /// managed equivalent. Everywhere else the text goes to the clipboard, which is the
    /// honest fallback rather than a silent no-op.
    /// </summary>
    public static class NativeShare
    {
#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void _DreidelShareText(string subject, string body);
#endif

        /// <summary>Returns true if the system sheet opened; false if it fell back to the clipboard.</summary>
        public static bool Share(string subject, string body)
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var intent = new AndroidJavaObject("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setAction",
                        intentClass.GetStatic<string>("ACTION_SEND"));
                    intent.Call<AndroidJavaObject>("setType", "text/plain");
                    intent.Call<AndroidJavaObject>("putExtra",
                        intentClass.GetStatic<string>("EXTRA_SUBJECT"), subject);
                    intent.Call<AndroidJavaObject>("putExtra",
                        intentClass.GetStatic<string>("EXTRA_TEXT"), body);

                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var chooser = intentClass.CallStatic<AndroidJavaObject>(
                               "createChooser", intent, subject))
                    {
                        activity.Call("startActivity", chooser);
                    }
                    return true;
                }
#elif UNITY_IOS && !UNITY_EDITOR
                _DreidelShareText(subject, body);
                return true;
#else
                GUIUtility.systemCopyBuffer = body;
                return false;
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Dreidel Royale] share failed, copied instead: " + e.Message);
                try { GUIUtility.systemCopyBuffer = body; } catch { }
                return false;
            }
        }

        /// <summary>
        /// The spin log as emoji, so a shared result reads as a story rather than a scoreline.
        /// </summary>
        public static string HistoryEmoji(System.Collections.Generic.IList<string> history, int max = 24)
        {
            if (history == null || history.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            int from = Mathf.Max(0, history.Count - max);
            for (int i = from; i < history.Count; i++)
            {
                switch (history[i])
                {
                    case "GIMEL": sb.Append("\U0001F7E1"); break;   // the whole pot
                    case "HEI":   sb.Append("\U0001F7E2"); break;   // half
                    case "NUN":   sb.Append("⚪"); break;       // nothing
                    default:      sb.Append("\U0001F534"); break;   // paid in
                }
            }
            return sb.ToString();
        }
    }
}
