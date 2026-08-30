using UnityEngine;

namespace DreidelRoyale.Core
{
    /// <summary>
    /// Storage guard, mirroring the web build's try/catch around localStorage: every
    /// read returns null rather than throwing, so a locked-down platform degrades to
    /// "nothing saved" instead of taking the game down.
    /// </summary>
    public static class Store
    {
        public static string Get(string key)
        {
            try { return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null; }
            catch { return null; }
        }

        public static void Set(string key, string value)
        {
            try { PlayerPrefs.SetString(key, value); PlayerPrefs.Save(); }
            catch { /* no-op where storage is blocked */ }
        }

        public static bool Flag(string key) { return Get(key) == "1"; }

        public static int Int(string key, int fallback)
        {
            int v;
            var raw = Get(key);
            return int.TryParse(raw, System.Globalization.NumberStyles.Integer,
                                System.Globalization.CultureInfo.InvariantCulture, out v) ? v : fallback;
        }
    }
}
