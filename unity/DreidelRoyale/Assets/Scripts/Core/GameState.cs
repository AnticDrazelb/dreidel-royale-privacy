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
        /// Minted by the host at first join and kept client-side. A held seat is rebound by
        /// name, and a name alone is guessable, so the token is what stops a stranger walking
        /// into a dropped player's chair.
        /// </summary>
        public string Token = "";

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
