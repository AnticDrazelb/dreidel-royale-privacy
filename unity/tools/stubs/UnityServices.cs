// Minimal Unity Gaming Services + Unity Transport surface, for type-checking outside the
// editor. Only the members RelayTransport actually touches are declared, with the real
// signatures - a stub that lies is worse than no stub at all.
using System;
using System.Threading.Tasks;

namespace Unity.Services.Core
{
    public enum ServicesInitializationState { Uninitialized, Initializing, Initialized }

    public static class UnityServices
    {
        public static ServicesInitializationState State { get { return default(ServicesInitializationState); } }
        public static Task InitializeAsync() { return null; }
    }
}

namespace Unity.Services.Authentication
{
    public interface IAuthenticationService
    {
        bool IsSignedIn { get; }
        Task SignInAnonymouslyAsync();
    }

    public static class AuthenticationService
    {
        public static IAuthenticationService Instance { get { return null; } }
    }
}

namespace Unity.Services.Relay.Models
{
    public class Allocation
    {
        public Guid AllocationId { get { return default(Guid); } }
    }

    public class JoinAllocation
    {
        public Guid AllocationId { get { return default(Guid); } }
    }
}

namespace Unity.Services.Relay
{
    using Unity.Services.Relay.Models;
    using Unity.Networking.Transport.Relay;

    /// Relay 1.1 exposes the conversion as extensions rather than RelayServerData constructors.
    public static class RelayServiceExtensions
    {
        public static RelayServerData ToRelayServerData(this Allocation a, string connectionType)
        { return default(RelayServerData); }
        public static RelayServerData ToRelayServerData(this JoinAllocation a, string connectionType)
        { return default(RelayServerData); }
    }

    public interface IRelayService
    {
        Task<Allocation> CreateAllocationAsync(int maxConnections, string region = null);
        Task<string> GetJoinCodeAsync(Guid allocationId);
        Task<JoinAllocation> JoinAllocationAsync(string joinCode);
    }

    public static class RelayService
    {
        public static IRelayService Instance { get { return null; } }
    }
}

namespace Unity.Collections
{
    /// Transport 2.0 moved these two out of Unity.Networking.Transport into here, so using
    /// them needs its own directive. Declared in the real namespace for that reason.
    public struct DataStreamReader
    {
        public int Length { get { return 0; } }
        public ushort ReadUShort() { return 0; }
        public byte ReadByte() { return 0; }
    }

    public struct DataStreamWriter
    {
        public bool WriteUShort(ushort v) { return false; }
        public bool WriteByte(byte v) { return false; }
    }
}

namespace Unity.Networking.Transport
{
    using Unity.Collections;

    public struct NetworkEndpoint
    {
        public static NetworkEndpoint AnyIpv4 { get { return default(NetworkEndpoint); } }
    }

    public struct NetworkSettings { }

    public struct NetworkPipeline { }

    public class FragmentationPipelineStage { }
    public class ReliableSequencedPipelineStage { }

    public struct NetworkConnection
    {
        public bool IsCreated { get { return false; } }
        public static bool operator ==(NetworkConnection a, NetworkConnection b) { return false; }
        public static bool operator !=(NetworkConnection a, NetworkConnection b) { return false; }
        public override bool Equals(object o) { return false; }
        public override int GetHashCode() { return 0; }
    }

    public struct NetworkEvent
    {
        public enum Type { Empty, Data, Connect, Disconnect }
    }


    public struct JobHandle
    {
        public void Complete() { }
    }

    public struct NetworkDriver
    {
        public static NetworkDriver Create(NetworkSettings settings) { return default(NetworkDriver); }
        public bool IsCreated { get { return false; } }
        public bool Bound { get { return false; } }
        public bool Listening { get { return false; } }
        public NetworkPipeline CreatePipeline(params Type[] stages) { return default(NetworkPipeline); }
        public int Bind(NetworkEndpoint endpoint) { return 0; }
        public int Listen() { return 0; }
        public NetworkConnection Connect() { return default(NetworkConnection); }
        public NetworkConnection Accept() { return default(NetworkConnection); }
        public JobHandle ScheduleUpdate() { return default(JobHandle); }
        public NetworkEvent.Type PopEventForConnection(NetworkConnection c, out DataStreamReader reader)
        {
            reader = default(DataStreamReader);
            return NetworkEvent.Type.Empty;
        }
        public int BeginSend(NetworkPipeline pipe, NetworkConnection id, out DataStreamWriter writer,
                             int requiredPayloadSize = 0)
        {
            writer = default(DataStreamWriter);
            return 0;
        }
        public int EndSend(DataStreamWriter writer) { return 0; }
        public void Dispose() { }
    }
}

namespace Unity.Networking.Transport.Utilities
{
    /// <summary>
    /// The pipeline stage parameters. These are the reason the stub declares them here
    /// rather than on NetworkSettings itself: in the real package they are extension
    /// methods in THIS namespace, so calling them needs its own using directive, and a
    /// stub that put them on the struct would happily compile code the editor rejects.
    /// (The real ones take `ref this` and return `ref NetworkSettings`; mcs cannot express
    /// that C# 7.2 form, and the returned reference is discarded at every call site here.)
    /// </summary>
    public static class PipelineParameterExtensions
    {
        public static NetworkSettings WithFragmentationStageParameters(
            this NetworkSettings settings, int payloadCapacity = 4 * 1024) { return settings; }

        public static NetworkSettings WithReliableStageParameters(
            this NetworkSettings settings, int windowSize = 32) { return settings; }
    }
}

namespace Unity.Networking.Transport.Relay
{
    using Unity.Services.Relay.Models;

    /// Verified against com.unity.transport 2.4.0: these two constructors exist, and the
    /// ToRelayServerData extension methods this port once used do not exist at all.
    public struct RelayServerData
    {
        public RelayServerData(Allocation allocation, string connectionType) { }
        public RelayServerData(JoinAllocation allocation, string connectionType) { }
    }

    /// An extension method in this namespace, so the using directive is required.
    public static class RelayParameterExtensions
    {
        public static NetworkSettings WithRelayParameters(
            this NetworkSettings settings, ref RelayServerData serverData,
            int relayConnectionTimeMS = 9000) { return settings; }
    }
}
