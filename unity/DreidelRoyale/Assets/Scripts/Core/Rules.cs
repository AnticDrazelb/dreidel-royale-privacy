using System.Collections.Generic;
using UnityEngine;

namespace DreidelRoyale.Core
{
    public class RulesDef
    {
        public string Id, Label;
        public RulesDef(string id, string label) { Id = id; Label = label; }
    }

    /// <summary>
    /// Stakes rise as the game goes on — this is what makes dreidel actually END.
    /// Classic rules circulate gelt forever (median 2p game: ~80 rounds, simulated);
    /// a poker-style ante escalation brings the median to ~21 and caps the tail.
    /// </summary>
    public static class Rules
    {
        public const int AnteRiseEvery = 5;   // rounds between stake rises
        public const int AnteCap = 5;         // ante never exceeds this
        public const int SuddenRiseEvery = 3; // Sudden Death rises faster

        public static readonly List<RulesDef> Defs = new List<RulesDef>
        {
            new RulesDef("rising",  "Rising Stakes"),
            new RulesDef("sudden",  "Sudden Death"),
            new RulesDef("classic", "Classic")
        };

        /// <summary>Sudden Death: 5 gelt each, antes rise every 3 rounds — games in minutes.</summary>
        public static int StartCoinsFor(string rules)
        {
            return rules == "sudden" ? 5 : Consts.StartCoins;
        }

        public static int RiseEveryFor(string rules)
        {
            return rules == "sudden" ? SuddenRiseEvery : AnteRiseEvery;
        }

        /// <summary>
        /// Which face a landing yaw resolves to. Normalises into (-360, 0] and picks the
        /// nearest face angle — identical to the web build so replays match exactly.
        /// </summary>
        public static Side ResolveFace(float finalDeg)
        {
            float norm = finalDeg % 360f;
            if (norm > 0f) norm -= 360f;
            Side side = Consts.Sides[0];
            float minDiff = float.MaxValue;
            foreach (var s in Consts.Sides)
            {
                float d = Mathf.Abs(norm - s.Angle);
                if (d > 180f) d = 360f - d;
                if (d < minDiff) { minDiff = d; side = s; }
            }
            return side;
        }

        /// <summary>
        /// What a SHIN/PEI costs: classic rules keep the authentic single "shtel", every
        /// other style pays the live ante.
        /// </summary>
        public static int ShinCost(GameState g)
        {
            return g.Rules == "classic" ? 1 : Mathf.Max(1, g.Ante);
        }

        /// <summary>The ante target for the current round, or 0 when the style never rises.</summary>
        public static int AnteTargetFor(GameState g)
        {
            if (g.Rules == "classic") return g.Ante;
            int baseAnte = Mathf.Max(1, g.BaseAnte);
            int every = RiseEveryFor(g.Rules);
            return Mathf.Min(AnteCap, baseAnte + (g.Round - 1) / every);
        }
    }
}
