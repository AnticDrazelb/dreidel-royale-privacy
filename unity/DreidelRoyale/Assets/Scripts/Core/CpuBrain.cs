using System.Collections.Generic;
using UnityEngine;

namespace DreidelRoyale.Core
{
    public class CpuDiffDef
    {
        public string Id, Label;
        public float Lo, Hi;
        public float ThinkLo, ThinkHi;   // seconds
        public CpuDiffDef(string id, string label, float lo, float hi, float tLo, float tHi)
        { Id = id; Label = label; Lo = lo; Hi = hi; ThinkLo = tLo; ThinkHi = tHi; }
    }

    public static class CpuBrain
    {
        public static readonly string[] Names =
            { "Dreidel Dan", "Golda", "Maccabee", "Latke Lou", "Shammai", "Ruthie" };

        // Dreidel landing is pure chance, so difficulty tunes the *spin strength* the bots
        // use (charge power range) — it changes the show, not the odds. Labelled honestly.
        public static readonly List<CpuDiffDef> Diffs = new List<CpuDiffDef>
        {
            new CpuDiffDef("easy",   "Easy",   0.20f, 0.55f, 0.90f, 1.50f),
            new CpuDiffDef("medium", "Medium", 0.45f, 0.85f, 0.65f, 1.15f),
            new CpuDiffDef("hard",   "Hard",   0.75f, 1.00f, 0.35f, 0.75f)
        };

        public static CpuDiffDef Diff(string id)
        {
            return Diffs.Find(d => d.Id == id) ?? Diffs[1];
        }

        // ---- table talk ----
        // Big pools plus shuffle-bag draws, so lines never repeat until a whole pool has
        // been heard (pure random repeats far too often).
        public static readonly Dictionary<string, string[]> Lines = new Dictionary<string, string[]>
        {
            { "GIMEL", new[]{
                "Gantz! All mine!","The whole pot — l'chaim!","Read it and weep!",
                "Mazel is with me tonight!","Come to papa, gelt!","That's how it's done, bubbeleh.",
                "I'd like to thank the dreidel.","Sweeter than sufganiyot!","The pot? MY pot.",
                "Did everyone see that? Good.","A great miracle happened HERE.","My bubbe taught me that spin.",
                "Ka-ching!","Don't hate the spinner…"
            } },
            { "BIG", new[]{
                "JACKPOT! Someone write this down!","A sweep for the history books!","I'm rich! RICH, I tell you!",
                "That's a whole latke fortune!","Retirement, here I come!","The dreidel LOVES me!",
                "Biggest pot of my life — l'chaim!","Somebody pinch me!"
            } },
            { "HEI", new[]{
                "Half the pot? Don't mind if I do.","I'll take my share, thank you.","Halb for me!",
                "Half now, the rest later.","A modest little windfall.","I'm not greedy. Just lucky.",
                "Halb! Better than nisht.","Fifty percent of delicious.","Sharing is caring — mostly for me.",
                "The polite person's jackpot.","I'll drink to half!","Some for me, some for… also me, later."
            } },
            { "NUN", new[]{
                "Nisht. Typical.","Nothing?! Oy vey.","This dreidel hates me.",
                "Nisht again? NISHT?!","I spun it with love and got bupkis.","Well. That was pointless.",
                "The dreidel and I need to talk.","Absolutely nothing. Wonderful.","I blame the table.",
                "Even my bubbe spins better.","Bupkis. As usual.","Nisht happens."
            } },
            { "PAY", new[]{
                "There goes my gelt…","Paying in AGAIN?","Shtel. Wonderful.",
                "Take it, take it all, why not.","The pot eats better than I do.","My gelt! My precious gelt!",
                "Fine. FINE. Have it.","This pot is a bottomless pit.","I'm basically a charity now.",
                "Oy, back in the pot.","Somebody stop this dreidel.","Consider it a loan. Interest-free. Forever."
            } },
            { "JAB", new[]{
                "Beginner's luck!","Oy, leave some for the rest of us!","That pot was MINE.",
                "Enjoy it while it lasts…","The dreidel was clearly bribed.","I demand a recount!",
                "Lucky spin. LUCKY.","My gelt! I knew it personally!","Even a broken dreidel lands Gimel twice a day.",
                "Don't spend it all on latkes.","I taught you that spin, remember.","The house always remembers.",
                "Yeah yeah, very impressive.","Next round, that pot is mine."
            } },
            { "JAB_PAY", new[]{
                "Ha! Pay up!","Music to my ears.","The pot thanks you kindly.",
                "Every little helps — for me.","Keep 'em coming!","A generous donation!",
                "That's the spirit. Keep paying.","The pot grows fat on your gelt."
            } },
            { "BROKE", new[]{
                "You can't squeeze gelt from a stone.","Pay with WHAT, exactly?","My pockets are already empty!",
                "The pot will have to take an IOU.","Ha! Nothing left to take!","I'm broke — joke's on the pot.",
                "Shin? I have bupkis to give.","Take my lint. It's all I have."
            } },
            { "ELIM", new[]{
                "I'm cleaned out…","Save me a latke, I'm done.","Tell my gelt I loved it.",
                "I came, I spun, I lost everything.","Avenge me, someone.","The dreidel giveth and taketh away.",
                "I'll just watch. Quietly. Weeping.","My bubbe will hear about this.","Out of gelt, out of luck.",
                "Farewell, cruel table."
            } }
        };

        // shuffle-bag state, per line key
        static readonly Dictionary<string, List<string>> Bags = new Dictionary<string, List<string>>();
        static readonly Dictionary<string, string> LastLine = new Dictionary<string, string>();

        /// <summary>
        /// Draw from a shuffled bag so the whole pool is heard before anything repeats,
        /// and never open a fresh bag with the line that just played.
        /// </summary>
        public static string DrawLine(string key)
        {
            // Shin and Pei are the same beat - paying in - so they share one pool rather
            // than needing a near-duplicate of it per fourth face.
            if (key == "SHIN" || key == "PEI") key = "PAY";

            string[] pool;
            if (!Lines.TryGetValue(key, out pool) || pool.Length == 0) return null;

            List<string> bag;
            if (!Bags.TryGetValue(key, out bag) || bag.Count == 0)
            {
                bag = new List<string>(pool);
                for (int i = bag.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    var t = bag[i]; bag[i] = bag[j]; bag[j] = t;
                }
                string last;
                if (bag.Count > 1 && LastLine.TryGetValue(key, out last) && bag[bag.Count - 1] == last)
                {
                    var t = bag[bag.Count - 1]; bag[bag.Count - 1] = bag[0]; bag[0] = t;
                }
                Bags[key] = bag;
            }

            var line = bag[bag.Count - 1];
            bag.RemoveAt(bag.Count - 1);
            LastLine[key] = line;
            return line;
        }
    }
}
