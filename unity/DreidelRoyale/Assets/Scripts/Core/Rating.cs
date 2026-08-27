using System.Collections;
using UnityEngine;

namespace DreidelRoyale.Core
{
    /// <summary>
    /// The rating nudge, carried across from the web build: after the player's third win,
    /// once ever, on a happy moment. The store decides whether a prompt actually appears
    /// (both platforms quota them heavily), so this asks and never checks - a nudge that
    /// argues with the OS is worse than no nudge.
    /// </summary>
    public static class Rating
    {
        /// <summary>
        /// Marked as asked before the prompt fires, so a player who is quota-blocked or who
        /// dismisses it is never asked a second time.
        /// </summary>
        public static bool ShouldAsk(bool justWon, LifetimeStats s)
        {
            if (!justWon) return false;
            if (s.wins < 3) return false;
            if (Store.Get("drdl-rated") == "1") return false;
            Store.Set("drdl-rated", "1");
            return true;
        }

        /// <summary>Let the winner celebration land first - the same 2.6s the original waited.</summary>
        public static IEnumerator AskAfterCelebration()
        {
            yield return new WaitForSeconds(2.6f);
            Request();
        }

        static void Request()
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                // Play's In-App Review lives in a Play Core wrapper on the native side. Its
                // absence is a normal state - a sideloaded or non-Play build simply has no
                // review flow - so a miss here is silent by design.
                using (var cls = new AndroidJavaClass("com.dreidelroyale.Review"))
                using (var inst = cls.CallStatic<AndroidJavaObject>("instance"))
                {
                    if (inst != null) inst.Call("requestReview");
                }
#elif UNITY_IOS && !UNITY_EDITOR
                _DreidelRequestReview();
#endif
            }
            catch { /* no review flow on this build; nothing to tell the player */ }
        }

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void _DreidelRequestReview();
#endif
    }
}
