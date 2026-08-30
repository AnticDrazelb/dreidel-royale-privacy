using System;
using System.Collections.Generic;
using UnityEngine;
using DreidelRoyale.Core;

namespace DreidelRoyale.Net
{
    public struct ChatLine
    {
        public string Name, Text;
        public int Seat;            // -1 for a table notice
        public float At;
        public bool IsNotice;
    }

    /// <summary>
    /// Table talk between players. The host relays: a guest never speaks directly to another
    /// guest, so there is one place that decides what a message says, who it came from, and
    /// how often anyone may send one.
    ///
    /// The sender's name is stamped from their seat rather than taken from the message, so
    /// nobody can put words in someone else's mouth.
    /// </summary>
    public class ChatSystem
    {
        public const int MaxLength = 120;
        public const int Keep = 60;            // scrollback worth holding
        const float MinInterval = 0.7f;        // per sender, seconds

        readonly List<ChatLine> _lines = new List<ChatLine>();
        readonly Dictionary<string, float> _lastSent = new Dictionary<string, float>();

        public IList<ChatLine> Lines { get { return _lines; } }

        /// <summary>Raised when a line lands, so the HUD can flash it without polling.</summary>
        public Action<ChatLine> OnLine;

        /// <summary>
        /// Quick phrases. Typing mid-turn on a phone is a chore, and a dreidel game is fast —
        /// most of what anyone wants to say at a table is one of these.
        /// </summary>
        public static readonly string[] QuickPhrases =
        {
            "Nice spin!", "Gevalt!", "Your turn", "Mazel tov!",
            "That's my gelt", "One more round?", "Ready", "Oy vey"
        };

        public void Clear() { _lines.Clear(); _lastSent.Clear(); }

        public void AddNotice(string text)
        {
            // Notices carry player names ("Ruthie dropped"), so they reach the parser too.
            var clean = Sanitise(text);
            if (string.IsNullOrEmpty(clean)) return;
            Add(new ChatLine { Name = "", Text = clean, Seat = -1, At = Time.time, IsNotice = true });
        }

        void Add(ChatLine line)
        {
            _lines.Add(line);
            if (_lines.Count > Keep) _lines.RemoveRange(0, _lines.Count - Keep);
            if (OnLine != null) OnLine(line);
        }

        /// <summary>
        /// A message arriving at its final destination. `seat` places the avatar colour; -1
        /// leaves it as a plain line.
        /// </summary>
        public void Receive(string name, string text, int seat)
        {
            var clean = Sanitise(text);
            if (string.IsNullOrEmpty(clean)) return;
            Add(new ChatLine { Name = Core.Player.CleanName(name), Text = clean, Seat = seat, At = Time.time });
        }

        /// <summary>True when this sender is inside their rate limit.</summary>
        public bool Allow(string senderId)
        {
            float last;
            if (_lastSent.TryGetValue(senderId ?? "", out last) && Time.time - last < MinInterval) return false;
            _lastSent[senderId ?? ""] = Time.time;
            return true;
        }

        /// <summary>
        /// Trim, cap, and defuse. The chat view renders rich text so the game's own colours
        /// work, which means an unescaped angle bracket from a player is a way to recolour or
        /// break someone else's screen — so brackets never survive the trip.
        /// </summary>
        public static string Sanitise(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            var s = raw.Replace('<', '(').Replace('>', ')');
            s = System.Text.RegularExpressions.Regex.Replace(s, "\\s+", " ").Trim();
            if (s.Length > MaxLength) s = s.Substring(0, MaxLength);
            return s.Length == 0 ? null : s;
        }
    }
}
