using System;

namespace DreidelRoyale.Net
{
    /// <summary>
    /// One live link to a peer. Mirrors what a PeerJS DataConnection gave the web build: an
    /// identity, an open flag, a send, and a close — nothing more, because nothing more is used.
    /// </summary>
    public interface INetPeer
    {
        /// <summary>Stable per-connection id. Seats are keyed by this.</summary>
        string Id { get; }
        bool IsOpen { get; }
        void Send(string json);
        void Close();
    }

    /// <summary>
    /// What the game needs from a network. The protocol above is transport-agnostic on
    /// purpose: LAN sockets and a relay differ in how bytes reach the other phone, not in
    /// what the table does with them.
    ///
    /// Everything is reported on the main thread — implementations queue and drain in Poll —
    /// so handlers can touch Unity objects freely.
    /// </summary>
    public interface INetTransport
    {
        string Name { get; }

        /// <summary>Open a room under `code`. Reports failure through OnError.</summary>
        void Host(string code);

        /// <summary>Dial the room named by `code`.</summary>
        void Join(string code);

        void Shutdown();

        /// <summary>Drain queued events onto the main thread. Called once per frame.</summary>
        void Poll();

        /// <summary>The room is open and reachable (host), or the dial connected (client).</summary>
        event Action OnReady;

        /// <summary>A peer connected. Host side only.</summary>
        event Action<INetPeer> OnPeerConnected;

        /// <summary>A peer's link closed. Host side only.</summary>
        event Action<INetPeer> OnPeerDisconnected;

        /// <summary>A message arrived. `from` is null on the client (it can only be the host).</summary>
        event Action<INetPeer, string> OnMessage;

        /// <summary>The link to the host dropped. Client side only.</summary>
        event Action OnHostLost;

        /// <summary>Something went wrong, with a line fit to show a player.</summary>
        event Action<string> OnError;

        /// <summary>True once hosting or joined and usable.</summary>
        bool IsReady { get; }

        /// <summary>The client's own link to the host, or null when hosting.</summary>
        INetPeer HostLink { get; }
    }
}
