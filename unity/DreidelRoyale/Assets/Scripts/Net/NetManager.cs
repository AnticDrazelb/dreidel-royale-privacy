using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DreidelRoyale.Audio;
using DreidelRoyale.Core;
using DreidelRoyale.UI;
using Random = UnityEngine.Random;

namespace DreidelRoyale.Net
{
    /// <summary>
    /// Multiplayer, ported from the web build's peer-to-peer model: the host owns the game
    /// state and every other phone renders what it is told. A spin is resolved once, on the
    /// host, and broadcast as an already-decided landing — which is why two phones never
    /// disagree about which letter came up.
    ///
    /// Everything here is transport-agnostic. LAN sockets and a relay differ in how bytes
    /// reach the other phone, not in what the table does with them.
    /// </summary>
    public class NetManager : MonoBehaviour
    {
        public GameController GC;
        public UIManager UI;
        public NetUI Screens;

        public INetTransport Transport;

        // ---- identity ----
        public bool IsHost { get; private set; }
        public bool IsObserver { get; private set; }
        public string MySeatId = "HOST";        // which seat is mine when hosting
        public string MySeatKnownId;            // authoritative seat id, told to us by the host
        public string MyName = "Player";
        public string RoomCodeText { get; private set; }

        readonly List<INetPeer> _connections = new List<INetPeer>();
        INetPeer _conn;                          // the client's link to the host

        string _joinedCode;
        bool _hostEnded, _reconnecting, _takeoverInProgress;
        int _reconnectTries;
        float _hostLossStart;
        List<string> _hostOrder = new List<string>();
        NetState _prevSnapshot;

        public readonly ChatSystem Chat = new ChatSystem();

        readonly Dictionary<INetPeer, float> _lastSeen = new Dictionary<INetPeer, float>();
        Coroutine _heartbeat, _reconnectTimer, _autoTakeover, _lossCountdown;

        public bool Active { get { return Transport != null && (IsHost || _conn != null); } }

        void Update() { if (Transport != null) Transport.Poll(); }

        // ---------------------------------------------------------------
        //  wiring
        // ---------------------------------------------------------------
        void Attach(INetTransport t)
        {
            Detach();
            Transport = t;
            t.OnReady += HandleReady;
            t.OnPeerConnected += HandlePeerConnected;
            t.OnPeerDisconnected += HandlePeerDisconnected;
            t.OnMessage += HandleMessage;
            t.OnHostLost += HandleHostLost;
            t.OnError += HandleError;
        }

        void Detach()
        {
            if (Transport == null) return;
            Transport.OnReady -= HandleReady;
            Transport.OnPeerConnected -= HandlePeerConnected;
            Transport.OnPeerDisconnected -= HandlePeerDisconnected;
            Transport.OnMessage -= HandleMessage;
            Transport.OnHostLost -= HandleHostLost;
            Transport.OnError -= HandleError;
            Transport.Shutdown();
            Transport = null;
        }

        public void LeaveEverything()
        {
            StopHeartbeat();
            StopMigrationTimers();
            if (IsHost) BroadcastRaw(NetMsg.Of(MsgType.HostEnd));   // tell the table before going
            _connections.Clear();
            _lastSeen.Clear();
            Chat.Clear();
            _conn = null;
            IsHost = false; IsObserver = false;
            _hostEnded = false; _reconnecting = false; _takeoverInProgress = false;
            _joinedCode = null; RoomCodeText = null;
            _prevSnapshot = null;
            Detach();
        }

        // ---------------------------------------------------------------
        //  host
        // ---------------------------------------------------------------
        public void HostGame(INetTransport transport, string name)
        {
            MyName = string.IsNullOrEmpty(name) ? "Player" : name;
            IsHost = true;
            MySeatId = "HOST";
            GC.IsLocalGame = false;
            GC.G = new GameState
            {
                Ante = GC.AnteAmount, BaseAnte = GC.AnteAmount,
                Rules = GC.RulesMode, Env = GC.HostEnvChoice
            };
            RoomCodeText = RoomCode.Generate();
            _joinedCode = RoomCodeText;

            TransportFactory = () => new LanTransport();
            Attach(transport);
            Screens.ShowLobby(RoomCodeText, true, "Opening the table...");
            transport.Host(RoomCodeText);
        }

