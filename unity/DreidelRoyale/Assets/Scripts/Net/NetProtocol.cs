using System;
using System.Collections.Generic;
using UnityEngine;
using DreidelRoyale.Core;

namespace DreidelRoyale.Net
{
    /// <summary>
    /// The wire protocol, ported message-for-message from the web build. It is deliberately
    /// small: the host is authoritative, so a turn costs one broadcast, and a spin costs one
    /// ANIM_SPIN carrying the landing the host already decided.
    ///
    /// Keeping the shape identical is what lets the transport underneath be swapped — the
    /// game layer never learns whether these arrived over Wi-Fi or a relay.
    /// </summary>
    public static class MsgType
    {
        public const string RequestName = "REQUEST_NAME";
        public const string JoinInfo    = "JOIN_INFO";
        public const string YouAre      = "YOU_ARE";
        public const string Observer    = "OBSERVER";
        public const string StateUpdate = "STATE_UPDATE";
        public const string AnimSpin    = "ANIM_SPIN";
        public const string ActionSpin  = "ACTION_SPIN";
        public const string StartCount  = "START_COUNT";
        public const string HostEnd     = "HOST_END";
        public const string Skin        = "SKIN";
        public const string Vote        = "VOTE";
        public const string Heartbeat   = "HB";
    }

    /// <summary>
    /// One envelope for every message. JsonUtility has no polymorphism and no dictionaries, so
    /// the union is flat and unused fields simply travel empty — at this message volume that
    /// costs nothing and keeps parsing to a single type.
    /// </summary>
    [Serializable]
    public class NetMsg
    {
        public string type;

        // JOIN_INFO / YOU_ARE
        public string name;
        public string skin;
        public string token;
        public string id;

        // STATE_UPDATE
        public NetState state;
        public List<string> order = new List<string>();

        // ANIM_SPIN
        public float delta, final, wobble, duration, power;

        // ACTION_SPIN
        // (power, above)

        // START_COUNT / VOTE
        public string env;

        public static NetMsg Of(string t) { return new NetMsg { type = t }; }

        public string ToJson() { return JsonUtility.ToJson(this); }

        public static NetMsg FromJson(string json)
        {
            try { return JsonUtility.FromJson<NetMsg>(json); }
            catch { return null; }
        }
    }

    /// <summary>
    /// The game state as it travels. GameState itself carries a Dictionary (the per-player
    /// gimel tally) that JsonUtility cannot serialise, so the wire form flattens it and the
    /// two conversions live here rather than being scattered over the network code.
    /// </summary>
    [Serializable]
    public class NetState
    {
        public List<Player> players = new List<Player>();
        public int pot, turnIndex, round, ante, baseAnte;
        public string status, rules, env;

        public int spins, biggestSweep;
        public string sweepBy;
        public List<string> gimelIds = new List<string>();
        public List<int> gimelCounts = new List<int>();
        public List<string> history = new List<string>();

        public static NetState From(GameState g)
        {
            var s = new NetState
            {
                players = g.Players,
                pot = g.Pot, turnIndex = g.TurnIndex, round = g.Round,
                ante = g.Ante, baseAnte = g.BaseAnte,
                status = g.Status.ToString(), rules = g.Rules, env = g.Env,
                spins = g.Stats.Spins, biggestSweep = g.Stats.BiggestSweep,
                sweepBy = g.Stats.SweepBy, history = g.Stats.History
            };
            foreach (var kv in g.Stats.Gimels) { s.gimelIds.Add(kv.Key); s.gimelCounts.Add(kv.Value); }
            return s;
        }

        public void Into(GameState g)
        {
            g.Players = players ?? new List<Player>();
            g.Pot = pot; g.TurnIndex = turnIndex; g.Round = round;
            g.Ante = ante; g.BaseAnte = baseAnte;
            g.Rules = rules; g.Env = env;
            GameStatus st;
            g.Status = Enum.TryParse(status ?? "Lobby", out st) ? st : GameStatus.Lobby;

            g.Stats = new GameStats
            {
                Spins = spins, BiggestSweep = biggestSweep, SweepBy = sweepBy,
                History = history ?? new List<string>()
            };
            if (gimelIds != null && gimelCounts != null)
                for (int i = 0; i < gimelIds.Count && i < gimelCounts.Count; i++)
                    g.Stats.Gimels[gimelIds[i]] = gimelCounts[i];
        }

        /// <summary>A deep-enough copy to diff the next update against.</summary>
        public NetState Clone() { return JsonUtility.FromJson<NetState>(JsonUtility.ToJson(this)); }
    }

    public static class RoomCode
    {
        // No I or O: they are the two letters people mis-read off a screen and mis-type back.
        const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";

        public static string Generate()
        {
            var s = new char[4];
            for (int i = 0; i < 4; i++) s[i] = Letters[UnityEngine.Random.Range(0, Letters.Length)];
            return new string(s);
        }

        public static bool IsValid(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length != 4) return false;
            foreach (var c in code) if (c < 'A' || c > 'Z') return false;
            return true;
        }

        public static string Clean(string raw)
        {
            return string.IsNullOrEmpty(raw) ? "" : raw.Trim().ToUpperInvariant();
        }
    }
}
