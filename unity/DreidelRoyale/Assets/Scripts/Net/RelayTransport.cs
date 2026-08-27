// Internet play, over Unity Relay.
//
// Relay is Unity's own free-tier connectivity service: the host asks for an allocation and
// gets back a short join code; guests dial that code and the relay carries the packets
// between them. Nobody has to open a port, and neither phone ever learns the other's
// address - which is the whole reason the web build used PeerJS, and why this maps onto the
// existing room-code UX without changing a single screen's shape.
//
// It needs a linked (free) Unity project - see unity/README.md. When there isn't one, the
// failure is reported as a line a player can act on, and Same Wi-Fi still works with no
// account at all.
//
// This is a plain class, not a MonoBehaviour, because NetManager already calls Poll() once
// per frame and every asynchronous step below is advanced from there. That keeps it
// interchangeable with LanTransport in TransportFactory, and keeps all Unity API contact on
// the main thread without a single lock.
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;   // the pipeline stage parameter extensions
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace DreidelRoyale.Net
{
    public class RelayTransport : INetTransport
    {
        /// <summary>Eight seats at the table, the host's chair included.</summary>
        public const int MaxGuests = 7;

        /// <summary>How long the whole open-a-room dance gets before we call it dead.</summary>
        const float SetupTimeout = 25f;

        public string Name { get { return "Online"; } }
        public bool IsOnline { get { return true; } }

        public event Action OnReady;
        public event Action<INetPeer> OnPeerConnected;
        public event Action<INetPeer> OnPeerDisconnected;
        public event Action<INetPeer, string> OnMessage;
        public event Action OnHostLost;
        public event Action<string> OnError;

        public bool IsReady { get; private set; }
        public INetPeer HostLink { get { return _hostPeer; } }

        /// <summary>
        /// The relay's own join code. Unlike a LAN table we cannot choose it: it comes back
        /// with the allocation, so it is empty until the room is actually open.
        /// </summary>
        public string RoomCode { get { return _joinCode ?? ""; } }

        // ---------------------------------------------------------------
        //  the asynchronous half, advanced one step per Poll()
        // ---------------------------------------------------------------
        enum Phase { Idle, Services, SignIn, Allocate, JoinCode, Dial, Bind, Live, Dead }

        Phase _phase = Phase.Idle;
        bool _asHost;
        string _joinCode;
        float _phaseStart;

        Task _servicesTask;
        Task _signInTask;
        Task<Allocation> _allocTask;
        Task<string> _codeTask;
        Task<JoinAllocation> _joinTask;

        NetworkDriver _driver;
        NetworkPipeline _pipeline;
        NetworkConnection _clientConn;
        readonly List<NetworkConnection> _serverConns = new List<NetworkConnection>();
        readonly Dictionary<int, RelayPeer> _peers = new Dictionary<int, RelayPeer>();
        RelayPeer _hostPeer;

        // ---------------------------------------------------------------
        //  entry points
        // ---------------------------------------------------------------
        /// <summary>`preferredCode` is ignored: a relay mints the code, we don't get a say.</summary>
        public void Host(string preferredCode)
        {
            Shutdown();
            _asHost = true;
            Advance(Phase.Services);
        }

        public void Join(string code)
        {
            Shutdown();
            _asHost = false;
            _joinCode = Net.RoomCode.Clean(code);
            Advance(Phase.Services);
        }

        void Advance(Phase next)
        {
            _phase = next;
            _phaseStart = Time.realtimeSinceStartup;
        }

        // ---------------------------------------------------------------
        public void Poll()
        {
            StepSetup();
            StepNetwork();
        }

        void StepSetup()
        {
            if (_phase == Phase.Idle || _phase == Phase.Live || _phase == Phase.Dead) return;

            // One clock over the whole sequence, so a step that hangs (no signal, a service
            // that never answers) fails visibly instead of leaving the lobby spinning.
            if (Time.realtimeSinceStartup - _phaseStart > SetupTimeout)
            {
                Fail(_asHost ? "Couldn't open an online table - check your connection."
                             : "Couldn't reach that table - check the code and your connection.");
                return;
            }

            switch (_phase)
            {
                case Phase.Services: StepServices(); break;
                case Phase.SignIn:   StepSignIn();   break;
                case Phase.Allocate: StepAllocate(); break;
                case Phase.JoinCode: StepJoinCode(); break;
                case Phase.Dial:     StepDial();     break;
                case Phase.Bind:     StepBind();     break;
            }
        }

        void StepServices()
        {
            if (_servicesTask == null)
            {
                if (UnityServices.State == ServicesInitializationState.Initialized)
                {
                    Advance(Phase.SignIn);
                    return;
                }
                try { _servicesTask = UnityServices.InitializeAsync(); }
                catch (Exception e) { FailNoProject(e); return; }
                return;
            }
            if (!_servicesTask.IsCompleted) return;
            if (_servicesTask.IsFaulted) { FailNoProject(_servicesTask.Exception); return; }
            _servicesTask = null;
            Advance(Phase.SignIn);
        }

        void StepSignIn()
        {
            // An anonymous sign-in is enough: the relay only needs to know that the caller is
            // a caller. Nobody is asked to make an account to play a dreidel game.
            if (AuthenticationService.Instance.IsSignedIn)
            {
                Advance(_asHost ? Phase.Allocate : Phase.Dial);
                return;
            }
            if (_signInTask == null)
            {
                try { _signInTask = AuthenticationService.Instance.SignInAnonymouslyAsync(); }
                catch (Exception e) { FailService(e); return; }
                return;
            }
            if (!_signInTask.IsCompleted) return;
            if (_signInTask.IsFaulted) { FailService(_signInTask.Exception); return; }
            _signInTask = null;
            Advance(_asHost ? Phase.Allocate : Phase.Dial);
        }

        void StepAllocate()
        {
            if (_allocTask == null)
            {
                try { _allocTask = RelayService.Instance.CreateAllocationAsync(MaxGuests); }
                catch (Exception e) { FailService(e); return; }
                return;
            }
            if (!_allocTask.IsCompleted) return;
            if (_allocTask.IsFaulted) { FailService(_allocTask.Exception); return; }
            Advance(Phase.JoinCode);
        }

        void StepJoinCode()
        {
            if (_codeTask == null)
            {
                try { _codeTask = RelayService.Instance.GetJoinCodeAsync(_allocTask.Result.AllocationId); }
                catch (Exception e) { FailService(e); return; }
                return;
            }
            if (!_codeTask.IsCompleted) return;
            if (_codeTask.IsFaulted) { FailService(_codeTask.Exception); return; }

            _joinCode = _codeTask.Result;
            // Relay 1.1 exposes the conversion as an extension on the allocation itself; the
            // RelayServerData constructor it replaced is gone in Unity Transport 2.
            var data = _allocTask.Result.ToRelayServerData("udp");
            if (!CreateDriver(ref data)) return;
            Advance(Phase.Bind);
        }

        void StepDial()
        {
            if (_joinTask == null)
            {
                if (string.IsNullOrEmpty(_joinCode)) { Fail("That code doesn't look right."); return; }
                try { _joinTask = RelayService.Instance.JoinAllocationAsync(_joinCode); }
                catch (Exception e) { FailService(e); return; }
                return;
            }
            if (!_joinTask.IsCompleted) return;
            if (_joinTask.IsFaulted) { Fail("No online table with that code."); return; }

            var data = _joinTask.Result.ToRelayServerData("udp");
            if (!CreateDriver(ref data)) return;
            Advance(Phase.Bind);
        }

        bool CreateDriver(ref RelayServerData data)
        {
            var settings = new NetworkSettings();
            settings.WithRelayParameters(ref data);

            // A full table's STATE_UPDATE is comfortably over a single datagram, and every
            // message the game sends has to arrive and arrive in order - a dropped turn
            // change would desync the table - so the traffic goes down a fragmenting,
            // reliable, sequenced pipeline rather than the default unreliable one.
            settings.WithFragmentationStageParameters(payloadCapacity: 16 * 1024);
            settings.WithReliableStageParameters(windowSize: 32);

            _driver = NetworkDriver.Create(settings);
            _pipeline = _driver.CreatePipeline(typeof(FragmentationPipelineStage),
                                               typeof(ReliableSequencedPipelineStage));

            if (_driver.Bind(NetworkEndpoint.AnyIpv4) != 0)
            {
                Fail("Couldn't start the connection.");
                return false;
            }
            return true;
        }

        void StepBind()
        {
            // Binding against a relay is a round trip, not a local call: the driver isn't
            // Bound until the relay has answered, so we pump it until it is.
            _driver.ScheduleUpdate().Complete();
            if (!_driver.Bound) return;

            if (_asHost)
            {
                _driver.Listen();
                IsReady = true;
                Advance(Phase.Live);
                if (OnReady != null) OnReady();
            }
            else
            {
                _clientConn = _driver.Connect();
                Advance(Phase.Live);      // ready is reported on the Connect event, not here
            }
        }

        // ---------------------------------------------------------------
        //  the live half
        // ---------------------------------------------------------------
        void StepNetwork()
        {
            if (_phase != Phase.Live || !_driver.IsCreated) return;
            _driver.ScheduleUpdate().Complete();

            if (_asHost) PumpHost();
            else PumpClient();
        }

        void PumpHost()
        {
            NetworkConnection c;
            while ((c = _driver.Accept()) != default(NetworkConnection))
            {
                _serverConns.Add(c);
                var peer = new RelayPeer(this, c);
                _peers[c.GetHashCode()] = peer;
                if (OnPeerConnected != null) OnPeerConnected(peer);
            }

            for (int i = _serverConns.Count - 1; i >= 0; i--)
            {
                var conn = _serverConns[i];
                DataStreamReader stream;
                NetworkEvent.Type ev;
                bool closed = false;

                while (!closed &&
                       (ev = _driver.PopEventForConnection(conn, out stream)) != NetworkEvent.Type.Empty)
                {
                    if (ev == NetworkEvent.Type.Data)
                    {
                        RelayPeer p;
                        if (_peers.TryGetValue(conn.GetHashCode(), out p) && OnMessage != null)
                            OnMessage(p, ReadString(ref stream));
                    }
                    else if (ev == NetworkEvent.Type.Disconnect)
                    {
                        closed = true;
                    }
                }

                if (closed)
                {
                    RelayPeer p;
                    if (_peers.TryGetValue(conn.GetHashCode(), out p))
                    {
                        p.MarkClosed();
                        _peers.Remove(conn.GetHashCode());
                        if (OnPeerDisconnected != null) OnPeerDisconnected(p);
                    }
                    _serverConns.RemoveAt(i);
                }
            }
        }

        void PumpClient()
        {
            if (!_clientConn.IsCreated) return;

            DataStreamReader stream;
            NetworkEvent.Type ev;
            while ((ev = _driver.PopEventForConnection(_clientConn, out stream)) != NetworkEvent.Type.Empty)
            {
                if (ev == NetworkEvent.Type.Connect)
                {
                    _hostPeer = new RelayPeer(this, _clientConn);
                    IsReady = true;
                    if (OnReady != null) OnReady();
                }
                else if (ev == NetworkEvent.Type.Data)
                {
                    if (OnMessage != null) OnMessage(null, ReadString(ref stream));
                }
                else if (ev == NetworkEvent.Type.Disconnect)
                {
                    _clientConn = default(NetworkConnection);
                    if (_hostPeer != null) _hostPeer.MarkClosed();

                    // A disconnect before we ever connected is a dial that failed, which the
                    // lobby must hear as an error; after that it is the host going quiet,
                    // which starts the reconnect flow instead.
                    bool wasUp = IsReady;
                    IsReady = false;
                    if (wasUp) { if (OnHostLost != null) OnHostLost(); }
                    else Fail("No online table with that code.");
                    return;
                }
            }
        }

        // ---------------------------------------------------------------
        //  framing - a ushort length then the UTF-8 body, same shape as LAN
        // ---------------------------------------------------------------
        internal void SendTo(NetworkConnection c, string json)
        {
            if (_phase != Phase.Live || !_driver.IsCreated || !c.IsCreated) return;

            var bytes = Encoding.UTF8.GetBytes(json);
            if (bytes.Length > ushort.MaxValue) return;

            DataStreamWriter writer;
            if (_driver.BeginSend(_pipeline, c, out writer) != 0) return;
            writer.WriteUShort((ushort)bytes.Length);
            for (int i = 0; i < bytes.Length; i++) writer.WriteByte(bytes[i]);
            _driver.EndSend(writer);
        }

        static string ReadString(ref DataStreamReader stream)
        {
            int len = stream.ReadUShort();
            if (len <= 0 || len > stream.Length) return "";
            var bytes = new byte[len];
            for (int i = 0; i < len; i++) bytes[i] = stream.ReadByte();
            return Encoding.UTF8.GetString(bytes);
        }

        // ---------------------------------------------------------------
        public void Shutdown()
        {
            _phase = Phase.Idle;
            IsReady = false;

            foreach (var p in _peers.Values) p.MarkClosed();
            _peers.Clear();
            _serverConns.Clear();
            if (_hostPeer != null) { _hostPeer.MarkClosed(); _hostPeer = null; }

            if (_driver.IsCreated) _driver.Dispose();
            _driver = default(NetworkDriver);
            _clientConn = default(NetworkConnection);

            _servicesTask = null; _signInTask = null;
            _allocTask = null; _codeTask = null; _joinTask = null;
            _joinCode = null;
        }

        void Fail(string why)
        {
            IsReady = false;
            _phase = Phase.Dead;
            if (_driver.IsCreated) _driver.Dispose();
            _driver = default(NetworkDriver);
            if (OnError != null) OnError(why);
        }

        /// <summary>
        /// The one failure worth naming precisely: the project has no Unity project id, so
        /// Relay was never going to work. Telling someone to "check their connection" when the
        /// build simply isn't linked would send them hunting in the wrong place.
        /// </summary>
        void FailNoProject(Exception e)
        {
            Debug.LogWarning("[Relay] services init failed: " + e);
            Fail("Online play isn't set up for this build. Same Wi-Fi still works.");
        }

        void FailService(Exception e)
        {
            Debug.LogWarning("[Relay] " + e);
            Fail(_asHost ? "Couldn't open an online table. Try again, or use Same Wi-Fi."
                         : "Couldn't reach that table. Check the code, or use Same Wi-Fi.");
        }
    }

    /// <summary>One relay connection, wearing the same face a LAN socket wears.</summary>
    public class RelayPeer : INetPeer
    {
        public string Id { get; private set; }
        public bool IsOpen { get; private set; }

        readonly RelayTransport _owner;
        readonly NetworkConnection _conn;

        public RelayPeer(RelayTransport owner, NetworkConnection conn)
        {
            _owner = owner; _conn = conn; IsOpen = true;
            Id = "R" + conn.GetHashCode();
        }

        public void Send(string json) { if (IsOpen) _owner.SendTo(_conn, json); }
        public void Close() { IsOpen = false; }
        internal void MarkClosed() { IsOpen = false; }
    }
}