        void HandleReady()
        {
            if (_takeoverInProgress) { MigrateToHost(); return; }
            if (IsHost)
            {
                GC.G.Players.Add(new Player("HOST", MyName, Consts.StartCoins) { Skin = GC.MySkinChoice });
                Screens.SetLobbyStatus("ONLINE", true);
                Screens.ShowLobby(RoomCodeText, true, "Share the code - friends join from their phones");
                Screens.RefreshLobby();
                StartHeartbeat();
            }
            else
            {
                _conn = Transport.HostLink;
                StopMigrationTimers();
                _reconnecting = false; _reconnectTries = 0;
                Screens.HideReconnect();
                Screens.ShowLobby(_joinedCode, false, "Waiting for the host...");
                Screens.SetLobbyStatus("CONNECTED", true);
                StartHeartbeat();
            }
        }

        void HandleError(string why)
        {
            if (_takeoverInProgress) { _takeoverInProgress = false; ScheduleReconnectOrGiveUp(); return; }
            if (_reconnecting) { ScheduleReconnectOrGiveUp(); return; }
            Screens.JoinFailed(why);
            UI.Toast(why, true);
        }

        void HandlePeerConnected(INetPeer peer)
        {
            if (!IsHost) return;
            _connections.Add(peer);
            _lastSeen[peer] = Time.time;
            peer.Send(NetMsg.Of(MsgType.RequestName).ToJson());
        }

        void HandlePeerDisconnected(INetPeer peer)
        {
            if (!IsHost) return;
            _connections.Remove(peer);
            _lastSeen.Remove(peer);

            var gone = GC.G.Players.FirstOrDefault(p => p.Id == peer.Id);
            if (gone != null && GC.G.Status == GameStatus.Playing)
            {
                // don't delete players mid-game - keep their seat so they can reconnect
                gone.Disconnected = true;
                UI.Toast(gone.Name + " dropped - seat held for reconnect", true);
                Chat.AddNotice(gone.Name + " dropped");
                var cur = GC.G.Current;
                if (cur != null && cur.Id == peer.Id) AdvanceTurnPastDisconnected();
            }
            else if (gone != null)
            {
                GC.G.Players.Remove(gone);
            }
            Broadcast();
        }

        /// <summary>If the turn belongs to a dropped player, skip ahead so play isn't stuck.</summary>
        void AdvanceTurnPastDisconnected()
        {
            int n = GC.G.TurnIndex, safety = 0;
            do
            {
                n = (n + 1) % Mathf.Max(1, GC.G.Players.Count);
                safety++;
            }
            while (safety < 16 && n < GC.G.Players.Count
                   && (GC.G.Players[n].Eliminated || GC.G.Players[n].Disconnected));
            GC.G.TurnIndex = n;
        }

        // ---------------------------------------------------------------
        //  join
        // ---------------------------------------------------------------
        public void JoinGame(INetTransport transport, string code, string name)
        {
            MyName = string.IsNullOrEmpty(name) ? "Player" : name;
            IsHost = false;
            IsObserver = false;
            GC.IsLocalGame = false;
            _joinedCode = RoomCode.Clean(code);
            RoomCodeText = _joinedCode;
            TransportFactory = () => new LanTransport();
            Attach(transport);
            transport.Join(_joinedCode);
        }

        // ---------------------------------------------------------------
        //  messages
        // ---------------------------------------------------------------
        void HandleMessage(INetPeer from, string json)
        {
            var m = NetMsg.FromJson(json);
            if (m == null || string.IsNullOrEmpty(m.type)) return;
            if (from != null) _lastSeen[from] = Time.time;      // any message counts as liveness
            if (IsHost && from != null) HostData(m, from);
            else ClientData(m);
        }

