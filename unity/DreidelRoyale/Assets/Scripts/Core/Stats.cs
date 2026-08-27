using System;
using UnityEngine;

namespace DreidelRoyale.Core
{
    /// <summary>Lifetime, this-device play record. Drives every unlock gate.</summary>
    [Serializable]
    public class LifetimeStats
    {
        public int games, wins, losses, bestSweep, spins, gimels, streak, bestStreak;

        public LifetimeStats Clone()
        {
            return new LifetimeStats
            {
                games = games, wins = wins, losses = losses, bestSweep = bestSweep,
                spins = spins, gimels = gimels, streak = streak, bestStreak = bestStreak
            };
        }
    }

    public static class Stats
    {
        const string Key = "drdl-stats";

        public static LifetimeStats Load()
        {
            try
            {
                var raw = Store.Get(Key);
                if (!string.IsNullOrEmpty(raw))
                {
                    var s = JsonUtility.FromJson<LifetimeStats>(raw);
                    if (s != null) return s;
                }
            }
            catch { /* corrupt record reads as a fresh one */ }
            return new LifetimeStats();
        }

        public static void Save(LifetimeStats s)
        {
            try { Store.Set(Key, JsonUtility.ToJson(s)); } catch { }
        }
    }
}
