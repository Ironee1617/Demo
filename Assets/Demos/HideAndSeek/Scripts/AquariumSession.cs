using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    [Serializable] public sealed class AquariumObserver
    {
        public int id;
        public string name;
        public Team team;
    }

    // Only the host simulates movement, hits, eating, role changes and victory.
    public sealed class AquariumSession : IDisposable
    {
        private const int Protocol = 6;
        [Serializable] private class Hello { public int protocol = Protocol; public string name, password; public Team team; }
        [Serializable] private class Message
        {
            public string kind;
            public int player;
            public Team team;
            public bool ready;
            public SwimInput input;
            public AquariumSnapshot state;
            public AquariumObserver[] observers;
            public string roomName;
            public bool hasPassword;
            public int humans;
        }
        private const string Channel = "aquarium.v6";
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
        public IReadOnlyList<AquariumRoom> Rooms { get; private set; } = new List<AquariumRoom>();
        public bool RoomsBusy { get; private set; }
        public string RoomsStatus { get; private set; } = "";
        public string RoomName { get; private set; } = "";
        public string RoomId { get { return lobbyId; } }
        public bool HasPassword { get; private set; }
        public int HumanCount { get { return Practice ? 1 : Model != null ? peers.Count : remoteHumans; } }
        public AquariumObserver[] Observers { get; private set; } = new AquariumObserver[0];
        public int SpectatorCount { get { return Observers.Length; } }
        public bool IsSpectator { get { return LocalId != 0 && Array.Exists(Observers, p => p.id == LocalId); } }
        public int SpectatingId
        {
            get
            {
                if (!IsSpectator || State == null) return 0;
                var target = Array.Find(State.players, p => p.alive && p.team == Team.Seeker)
                    ?? Array.Find(State.players, p => p.alive) ?? Array.Find(State.players, p => p.team == Team.Seeker);
                return target == null ? 0 : target.id;
            }
        }
        private NetworkManager network;
        private GameObject networkObject;
        private readonly Dictionary<ulong, int> peers = new Dictionary<ulong, int>();
        private readonly Dictionary<ulong, Hello> approved = new Dictionary<ulong, Hello>();
        private readonly Dictionary<int, AquariumObserver> waiting = new Dictionary<int, AquariumObserver>();
        private int generation, nextPlayer = 1;
        private int remoteHumans;
        private float accumulator, snapshotTimer, connectDeadline;
        private bool leaving;
        private string lobbyId = "", roomPassword = "", publishedPhase = "";
        private bool ownsLobby, heartbeatPending, directoryPending;
        private float nextHeartbeat, nextDirectory;
        private Task connectionOperation = Task.CompletedTask, cleanupOperation = Task.CompletedTask;

        public void StartPractice(string name, Team team)
        {
            Leave(); Practice = true; LocalId = 1;
            Model = new AquariumModel(); Model.AddPlayer(1, name, team);
            int nextId = 2;
            if (team == Team.Hider) Model.AddPlayer(nextId++, "연습 잠수부", Team.Seeker, true);
            while (nextId <= AquariumModel.MaxPlayers)
            {
                Model.AddPlayer(nextId, "연습 외계인 " + (nextId - 1), Team.Hider, true);
                nextId++;
            }
            Model.StartRound(); State = Model.Snapshot(); Status = "";
        }

        public async Task RefreshRooms()
        {
            if (RoomsBusy || Busy || Connected) return;
            RoomsBusy = true; RoomsStatus = "방 목록을 불러오는 중…";
            try
            {
                var rooms = await AquariumRooms.QueryAsync();
                if (!Connected && !Busy)
                { Rooms = rooms; RoomsStatus = rooms.Count == 0 ? "대기 중인 방이 없습니다. 새 방을 만들어 보세요." : ""; }
            }
            catch (Exception ex) { RoomsStatus = "목록을 불러오지 못했습니다. " + FriendlyError(ex); }
            finally { RoomsBusy = false; }
        }

        public Task CreateRoom(string roomName, string password, string name, Team team)
        {
            return BeginConnection(async request =>
            {
                var allocation = await RelayService.Instance.CreateAllocationAsync(AquariumModel.MaxPlayers - 1);
                if (request != generation) return;
                string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                if (request != generation) return;
                var lobby = await AquariumRooms.CreateAsync(roomName, password, code, allocation.AllocationId.ToString());
                if (request != generation) { await CleanupLobby(lobby.Id, true); return; }
                lobbyId = lobby.Id; ownsLobby = true;
                var description = AquariumRooms.Describe(lobby);
                RoomName = description.name; HasPassword = description.hasPassword; roomPassword = password ?? "";
                Code = code; publishedPhase = "Waiting"; nextHeartbeat = Time.realtimeSinceStartup + 15;
                CreateNetwork(name, team, password);
                network.GetComponent<UnityTransport>().SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));
                StartHost(name, team);
            });
        }

        public Task JoinRoom(string roomId, string password, string name, Team team)
        { return BeginConnection(request => JoinInternet(request, roomId, null, password, name, team)); }

        // The displayed Relay code remains valid through rematches and participant departures.
        public Task OpenInternet(bool host, string code, string name, Team team, string password = "")
        {
            return host ? CreateRoom(AquariumModel.CleanName(name) + "의 수조", password, name, team)
                : BeginConnection(request => JoinInternet(request, null, code, password, name, team));
        }

        private Task BeginConnection(Func<int, Task> connect)
        {
            if (Busy || Connected) return Task.CompletedTask;
            var previous = connectionOperation;
            Leave(); Busy = true; Status = "온라인 방에 연결하는 중…";
            connectionOperation = RunConnection(generation, previous, connect);
            return connectionOperation;
        }

        private async Task RunConnection(int request, Task previous, Func<int, Task> connect)
        {
            try
            {
                // A cancelled cloud join must clean up before a new join uses the same account.
                await previous; await cleanupOperation;
                if (request != generation) return;
                await AquariumRooms.EnsureServicesAsync();
                if (request != generation) return;
                await connect(request);
            }
            catch (Exception ex)
            {
                if (request != generation) return;
                Leave(); Status = "접속 실패: " + FriendlyError(ex);
                Debug.LogWarning("Aquarium connection: " + ex.GetType().Name);
            }
        }

        private async Task JoinInternet(int request, string roomId, string code, string password, string name, Team team)
        {
            AquariumRoom room = null;
            if (string.IsNullOrEmpty(roomId))
            {
                var found = await AquariumRooms.FindByCodeAsync((code ?? "").Trim().ToUpperInvariant());
                if (request != generation) return;
                if (found == null) throw new InvalidOperationException("방을 찾을 수 없습니다. 최신 방 코드와 게임 버전을 확인해 주세요.");
                room = AquariumRooms.Describe(found); roomId = found.Id;
            }
            else
            {
                foreach (var candidate in Rooms) if (candidate.id == roomId) { room = candidate; break; }
                if (room == null)
                {
                    var rooms = await AquariumRooms.QueryAsync();
                    if (request != generation) return;
                    room = rooms.Find(candidate => candidate.id == roomId);
                }
                if (room == null) throw new InvalidOperationException("방이 종료되었거나 목록이 바뀌었습니다. 새로고침해 주세요.");
            }
            // Associate this connection's Relay allocation when joining Lobby, so the service
            // can also remove the matching membership after an unexpected disconnection.
            var allocation = await RelayService.Instance.JoinAllocationAsync(room.code);
            if (request != generation) return;
            var lobby = await AquariumRooms.JoinAsync(roomId, password, allocation.AllocationId.ToString());
            if (request != generation) { await CleanupLobby(lobby.Id, false); return; }
            lobbyId = lobby.Id; ownsLobby = false;
            var description = AquariumRooms.Describe(lobby);
            RoomName = description.name; HasPassword = description.hasPassword; Code = description.code;
            CreateNetwork(name, team, password);
            network.GetComponent<UnityTransport>().SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));
            StartClient();
        }

        private static string FriendlyError(Exception ex)
        {
            if (ex is InvalidOperationException) return ex.Message;
            var lobbyError = ex as Unity.Services.Lobbies.LobbyServiceException;
            if (lobbyError != null)
            {
                string reason = lobbyError.Reason.ToString();
                if (reason.IndexOf("Password", StringComparison.OrdinalIgnoreCase) >= 0) return "비밀번호가 맞지 않습니다. 다시 입력해 주세요.";
                if (reason.IndexOf("Full", StringComparison.OrdinalIgnoreCase) >= 0) return "방이 가득 찼습니다. 다른 방을 선택해 주세요.";
                if (reason.IndexOf("NotFound", StringComparison.OrdinalIgnoreCase) >= 0) return "방이 종료되었거나 사라졌습니다. 목록을 새로고침해 주세요.";
                if (reason.IndexOf("RateLimit", StringComparison.OrdinalIgnoreCase) >= 0) return "잠시 기다린 뒤 다시 시도해 주세요.";
            }
            if (ex is ArgumentException) return ex.Message;
            return "인터넷 연결과 온라인 서비스 설정을 확인한 뒤 다시 시도해 주세요.";
        }

        // Used by the two-process smoke test; normal players use Relay codes.
        public void OpenLocalTest(bool host, string name, Team team, ushort port = 7779, string password = "")
        {
            Leave(); RoomName = "로컬 검사 수조"; HasPassword = !string.IsNullOrEmpty(password);
            roomPassword = host ? password ?? "" : "";
            CreateNetwork(name, team, password);
            network.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", port);
            Code = "LOCAL TEST";
            if (host) StartHost(name, team); else StartClient();
        }

        private void CreateNetwork(string name, Team team, string password = "")
        {
            networkObject = new GameObject("Aquarium Relay Session");
            var transport = networkObject.AddComponent<UnityTransport>();
            transport.MaxPayloadSize = PacketLimit;
            network = networkObject.AddComponent<NetworkManager>();
            network.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport, EnableSceneManagement = false, ConnectionApproval = true,
                TickRate = 30, ClientConnectionBufferTimeout = 20, ProtocolVersion = Protocol,
                ConnectionData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Hello { name = AquariumModel.CleanName(name), team = team, password = password ?? "" }))
            };
            network.ConnectionApprovalCallback += Approve;
            network.OnClientConnectedCallback += OnConnected;
            network.OnClientDisconnectCallback += OnDisconnected;
            network.OnTransportFailure += OnTransportFailure;
        }

        private void StartHost(string name, Team team)
        {
            Model = new AquariumModel(); Model.AddPlayer(1, name, team); LocalId = 1; peers[NetworkManager.ServerClientId] = 1;
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
            if (hello == null || hello.protocol != Protocol) reason = "방 목록 기능이 포함된 같은 버전의 데모로 접속해 주세요.";
            else if (!string.Equals(roomPassword, hello.password ?? "", StringComparison.Ordinal)) reason = "비밀번호가 맞지 않습니다. 다시 입력해 주세요.";
            else if (Model == null) reason = "방이 종료되었습니다.";
            else if (peers.Count + approved.Count >= AquariumModel.MaxPlayers) reason = "방이 가득 찼습니다. 관전자를 포함해 최대 5명입니다.";
            else if (hello.team != Team.Hider && hello.team != Team.Seeker) reason = "외계인 또는 잠수부 역할을 선택해 주세요.";
            else if (Model.Phase == RoundPhase.Lobby) hello.team = AvailableTeam(hello.team);
            response.Approved = reason.Length == 0; response.Reason = reason;
            if (response.Approved) { hello.password = null; approved[request.ClientNetworkId] = hello; }
        }

        private int RoleCount(Team team)
        {
            int count = Model.Players.FindAll(p => p.team == team).Count;
            foreach (var hello in approved.Values) if (hello.team == team) count++;
            return count;
        }

        private Team AvailableTeam(Team requested)
        { return RoleCount(requested) < (requested == Team.Seeker ? 1 : 4) ? requested : requested == Team.Seeker ? Team.Hider : Team.Seeker; }

        private void UpdateObservers() { Observers = new List<AquariumObserver>(waiting.Values).ToArray(); }

        private Message StateMessage(string kind = "state", int player = 0)
        {
            return new Message { kind = kind, player = player, state = Model.Snapshot(), observers = Observers,
                roomName = RoomName, hasPassword = HasPassword, humans = peers.Count };
        }

        private void OnConnected(ulong client)
        {
            if (Model == null || client == NetworkManager.ServerClientId) return;
            Hello hello;
            if (!approved.TryGetValue(client, out hello)) return;
            approved.Remove(client);
            int id = ++nextPlayer;
            while (Model.Player(id) != null || waiting.ContainsKey(id)) id = ++nextPlayer;
            if (Model.Phase == RoundPhase.Lobby) Model.AddPlayer(id, hello.name, AvailableTeam(hello.team));
            else waiting[id] = new AquariumObserver { id = id, name = AquariumModel.CleanName(hello.name), team = hello.team };
            peers[client] = id;
            UpdateObservers();
            Send(client, StateMessage("welcome", id));
            snapshotTimer = 1;
        }

        private void OnDisconnected(ulong client)
        {
            if (leaving) return;
            if (Model != null)
            {
                int id;
                if (peers.TryGetValue(client, out id))
                {
                    if (waiting.ContainsKey(id)) waiting.Remove(id); else Model.RemovePlayer(id);
                    peers.Remove(client); UpdateObservers(); snapshotTimer = 1;
                }
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
                if (waiting.ContainsKey(id)) return;
                if (message.kind == "input") Model.SetInput(id, message.input);
                else if (message.kind == "role") Model.SetRole(id, message.team, message.ready);
            }
            else if (sender == NetworkManager.ServerClientId)
            {
                if (message.kind == "welcome") LocalId = message.player;
                if (message.state != null && (State == null || message.state.sequence > State.sequence))
                {
                    bool entering = Busy;
                    State = message.state; Observers = message.observers ?? new AquariumObserver[0];
                    RoomName = message.roomName ?? RoomName; HasPassword = message.hasPassword; remoteHumans = message.humans;
                    ReceivedSnapshots++; Busy = false;
                    if (entering) Status = IsSpectator ? "경기 중인 방에 관전으로 입장했습니다. 다음 판부터 플레이합니다." : "";
                }
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
            if (IsSpectator) return;
            if (Model != null) Model.SetInput(LocalId, input);
            else if (Connected) Send(NetworkManager.ServerClientId, new Message { kind = "input", input = input });
        }
        public void Role(Team team, bool ready)
        {
            if (IsSpectator) return;
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
        public void Rematch()
        {
            if (Model == null) return;
            Model.ReturnToLobby();
            if (Model.Phase != RoundPhase.Lobby) return;
            var entrants = new List<AquariumObserver>(waiting.Values); entrants.Sort((a, b) => a.id.CompareTo(b.id));
            foreach (var entrant in entrants) Model.AddPlayer(entrant.id, entrant.name, AvailableTeam(entrant.team));
            waiting.Clear(); UpdateObservers(); snapshotTimer = 1;
        }

        public void Update(float dt)
        {
            if (Busy && network != null && Model == null && connectDeadline > 0 && Time.realtimeSinceStartup > connectDeadline)
            { Leave(); Status = "접속 시간이 초과되었습니다. 방 코드와 방장의 접속 상태를 확인해 주세요."; }
            if (Model == null) return;
            accumulator += Mathf.Min(dt, 0.2f);
            while (accumulator >= 1f / 30f) { Model.Tick(1f / 30f); accumulator -= 1f / 30f; }
            State = Model.Snapshot();
            snapshotTimer += dt;
            if (network != null && snapshotTimer >= 0.05f)
            {
                snapshotTimer = 0;
                var message = StateMessage();
                foreach (var peer in peers) if (peer.Key != NetworkManager.ServerClientId) Send(peer.Key, message);
            }
            MaintainDirectory();
        }

        private void MaintainDirectory()
        {
            if (!ownsLobby || string.IsNullOrEmpty(lobbyId)) return;
            float now = Time.realtimeSinceStartup;
            if (!heartbeatPending && now >= nextHeartbeat)
            {
                nextHeartbeat = now + 15; heartbeatPending = true;
                _ = Heartbeat(generation, lobbyId);
            }
            string phase = Model.Phase == RoundPhase.Lobby ? "Waiting" : Model.Phase.ToString();
            if (!directoryPending && phase != publishedPhase && now >= nextDirectory)
            {
                nextDirectory = now + 1.2f; directoryPending = true;
                _ = PublishPhase(generation, lobbyId, phase);
            }
        }

        private async Task Heartbeat(int request, string id)
        {
            try { await AquariumRooms.HeartbeatAsync(id); }
            catch (Exception) { if (request == generation) Status = "방 목록 연결이 지연되고 있습니다. 인터넷 연결을 확인해 주세요."; }
            finally { if (request == generation) heartbeatPending = false; }
        }

        private async Task PublishPhase(int request, string id, string phase)
        {
            try { await AquariumRooms.UpdateAsync(id, phase); if (request == generation) publishedPhase = phase; }
            catch (Exception) { if (request == generation) Status = "방 상태 갱신이 지연되고 있습니다."; }
            finally { if (request == generation) directoryPending = false; }
        }

        private static async Task CleanupLobby(string id, bool host)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try { await AquariumRooms.LeaveAsync(id, host); return; }
                catch (Exception ex)
                {
                    if (attempt == 2) { Debug.LogWarning("Aquarium room cleanup: " + ex.GetType().Name); return; }
                    await Task.Delay(500 * (attempt + 1));
                }
            }
        }

        private static async Task CleanupAfter(Task previous, string id, bool host)
        { await previous; await CleanupLobby(id, host); }

        public void Leave()
        {
            generation++; leaving = true;
            if (!string.IsNullOrEmpty(lobbyId)) cleanupOperation = CleanupAfter(cleanupOperation, lobbyId, ownsLobby);
            lobbyId = ""; ownsLobby = false; roomPassword = ""; publishedPhase = "";
            RoomName = ""; HasPassword = heartbeatPending = directoryPending = false;
            Status = "";
            nextHeartbeat = nextDirectory = 0; remoteHumans = 0;
            if (network != null)
            {
                network.OnClientConnectedCallback -= OnConnected; network.OnClientDisconnectCallback -= OnDisconnected;
                network.ConnectionApprovalCallback -= Approve; network.OnTransportFailure -= OnTransportFailure;
                network.Shutdown(); UnityEngine.Object.Destroy(networkObject);
            }
            network = null; networkObject = null; Model = null; State = null; LocalId = 0; Code = "";
            Busy = Practice = false; peers.Clear(); approved.Clear(); nextPlayer = 1; accumulator = snapshotTimer = connectDeadline = 0;
            waiting.Clear(); Observers = new AquariumObserver[0];
            ReceivedSnapshots = 0; leaving = false;
        }
        public async Task ShutdownAsync()
        { Leave(); await connectionOperation; await cleanupOperation; }
        public void Dispose() { Leave(); }
    }
}