        void HostData(NetMsg d, INetPeer c)
        {
            switch (d.type)
            {
                case MsgType.Heartbeat:
                    break;

                case MsgType.JoinInfo:
                {
                    var wantName = CleanName(d.name);

                    // Reconnect: a held seat with this name is rebound to the new link - but a
                    // name alone is guessable, so the seat's token (minted at first join and
                    // kept client-side) must match too. No token, no rebind: an unknown
                    // claimant becomes an observer rather than stealing a held seat.
                    var held = GC.G.Players.FirstOrDefault(p =>
                        p.Disconnected && !p.Forfeited && p.Name == wantName
                        && !string.IsNullOrEmpty(p.Token) && d.token == p.Token);
                    if (held != null)
                    {
                        held.Id = c.Id;
                        held.Disconnected = false;
                        if (Unlocks.ValidSkin(d.skin)) held.Skin = d.skin;
                        c.Send(new NetMsg { type = MsgType.YouAre, id = held.Id, name = held.Name }.ToJson());
                        UI.Toast(held.Name + " reconnected");
                        Broadcast();
                        return;
                    }

                    var seated = GC.G.Players.FirstOrDefault(p => p.Id == c.Id);
                    if (seated != null)
                    {
                        // A duplicate JOIN_INFO from a peer we already seated (a retry, a flaky
                        // link) is answered idempotently rather than ignored - the client may
                        // simply have missed YOU_ARE.
                        c.Send(new NetMsg { type = MsgType.YouAre, id = seated.Id, name = seated.Name }.ToJson());
                        c.Send(StateMsg().ToJson());
                        return;
                    }

                    if (GC.G.Status != GameStatus.Lobby)
                    {
                        // The game is already running and this isn't a held seat, so they join
                        // as an observer. This is also how a forfeited ex-host comes back.
                        c.Send(NetMsg.Of(MsgType.Observer).ToJson());
                        c.Send(StateMsg().ToJson());
                        UI.Toast("An observer joined the table");
                        return;
                    }

                    if (GC.G.Players.Count >= 8)
                    {
                        c.Send(NetMsg.Of(MsgType.Observer).ToJson());
                        c.Send(StateMsg().ToJson());
                        return;
                    }

                    var name = UniqueName(wantName);
                    var token = Guid.NewGuid().ToString("N").Substring(0, 10);
                    Chat.AddNotice(name + " joined the table");
                    GC.G.Players.Add(new Player(c.Id, name, Consts.StartCoins)
                    {
                        Skin = Unlocks.ValidSkin(d.skin) ? d.skin : "wood",
                        Token = token
                    });
                    c.Send(new NetMsg { type = MsgType.YouAre, id = c.Id, name = name, token = token }.ToJson());
                    Sfx.Play("coin");
                    Broadcast();
                    break;
                }

                case MsgType.Skin:
                {
                    var p = GC.G.Players.FirstOrDefault(pl => pl.Id == c.Id);
                    if (p != null && Unlocks.ValidSkin(d.skin)) { p.Skin = d.skin; Broadcast(); }
                    break;
                }

                case MsgType.Vote:
                    // Lobby only: a mid-game vote is at best churn, at worst a griefing lever.
                    if (GC.G.Status == GameStatus.Lobby && EnvDefs.All.ContainsKey(d.env ?? ""))
                    {
                        _envVotes[c.Id] = d.env;
                        Broadcast();
                    }
                    break;

                case MsgType.ActionSpin:
                {
                    var cur = GC.G.Current;
                    if (GC.G.Status == GameStatus.Playing && cur != null && cur.Id == c.Id)
                        GC.ExecutePhysicsSpin(d.power);
                    break;
                }

                case MsgType.Chat:
                {
                    if (!Chat.Allow(c.Id)) return;              // one voice, not a flood
                    var body = ChatSystem.Sanitise(d.text);
                    if (body == null) return;
                    // The name comes from the seat, never from the message, so nobody can put
                    // words in someone else's mouth. A speaker with no seat is an observer.
                    var seat = GC.G.Players.FindIndex(p => p.Id == c.Id);
                    var who = seat >= 0 ? GC.G.Players[seat].Name : "Observer";
                    RelayChat(who, body, seat);
                    break;
                }
            }
        }

