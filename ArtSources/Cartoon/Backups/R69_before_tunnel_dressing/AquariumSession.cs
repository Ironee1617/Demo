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
        public int alien, diver;
    }

    // Only the host simulates movement, hits, eating, role changes and victory.
    public sealed class AquariumSession : IDisposable
    {
        // 8: players carry their chosen alien (Hello.alien, the "alien" message, Swimmer.alienKind).
        // R37 (9): snapshots carry the game's scenery seed; host and clients must lay the tank out the same way.
        // R40 (10): 50 fish with small schooling Fish0; an older build would draw the sky-blue fish at the old size.
        // R46 (11): smaller alien and drains, bigger sky-blue fish; builds must agree on the sizes.
        // R47 (12): diver kinds (Hello.diver, the "diver" message, Swimmer.diverKind), abilities and new endings.
        // R57 (13): snapshots carry the host's map (AquariumSnapshot.map, AquariumMaps); every machine lays out that map.
        // R58 (14): the twin tanks' scenery seed now lays out rocks with collision; builds must build the same rocks.
        // R59 (15): the twin tanks' layouts gained giant formations (collision), so builds must match again.
        // R60 (16): lobby / after-game chat ("chat" -> "chatline") and the aliens' pings ("ping" -> "pingmark");
        //          divers wait in the changing room before the dive (AquariumLockerRoom), positions differ.
        private const int Protocol = 17;
        [Serializable] private class Hello { public int protocol = Protocol; public string name, password; public Team team; public int alien, diver; }
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
            public int alien, diver;
            public string text, name;           // R60: chat
            public float px, py, pz;            // R60: ping point
        }
        // R60: chat lines (lobby and after the round, never while playing) and the aliens' pings, newest last.
        public sealed class ChatLine { public int player; public string name, text; public Team team; public bool spectator; public float at; }
        public sealed class PingMark { public int player; public string name; public Point at; public float shownAt; public int alienKind; }
        public readonly List<ChatLine> Chat = new List<ChatLine>();
        public readonly List<PingMark> Pings = new List<PingMark>();
        public const int ChatLimit = 80;
        public bool CanChat { get { return Connected && !Practice && State.phase != RoundPhase.Playing; } }
        private readonly Dictionary<int, float> chatTimes = new Dictionary<int, float>(), pingTimes = new Dictionary<int, float>();
        private const string Channel = "aquarium.v6";
        private const int PacketLimit = 60000;
        public AquariumModel Model { get; private set; }
        public AquariumSnapshot State { get; private set; }
        // Draw between completed ticks; simulation and hit detection remain authoritative.
        public float PresentationTime
        {
            get { return State == null ? 0 : Model != null ? State.time + accumulator - 1f / 30f
                : float.IsNaN(remotePresentationTime) ? State.time - .05f : remotePresentationTime; }
        }
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
        public bool IsSpectator
        {
            get
            {
                // R66: a plain loop (the lambda captured `this` and allocated on every one of the many calls per frame).
                if (LocalId == 0) return false;
                foreach (var o in Observers) if (o.id == LocalId) return true;
                return false;
            }
        }
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
        private readonly List<ulong> stateRecipients = new List<ulong>(AquariumModel.MaxPlayers - 1);
        private int generation, nextPlayer = 1;
        private int remoteHumans;
        private float accumulator, snapshotTimer, connectDeadline;
        private float remotePresentationTime = float.NaN;
        private bool leaving;
        private string lobbyId = "", roomPassword = "", publishedPhase = "";
        private bool ownsLobby, heartbeatPending, directoryPending;
        private float nextHeartbeat, nextDirectory;
        private Task connectionOperation = Task.CompletedTask, cleanupOperation = Task.CompletedTask;

        // Rounds open with the diver's deck entry (checks that measure from a fixed start turn it off).
        public bool DiveEntry = true;
        // The alien the local player picked (AquariumAlienKinds); sent when hosting or joining a room.
        public int PreferredAlien;
        // R47: the diver the local player picked (AquariumDiverKinds).
        public int PreferredDiver;
        // R57: the map the local player hosts with (AquariumMaps); the host's pick reaches clients in the snapshot.
        public int PreferredMap;

        // Starts a practice round at once (QA drivers and checks use this).
        public void StartPractice(string name, Team team, int alien = 0)
        {
            OpenPracticeLobby(name, team, alien);
            Model.StartRound(); State = Model.Snapshot();
        }

        // AI practice opens its own lobby: pick the role and the alien, then start the round (StartRound).
        public void OpenPracticeLobby(string name, Team team, int alien) { OpenPracticeLobby(name, team, alien, PreferredDiver); }
        public void OpenPracticeLobby(string name, Team team, int alien, int diver)
        {
            Leave(); Practice = true; LocalId = 1; PreferredAlien = AquariumAlienKinds.Clamp(alien); PreferredDiver = AquariumDiverKinds.Clamp(diver);
            Model = new AquariumModel(speciesCount: AquariumSpecies.Count, drainSeed: Guid.NewGuid().GetHashCode()) { DiveEntry = DiveEntry };
            Model.SetMap(PreferredMap);
            var self = Model.AddPlayer(1, name, team); self.alienKind = PreferredAlien; self.diverKind = PreferredDiver;
            FillPracticeBots(team);
            State = Model.Snapshot(); Status = "";
        }

        // Practice lobby role switch: the AI fills every other seat (one diver unless the player dives).
        public void PracticeRole(Team team)
        {
            if (!Practice || Model == null || Model.Phase != RoundPhase.Lobby) return;
            Model.SetRole(LocalId, team, true); Model.SetDiver(LocalId, PreferredDiver);
            Model.Players.RemoveAll(p => p.bot);
            FillPracticeBots(team);
            State = Model.Snapshot();
        }

        private void FillPracticeBots(Team team)
        {
            int nextId = 2;
            if (team == Team.Hider) Model.AddPlayer(nextId++, "연습 잠수부", Team.Seeker, true);
            while (nextId <= AquariumModel.MaxPlayers)
            {
                Model.AddPlayer(nextId, "연습 외계인 " + (nextId - 1), Team.Hider, true);
                nextId++;
            }
            AssignPracticeBotAliens();
        }

        // The AI aliens take the kinds the player did not pick, so a practice tank shows different aliens.
        private void AssignPracticeBotAliens()
        {
            var local = Model.Player(LocalId);
            int mine = local != null && local.team == Team.Hider ? local.alienKind : -1, next = 0;
            foreach (var p in Model.Players)
            {
                if (!p.bot || p.team != Team.Hider) continue;
                if (next % AquariumAlienKinds.Count == mine) next++;
                p.alienKind = next++ % AquariumAlienKinds.Count;
            }
        }

        // The local player's alien, in the lobby (the host rules it like the team).
        public void SetAlien(int kind)
        {
            PreferredAlien = AquariumAlienKinds.Clamp(kind);
            if (IsSpectator) return;
            if (Model != null) { Model.SetAlien(LocalId, PreferredAlien); if (Practice && Model.Phase == RoundPhase.Lobby) AssignPracticeBotAliens(); State = Model.Snapshot(); }
            else if (Connected) Send(NetworkManager.ServerClientId, new Message { kind = "alien", alien = PreferredAlien });
        }

        // R47: the local player's diver, in the lobby (mirrors SetAlien).
        public void SetDiver(int kind)
        {
            PreferredDiver = AquariumDiverKinds.Clamp(kind);
            if (IsSpectator) return;
            if (Model != null) { Model.SetDiver(LocalId, PreferredDiver); State = Model.Snapshot(); }
            else if (Connected) Send(NetworkManager.ServerClientId, new Message { kind = "diver", diver = PreferredDiver });
        }

        // R57: the host's map pick (lobby only); clients follow the map in the snapshots.
        public void SetMap(int map)
        {
            PreferredMap = AquariumMaps.Clamp(map);
            if (Model == null || Model.Phase != RoundPhase.Lobby) return;
            Model.SetMap(PreferredMap); State = Model.Snapshot(); snapshotTimer = 1;
        }
        public int Map { get { return State != null ? AquariumMaps.Clamp(State.map) : AquariumMaps.Clamp(PreferredMap); } }

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
                ConnectionData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Hello { name = AquariumModel.CleanName(name), team = team, password = password ?? "", alien = PreferredAlien, diver = PreferredDiver }))
            };
            network.ConnectionApprovalCallback += Approve;
            network.OnClientConnectedCallback += OnConnected;
            network.OnClientDisconnectCallback += OnDisconnected;
            network.OnTransportFailure += OnTransportFailure;
        }

        private void StartHost(string name, Team team)
        {
            Model = new AquariumModel(speciesCount: AquariumSpecies.Count, drainSeed: Guid.NewGuid().GetHashCode()) { DiveEntry = DiveEntry }; Model.SetMap(PreferredMap); { var self = Model.AddPlayer(1, name, team); self.alienKind = AquariumAlienKinds.Clamp(PreferredAlien); self.diverKind = AquariumDiverKinds.Clamp(PreferredDiver); } LocalId = 1; peers[NetworkManager.ServerClientId] = 1;
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
            if (hello == null || hello.protocol != Protocol) reason = "같은 게임 규칙 버전의 데모로 접속해 주세요.";
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
            return new Message { kind = kind, player = player, state = State, observers = Observers,
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
            if (Model.Phase == RoundPhase.Lobby) { var added = Model.AddPlayer(id, hello.name, AvailableTeam(hello.team)); if (added != null) { added.alienKind = AquariumAlienKinds.Clamp(hello.alien); added.diverKind = AquariumDiverKinds.Clamp(hello.diver); } }
            else waiting[id] = new AquariumObserver { id = id, name = AquariumModel.CleanName(hello.name), team = hello.team, alien = AquariumAlienKinds.Clamp(hello.alien), diver = AquariumDiverKinds.Clamp(hello.diver) };
            peers[client] = id;
            UpdateObservers();
            State = Model.Snapshot();
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
                if (message.kind == "chat") { HostChat(id, message.text); return; }   // R60: spectators waiting for the next round chat too
                if (waiting.ContainsKey(id)) return;
                if (message.kind == "input") Model.SetInput(id, message.input);
                else if (message.kind == "role") Model.SetRole(id, message.team, message.ready);
                else if (message.kind == "alien") Model.SetAlien(id, message.alien);
                else if (message.kind == "diver") Model.SetDiver(id, message.diver);
                else if (message.kind == "ping") HostPing(id, new Point(message.px, message.py, message.pz));
            }
            else if (sender == NetworkManager.ServerClientId)
            {
                if (message.kind == "welcome") LocalId = message.player;
                if (message.kind == "chatline") { AddChat(message.player, message.name, message.text, message.team, message.ready); return; }
                if (message.kind == "pingmark") { AddPing(message.player, message.name, new Point(message.px, message.py, message.pz), message.alien); return; }
                if (message.state != null && (State == null || message.state.sequence > State.sequence))
                {
                    bool entering = Busy;
                    if (State == null || State.round != message.state.round || State.phase != message.state.phase || message.state.time < State.time)
                        remotePresentationTime = message.state.time - .05f;
                    State = message.state; Observers = message.observers ?? new AquariumObserver[0];
                    AquariumMaps.Select(State.map);
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

        private void WriteState(FastBufferWriter writer)
        {
            writer.WriteValueSafe(JsonUtility.ToJson(StateMessage()));
        }

        private void BroadcastState()
        {
            if (network == null || !network.IsListening) return;
            stateRecipients.Clear();
            foreach (var peer in peers) if (peer.Key != NetworkManager.ServerClientId) stateRecipients.Add(peer.Key);
            if (stateRecipients.Count == 0) return;
            // The render snapshot and one encoded packet serve every remote peer.
            using (var writer = new FastBufferWriter(PacketLimit, Allocator.Temp))
            {
                WriteState(writer);
                network.CustomMessagingManager.SendNamedMessage(Channel, stateRecipients, writer, NetworkDelivery.ReliableFragmentedSequenced);
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
            foreach (var entrant in entrants) { var added = Model.AddPlayer(entrant.id, entrant.name, AvailableTeam(entrant.team)); if (added != null) { added.alienKind = AquariumAlienKinds.Clamp(entrant.alien); added.diverKind = AquariumDiverKinds.Clamp(entrant.diver); } }
            waiting.Clear(); UpdateObservers(); snapshotTimer = 1;
        }

        public void Update(float dt)
        {
            if (Busy && network != null && Model == null && connectDeadline > 0 && Time.realtimeSinceStartup > connectDeadline)
            { Leave(); Status = "접속 시간이 초과되었습니다. 방 코드와 방장의 접속 상태를 확인해 주세요."; }
            if (Model == null)
            {
                if (State != null)
                {
                    if (float.IsNaN(remotePresentationTime)) remotePresentationTime = State.time - .05f;
                    remotePresentationTime = Mathf.Clamp(remotePresentationTime + Mathf.Clamp(dt, 0, .2f), State.time - .15f, State.time);
                }
                return;
            }
            accumulator += Mathf.Min(dt, 0.2f);
            bool ticked=false;
            while (accumulator >= 1f / 30f) { Model.Tick(1f / 30f); accumulator -= 1f / 30f;ticked=true; }
            if(ticked || State==null)State = Model.Snapshot();
            snapshotTimer += dt;
            if (network != null && snapshotTimer >= 0.05f)
            {
                snapshotTimer %= 0.05f;
                BroadcastState();
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

        // ---- R60: chat and pings ------------------------------------------------------------------------------
        public static string CleanChat(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new System.Text.StringBuilder(text.Length);
            foreach (char c in text) if (!char.IsControl(c)) sb.Append(c);
            var clean = sb.ToString().Trim();
            return clean.Length > ChatLimit ? clean.Substring(0, ChatLimit) : clean;
        }
        public void SendChat(string text)
        {
            text = CleanChat(text);
            if (text.Length == 0 || !CanChat) return;
            if (Model != null) HostChat(LocalId, text);
            else Send(NetworkManager.ServerClientId, new Message { kind = "chat", text = text });
        }
        // The host checks the phase and the pace (3 lines in 2 s), names the sender and passes the line to everyone.
        private void HostChat(int id, string text)
        {
            text = CleanChat(text);
            if (text.Length == 0 || Model == null || Model.Phase == RoundPhase.Playing) return;
            float now = Time.realtimeSinceStartup, last;
            if (chatTimes.TryGetValue(id, out last) && now - last < .6f) return;
            chatTimes[id] = now;
            var player = Model.Players.Find(p => p.id == id);
            AquariumObserver observer = null;
            if (player == null) { waiting.TryGetValue(id, out observer); if (observer == null) observer = Array.Find(Observers, o => o.id == id); }
            if (player == null && observer == null) return;
            string name = player != null ? player.name : observer.name; Team team = player != null ? player.team : observer.team;
            AddChat(id, name, text, team, player == null);
            var line = new Message { kind = "chatline", player = id, name = name, text = text, team = team, ready = player == null };
            foreach (var peer in peers) if (peer.Key != NetworkManager.ServerClientId) Send(peer.Key, line);
        }
        private void AddChat(int id, string name, string text, Team team, bool spectator)
        {
            Chat.Add(new ChatLine { player = id, name = name, text = CleanChat(text), team = team, spectator = spectator, at = Time.realtimeSinceStartup });
            if (Chat.Count > 60) Chat.RemoveRange(0, Chat.Count - 60);
        }

        // R60: an alien points at a spot (middle mouse); only the aliens see it. Pointing only, no game effect.
        public void SendPing(Point at)
        {
            if (!Connected || IsSpectator || State.phase != RoundPhase.Playing) return;
            if (Model != null) HostPing(LocalId, at);
            else Send(NetworkManager.ServerClientId, new Message { kind = "ping", px = at.x, py = at.y, pz = at.z });
        }
        private void HostPing(int id, Point at)
        {
            if (Model == null || Model.Phase != RoundPhase.Playing) return;
            var sender = Model.Players.Find(p => p.id == id);
            if (sender == null || sender.team != Team.Hider || !sender.alive || sender.bot) return;
            if (float.IsNaN(at.x) || float.IsNaN(at.y) || float.IsNaN(at.z) || (at - sender.position).Length > 60) return;
            float now = Time.realtimeSinceStartup, last;
            if (pingTimes.TryGetValue(id, out last) && now - last < .45f) return;
            pingTimes[id] = now;
            var local = Model.Players.Find(p => p.id == LocalId);
            if (local != null && local.team == Team.Hider) AddPing(id, sender.name, at, sender.alienKind);
            var mark = new Message { kind = "pingmark", player = id, name = sender.name, px = at.x, py = at.y, pz = at.z, alien = sender.alienKind };
            foreach (var peer in peers)
            {
                if (peer.Key == NetworkManager.ServerClientId) continue;
                var target = Model.Players.Find(p => p.id == peer.Value);
                if (target != null && target.team == Team.Hider) Send(peer.Key, mark);
            }
        }
        private void AddPing(int id, string name, Point at, int alien)
        {
            Pings.RemoveAll(p => p.player == id);   // one mark per alien: a new ping moves it
            Pings.Add(new PingMark { player = id, name = name, at = at, shownAt = Time.realtimeSinceStartup, alienKind = alien });
        }

        public void Leave()
        {
            generation++; leaving = true;
            Chat.Clear(); Pings.Clear(); chatTimes.Clear(); pingTimes.Clear();
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
            Busy = Practice = false; peers.Clear(); approved.Clear(); stateRecipients.Clear(); nextPlayer = 1; accumulator = snapshotTimer = connectDeadline = 0;
            waiting.Clear(); Observers = new AquariumObserver[0];
            ReceivedSnapshots = 0; remotePresentationTime = float.NaN; leaving = false;
        }
        public async Task ShutdownAsync()
        { Leave(); await connectionOperation; await cleanupOperation; }
        public void Dispose() { Leave(); }
    }
}
