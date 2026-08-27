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
                "Did everyone see that? Good.","A great miracle happened HERE." } },
            { "BIG", new[]{
                "That is a LOT of gelt.","I may need a bigger pocket.","Somebody get me a wheelbarrow.",
                "This is my Hanukkah miracle.","Eight nights of this, please." } },
            { "HEI", new[]{
                "Halb — I'll take it.","Half a pot is still a pot.","Not greedy. Just hungry.",
                "A tidy little scoop.","Better than nothing, nu?" } },
            { "NUN", new[]{
                "Nisht. Story of my life.","Nothing. Marvellous.","The dreidel mocks me.",
                "I felt that one was good, too.","Nun again? Really?","Well. That happened." } },
            { "SHIN", new[]{
                "Shtel… there goes another.","Paying the house, as usual.","Ow. My gelt.",
                "Consider it a donation.","Take it. Take it all.","This dreidel has it in for me." } },
            { "PEI", new[]{
                "Pei… there goes another.","Paying the house, as usual.","Ow. My gelt.",
                "Consider it a donation.","Take it. Take it all." } },
            { "BROKE", new[]{
                "I have nothing left to give.","My pockets are officially lint.",
                "Can I pay you in latkes?","This is fine. Everything is fine." } },
            { "JAB", new[]{
                "Beginner's luck.","Enjoy it while it lasts.","The dreidel is clearly broken.",
                "Hmph. Lucky spin.","I'm letting you have that one." } },
            { "JAB_PAY", new[]{
                "Thank you kindly.","Into the pot it goes.","Don't mind if I do.",
                "That'll do nicely." } },
            { "ELIM", new[]{
                "I'm out. It's been an honour.","No gelt, no glory.","Tell my story.",
                "I'll just watch, then." } }
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