        readonly Dictionary<string, string> _envVotes = new Dictionary<string, string>();

        void ClientData(NetMsg d)
        {
            switch (d.type)
            {
                case MsgType.RequestName:
                    Send(new NetMsg
                    {
                        type = MsgType.JoinInfo,
                        name = MyName,
                        skin = GC.MySkinChoice,
                        token = Store.Get("drdl-token-" + _joinedCode)
                    });
                    break;

                case MsgType.StateUpdate:
                    if (d.order != null && d.order.Count > 0) _hostOrder = d.order;
                    SyncState(d.state);
                    break;

                case MsgType.Observer:
                    EnterObserverMode();
                    break;

                case MsgType.YouAre:
                    MySeatKnownId = d.id;
                    if (!string.IsNullOrEmpty(d.name)) MyName = d.name;
                    if (!string.IsNullOrEmpty(d.token)) Store.Set("drdl-token-" + _joinedCode, d.token);
                    break;

                case MsgType.AnimSpin:
                    GC.PerformNetworkSpin(d.delta, d.final, d.wobble, d.duration,
                                          d.power > 0f ? d.power : 0.6f);
                    break;

                case MsgType.StartCount:
                    if (!string.IsNullOrEmpty(d.env)) GC.ApplyEnv(d.env);
                    GC.StartCoroutine(GC.Countdown(null));
                    break;

                case MsgType.Chat:
                    Chat.Receive(d.name, d.text, Mathf.RoundToInt(d.delta));
                    break;

                case MsgType.HostEnd:
                    _hostEnded = true;
                    BeginHostLossFlow(true);
                    break;
            }
        }

        // ---------------------------------------------------------------
        //  state
        // ---------------------------------------------------------------
        NetMsg StateMsg()
        {
            return new NetMsg
            {
                type = MsgType.StateUpdate,
                state = NetState.From(GC.G),
                order = _connections.Select(c => c.Id).ToList()
            };
        }

        public void Broadcast()
        {
            if (!IsHost) return;

            // Invariant: no two seats share a peer id, ever. The JOIN_INFO handler blocks
            // duplicates at the source; this is the backstop, running on every broadcast so
            // even a future race that spawned a ghost seat is collapsed before any client
            // sees it or a turn stalls on it. Keeps the first (real) occurrence.
            var seen = new HashSet<string>();
            int removed = 0;
            GC.G.Players = GC.G.Players.Where(p =>
            {
                if (seen.Contains(p.Id)) { removed++; return false; }
                seen.Add(p.Id);
                return true;
            }).ToList();
            if (removed > 0 && GC.G.TurnIndex >= GC.G.Players.Count) GC.G.TurnIndex = 0;

            var msg = StateMsg().ToJson();
            foreach (var c in _connections.ToList()) if (c.IsOpen) c.Send(msg);

            if (GC.G.Status == GameStatus.Lobby) Screens.RefreshLobby();
            else if (GC.G.Status == GameStatus.Playing) GC.Hud.Refresh();
            else if (GC.G.Status == GameStatus.GameOver)
            {
                var w = GC.G.Players.FirstOrDefault(p => !p.Eliminated);
                GC.ShowWinner(w != null ? w.Name : "Nobody");
            }
        }

        void BroadcastRaw(NetMsg m)
        {
            var json = m.ToJson();
            foreach (var c in _connections.ToList()) if (c.IsOpen) c.Send(json);
        }

        public void Send(NetMsg m)
        {
            if (_conn != null && _conn.IsOpen) _conn.Send(m.ToJson());
        }

        /// <summary>Broadcast an already-decided landing, so no two phones disagree.</summary>
        public void BroadcastSpin(float delta, float final, float wobble, float duration, float power)
        {
            if (!IsHost) return;
            BroadcastRaw(new NetMsg
            {
                type = MsgType.AnimSpin,
                delta = delta, final = final, wobble = wobble, duration = duration, power = power
            });
        }

