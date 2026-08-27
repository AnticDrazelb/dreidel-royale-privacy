// Internet play over Unity Relay.
//
// Guarded because it needs three packages that this project does not ship by default
// (com.unity.services.core, com.unity.services.authentication, com.unity.services.relay)
// plus com.unity.transport. Adding them and defining DREIDEL_RELAY turns it on; see
// unity/README.md for the three steps. LAN play needs none of this and always works.
//
// Relay is Unity's own free-tier NAT punch-through: the host allocates, gets back a join
// code, and guests dial that code. It is the closest thing to what PeerJS gave the web
// build, and the room-code UX is identical either way.
#if DREIDEL_RELAY
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace DreidelRoyale.Net
{
    public class RelayTransport : MonoBehaviour, INetTransport
    {
        public const int MaxGuests = 7;          // eight seats at the table, host included

        public string Name { get { return "Online"; } }

        public event Action OnReady;
        public event Action<INetPeer> OnPeerConnected;
        public event Action<INetPeer> OnPeerDisconnected;
        public event Action<INetPeer, string> OnMessage;
        public event Action OnHostLost;
        public event Action<string> OnError;

        public bool IsReady { get; private set; }
        public INetPeer HostLink { get { return _hostPeer; } }

        /// <summary>The relay join code, which is what players actually type. Host side only.</summary>
        public string JoinCode { get; private set; }

        NetworkDriver _driver;
        NetworkConnection _clientConn;
        readonly List<NetworkConnection> _serverConns = new List<NetworkConnection>();
        readonly Dictionary<int, RelayPeer> _peers = new Dictionary<int, RelayPeer>();
        RelayPeer _hostPeer;
        bool _isHost, _running;

        // ---------------------------------------------------------------
        public void Host(string code) { StartCoroutine(HostRoutine()); }
        public void Join(string code) { StartCoroutine(JoinRoutine(RoomCode.Clean(code))); }

        IEnumerator EnsureServices()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                var init = UnityServices.InitializeAsync();
                while (!init.IsCompleted) yield return null;
            }
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                Fail("Online play needs a linked Unity project. Wi-Fi play works without one.");
                yield break;
            }
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                var signIn = AuthenticationService.Instance.SignInAnonymouslyAsync();
                while (!signIn.IsCompleted) yield return null;
                if (signIn.IsFaulted) { Fail("Couldn't reach the online service."); yield break; }
            }
        }

        IEnumerator HostRoutine()
        {
            yield return EnsureServices();
            if (UnityServices.State != ServicesInitializationState.Initialized) yield break;

            Allocation alloc = null;
            var task = RelayService.Instance.CreateAllocationAsync(MaxGuests);
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) { Fail("Couldn't open an online table."); yield break; }
            alloc = task.Result;

            var codeTask = RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
            while (!codeTask.IsCompleted) yield return null;
            if (codeTask.IsFaulted) { Fail("Couldn't get a room code."); yield break; }
            JoinCode = codeTask.Result;

            var settings = new NetworkSettings();
            settings.WithRelayParameters(ref RelayServerData(alloc, "udp"));
            _driver = NetworkDriver.Create(settings);
            if (_driver.Bind(NetworkEndPoint.AnyIpv4) != 0) { Fail("Couldn't bind the online table."); yield break; }
            _driver.Listen();

            _isHost = true; _running = true; IsReady = true;
            if (OnReady != null) OnReady();
        }

        IEnumerator JoinRoutine(string code)
        {
            yield return EnsureServices();
            if (UnityServices.State != ServicesInitializationState.Initialized) yield break;

            var task = RelayService.Instance.JoinAllocationAsync(code);
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) { Fail("No online table with that code."); yield break; }

            var settings = new NetworkSettings();
            settings.WithRelayParameters(ref RelayServerData(task.Result, "udp"));
            _driver = NetworkDriver.Create(settings);
            if (_driver.Bind(NetworkEndPoint.AnyIpv4) != 0) { Fail("Couldn't start the connection."); yield break; }

            _clientConn = _driver.Connect();
            _isHost = false; _running = true;
        }

        static ref RelayServerData RelayServerData(Allocation a, string type)
        {
            _scratch = new RelayServerData(a, type);
            return ref _scratch;
        }

        static RelayServerData _scratch;

        static ref RelayServerData RelayServerData(JoinAllocation a, string type)
        {
            _scratchJoin = new RelayServerData(a, type);
            return ref _scratchJoin;
        }

        static RelayServerData _scratchJoin;

        // ---------------------------------------------------------------
        public void Poll()
        {
            if (!_running || !_driver.IsCreated) return;
            _driver.ScheduleUpdate().Complete();

            if (_isHost)
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
                    PumpConnection(_serverConns[i], ref i);
            }
            else
            {
                if (!_clientConn.IsCreated) return;
                DataStreamReader stream;
                NetworkEvent.Type ev;
                while ((ev = _driver.PopEventForConnection(_clientConn, out stream)) != NetworkEvent.Type.Empty)
                {
                    if (ev == NetworkEvent.Type.Connect)
                    {
                        _hostPeer = new RelayPeer(this, _clientConn) { IsHostLink = true };
                        IsReady = true;
                        if (OnReady != null) OnReady();
                    }
                    else if (ev == NetworkEvent.Type.Data)
                    {
                        var json = ReadString(ref stream);
                        if (OnMessage != null) OnMessage(null, json);
                    }
                    else if (ev == NetworkEvent.Type.Disconnect)
                    {
                        _clientConn = default(NetworkConnection);
                        IsReady = false;
                        if (OnHostLost != null) OnHostLost();
                    }
                }
            }
        }

        void PumpConnection(NetworkConnection c, ref int index)
        {
            DataStreamReader stream;
            NetworkEvent.Type ev;
            while ((ev = _driver.PopEventForConnection(c, out stream)) != NetworkEvent.Type.Empty)
            {
                if (ev == NetworkEvent.Type.Data)
                {
                    RelayPeer p;
                    if (_peers.TryGetValue(c.GetHashCode(), out p) && OnMessage != null)
                        OnMessage(p, ReadString(ref stream));
                }
                else if (ev == NetworkEvent.Type.Disconnect)
                {
                    RelayPeer p;
                    if (_peers.TryGetValue(c.GetHashCode(), out p))
                    {
                        p.MarkClosed();
                        _peers.Remove(c.GetHashCode());
                        if (OnPeerDisconnected != null) OnPeerDisconnected(p);
                    }
                    _serverConns.RemoveAt(index);
                    index--;
                    return;
                }
            }
        }

        static string ReadString(ref DataStreamReader stream)
        {
            int len = stream.ReadUShort();
            var bytes = new byte[len];
            for (int i = 0; i < len; i++) bytes[i] = stream.ReadByte();
            return Encoding.UTF8.GetString(bytes);
        }

        internal void SendTo(NetworkConnection c, string json)
        {
            if (!_driver.IsCreated || !c.IsCreated) return;
            var bytes = Encoding.UTF8.GetBytes(json);
            DataStreamWriter writer;
            if (_driver.BeginSend(c, out writer) != 0) return;
            writer.WriteUShort((ushort)bytes.Length);
            foreach (var b in bytes) writer.WriteByte(b);
            _driver.EndSend(writer);
        }

        public void Shutdown()
        {
            _running = false; IsReady = false;
            if (_driver.IsCreated) { _driver.Dispose(); }
            _serverConns.Clear(); _peers.Clear();
            _hostPeer = null;
            JoinCode = null;
        }

        void OnDestroy() { Shutdown(); }

        void Fail(string why)
        {
            IsReady = false;
            if (OnError != null) OnError(why);
        }
    }

    public class RelayPeer : INetPeer
    {
        public string Id { get; private set; }
        public bool IsOpen { get; private set; }
        public bool IsHostLink;

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
#endif
