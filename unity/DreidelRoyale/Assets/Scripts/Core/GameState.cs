using System;
using System.Collections.Generic;
using System.Linq;

namespace DreidelRoyale.Core
{
    public enum GameStatus { Lobby, Playing, GameOver }

    [Serializable]
    public class Player
    {
        public string Id;
        public string Name;
        public int Coins;
        public bool Eliminated;
        public bool Cpu;
        public bool Disconnected;
        public bool Forfeited;
        public string Skin = "";

        /// <summary>
        /// The VERIFIER for this seat, not the secret itself.
        ///
        /// A held seat is rebound by name, and a name alone is guessable, so a reconnecting
        /// player must also present a token minted for them at first join. That token reaches
        /// its owner once, privately, in YOU_ARE — and never appears here.
        ///
        /// It has to be this way round. The seat list is broadcast to the whole table on every
        /// state update, so anything stored on a Player is public to every client; a plaintext
        /// token here would hand every player the key to every other player's chair, which is
        /// the exact theft it exists to prevent. But it cannot simply be left off the wire
        /// either: when the host drops, the new host inherits its seat list from the last
        /// state update it received, and still has to validate reconnects. A one-way hash
        /// satisfies both — it survives migration, and it is useless to anyone listening.
        /// </summary>
        public string TokenHash = "";

        /// <summary>
        /// A copy of this seat as it travels. Seats are copied rather than sent by reference
        /// so that anything the wire form omits can never reach back into the live game state.
        /// </summary>
        public Player ForWire()
        {
            return new Player
            {
                Id = Id, Name = Name, Coins = Coins,
                Eliminated = Eliminated, Cpu = Cpu,
                Disconnected = Disconnected, Forfeited = Forfeited,
                Skin = Skin, TokenHash = TokenHash
            };
        }

        /// <summary>
        /// Hash a presented token to compare against <see cref="TokenHash"/>. SHA-256 folded
        /// to 16 hex characters: 64 bits of preimage resistance is far more than a value that
        /// only has to survive one evening at a dreidel table needs, and it keeps the seat
        /// list small enough to matter on a relay.
        /// </summary>
        /// <summary>
        /// The single rule for a player-supplied name, wherever it comes from — a guest's
        /// JOIN_INFO, the host's own text field, or a pass-and-play entry.
        ///
        /// Angle brackets are defused because names are rendered into the same rich-text
        /// sinks as chat: the seat rows, the toasts and the chat feed all parse markup. Chat
        /// has always been defused; a name is the more damaging of the two and was not. It
        /// is broadcast in every state update, re-rendered on every row, and lasts the whole
        /// game, where a chat line scrolls away.
        /// </summary>
        public static string CleanName(string raw, int max = 16)
        {
            if (string.IsNullOrEmpty(raw)) return "Player";
            var n = raw.Replace('<', '(').Replace('>', ')');
            n = System.Text.RegularExpressions.Regex.Replace(n, "\\s+", " ").Trim();
            if (n.Length > max) n = n.Substring(0, max);
            return n.Length == 0 ? "Player" : n;
        }

        public static string HashToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return "";
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token));
                var sb = new System.Text.StringBuilder(16);
                for (int i = 0; i < 8; i++) sb.Append(bytes[i].ToString("x2"));
                return sb.ToString();
            }
        }

        public Player() { }
        public Player(string id, string name, int coins, bool cpu = false)
        {
            Id = id; Name = name; Coins = coins; Cpu = cpu;
        }
    }

    /// <summary>Per-game tallies. Feeds the winner card, the share string and the unlock diff.</summary>
    [Serializable]
    public class GameStats
    {
        public int Spins;
        public Dictionary<string, int> Gimels = new Dictionary<string, int>();
        public int BiggestSweep;
        public string SweepBy = "";
        public List<string> History = new List<string>();

        public int GimelsFor(string playerId)
        {
            int n;
            return Gimels.TryGetValue(playerId, out n) ? n : 0;
        }

        public int TotalGimels() { return Gimels.Values.Sum(); }
    }

    [Serializable]
    public class GameState
    {
        public List<Player> Players = new List<Player>();
        public int Pot;
        public int TurnIndex;
        public GameStatus Status = GameStatus.Lobby;
        public int Round = 1;
        public int Ante = 1;
        public int BaseAnte = 1;
        public string Rules = "rising";
        public string Env = "midnight";
        public GameStats Stats = new GameStats();

        public Player Current
        {
            get
            {
                return (TurnIndex >= 0 && TurnIndex < Players.Count) ? Players[TurnIndex] : null;
            }
        }

        public IEnumerable<Player> Alive { get { return Players.Where(p => !p.Eliminated); } }
        public int AliveCount { get { return Players.Count(p => !p.Eliminated); } }

        /// <summary>
        /// The outright gelt leader, or null when the top stack is shared. The leader
        /// wears the crown, and the gold dreidel stands in for a default wood one.
        /// </summary>
        public string LeaderId()
        {
            var alive = Alive.ToList();
            if (alive.Count == 0) return null;
            int max = alive.Max(p => p.Coins);
            var leaders = alive.Where(p => p.Coins == max).ToList();
            return leaders.Count == 1 ? leaders[0].Id : null;
        }
    }
}
