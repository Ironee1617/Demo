using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Transporting;
using FishNet.Transporting.FishyEOSPlugin;
using FishNet.Transporting.Tugboat;
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

    // R85: every session message travels as one FishNet broadcast carrying the encoded bytes (AquariumNetCodec).
    public struct AquariumPacket : IBroadcast { public byte[] data; }

    // Only the host simulates movement, hits, eating, role changes and victory.
    // R85: the network runs on FishNet. Online rooms connect peer to peer over Epic Online Services (FishyEOS
    // transport, each player signs in with an anonymous EOS device id); Unity Lobby still lists the rooms and
    // carries the host's EOS product user id. The local test (one PC) runs over FishNet's Tugboat (UDP).
    // The host runs the FishNet server only (its own player is played straight on the model).
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
        // R72 (21): the lobby role is a wish to be 술래; the host draws the one diver at the start.
        // R73 (22): the grotto map and the host's "fill empty seats with AI" switch. R75 (23): no hunger, eat cooldown.
        // R76 (24): egg countdown from a death, drone dart, harpoon hit +10%.
        private const int Protocol = 25; // R85 (25): FishNet + EOS peer to peer, binary snapshots (AquariumNetCodec).
        private const string SocketName = "AquariumEOS";
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
            public Hello hello;                 // R85: the first message of a joining client
        }
        // R60: chat lines (lobby and after the round, never while playing) and the aliens' pings, newest last.
        public sealed class ChatLine { public int player; public string name, text; public Team team; public bool spectator; public float at; }
        public sealed class PingMark { public int player; public string name; public Point at; public float shownAt; public int alienKind; }
        public readonly List<ChatLine> Chat = new List<ChatLine>();
        public readonly List<PingMark> Pings = new List<PingMark>();
        public const int ChatLimit = 80;
        public bool CanChat { get { return Connected && !Practice && State.phase != RoundPhase.Playing; } }
        private readonly Dictionary<int, float> chatTimes = new Dictionary<int, float>(), pingTimes = new Dictionary<int, float>();
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
        // R85: the size of the last snapshot packet (bytes, encoded), for checks and the frame log.
        public int LastSnapshotBytes { get; private set; }
        public IReadOnlyList<AquariumRoom> Rooms { get; private set; } = new List<AquariumRoom>();
        public bool RoomsBusy { get; private set; }
        public string RoomsStatus { get; private set; } = "";
        public string RoomName { get; private set; } = "";
        public string RoomId { get { return lobbyId; } }
        public bool HasPassword { get; private set; }
        public int HumanCount { get { return Practice ? 1 : Model != null ? peers.Count + 1 : remoteHumans; } }
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
        // Host: FishNet client id -> player id (the host itself is not in here), the connections, and the ones that
        // connected but have not said hello yet (with the time they connected).
        private readonly Dictionary<int, int> peers = new Dictionary<int, int>();
        private readonly Dictionary<int, NetworkConnection> conns = new Dictionary<int, NetworkConnection>();
        private readonly Dictionary<int, float> connecting = new Dictionary<int, float>();
        private readonly Dictionary<int, NetworkConnection> connectingConns = new Dictionary<int, NetworkConnection>();
        private readonly Dictionary<int, AquariumObserver> waiting = new Dictionary<int, AquariumObserver>();
        private int generation, nextPlayer = 1;
        private int remoteHumans;
        private float accumulator, snapshotTimer, connectDeadline;
        private float remotePresentationTime = float.NaN;
        private bool leaving;
        private string lobbyId = "", roomPassword = "", publishedPhase = "", byeReason = "";
        private bool ownsLobby, heartbeatPending, directoryPending;
        private float nextHeartbeat, nextDirectory;
        private Task connectionOperation = Task.CompletedTask, cleanupOperation = Task.CompletedTask;
        private TaskCompletionSource<bool> serverReady;
        private Hello pendingHello;
        private int savedFrameRate = -1;

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
            else if (Connected) { SendToHost(new Message { kind = "alien", alien = PreferredAlien }); pendAlien = PreferredAlien; Pend(); }
        }

        // R47: the local player's diver, in the lobby (mirrors SetAlien).
        public void SetDiver(int kind)
        {
            PreferredDiver = AquariumDiverKinds.Clamp(kind);
            if (IsSpectator) return;
            if (Model != null) { Model.SetDiver(LocalId, PreferredDiver); State = Model.Snapshot(); }
            else if (Connected) { SendToHost(new Message { kind = "diver", diver = PreferredDiver }); pendDiver = PreferredDiver; Pend(); }
        }

        // R57: the host's map pick (lobby only); clients follow the map in the snapshots.
        public void SetMap(int map)
        {
            PreferredMap = AquariumMaps.Clamp(map);
            if (Model == null || Model.Phase != RoundPhase.Lobby) return;
            Model.SetMap(PreferredMap); State = Model.Snapshot(); snapshotTimer = 1;
        }
        public int Map { get { return State != null ? AquariumMaps.Clamp(State.map) : AquariumMaps.Clamp(PreferredMap); } }
        // R73: the host's "fill empty seats with AI" switch (lobby only); clients read it from the snapshots.
        public bool FillWithAI { get { return State != null ? !State.noAI : Model == null || Model.FillWithAI; } }
        public void SetFillWithAI(bool on)
        {
            if (Model == null || Model.Phase != RoundPhase.Lobby) return;
            Model.FillWithAI = on; State = Model.Snapshot(); snapshotTimer = 1;
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

        // R85: the host signs in to EOS and opens its P2P socket first; the room then goes on the list with the
        // host's EOS id (where joiners connect) and a short room code (what players type).
        public Task CreateRoom(string roomName, string password, string name, Team team)
        {
            return BeginConnection(async request =>
            {
                roomPassword = password ?? "";
                CreateNetwork(true, "");
                serverReady = new TaskCompletionSource<bool>();
                Status = "Epic 온라인 서비스에 접속하는 중…";
                if (!network.ServerManager.StartConnection()) throw new InvalidOperationException("방을 열지 못했습니다. 잠시 후 다시 시도해 주세요.");
                var ready = serverReady.Task;
                var done = await Task.WhenAny(ready, Task.Delay(30000));
                if (request != generation) return;
                if (done != ready || !ready.Result) throw new InvalidOperationException("Epic 온라인 서비스에 접속하지 못했습니다. 인터넷 연결을 확인해 주세요.");
                RestoreFrameRate();
                string hostId = LocalEosId();
                if (hostId.Length == 0) throw new InvalidOperationException("Epic 온라인 서비스 로그인 정보를 받지 못했습니다.");
                string code = NewRoomCode();
                var lobby = await AquariumRooms.CreateAsync(roomName, password, code, hostId);
                if (request != generation) { await CleanupLobby(lobby.Id, true); return; }
                lobbyId = lobby.Id; ownsLobby = true;
                var description = AquariumRooms.Describe(lobby);
                RoomName = description.name; HasPassword = description.hasPassword;
                Code = code; publishedPhase = "Waiting"; nextHeartbeat = Time.realtimeSinceStartup + 15;
                CreateHostModel(name, team);
            });
        }

        public Task JoinRoom(string roomId, string password, string name, Team team)
        { return BeginConnection(request => JoinInternet(request, roomId, null, password, name, team)); }

        // The displayed room code remains valid through rematches and participant departures.
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
                Debug.LogWarning("Aquarium connection: " + ex.GetType().Name + " " + ex.Message);
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
            if (string.IsNullOrEmpty(room.host)) throw new InvalidOperationException("방장의 연결 정보가 없습니다. 같은 버전의 게임인지 확인해 주세요.");
            var lobby = await AquariumRooms.JoinAsync(roomId, password);
            if (request != generation) { await CleanupLobby(lobby.Id, false); return; }
            lobbyId = lobby.Id; ownsLobby = false;
            var description = AquariumRooms.Describe(lobby);
            RoomName = description.name; HasPassword = description.hasPassword; Code = description.code;
            pendingHello = NewHello(name, team, password);
            CreateNetwork(true, description.host.Length > 0 ? description.host : room.host);
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

        // Used by the two-process smoke test and for trying the network on one PC: FishNet's Tugboat (UDP) on this
        // machine instead of EOS (EOS gives two copies on one PC the same device id, so they cannot meet).
        public void OpenLocalTest(bool host, string name, Team team, ushort port = 7779, string password = "")
        {
            Leave(); RoomName = "로컬 검사 수조"; HasPassword = !string.IsNullOrEmpty(password);
            roomPassword = host ? password ?? "" : "";
            CreateNetwork(false, "", port);
            Code = "LOCAL TEST";
            if (host)
            {
                if (!network.ServerManager.StartConnection()) throw new InvalidOperationException("로컬 검사 방을 열지 못했습니다.");
                RestoreFrameRate();
                CreateHostModel(name, team);
            }
            else { pendingHello = NewHello(name, team, password); StartClient(); }
        }

        private Hello NewHello(string name, Team team, string password)
        { return new Hello { name = AquariumModel.CleanName(name), team = team, password = password ?? "", alien = PreferredAlien, diver = PreferredDiver }; }

        private void CreateNetwork(bool eos, string remoteHost, ushort port = 7779)
        {
            savedFrameRate = Application.targetFrameRate;
            networkObject = new GameObject(eos ? "Aquarium EOS Session" : "Aquarium Local Session");
            networkObject.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(networkObject);
            if (eos)
            {
                var transport = networkObject.AddComponent<FishyEOS>();
                transport.SocketName = SocketName;
                transport.RemoteProductUserId = remoteHost ?? "";
            }
            else
            {
                var transport = networkObject.AddComponent<Tugboat>();
                transport.SetClientAddress("127.0.0.1");
                transport.SetPort(port);
            }
            var net = networkObject.AddComponent<NetworkManager>();
            // The old network (Leave) is destroyed at the end of the frame; FishNet would otherwise destroy this new one.
            var persistence = typeof(NetworkManager).GetField("_persistence", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (persistence != null) persistence.SetValue(net, Enum.Parse(persistence.FieldType, "AllowMultiple"));
            net.SpawnablePrefabs = ScriptableObject.CreateInstance<DefaultPrefabObjects>();
            network = net;
            networkObject.SetActive(true);
            // Events from an older network (torn down in Leave) are ignored.
            net.ServerManager.RegisterBroadcast<AquariumPacket>((conn, packet, channel) => { if (net == network) OnServerPacket(conn, packet); }, false);
            net.ClientManager.RegisterBroadcast<AquariumPacket>((packet, channel) => { if (net == network) OnClientPacket(packet); });
            net.ServerManager.OnServerConnectionState += args => { if (net == network) OnServerState(args); };
            net.ServerManager.OnRemoteConnectionState += (conn, args) => { if (net == network) OnRemoteState(conn, args); };
            net.ClientManager.OnClientConnectionState += args => { if (net == network) OnClientState(args); };
        }

        // FishNet sets the frame rate when only a server runs; the game keeps its own.
        private void RestoreFrameRate() { if (savedFrameRate != -2) Application.targetFrameRate = savedFrameRate; }

        private static string LocalEosId()
        {
            try { var id = FishNet.Plugins.FishyEOS.Util.EOS.LocalProductUserId; return id == null ? "" : id.ToString() ?? ""; }
            catch (Exception) { return ""; }
        }

        private static string NewRoomCode()
        {
            const string letters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var random = new System.Random(Guid.NewGuid().GetHashCode());
            var chars = new char[6];
            for (int i = 0; i < chars.Length; i++) chars[i] = letters[random.Next(letters.Length)];
            return new string(chars);
        }

        private void CreateHostModel(string name, Team team)
        {
            Model = new AquariumModel(speciesCount: AquariumSpecies.Count, drainSeed: Guid.NewGuid().GetHashCode()) { DiveEntry = DiveEntry }; Model.SetMap(PreferredMap);
            var self = Model.AddPlayer(1, name, team); self.alienKind = AquariumAlienKinds.Clamp(PreferredAlien); self.diverKind = AquariumDiverKinds.Clamp(PreferredDiver);
            LocalId = 1;
            Busy = false; Status = ""; State = Model.Snapshot();
        }

        private void StartClient()
        {
            Busy = true; Status = "방장에게 연결하는 중…"; connectDeadline = Time.realtimeSinceStartup + 40;
            if (!network.ClientManager.StartConnection()) throw new InvalidOperationException("참가 연결을 시작하지 못했습니다.");
        }

        // ---- FishNet events --------------------------------------------------------------------------------------
        private void OnServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started) { if (serverReady != null) serverReady.TrySetResult(true); }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                if (serverReady != null) serverReady.TrySetResult(false);
                if (Model != null && !Practice && !leaving)
                { Leave(); Status = "연결이 중단되었습니다. 인터넷 연결을 확인한 뒤 다시 방을 열어 주세요."; }
            }
        }

        private void OnClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                Status = "방장에게 참가 요청을 보내는 중…";
                if (pendingHello != null) SendToHost(new Message { kind = "hello", hello = pendingHello });
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped && Model == null && !leaving)
            {
                string reason = byeReason;
                bool wasIn = Connected;
                Leave();
                Status = reason.Length > 0 ? reason : wasIn ? "방장과 연결이 끊어졌습니다. 방장이 나가면 방이 종료됩니다." : "방장에게 연결하지 못했습니다. 잠시 후 다시 시도해 주세요.";
            }
        }

        private void OnRemoteState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            int client = conn.ClientId;
            if (args.ConnectionState == RemoteConnectionState.Started)
            {
                if (Model == null) { conn.Disconnect(true); return; }
                connecting[client] = Time.realtimeSinceStartup; connectingConns[client] = conn;
                return;
            }
            if (args.ConnectionState != RemoteConnectionState.Stopped || leaving) return;
            connecting.Remove(client); connectingConns.Remove(client);
            int id;
            if (Model != null && peers.TryGetValue(client, out id))
            {
                if (waiting.ContainsKey(id)) waiting.Remove(id); else Model.RemovePlayer(id);
                UpdateObservers(); snapshotTimer = 1;
            }
            peers.Remove(client); conns.Remove(client);
        }

        // The host: a joiner's hello (checked like the old connection approval), then its messages.
        private void OnServerPacket(NetworkConnection conn, AquariumPacket packet)
        {
            int client = conn.ClientId;
            Message message;
            if (!AquariumNetCodec.TryDecode(packet.data, Protocol, out message))
            {
                if (connecting.ContainsKey(client)) Drop(conn, "같은 게임 규칙 버전의 데모로 접속해 주세요.");
                return;
            }
            if (message.kind == "hello") { Welcome(conn, message.hello); return; }
            if (Model == null) return;
            int id; if (!peers.TryGetValue(client, out id)) return;
            if (message.kind == "chat") { HostChat(id, message.text); return; }   // R60: spectators waiting for the next round chat too
            if (waiting.ContainsKey(id)) return;
            if (message.kind == "input") Model.SetInput(id, message.input);
            else if (message.kind == "role") Model.SetRole(id, message.team, message.ready);
            else if (message.kind == "alien") Model.SetAlien(id, message.alien);
            else if (message.kind == "diver") Model.SetDiver(id, message.diver);
            else if (message.kind == "ping") HostPing(id, new Point(message.px, message.py, message.pz));
        }

        private void Welcome(NetworkConnection conn, Hello hello)
        {
            int client = conn.ClientId;
            if (!connecting.ContainsKey(client)) return;
            connecting.Remove(client); connectingConns.Remove(client);
            string reason = "";
            if (hello == null || hello.protocol != Protocol) reason = "같은 게임 규칙 버전의 데모로 접속해 주세요.";
            else if (!string.Equals(roomPassword, hello.password ?? "", StringComparison.Ordinal)) reason = "비밀번호가 맞지 않습니다. 다시 입력해 주세요.";
            else if (Model == null) reason = "방이 종료되었습니다.";
            else if (peers.Count + 1 >= AquariumModel.MaxPlayers) reason = "방이 가득 찼습니다. 관전자를 포함해 최대 5명입니다.";
            else if (hello.team != Team.Hider && hello.team != Team.Seeker) reason = "외계인 또는 잠수부 역할을 선택해 주세요.";
            if (reason.Length > 0) { Drop(conn, reason); return; }
            int id = ++nextPlayer;
            while (Model.Player(id) != null || waiting.ContainsKey(id)) id = ++nextPlayer;
            if (Model.Phase == RoundPhase.Lobby) { var added = Model.AddPlayer(id, hello.name, AvailableTeam(hello.team)); if (added != null) { added.alienKind = AquariumAlienKinds.Clamp(hello.alien); added.diverKind = AquariumDiverKinds.Clamp(hello.diver); } }
            else waiting[id] = new AquariumObserver { id = id, name = AquariumModel.CleanName(hello.name), team = hello.team, alien = AquariumAlienKinds.Clamp(hello.alien), diver = AquariumDiverKinds.Clamp(hello.diver) };
            peers[client] = id; conns[client] = conn;
            UpdateObservers();
            State = Model.Snapshot();
            SendTo(client, StateMessage("welcome", id));
            snapshotTimer = 1;
        }

        // The client: everything comes from the host.
        private void OnClientPacket(AquariumPacket packet)
        {
            Message message;
            if (!AquariumNetCodec.TryDecode(packet.data, Protocol, out message)) return;
            if (message.kind == "bye") { byeReason = message.text ?? ""; return; }
            if (message.kind == "welcome") LocalId = message.player;
            if (message.kind == "chatline") { AddChat(message.player, message.name, message.text, message.team, message.ready); return; }
            if (message.kind == "pingmark") { AddPing(message.player, message.name, new Point(message.px, message.py, message.pz), message.alien); return; }
            if (message.state != null && message.state.players != null && message.state.players.Length > 0 && (State == null || message.state.sequence > State.sequence))
            {
                bool entering = Busy;
                if (State == null || State.round != message.state.round || State.phase != message.state.phase || message.state.time < State.time)
                    remotePresentationTime = message.state.time - .05f;
                State = message.state; Observers = message.observers ?? new AquariumObserver[0]; ApplyPending();
                AquariumMaps.Select(State.map);
                RoomName = message.roomName ?? RoomName; HasPassword = message.hasPassword; remoteHumans = message.humans;
                ReceivedSnapshots++; Busy = false;
                if (entering) Status = IsSpectator ? "경기 중인 방에 관전으로 입장했습니다. 다음 판부터 플레이합니다." : "";
            }
        }

        private int RoleCount(Team team) { return Model.Players.FindAll(p => p.team == team).Count; }

        private Team AvailableTeam(Team requested)
        { return RoleCount(requested) < (requested == Team.Seeker ? 1 : 4) ? requested : requested == Team.Seeker ? Team.Hider : Team.Seeker; }

        private void UpdateObservers() { Observers = new List<AquariumObserver>(waiting.Values).ToArray(); }

        private Message StateMessage(string kind = "state", int player = 0)
        {
            return new Message { kind = kind, player = player, state = State, observers = Observers,
                roomName = RoomName, hasPassword = HasPassword, humans = peers.Count + 1 };
        }

        // ---- sending ---------------------------------------------------------------------------------------------
        private void SendTo(int client, Message message)
        {
            NetworkConnection conn;
            if (network == null || !network.ServerManager.Started || !conns.TryGetValue(client, out conn)) return;
            network.ServerManager.Broadcast(conn, new AquariumPacket { data = AquariumNetCodec.Encode(message, Protocol) }, false, FishNet.Transporting.Channel.Reliable);
        }

        private void SendToHost(Message message)
        {
            if (network == null || !network.ClientManager.Started) return;
            network.ClientManager.Broadcast(new AquariumPacket { data = AquariumNetCodec.Encode(message, Protocol) }, FishNet.Transporting.Channel.Reliable);
        }

        // A short reason to the client, then the door (the reason reaches it before the disconnect).
        private void Drop(NetworkConnection conn, string reason)
        {
            if (network == null || conn == null) return;
            if (network.ServerManager.Started)
                network.ServerManager.Broadcast(conn, new AquariumPacket { data = AquariumNetCodec.Encode(new Message { kind = "bye", text = reason }, Protocol) }, false, FishNet.Transporting.Channel.Reliable);
            conn.Disconnect(false);
        }

        // Snapshots go unreliable: a late one is simply replaced by the next (the client keeps the newest sequence).
        private void BroadcastState()
        {
            if (network == null || !network.ServerManager.Started || conns.Count == 0) return;
            var data = AquariumNetCodec.Encode(StateMessage(), Protocol);
            LastSnapshotBytes = data.Length;
            var packet = new AquariumPacket { data = data };
            foreach (var conn in conns.Values) network.ServerManager.Broadcast(conn, packet, false, FishNet.Transporting.Channel.Unreliable);
        }

        public void Input(SwimInput input)
        {
            if (IsSpectator) return;
            if (Model != null) Model.SetInput(LocalId, input);
            else if (Connected) SendToHost(new Message { kind = "input", input = input });
        }
        public void Role(Team team, bool ready)
        {
            if (IsSpectator) return;
            if (Model != null) Model.SetRole(LocalId, team, ready);
            else { SendToHost(new Message { kind = "role", team = team, ready = ready }); pendTeam = (int)team; pendReady = ready ? 1 : 0; Pend(); }
        }
        public string StartRound()
        {
            if (Model == null) return "방장만 시작할 수 있습니다.";
            if (connecting.Count > 0) return "참가자가 입장 중입니다. 잠시 후 시작해 주세요.";
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
            // A connection that never says hello is let go after 15 s.
            if (connecting.Count > 0)
            {
                int stale = -1; float now = Time.realtimeSinceStartup;
                foreach (var c in connecting) if (now - c.Value > 15f) { stale = c.Key; break; }
                NetworkConnection conn;
                if (stale >= 0) { if (connectingConns.TryGetValue(stale, out conn)) conn.Disconnect(true); connecting.Remove(stale); connectingConns.Remove(stale); }
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
            else SendToHost(new Message { kind = "chat", text = text });
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
            foreach (var peer in peers) SendTo(peer.Key, line);
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
            else SendToHost(new Message { kind = "ping", px = at.x, py = at.y, pz = at.z });
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
                var target = Model.Players.Find(p => p.id == peer.Value);
                if (target != null && target.team == Team.Hider) SendTo(peer.Key, mark);
            }
        }
        private void AddPing(int id, string name, Point at, int alien)
        {
            Pings.RemoveAll(p => p.player == id);   // one mark per alien: a new ping moves it
            Pings.Add(new PingMark { player = id, name = name, at = at, shownAt = Time.realtimeSinceStartup, alienKind = alien });
        }

        // R84: a client's own lobby choices (alien, diver, 술래 wish, ready) show at once: they are laid over the
        // host's snapshots until the host's state agrees (or 5 s pass), instead of waiting for the round trip.
        private int pendAlien = -1, pendDiver = -1, pendTeam = -1, pendReady = -1;
        private float pendUntil;
        private void Pend() { pendUntil = Time.realtimeSinceStartup + 5f; ApplyPending(); }
        private void ApplyPending()
        {
            if (pendAlien < 0 && pendDiver < 0 && pendTeam < 0 && pendReady < 0) return;
            Swimmer me = null;
            if (Model == null && State != null && State.players != null && State.phase == RoundPhase.Lobby && Time.realtimeSinceStartup <= pendUntil)
                foreach (var p in State.players) if (p.id == LocalId) { me = p; break; }
            if (me == null) { pendAlien = pendDiver = pendTeam = pendReady = -1; return; }
            if (pendAlien >= 0) { if (me.alienKind == pendAlien) pendAlien = -1; else me.alienKind = pendAlien; }
            if (pendDiver >= 0) { if (me.diverKind == pendDiver) pendDiver = -1; else me.diverKind = pendDiver; }
            if (pendTeam >= 0) { if ((int)me.team == pendTeam) pendTeam = -1; else me.team = (Team)pendTeam; }
            if (pendReady >= 0) { bool r = pendReady == 1; if (me.ready == r) pendReady = -1; else me.ready = r; }
        }

        // R84: the host puts a player out of the room (waiting room only).
        public const string KickReason = "방장이 방에서 내보냈습니다.";
        public bool Kick(int playerId)
        {
            if (Model == null || Practice || network == null || playerId == LocalId || Model.Phase != RoundPhase.Lobby) return false;
            foreach (var peer in peers)
            {
                NetworkConnection conn;
                if (peer.Value == playerId && conns.TryGetValue(peer.Key, out conn)) { Drop(conn, KickReason); return true; }
            }
            return false;
        }

        public void Leave()
        {
            generation++; leaving = true;
            Chat.Clear(); Pings.Clear(); chatTimes.Clear(); pingTimes.Clear();
            if (!string.IsNullOrEmpty(lobbyId)) cleanupOperation = CleanupAfter(cleanupOperation, lobbyId, ownsLobby);
            lobbyId = ""; ownsLobby = false; roomPassword = ""; publishedPhase = ""; byeReason = "";
            RoomName = ""; HasPassword = heartbeatPending = directoryPending = false;
            Status = "";
            nextHeartbeat = nextDirectory = 0; remoteHumans = 0;
            if (network != null)
            {
                var net = network; network = null;
                try
                {
                    if (net.ClientManager != null && net.ClientManager.Started) net.ClientManager.StopConnection();
                    if (net.ServerManager != null && net.ServerManager.Started) net.ServerManager.StopConnection(true);
                }
                catch (Exception ex) { Debug.LogWarning("Aquarium network shutdown: " + ex.GetType().Name); }
                if (networkObject != null) UnityEngine.Object.Destroy(networkObject);
                RestoreFrameRate();
            }
            if (serverReady != null) serverReady.TrySetResult(false);
            serverReady = null; pendingHello = null;
            network = null; networkObject = null; Model = null; State = null; LocalId = 0; Code = "";
            Busy = Practice = false; peers.Clear(); conns.Clear(); connecting.Clear(); connectingConns.Clear(); nextPlayer = 1; accumulator = snapshotTimer = connectDeadline = 0;
            waiting.Clear(); Observers = new AquariumObserver[0]; pendAlien = pendDiver = pendTeam = pendReady = -1;
            ReceivedSnapshots = 0; LastSnapshotBytes = 0; remotePresentationTime = float.NaN; leaving = false;
        }
        public async Task ShutdownAsync()
        { Leave(); await connectionOperation; await cleanupOperation; }
        public void Dispose() { Leave(); }
    }
}
