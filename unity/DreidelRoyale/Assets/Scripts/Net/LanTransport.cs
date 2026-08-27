using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DreidelRoyale.Net
{
    /// <summary>
    /// Local-network play over plain TCP, with UDP broadcast to turn a four-letter room code
    /// into an address.
    ///
    /// This is the transport that costs nothing and can't be switched off: no account, no
    /// service, no key, no quota, nothing to expire. It is also the case the game is actually
    /// for — the web build's own join screen says "best played on Wi-Fi", because a dreidel
    /// game is a room full of people. Everything runs on background threads and is handed to
    /// the main thread in Poll, so handlers can touch Unity objects freely.
    /// </summary>
    public class LanTransport : INetTransport
    {
        public const int DiscoveryPort = 47653;
        public const int BasePort = 47654;
        const string Magic = "DRDL1";

        public string Name { get { return "Wi-Fi"; } }

        public event Action OnReady;
        public event Action<INetPeer> OnPeerConnected;
        public event Action<INetPeer> OnPeerDisconnected;
        public event Action<INetPeer, string> OnMessage;
        public event Action OnHostLost;
        public event Action<string> OnError;

        public bool IsReady { get; private set; }
        public INetPeer HostLink { get { return _hostPeer; } }

        readonly ConcurrentQueue<Action> _main = new ConcurrentQueue<Action>();
        readonly List<LanPeer> _peers = new List<LanPeer>();

        TcpListener _listener;
        UdpClient _beacon;
        Thread _acceptThread, _beaconThread, _discoverThread;
        volatile bool _running;

        string _code;
        int _port;
        LanPeer _hostPeer;
        AndroidMulticastLock _multicast;

        // ---------------------------------------------------------------
        //  host
        // ---------------------------------------------------------------
        public void Host(string code)
        {
            Shutdown();
            _code = RoomCode.Clean(code);
            _running = true;

            try
            {
                // A fixed base port means a player whose network blocks broadcast can still be
                // reached by typing the host's IP, so discovery is a convenience rather than a
                // dependency.
                for (int i = 0; i < 12 && _listener == null; i++)
                {
                    try
                    {
                        var l = new TcpListener(IPAddress.Any, BasePort + i);
                        l.Start();
                        _listener = l;
                        _port = BasePort + i;
                    }
                    catch (SocketException) { /* in use — step along */ }
                }
                if (_listener == null) throw new IOException("No free port for the table");
            }
            catch (Exception e)
            {
                Fail("Couldn't open the table on this network: " + e.Message);
                return;
            }

            _multicast = AndroidMulticastLock.Acquire();   // the beacon listens for broadcast
            _acceptThread = StartThread(AcceptLoop, "drdl-accept");
            _beaconThread = StartThread(BeaconLoop, "drdl-beacon");

            IsReady = true;
            Post(() => { if (OnReady != null) OnReady(); });
        }

        void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    var peer = new LanPeer(client, this);
                    lock (_peers) _peers.Add(peer);
                    peer.Start();
                    Post(() => { if (OnPeerConnected != null) OnPeerConnected(peer); });
                }
                catch (SocketException) { break; }        // listener stopped
                catch (ObjectDisposedException) { break; }
                catch (Exception) { break; }
            }
        }

        /// <summary>Answer "who has room ABCD?" with the port this table is listening on.</summary>
        void BeaconLoop()
        {
            UdpClient udp = null;
            try
            {
                udp = new UdpClient();
                udp.ExclusiveAddressUse = false;
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
                _beacon = udp;

                while (_running)
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    var data = udp.Receive(ref from);
                    var text = Encoding.UTF8.GetString(data);
                    // "DRDL1?ABCD" -> "DRDL1!ABCD:port"
                    if (!text.StartsWith(Magic + "?")) continue;
                    var want = text.Substring(Magic.Length + 1);
                    if (!string.Equals(want, _code, StringComparison.OrdinalIgnoreCase)) continue;
                    var reply = Encoding.UTF8.GetBytes(Magic + "!" + _code + ":" + _port);
                    udp.Send(reply, reply.Length, from);
                }
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
            catch (Exception e) { Post(() => Fail("Discovery failed: " + e.Message)); }
            finally { if (udp != null) { try { udp.Close(); } catch { } } }
        }

        // ---------------------------------------------------------------
        //  join
        // ---------------------------------------------------------------
        public void Join(string code)
        {
            Shutdown();
            _code = RoomCode.Clean(code);
            _running = true;
            _multicast = AndroidMulticastLock.Acquire();
            _discoverThread = StartThread(DiscoverAndConnect, "drdl-join");
        }

        void DiscoverAndConnect()
        {
            string host = null;
            int port = BasePort;

            // Typing the host's address is the escape hatch for a network that drops broadcast
            // (guest Wi-Fi and AP isolation both do).
            IPAddress direct;
            if (IPAddress.TryParse(_code, out direct)) { host = _code; }
            else if (!Discover(out host, out port))
            {
                Post(() => Fail("No table with that code on this Wi-Fi. Check the code, or make sure both phones are on the same network."));
                return;
            }

            try
            {
                var client = new TcpClient();
                var async = client.BeginConnect(host, port, null, null);
                if (!async.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5)) || !client.Connected)
                {
                    try { client.Close(); } catch { }
                    Post(() => Fail("Found the table but couldn't connect."));
                    return;
                }
                client.EndConnect(async);

                _hostPeer = new LanPeer(client, this) { IsHostLink = true };
                _hostPeer.Start();
                IsReady = true;
                Post(() => { if (OnReady != null) OnReady(); });
            }
            catch (Exception e)
            {
                Post(() => Fail("Couldn't reach the table: " + e.Message));
            }
        }

        bool Discover(out string host, out int port)
        {
            host = null; port = BasePort;
            UdpClient udp = null;
            try
            {
                udp = new UdpClient { EnableBroadcast = true };
                udp.Client.ReceiveTimeout = 700;
                var ask = Encoding.UTF8.GetBytes(Magic + "?" + _code);
                var target = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);

                // Ask a few times: the first broadcast after a radio wakes is the one most
                // likely to be dropped.
                for (int attempt = 0; attempt < 6 && _running; attempt++)
                {
                    udp.Send(ask, ask.Length, target);
                    try
                    {
                        var from = new IPEndPoint(IPAddress.Any, 0);
                        var data = udp.Receive(ref from);
                        var text = Encoding.UTF8.GetString(data);
                        var prefix = Magic + "!" + _code + ":";
                        if (!text.StartsWith(prefix)) continue;
                        if (!int.TryParse(text.Substring(prefix.Length), out port)) continue;
                        host = from.Address.ToString();
                        return true;
                    }
                    catch (SocketException) { /* timed out — ask again */ }
                }
            }
            catch (Exception) { }
            finally { if (udp != null) { try { udp.Close(); } catch { } } }
            return false;
        }

        // ---------------------------------------------------------------
        public void Shutdown()
        {
            _running = false;
            IsReady = false;

            // Close() calls back into PeerClosed, which removes from this same list, so the
            // snapshot is taken first rather than mutating what is being enumerated.
            List<LanPeer> closing;
            lock (_peers) { closing = new List<LanPeer>(_peers); _peers.Clear(); }
            foreach (var p in closing) p.Close();
            if (_hostPeer != null) { _hostPeer.Close(); _hostPeer = null; }

            if (_listener != null) { try { _listener.Stop(); } catch { } _listener = null; }
            if (_beacon != null) { try { _beacon.Close(); } catch { } _beacon = null; }

            StopThread(ref _acceptThread); StopThread(ref _beaconThread); StopThread(ref _discoverThread);
            if (_multicast != null) { _multicast.Release(); _multicast = null; }

            Action drop;
            while (_main.TryDequeue(out drop)) { }
        }

        static void StopThread(ref Thread t)
        {
            if (t == null) return;
            try { if (t.IsAlive) t.Join(120); } catch { }
            t = null;
        }

        public void Poll()
        {
            Action a;
            // A bounded drain: a burst of arrivals must not stall a frame.
            for (int i = 0; i < 64 && _main.TryDequeue(out a); i++)
            {
                try { a(); } catch (Exception e) { Debug.LogWarning("net handler: " + e.Message); }
            }
        }

        // ---- called from peer threads ----
        internal void Post(Action a) { _main.Enqueue(a); }

        internal void PeerMessage(LanPeer p, string json)
        {
            Post(() => { if (OnMessage != null) OnMessage(p.IsHostLink ? null : p, json); });
        }

        internal void PeerClosed(LanPeer p)
        {
            lock (_peers) _peers.Remove(p);
            Post(() =>
            {
                if (p.IsHostLink)
                {
                    if (_hostPeer == p) { IsReady = false; if (OnHostLost != null) OnHostLost(); }
                }
                else if (OnPeerDisconnected != null) OnPeerDisconnected(p);
            });
        }

        void Fail(string why)
        {
            IsReady = false;
            if (OnError != null) OnError(why);
        }

        Thread StartThread(ThreadStart fn, string name)
        {
            var t = new Thread(fn) { IsBackground = true, Name = name };
            t.Start();
            return t;
        }

        /// <summary>Every local address worth showing a player who has to type one in.</summary>
        public static List<string> LocalAddresses()
        {
            var found = new List<string>();
            try
            {
                foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                        found.Add(ip.ToString());
            }
            catch { }
            return found;
        }
    }

    /// <summary>
    /// One TCP link, framed as a 4-byte big-endian length followed by UTF-8 JSON. TCP is a
    /// stream, so without the length prefix two messages sent back to back arrive as one.
    /// </summary>
    public class LanPeer : INetPeer
    {
        public string Id { get; private set; }
        public bool IsOpen { get { return _open && _client != null && _client.Connected; } }
        public bool IsHostLink;

        readonly TcpClient _client;
        readonly LanTransport _owner;
        NetworkStream _stream;
        Thread _reader;
        volatile bool _open;
        readonly object _writeLock = new object();

        static int _nextId;

        public LanPeer(TcpClient client, LanTransport owner)
        {
            _client = client;
            _owner = owner;
            _client.NoDelay = true;                       // turn-based and tiny: latency beats packing
            Id = "P" + (++_nextId) + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
        }

        public void Start()
        {
            _stream = _client.GetStream();
            _open = true;
            _reader = new Thread(ReadLoop) { IsBackground = true, Name = "drdl-read" };
            _reader.Start();
        }

        void ReadLoop()
        {
            var header = new byte[4];
            try
            {
                while (_open)
                {
                    if (!ReadExact(header, 4)) break;
                    int len = (header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3];
                    if (len <= 0 || len > 1 << 20) break;   // a sane cap: our messages are bytes, not megabytes
                    var body = new byte[len];
                    if (!ReadExact(body, len)) break;
                    _owner.PeerMessage(this, Encoding.UTF8.GetString(body));
                }
            }
            catch (Exception) { /* closed under us */ }
            finally { CloseInternal(); }
        }

        bool ReadExact(byte[] buffer, int count)
        {
            int got = 0;
            while (got < count)
            {
                int n = _stream.Read(buffer, got, count - got);
                if (n <= 0) return false;
                got += n;
            }
            return true;
        }

        public void Send(string json)
        {
            if (!_open) return;
            try
            {
                var body = Encoding.UTF8.GetBytes(json);
                var frame = new byte[4 + body.Length];
                frame[0] = (byte)(body.Length >> 24); frame[1] = (byte)(body.Length >> 16);
                frame[2] = (byte)(body.Length >> 8);  frame[3] = (byte)body.Length;
                Buffer.BlockCopy(body, 0, frame, 4, body.Length);
                lock (_writeLock) { _stream.Write(frame, 0, frame.Length); _stream.Flush(); }
            }
            catch (Exception) { CloseInternal(); }
        }

        public void Close() { CloseInternal(); }

        void CloseInternal()
        {
            if (!_open) return;
            _open = false;
            try { if (_stream != null) _stream.Close(); } catch { }
            try { _client.Close(); } catch { }
            _owner.PeerClosed(this);
        }
    }

    /// <summary>
    /// Android drops broadcast packets at the Wi-Fi driver unless something holds a multicast
    /// lock, so discovery silently finds nothing without one. A no-op everywhere else.
    /// </summary>
    public class AndroidMulticastLock
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject _lock;
#endif

        public static AndroidMulticastLock Acquire()
        {
            var m = new AndroidMulticastLock();
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var wifi = activity.Call<AndroidJavaObject>("getSystemService", "wifi"))
                {
                    m._lock = wifi.Call<AndroidJavaObject>("createMulticastLock", "dreidel-royale");
                    m._lock.Call("setReferenceCounted", true);
                    m._lock.Call("acquire");
                }
            }
            catch { m._lock = null; }
#endif
            return m;
        }

        public void Release()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { if (_lock != null) { _lock.Call("release"); _lock.Dispose(); _lock = null; } } catch { }
#endif
        }
    }
}
