using System;
using System.Collections.Generic;
using System.Linq;

namespace DreidelRoyale.Core
{
    /// <summary>
    /// One source of truth per unlock: the gate, the wording and the progress readout all
    /// come from the same spec, so a target can never drift from its own label.
    ///   Label — the goal in words       Unit — short noun for the compact counter
    ///   At    — reads the lifetime stat Need — the number that earns it
    /// </summary>
    public class Unlock
    {
        public string Label, Unit;
        public Func<LifetimeStats, int> At;
        public int Need;

        public Unlock(string label, string unit, Func<LifetimeStats, int> at, int need)
        {
            Label = label; Unit = unit; At = at; Need = need;
        }

        public bool Test(LifetimeStats s) { return At(s) >= Need; }

        public int Current(LifetimeStats s) { return Math.Max(0, Math.Min(Need, At(s))); }
        public float Pct(LifetimeStats s) { return Need <= 0 ? 1f : Current(s) / (float)Need; }

        /// <summary>
        /// Locked chips trade the goal text for a live counter — "312/500 SPINS" says more
        /// than "SPIN 500 TIMES" and usually takes less room. One-step goals keep their
        /// wording: a bar that can only be empty or full is noise.
        /// </summary>
        public string LockLabel(LifetimeStats s)
        {
            return Need > 1 ? string.Format("{0}/{1} {2}", Current(s), Need, Unit) : Label;
        }

        public string Hint(string name, LifetimeStats s)
        {
            return Need > 1
                ? string.Format("{0} — {1} ({2} of {3})", name, Label.ToLower(), Current(s), Need)
                : string.Format("{0} — {1}", name, Label.ToLower());
        }
    }

    public class SkinDef
    {
        public string Id, Name;
        public Unlock Unlock;
        public bool Premium;

        public SkinDef(string id, string name, Unlock unlock = null, bool premium = false)
        {
            Id = id; Name = name; Unlock = unlock; Premium = premium;
        }
    }

    public class EnvUnlockDef
    {
        public string Id, Name;
        public Unlock Unlock;
        public EnvUnlockDef(string id, string name, Unlock unlock) { Id = id; Name = name; Unlock = unlock; }
    }

    public static class Unlocks
    {
        static Unlock Earn(string label, string unit, Func<LifetimeStats, int> at, int need)
        {
            return new Unlock(label, unit, at, need);
        }

        /// <summary>
        /// Dreidels, earned through play. `gold` is deliberately absent: it is not pickable,
        /// it stays the gelt leader's crown.
        /// </summary>
        public static readonly List<SkinDef> Skins = new List<SkinDef>
        {
            new SkinDef("wood",     "Olive Wood"),
            new SkinDef("blocky",   "Grass Block"),
            new SkinDef("heeler",   "Blue Pup"),
            new SkinDef("ruby",     "Ruby",      Earn("Win a game",      "wins",   s => s.wins,       1)),
            new SkinDef("frost",    "Ice",       Earn("Win 5 games",     "wins",   s => s.wins,       5)),
            new SkinDef("onyx",     "Onyx",      Earn("Sweep a 12+ pot", "pot",    s => s.bestSweep, 12)),
            new SkinDef("emerald",  "Emerald",   Earn("Play 25 games",   "games",  s => s.games,     25)),
            new SkinDef("amber",    "Amber",     Earn("Sweep a 20+ pot", "pot",    s => s.bestSweep, 20)),
            new SkinDef("diamond",  "Diamond",   Earn("Win 10 games",    "wins",   s => s.wins,      10)),
            new SkinDef("streaker", "Menorah",   Earn("Win 8 in a row",  "streak", s => s.bestStreak, 8)),
            new SkinDef("goldpup",  "Gold Champ",Earn("Spin 500 times",  "spins",  s => s.spins,    500)),
            new SkinDef("nertamid", "Ner Tamid",     null, true),
            new SkinDef("oil",      "Oil Miracle",   null, true),
            new SkinDef("founder",  "Founder's Gold",null, true)
        };

        // The three paid dreidels outrank the leader's gold: the crown on the name is
        // enough. Applies ONLY to these — earned skins are still replaced as usual.
        public static readonly string[] PremiumSkins = { "nertamid", "oil", "founder" };

        /// <summary>
        /// Tables earned through play. Anything not listed is free from the start
        /// (Midnight = default; Blocky Biome and Backyard pair with their dreidels).
        /// </summary>
        public static readonly List<EnvUnlockDef> EnvUnlocks = new List<EnvUnlockDef>
        {
            new EnvUnlockDef("den",   "Maple Den",    Earn("Play 3 games",  "games", s => s.games, 3)),
            new EnvUnlockDef("frost", "Silver Frost", Earn("Win 3 games",   "wins",  s => s.wins,  3)),
            new EnvUnlockDef("felt",  "Casino Felt",  Earn("Play 15 games", "games", s => s.games, 15))
        };

        public static SkinDef Skin(string id) { return Skins.FirstOrDefault(d => d.Id == id); }
        public static bool ValidSkin(string id) { return id != null && Skins.Any(d => d.Id == id); }

        public static EnvUnlockDef EnvUnlock(string id) { return EnvUnlocks.FirstOrDefault(e => e.Id == id); }

        public static bool EnvUnlocked(string id, LifetimeStats s)
        {
            if (OwnsFullCollection()) return true;
            var e = EnvUnlock(id);
            return e == null || e.Unlock.Test(s);
        }

        public static bool SkinUnlocked(SkinDef d, LifetimeStats s)
        {
            if (OwnsFullCollection()) return true;   // Full Collection = every dreidel
            if (d.Premium) return false;             // premium needs the Full Collection
            return d.Unlock == null || d.Unlock.Test(s);
        }

        // ---- Full Collection: one-time unlock of every premium dreidel ----
        public static bool OwnsFullCollection() { return Store.Get("drdl-fullcollection") == "1"; }
        public static void GrantFullCollection() { Store.Set("drdl-fullcollection", "1"); }

        public const string TestUnlockCode = "NERGELT9X4";
    }
}