        public void SendSpinRequest(float power)
        {
            Send(new NetMsg { type = MsgType.ActionSpin, power = power });
        }

        /// <summary>Say something at the table. Guests ask; the host is the one who relays.</summary>
        public void SayChat(string text)
        {
            var body = ChatSystem.Sanitise(text);
            if (body == null) return;
            if (IsHost)
            {
                if (!Chat.Allow("HOST")) return;
                var seat = GC.G.Players.FindIndex(p => p.Id == MySeatId);
                RelayChat(seat >= 0 ? GC.G.Players[seat].Name : MyName, body, seat);
            }
            else Send(new NetMsg { type = MsgType.Chat, text = body });
        }

        void RelayChat(string who, string body, int seat)
        {
            var msg = new NetMsg { type = MsgType.Chat, name = who, text = body };
            msg.delta = seat;                     // seat index rides along for the avatar colour
            BroadcastRaw(msg);
            Chat.Receive(who, body, seat);        // the host shows its own relay too
        }

        public void SendSkin(string skin) { Send(new NetMsg { type = MsgType.Skin, skin = skin }); }
        public void SendVote(string env) { Send(new NetMsg { type = MsgType.Vote, env = env }); }

        /// <summary>
        /// A client rendering the host's truth. Everything visual that the host produced as a
        /// side effect of applying a turn is reproduced here by diffing against the previous
        /// snapshot: the gelt flies, the deltas pop, and eliminations announce themselves.
        /// </summary>
        void SyncState(NetState s)
        {
            if (s == null) return;
            var old = _prevSnapshot;
            s.Into(GC.G);
            _prevSnapshot = s.Clone();

            if (GC.G.Status == GameStatus.Lobby)
            {
                if (!string.IsNullOrEmpty(GC.G.Env)) GC.ApplyEnv(GC.G.Env);
                Screens.RefreshLobby();
                return;
            }

            if (GC.G.Status == GameStatus.Playing)
            {
                if (!string.IsNullOrEmpty(GC.G.Env)) GC.ApplyEnv(GC.G.Env);
                UI.HideWinner();                       // a rematch from the host

                bool wasPlaying = old != null && old.status == GameStatus.Playing.ToString();
                if (!wasPlaying)
                {
                    UI.ShowGame();
                    GC.StartingPlayers = Mathf.Max(GC.StartingPlayers, GC.G.Players.Count);
                    GC.ResetShowdown();
                    GC.Music.SetIntensity(1);
                    GC.View.SetDrama(false);
                }

                int alive = GC.G.AliveCount;
                if (alive == 2 && GC.StartingPlayers > 2) GC.TriggerShowdownPublic();

                if (wasPlaying)
                {
                    if (s.ante > old.ante)
                    {
                        UI.Toast("Stakes rise - ante is now " + s.ante);
                        Sfx.Play("go"); Sfx.Buzz(30, 40, 60);
                    }
                    for (int i = 0; i < s.players.Count; i++)
                    {
                        var p = s.players[i];
                        var prev = old.players.FirstOrDefault(q => q.Id == p.Id);
                        if (prev == null) continue;
                        int diff = p.Coins - prev.Coins;
                        var row = GC.Hud.RowFor(i);
                        if (diff > 0) { UI.Fx.FlyGelt(GC.Hud.PotBox, row, diff); GC.Hud.PopDelta(i, "+" + diff, true); }
                        else if (diff < 0) { UI.Fx.FlyGelt(row, GC.Hud.PotBox, -diff); GC.Hud.PopDelta(i, "-" + (-diff), false); }
                        if (!prev.Eliminated && p.Eliminated)
                        {
                            UI.Toast(p.Name + " is out of gelt!", true);
                            Sfx.Play("elim");
                        }
                    }
                }
                GC.Hud.Refresh();
                return;
            }

            if (GC.G.Status == GameStatus.GameOver)
            {
                var w = GC.G.Players.FirstOrDefault(p => !p.Eliminated);
                GC.ShowWinner(w != null ? w.Name : "Nobody");
            }
        }

