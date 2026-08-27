using System.Collections.Generic;
using UnityEngine;

namespace DreidelRoyale.Core
{
    /// <summary>
    /// One face of the dreidel. `Angle` is the spinner yaw (degrees) at which this face
    /// ends up pointing at the camera — the same convention the web build uses, so the
    /// landing maths in <see cref="Rules.ResolveFace"/> ports across unchanged.
    /// </summary>
    public class Side
    {
        public string Char;
        public string Name;
        public string Phon;
        public float Angle;
        public string Text;
        public string Chant;

        public Side(string ch, string name, string phon, float angle, string text, string chant = null)
        {
            Char = ch; Name = name; Phon = phon; Angle = angle; Text = text; Chant = chant;
        }
    }

    public static class Consts
    {
        public const string AppPrefix = "drdl-v9-";
        public const int StartCoins = 10;
        public const float SweetSpot = 0.88f;
        public const float ChargeTime = 1.5f;      // seconds to a full wind-up

        // The fourth face differs by region:
        //   Diaspora: Shin — "Nes Gadol Haya SHAM" (a great miracle happened THERE)
        //   Israel:   Pei  — "Nes Gadol Haya PO"   (a great miracle happened HERE)
        // Both mean the same in play: put one in the pot.
        public static readonly Side FourthDiaspora =
            new Side("ש", "SHIN", "Shin", -270f, "Shtel — put one in the pot.", "SHAM");
        public static readonly Side FourthIsrael =
            new Side("פ", "PEI", "Pei", -270f, "Pei — put one in the pot.", "PO");

        public static bool IsraelMode;

        public static Side Fourth() { return IsraelMode ? FourthIsrael : FourthDiaspora; }

        /// <summary>NUN, GIMEL, HEI, then the region-dependent fourth face.</summary>
        public static readonly Side[] Sides =
        {
            new Side("נ", "NUN",   "Nun",   0f,    "Nisht — nothing happens."),
            new Side("ג", "GIMEL", "Gimel", -90f,  "Gantz — take the whole pot!"),
            new Side("ה", "HEI",   "Hei",   -180f, "Halb — take half the pot."),
            FourthDiaspora
        };

        public static readonly string[] Chants = { "NES", "GADOL", "HAYA", "SHAM" };

        /// <summary>Re-point the fourth face and its chant word after a region toggle.</summary>
        public static void RefreshSides()
        {
            Sides[3] = Fourth();
            Chants[3] = Fourth().Chant;
        }

        // Per-table result flavour. The Backyard swaps the Yiddish glosses for
        // playtime-speak; every other table keeps the traditional lines.
        static readonly Dictionary<string, Dictionary<string, string>> LevelFlavor =
            new Dictionary<string, Dictionary<string, string>>
            {
                {
                    "backyard", new Dictionary<string, string>
                    {
                        { "NUN",   "Nothing happens… for real life." },
                        { "GIMEL", "WACKADOO! Take the whole pot!" },
                        { "HEI",   "Hooray — take half the pot!" },
                        { "SHIN",  "Oh biscuits… put one in the pot." },
                        { "PEI",   "Oh biscuits… put one in the pot." }
                    }
                }
            };

        public static string FlavorText(Side side, string appliedEnv)
        {
            Dictionary<string, string> f;
            string line;
            if (appliedEnv != null && LevelFlavor.TryGetValue(appliedEnv, out f)
                && f.TryGetValue(side.Name, out line)) return line;
            return side.Text;
        }

        /// <summary>Avatar hues, one per seat, in join order.</summary>
        public static readonly int[] PlayerHues = { 45, 210, 340, 150, 275, 20, 185, 310 };

        /// <summary>
        /// The seat's avatar colour. The stylesheet writes these as hsl(h, 62%, 62%), and HSL
        /// is not HSV — converting through the wrong one shifts every avatar's brightness.
        /// </summary>
        public static Color HueColor(int index, float s = 0.62f, float l = 0.62f)
        {
            return Hsl(PlayerHues[index % PlayerHues.Length] / 360f, s, l);
        }

        public static Color Hsl(float h, float s, float l)
        {
            float c = (1f - Mathf.Abs(2f * l - 1f)) * s;
            float x = c * (1f - Mathf.Abs((h * 6f) % 2f - 1f));
            float m = l - c / 2f;
            float r, g, b;
            int seg = Mathf.FloorToInt(h * 6f) % 6;
            if (seg < 0) seg += 6;
            switch (seg)
            {
                case 0: r = c; g = x; b = 0; break;
                case 1: r = x; g = c; b = 0; break;
                case 2: r = 0; g = c; b = x; break;
                case 3: r = 0; g = x; b = c; break;
                case 4: r = x; g = 0; b = c; break;
                default: r = c; g = 0; b = x; break;
            }
            return new Color(r + m, g + m, b + m);
        }
    }
}
