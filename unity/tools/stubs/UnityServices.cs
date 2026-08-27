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
    public class AuthenticationService
    {
        public static AuthenticationService Instance { get { return null; } }
        public bool IsSignedIn { get { return false; } }
        public Task SignInAnonymouslyAsync() { return null; }
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

    public class RelayService
    {
        public static RelayService Instance { get { return null; } }
        public Task<Allocation> CreateAllocationAsync(int maxConnections, string region = null) { return null; }
        public Task<string> GetJoinCodeAsync(Guid allocationId) { return null; }
        public Task<JoinAllocation> JoinAllocationAsync(string joinCode) { return null; }
    }
}

namespace Unity.Networking.Transport
{
    public struct NetworkEndPoint
    {
        public static NetworkEndPoint AnyIpv4 { get { return default(NetworkEndPoint); } }
    }

    // In the real package these three are `ref this` extension methods returning
    // `ref NetworkSettings`. mcs rejects that C# 7.2 form, so the stub declares them as
    // instance members instead: identical at every call site the port uses, since the
    // returned reference is always discarded.
    public struct NetworkSettings
    {
        public void WithFragmentationStageParameters(int payloadCapacity = 4 * 1024) { }
        public void WithReliableStageParameters(int windowSize = 32) { }
        public void WithRelayParameters(ref Relay.RelayServerData serverData,
                                        int relayConnectionTimeMS = 9000) { }
    }

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
        public int Bind(NetworkEndPoint endpoint) { return 0; }
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

namespace Unity.Networking.Transport.Relay
{
    using Unity.Services.Relay.Models;

    public struct RelayServerData
    {
        public RelayServerData(Allocation allocation, string connectionType) { }
        public RelayServerData(JoinAllocation allocation, string connectionType) { }
    }
}