        // ---------------------------------------------------------------
        //  lobby -> game
        // ---------------------------------------------------------------
        public void StartCountdown()
        {
            if (!IsHost) return;
            if (GC.G.Players.Count < 2) { UI.Toast("Waiting for at least 2 players", true); return; }

            var chosen = TallyEnvVote();
            GC.G.Env = chosen;
            GC.ApplyEnv(chosen);
            GC.G.Ante = GC.AnteAmount; GC.G.BaseAnte = GC.AnteAmount; GC.G.Rules = GC.RulesMode;
            BroadcastRaw(new NetMsg { type = MsgType.StartCount, env = chosen });
            GC.StartCoroutine(GC.Countdown(() => { GC.BeginPlayPublic(); Broadcast(); }));
        }

        /// <summary>Everyone gets a say; the host's own pick breaks a tie.</summary>
        string TallyEnvVote()
        {
            var counts = new Dictionary<string, int>();
            foreach (var v in _envVotes.Values)
                if (EnvDefs.All.ContainsKey(v)) counts[v] = (counts.ContainsKey(v) ? counts[v] : 0) + 1;
            var mine = GC.HostEnvChoice;
            counts[mine] = (counts.ContainsKey(mine) ? counts[mine] : 0) + 1;

            string best = mine; int bestN = -1;
            foreach (var kv in counts) if (kv.Value > bestN) { bestN = kv.Value; best = kv.Key; }
            return best;
        }

        // ---------------------------------------------------------------
        //  liveness
        // ---------------------------------------------------------------
        void StartHeartbeat()
        {
            StopHeartbeat();
            _heartbeat = StartCoroutine(HeartbeatLoop());
        }

        void StopHeartbeat()
        {
            if (_heartbeat != null) { StopCoroutine(_heartbeat); _heartbeat = null; }
        }

        IEnumerator HeartbeatLoop()
        {
            var beat = new WaitForSeconds(2f);
            float lastWatch = 0f;
            while (true)
            {
                if (!IsHost && _conn != null && _conn.IsOpen) Send(NetMsg.Of(MsgType.Heartbeat));

                // Host-side liveness. Guests beat every 2s, but a mobile network drop can leave
                // a socket looking open for the best part of a minute - during which the turn
                // sits on a zombie. Force-closing a silent link funnels it through the EXISTING
                // close path: seat held, turn advanced, state broadcast.
                if (IsHost && Time.time - lastWatch > 3f)
                {
                    lastWatch = Time.time;
                    foreach (var c in _connections.ToList())
                    {
                        float seen;
                        if (c.IsOpen && _lastSeen.TryGetValue(c, out seen) && Time.time - seen > 12f)
                            c.Close();
                    }
                }
                yield return beat;
            }
        }

        // ---------------------------------------------------------------
        //  host loss, reconnect, migration
        // ---------------------------------------------------------------
        void HandleHostLost()
        {
            if (_reconnecting || _migrating) return;      // already handling it
            BeginHostLossFlow(_hostEnded);
        }

        bool _migrating;

        void BeginHostLossFlow(bool deliberate)
        {
            if (IsHost || _migrating) return;

            // With only one other player there is nobody to migrate the chair to - but the
            // host (deliberate leave OR accidental drop) may still be trying to rejoin, so
            // there is always a grace window before falling back to the menu, rather than
            // ending the game on the spot.
            int others = GC.G.Players.Count(p => p.Id != "HOST" && !p.Forfeited);
            bool canMigrate = others > 1 && !IsObserver;

            _reconnecting = true;
            _reconnectTries = 0;
            _hostLossStart = Time.time;
            _migrating = true;

            Screens.ShowReconnect(
                deliberate ? "Host Left" : "Host Disconnected",
                !canMigrate ? "Waiting for the host to come back..."
                    : deliberate ? "The host left - choosing a new host..."
                    : "Connection to the host lost - reconnecting... a new host will be chosen shortly.",
                canMigrate);

            StartCoroutine(DelayedReconnect(0.6f));       // the old host may return

            if (!canMigrate)
            {
                // Can't migrate, so we can't wait forever: run a countdown and bail out to the
                // menu if the host hasn't come back by zero.
                if (_lossCountdown != null) StopCoroutine(_lossCountdown);
                _lossCountdown = StartCoroutine(HostLossCountdown(30));
            }
            else
            {
                // Automatic host assignment: near-instant on a deliberate leave, a short grace
                // for an accidental drop (the host may just be reloading). Staggered by join
                // order so two claims don't race; losers simply rejoin as clients.
                int pos = Mathf.Max(0, _hostOrder.IndexOf(MySeatKnownId ?? ""));
                float grace = deliberate ? 1.2f : 8f;
                if (_autoTakeover != null) StopCoroutine(_autoTakeover);
                _autoTakeover = StartCoroutine(AutoTakeover(grace + pos * 4f));
            }
        }

