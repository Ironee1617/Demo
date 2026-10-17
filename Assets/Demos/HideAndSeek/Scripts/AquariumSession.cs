using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // Only the host simulates movement, hits, eating, role changes and victory.
    public sealed class AquariumSession : IDisposable
    {
        private const int Protocol = 4;
        [Serializable] private class Hello { public int protocol = Protocol; public string name; public Team team; }
        [Serializable] private class Message
        {
            public string kind;
            public int player;
            public Team team;
            public bool ready;
            public SwimInput input;
            public AquariumSnapshot state;
        }
        private const string Channel = "aquarium.v4";
        private const int PacketLimit = 60000;
        public AquariumModel Model { get; private set; }
        public AquariumSnapshot State { get; private set; }
        public int LocalId { get; private set; }
        public string Code { get; private set; } = "";
        public string Status { get; private set; } = "";
        public bool Busy { get; private set; }
        public bool Practice { get; private set; }
        public bool IsHost { get { return Model != null; } }
        public bool Connected { get { return LocalId != 0 && State != null; } }
        public int ReceivedSnapshots { get; private set; }
        private NetworkManager network;
        private GameObject networkObject;
        private readonly Dictionary<ulong, int> peers = new Dictionary<ulong, int>();
        private readonly Dictionary<ulong, Hello> approved = new Dictionary<ulong, Hello>();
        private int generation, nextPlayer = 1;
        private float accumulator, snapshotTimer, connectDeadline;
        private bool leaving;

        public void StartPractice(string name, Team team)
        {
            Leave(); Practice = true; LocalId = 1;
            Model = new AquariumModel(speciesCount: AquariumSpecies.Count); Model.AddPlayer(1, name, team);
            int nextId = 2;
            if (team == Team.Hider) Model.AddPlayer(nextId++, "연습 잠수부", Team.Seeker, true);
            while (nextId <= AquariumModel.MaxPlayers)
            {
                Model.AddPlayer(nextId, "연습 외계인 " + (nextId - 1), Team.Hider, true);
                nextId++;
            }
            Model.StartRound(); State = Model.Snapshot(); Status = "";
        }

        public async Task OpenInternet(bool host, string code, string name, Team team)
        {
            if (Busy || Connected) return;
            Leave(); Busy = true; Status = "Unity Relay에 연결하는 중…";
            int request = generation;
            try
            {
                if (string.IsNullOrEmpty(Application.cloudProjectId))
                    throw new InvalidOperationException("Unity Cloud 연결이 필요합니다. Edit → Project Settings → Services에서 프로젝트를 연결해 주세요.");
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync(new InitializationOptions().SetProfile("aquarium_" + Guid.NewGuid().ToString("N").Substring(0, 12)));
                if (request != generation) return;
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
                if (request != generation) return;
                CreateNetwork(name, team);
                var transport = network.GetComponent<UnityTransport>();
                if (host)
                {
                    var allocation = await RelayService.Instance.CreateAllocationAsync(AquariumModel.MaxPlayers - 1);
                    if (request != generation) return;
                    transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));
                    Code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                    if (request != generation) return;
                    StartHost(name, team);
                }
                else
                {
                    Code = code.Trim().ToUpperInvariant();
                    var allocation = await RelayService.Instance.JoinAllocationAsync(Code);
                    if (request != generation) return;
                    transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));
                    StartClient();
                }
            }
            catch (Exception ex)
            {
                if (request != generation) return;
                Leave();
                Status = "접속 실패: " + FriendlyError(ex);
                Debug.LogWarning("Aquarium connection: " + ex.GetType().Name + " / " + ex.Message);
            }
        }

        private static string FriendlyError(Exception ex)
        {
            if (ex is InvalidOperationException) return ex.Message;
            return "방 코드와 인터넷 연결을 확인해 주세요. Cloud 프로젝트의 Authentication과 Relay도 활성화되어야 합니다.\n" + ex.Message;
        }

        // Used by the two-process smoke test; normal players use Relay codes.
        public void OpenLocalTest(bool host, string name, Team team, ushort port = 7779)
        {
            Leave(); CreateNetwork(name, team);
            network.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", port);
            Code = "LOCAL TEST";
            if (host) StartHost(name, team); else StartClient();
        }

        private void CreateNetwork(string name, Team team)
        {
            networkObject = new GameObject("Aquarium Relay Session");
            var transport = networkObject.AddComponent<UnityTransport>();
            transport.MaxPayloadSize = PacketLimit;
            network = networkObject.AddComponent<NetworkManager>();
            network.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport, EnableSceneManagement = false, ConnectionApproval = true,
                TickRate = 30, ClientConnectionBufferTimeout = 20, ProtocolVersion = Protocol,
                ConnectionData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Hello { name = AquariumModel.CleanName(name), team = team }))
            };
            network.ConnectionApprovalCallback += Approve;
            network.OnClientConnectedCallback += OnConnected;
            network.OnClientDisconnectCallback += OnDisconnected;
            network.OnTransportFailure += OnTransportFailure;
        }

        private void StartHost(string name, Team team)
        {
            Model = new AquariumModel(speciesCount: AquariumSpecies.Count); Model.AddPlayer(1, name, team); LocalId = 1; peers[NetworkManager.ServerClientId] = 1;
            if (!network.StartHost()) throw new InvalidOperationException("방을 열지 못했습니다. 잠시 후 다시 시도해 주세요.");
            network.CustomMessagingManager.RegisterNamedMessageHandler(Channel, Receive);
            Busy = false; Status = ""; State = Model.Snapshot();
        }

        private void StartClient()
        {
            Busy = true; Status = "방장에게 연결하는 중…"; connectDeadline = Time.realtimeSinceStartup + 25;
            if (!network.StartClient()) throw new InvalidOperationException("참가 연결을 시작하지 못했습니다.");
            network.CustomMessagingManager.RegisterNamedMessageHandler(Channel, Receive);
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false; response.Pending = false;
            if (request.ClientNetworkId == NetworkManager.ServerClientId) { response.Approved = true; return; }
            string reason = "";
            Hello hello = null;
            try { if (request.Payload.Length <= 512) hello = JsonUtility.FromJson<Hello>(Encoding.UTF8.GetString(request.Payload)); } catch { }
            if (hello == null || hello.protocol != Protocol) reason = "기획서 v0.1이 적용된 같은 버전의 데모로 접속해 주세요.";
            else if (Model == null || Model.Phase != RoundPhase.Lobby) reason = "진행 중인 방입니다. 다음 라운드 대기실에서 접속해 주세요.";
            else if (Model.Players.Count + approved.Count >= AquariumModel.MaxPlayers) reason = "방이 가득 찼습니다. 외계인 4명과 잠수부 1명, 최대 5명입니다.";
            else if (hello.team != Team.Hider && hello.team != Team.Seeker) reason = "외계인 또는 잠수부 역할을 선택해 주세요.";
            else if (RoleCount(hello.team) >= (hello.team == Team.Seeker ? 1 : 4))
                reason = hello.team == Team.Seeker ? "잠수부는 1명만 참가할 수 있습니다. 외계인을 선택해 다시 접속해 주세요." : "외계인 4자리가 모두 찼습니다. 잠수부를 선택해 다시 접속해 주세요.";
            response.Approved = reason.Length == 0; response.Reason = reason;
            if (response.Approved) approved[request.ClientNetworkId] = hello;
        }

        private int RoleCount(Team team)
        {
            int count = Model.Players.FindAll(p => p.team == team).Count;
            foreach (var hello in approved.Values) if (hello.team == team) count++;
            return count;
        }

        private void OnConnected(ulong client)
        {
            if (Model == null || client == NetworkManager.ServerClientId) return;
            Hello hello;
            if (!approved.TryGetValue(client, out hello)) return;
            approved.Remove(client);
            int id = ++nextPlayer;
            if (Model.AddPlayer(id, hello.name, hello.team == Team.Seeker ? Team.Seeker : Team.Hider) == null)
            { network.DisconnectClient(client, "이미 경기가 시작됐습니다. 다음 대기실에서 참가해 주세요."); return; }
            peers[client] = id;
            Send(client, new Message { kind = "welcome", player = id, state = Model.Snapshot() });
            snapshotTimer = 1;
        }

        private void OnDisconnected(ulong client)
        {
            if (leaving) return;
            if (Model != null)
            {
                int id; if (peers.TryGetValue(client, out id)) { Model.RemovePlayer(id); peers.Remove(client); }
                approved.Remove(client);
            }
            else
            {
                string reason = network == null ? "" : network.DisconnectReason;
                Leave(); Status = string.IsNullOrEmpty(reason) ? "방장과 연결이 끊어졌습니다. 방장이 나가면 방이 종료됩니다." : reason;
            }
        }

        private void OnTransportFailure() { Leave(); Status = "연결이 중단되었습니다. 인터넷 연결을 확인한 뒤 다시 방을 열어 주세요."; }

        private void Receive(ulong sender, FastBufferReader reader)
        {
            if (reader.Length > PacketLimit || reader.Length < 4) return;
            Message message;
            try { string json; reader.ReadValueSafe(out json); message = JsonUtility.FromJson<Message>(json); }
            catch { return; }
            if (message == null) return;
            if (Model != null)
            {
                int id; if (!peers.TryGetValue(sender, out id)) return;
                if (message.kind == "input") Model.SetInput(id, message.input);
                else if (message.kind == "role") Model.SetRole(id, message.team, message.ready);
            }
            else if (sender == NetworkManager.ServerClientId)
            {
                if (message.kind == "welcome") LocalId = message.player;
                if (message.state != null && (State == null || message.state.sequence > State.sequence))
                { State = message.state; ReceivedSnapshots++; Busy = false; Status = ""; }
            }
        }

        private void Send(ulong target, Message message)
        {
            if (network == null || !network.IsListening) return;
            string json = JsonUtility.ToJson(message);
            using (var writer = new FastBufferWriter(PacketLimit, Allocator.Temp))
            {
                writer.WriteValueSafe(json);
                network.CustomMessagingManager.SendNamedMessage(Channel, target, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        public void Input(SwimInput input)
        {
            if (Model != null) Model.SetInput(LocalId, input);
            else if (Connected) Send(NetworkManager.ServerClientId, new Message { kind = "input", input = input });
        }
        public void Role(Team team, bool ready)
        {
            if (Model != null) Model.SetRole(LocalId, team, ready);
            else Send(NetworkManager.ServerClientId, new Message { kind = "role", team = team, ready = ready });
        }
        public string StartRound()
        {
            if (Model == null) return "방장만 시작할 수 있습니다.";
            if (approved.Count > 0) return "참가자가 입장 중입니다. 잠시 후 시작해 주세요.";
            string problem = Model.StartProblem();
            if (problem.Length == 0) Model.StartRound();
            return problem;
        }
        public void Rematch() { if (Model != null) Model.ReturnToLobby(); }

        public void Update(float dt)
        {
            if (Busy && network != null && Model == null && connectDeadline > 0 && Time.realtimeSinceStartup > connectDeadline)
            { Leave(); Status = "접속 시간이 초과되었습니다. 방 코드와 방장의 접속 상태를 확인해 주세요."; }
            if (Model == null) return;
            accumulator += Mathf.Min(dt, 0.2f);
            while (accumulator >= 1f / 30f) { Model.Tick(1f / 30f); accumulator -= 1f / 30f; }
            State = Model.Snapshot();
            snapshotTimer += dt;
            if (network != null && snapshotTimer >= 0.1f)
            {
                snapshotTimer = 0;
                var message = new Message { kind = "state", state = State };
                foreach (var peer in peers) if (peer.Key != NetworkManager.ServerClientId) Send(peer.Key, message);
            }
        }

        public void Leave()
        {
            generation++; leaving = true;
            if (network != null)
            {
                network.OnClientConnectedCallback -= OnConnected; network.OnClientDisconnectCallback -= OnDisconnected;
                network.ConnectionApprovalCallback -= Approve; network.OnTransportFailure -= OnTransportFailure;
                network.Shutdown(); UnityEngine.Object.Destroy(networkObject);
            }
            network = null; networkObject = null; Model = null; State = null; LocalId = 0; Code = "";
            Busy = Practice = false; peers.Clear(); approved.Clear(); nextPlayer = 1; accumulator = snapshotTimer = connectDeadline = 0;
            ReceivedSnapshots = 0; leaving = false;
        }
        public void Dispose() { Leave(); }
    }
}
