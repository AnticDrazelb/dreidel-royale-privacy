using UnityEngine;

namespace DreidelRoyale.Audio
{
    /// <summary>
    /// Patterned vibration. The web build calls navigator.vibrate with an on/off pattern;
    /// Android's Vibrator takes the same shape, so the patterns port across unchanged. Other
    /// platforms fall back to a single pulse, and anything without a motor stays silent.
    /// </summary>
    public static class Haptics
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject _vibrator;
        static bool _looked;

        static AndroidJavaObject Vibrator
        {
            get
            {
                if (!_looked)
                {
                    _looked = true;
                    try
                    {
                        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                            _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    }
                    catch { _vibrator = null; }
                }
                return _vibrator;
            }
        }
#endif

        public static void Vibrate(params int[] patternMs)
        {
            if (patternMs == null || patternMs.Length == 0) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            var v = Vibrator;
            if (v == null) return;
            try
            {
                if (patternMs.Length == 1) { v.Call("vibrate", (long)patternMs[0]); return; }
                // Android expects [offDelay, on, off, on, ...]; the web pattern starts with an
                // "on" duration, so a zero delay is prepended.
                var pattern = new long[patternMs.Length + 1];
                pattern[0] = 0;
                for (int i = 0; i < patternMs.Length; i++) pattern[i + 1] = patternMs[i];
                v.Call("vibrate", pattern, -1);
            }
            catch { }
#else
            if (SystemInfo.supportsVibration) Handheld.Vibrate();
#endif
        }
    }
}