        IEnumerator DelayedReconnect(float t)
        {
            yield return new WaitForSeconds(t);
            TryReconnect();
        }

        IEnumerator AutoTakeover(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!IsHost && !IsObserver) AttemptTakeover();
        }

        IEnumerator HostLossCountdown(int secs)
        {
            int left = secs;
            while (left > 0)
            {
                Screens.SetReconnectCountdown("Returning to menu in " + left + "s...");
                yield return new WaitForSeconds(1f);
                left--;
                if (!_reconnecting) yield break;
            }
            Screens.SetReconnectCountdown("");
            LeaveEverything();
            GC.QuitToMenuFromNetwork();
        }

        void StopMigrationTimers()
        {
            _migrating = false;
            if (_reconnectTimer != null) { StopCoroutine(_reconnectTimer); _reconnectTimer = null; }
            if (_autoTakeover != null) { StopCoroutine(_autoTakeover); _autoTakeover = null; }
            if (_lossCountdown != null) { StopCoroutine(_lossCountdown); _lossCountdown = null; }
            _reconnecting = false;
            Screens.SetReconnectCountdown("");
        }

        public void TryReconnect()
        {
            if (IsHost || _takeoverInProgress) return;
            if (_conn != null && _conn.IsOpen) return;    // a stale retry must never kill a live game
            if (string.IsNullOrEmpty(_joinedCode)) { Screens.ShowReconnectDead("Connection lost."); return; }

            _reconnectTries++;
            Attach(NewTransportLikeCurrent());
            Transport.Join(_joinedCode);
            _reconnectTimer = StartCoroutine(ReconnectWatchdog());
        }

        IEnumerator ReconnectWatchdog()
        {
            yield return new WaitForSeconds(4f);
            _reconnectTimer = null;
            if (_conn == null || !_conn.IsOpen) ScheduleReconnectOrGiveUp();
        }

        void ScheduleReconnectOrGiveUp()
        {
            if (IsHost) return;
            if (_conn != null && _conn.IsOpen) return;
            if (_hostLossStart > 0f && Time.time - _hostLossStart > 120f)
            {
                StopMigrationTimers();
                Screens.ShowReconnectDead("This game has ended.");
                return;
            }
            if (_reconnectTimer != null) return;          // one retry in flight at a time
            _reconnectTimer = StartCoroutine(DelayedReconnect(2.5f));
        }

        /// <summary>Claiming the chair means opening the room under the same code.</summary>
        void AttemptTakeover()
        {
            if (IsHost || _takeoverInProgress || string.IsNullOrEmpty(_joinedCode)) return;
            if (IsObserver) { StartCoroutine(DelayedReconnect(0.3f)); return; }   // observers never take the chair

            _takeoverInProgress = true;
            if (_autoTakeover != null) { StopCoroutine(_autoTakeover); _autoTakeover = null; }
            Screens.SetReconnectMessage("Claiming the table...");

            Attach(NewTransportLikeCurrent());
            Transport.Host(_joinedCode);
        }

        void MigrateToHost()
        {
            IsHost = true;
            _takeoverInProgress = false;
            _hostEnded = false;
            _conn = null;
            _connections.Clear();
            _lastSeen.Clear();
            RoomCodeText = _joinedCode;
            StopMigrationTimers();
            Screens.HideReconnect();

            // Hard-reset the spin state machine: a spin that never resolved, or a charge in
            // flight when the host vanished, must never block the new host.
            GC.HardResetSpin();

            // adopt my existing seat
            var mine = GC.G.Players.FirstOrDefault(p => p.Id == MySeatKnownId)
                    ?? GC.G.Players.FirstOrDefault(p => p.Name == MyName && !p.Forfeited);
            MySeatId = mine != null ? mine.Id : "HOST";
            if (mine != null) mine.Disconnected = false;

            // the old host's seat is forfeited - they may return only as an observer
            var oh = GC.G.Players.FirstOrDefault(p => p.Id == "HOST");
            if (oh != null && !oh.Forfeited)
            {
                oh.Forfeited = true;
                oh.Disconnected = true;
                if (GC.G.Status == GameStatus.Playing && !oh.Eliminated)
                {
                    oh.Eliminated = true;
                    UI.Toast(oh.Name + "'s seat is forfeited", true);
                }
            }

            // everyone else must find their way back to the new chair
            foreach (var pp in GC.G.Players) if (pp.Id != MySeatId && !pp.Forfeited) pp.Disconnected = true;

            UI.Toast("You are the new host - the table is yours");
            Sfx.Play("go"); Sfx.Buzz(30, 50, 80);

            if (GC.G.Status == GameStatus.Playing || GC.G.Status == GameStatus.GameOver)
            {
                var cur = GC.G.Current;
                if (cur == null || cur.Eliminated || cur.Disconnected || cur.Forfeited)
                    AdvanceTurnPastDisconnected();
                UI.ShowGame();
                GC.Hud.Refresh();
                GC.CheckWinConditionPublic(GC.G.AliveCount);
            }
            else
            {
                Screens.ShowLobby(RoomCodeText, true, "Share the code - friends join from their phones");
                Screens.SetLobbyStatus("ONLINE", true);
                Screens.RefreshLobby();
            }
            StartHeartbeat();
        }

        public void ManualTakeover() { AttemptTakeover(); }

        void EnterObserverMode()
        {
            IsObserver = true;
            StopMigrationTimers();
            Screens.HideReconnect();
            Screens.ShowObserverChip(true);
            if (GC.G.Status == GameStatus.Playing || GC.G.Status == GameStatus.GameOver) UI.ShowGame();
            UI.Toast("Watching as an observer");
        }

        /// <summary>
        /// How to build another link of the kind that got us here. Set once when the table is
        /// opened or joined, so a reconnect or a takeover never silently changes transport.
        /// </summary>
        public Func<INetTransport> TransportFactory = () => new LanTransport();

        INetTransport NewTransportLikeCurrent() { return TransportFactory(); }

        // ---------------------------------------------------------------
        //  helpers
        // ---------------------------------------------------------------
        /// <summary>
        /// Names are protocol keys - the reconnect rebind matches BY NAME - so they must be
        /// sane and unique: trimmed, capped, defaulted, and suffixed on collision so a new
        /// "Ben" can never be rebound into a dropped Ben's held seat.
        /// </summary>
        static string CleanName(string n)
        {
            if (string.IsNullOrEmpty(n)) return "Player";
            n = System.Text.RegularExpressions.Regex.Replace(n, "\\s+", " ").Trim();
            if (n.Length > 16) n = n.Substring(0, 16);
            return string.IsNullOrEmpty(n) ? "Player" : n;
        }

        string UniqueName(string basename)
        {
            var name = basename;
            int k = 2;
            while (GC.G.Players.Any(p => p.Name == name))
            {
                var stem = basename.Length > 13 ? basename.Substring(0, 13) : basename;
                name = stem + " " + (k++);
            }
            return name;
        }

        /// <summary>Whose turn it is, from this phone's point of view.</summary>
        public bool IsMySeat(Player p)
        {
            if (p == null || IsObserver) return false;
            return IsHost ? p.Id == MySeatId : p.Id == MySeatKnownId;
        }

        void OnApplicationQuit() { LeaveEverything(); }
    }
}
