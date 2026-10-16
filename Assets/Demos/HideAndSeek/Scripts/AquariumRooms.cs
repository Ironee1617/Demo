using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    [Serializable]
    public sealed class AquariumRoom
    {
        public string id, name, code, phase;
        public int players, maxPlayers;
        public bool hasPassword;
        public bool CanJoin { get { return players < maxPlayers; } }
    }

    // Lobby discovers rooms and checks passwords; Relay still carries the game connection.
    public static class AquariumRooms
    {
        public const string ProtocolTag = "aquarium-v6";
        public const string ProtocolKey = "protocol";
        public const string RelayCodeKey = "relayCode";
        public const string PhaseKey = "phase";
        public const string WaitingPhase = "Waiting";
        private static Task initialization;
        private static readonly SemaphoreSlim queryGate = new SemaphoreSlim(1, 1);
        private static long nextQueryAt;

        public static Task EnsureServicesAsync()
        {
            if (initialization != null && !initialization.IsCompleted) return initialization;
            if (UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn)
                return Task.CompletedTask;
            initialization = InitializeAsync();
            return initialization;
        }

        private static async Task InitializeAsync()
        {
            if (string.IsNullOrEmpty(Application.cloudProjectId))
                throw new InvalidOperationException("Unity Cloud 연결이 필요합니다. Edit → Project Settings → Services에서 프로젝트를 연결해 주세요.");
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync(new InitializationOptions().SetProfile("aquarium_" + Guid.NewGuid().ToString("N").Substring(0, 12)));
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        public static async Task<List<AquariumRoom>> QueryAsync()
        {
            await EnsureServicesAsync();
            var filters = ProtocolFilters();
            filters.Add(new QueryFilter(QueryFilter.FieldOptions.S3, WaitingPhase, QueryFilter.OpOptions.EQ));
            var result = await QueryLobbiesAsync(new QueryLobbiesOptions
            {
                Count = 50,
                Filters = filters,
                Order = new List<QueryOrder> { new QueryOrder(false, QueryOrder.FieldOptions.Created) }
            });
            var rooms = new List<AquariumRoom>();
            if (result.Results != null)
                foreach (var lobby in result.Results) rooms.Add(Describe(lobby));
            return rooms;
        }

        public static async Task<Lobby> CreateAsync(string roomName, string password, string relayCode, string allocationId = "")
        {
            roomName = (roomName ?? "").Trim();
            if (roomName.Length < 1 || roomName.Length > 32)
                throw new InvalidOperationException("방 이름은 1~32자로 입력해 주세요.");
            ValidatePassword(password);
            relayCode = NormalizeCode(relayCode);
            if (relayCode.Length == 0) throw new InvalidOperationException("방 연결 코드를 만들지 못했습니다.");
            await EnsureServicesAsync();
            return await LobbyService.Instance.CreateLobbyAsync(roomName, AquariumModel.MaxPlayers, new CreateLobbyOptions
            {
                IsPrivate = false,
                IsLocked = false,
                Password = string.IsNullOrEmpty(password) ? null : password,
                Player = new Player(AuthenticationService.Instance.PlayerId, allocationId: string.IsNullOrEmpty(allocationId) ? null : allocationId),
                Data = new Dictionary<string, DataObject>
                {
                    { ProtocolKey, PublicData(ProtocolTag, DataObject.IndexOptions.S1) },
                    { RelayCodeKey, PublicData(relayCode, DataObject.IndexOptions.S2) },
                    { PhaseKey, PublicData(WaitingPhase, DataObject.IndexOptions.S3) }
                }
            });
        }

        public static async Task<Lobby> JoinAsync(string lobbyId, string password, string allocationId = "")
        {
            ValidatePassword(password);
            await EnsureServicesAsync();
            return await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, new JoinLobbyByIdOptions
            {
                Password = string.IsNullOrEmpty(password) ? null : password,
                Player = new Player(AuthenticationService.Instance.PlayerId, allocationId: string.IsNullOrEmpty(allocationId) ? null : allocationId)
            });
        }

        public static async Task<Lobby> FindByCodeAsync(string relayCode)
        {
            relayCode = NormalizeCode(relayCode);
            if (relayCode.Length == 0) throw new InvalidOperationException("방 코드를 입력해 주세요.");
            await EnsureServicesAsync();
            var filters = ProtocolFilters();
            filters.Add(new QueryFilter(QueryFilter.FieldOptions.S2, relayCode, QueryFilter.OpOptions.EQ));
            var result = await QueryLobbiesAsync(new QueryLobbiesOptions { Count = 1, Filters = filters });
            return result.Results != null && result.Results.Count > 0 ? result.Results[0] : null;
        }

        private static async Task<QueryResponse> QueryLobbiesAsync(QueryLobbiesOptions options)
        {
            await queryGate.WaitAsync();
            try
            {
                long remaining;
                while ((remaining = nextQueryAt - Stopwatch.GetTimestamp()) > 0)
                    await Task.Delay((int)Math.Ceiling(remaining * 1000d / Stopwatch.Frequency));
                // Both entry paths share the service's limit, including unsuccessful requests.
                nextQueryAt = Stopwatch.GetTimestamp() + (long)Math.Ceiling(Stopwatch.Frequency * 1.1d);
                return await LobbyService.Instance.QueryLobbiesAsync(options);
            }
            finally { queryGate.Release(); }
        }

        public static async Task UpdateAsync(string lobbyId, string phase)
        {
            await EnsureServicesAsync();
            await LobbyService.Instance.UpdateLobbyAsync(lobbyId, new UpdateLobbyOptions
            {
                IsLocked = false,
                Data = new Dictionary<string, DataObject> { { PhaseKey, PublicData(phase, DataObject.IndexOptions.S3) } }
            });
        }

        public static async Task HeartbeatAsync(string lobbyId)
        {
            await EnsureServicesAsync();
            await LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
        }

        public static async Task LeaveAsync(string lobbyId, bool host)
        {
            if (string.IsNullOrEmpty(lobbyId)) return;
            await EnsureServicesAsync();
            try
            {
                if (host) await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
                else await LobbyService.Instance.RemovePlayerAsync(lobbyId, AuthenticationService.Instance.PlayerId);
            }
            catch (LobbyServiceException ex) when (AlreadyRemoved(ex)) { }
        }

        public static AquariumRoom Describe(Lobby lobby)
        {
            if (lobby == null) throw new ArgumentNullException(nameof(lobby));
            return new AquariumRoom
            {
                id = lobby.Id, name = lobby.Name, code = ReadData(lobby, RelayCodeKey), phase = ReadData(lobby, PhaseKey),
                players = lobby.MaxPlayers - lobby.AvailableSlots, maxPlayers = lobby.MaxPlayers, hasPassword = lobby.HasPassword
            };
        }

        private static string ReadData(Lobby lobby, string key)
        {
            DataObject value;
            return lobby.Data != null && lobby.Data.TryGetValue(key, out value) && value != null ? value.Value ?? "" : "";
        }

        private static List<QueryFilter> ProtocolFilters()
        {
            return new List<QueryFilter> { new QueryFilter(QueryFilter.FieldOptions.S1, ProtocolTag, QueryFilter.OpOptions.EQ) };
        }

        private static DataObject PublicData(string value, DataObject.IndexOptions index)
        {
            return new DataObject(DataObject.VisibilityOptions.Public, value, index);
        }

        private static string NormalizeCode(string code) { return (code ?? "").Trim().ToUpperInvariant(); }
        private static void ValidatePassword(string password)
        {
            if (!string.IsNullOrEmpty(password) && (password.Length < 8 || password.Length > 64))
                throw new InvalidOperationException("비밀번호는 8~64자로 입력하거나 비워 두세요.");
        }

        private static bool AlreadyRemoved(LobbyServiceException ex)
        {
            return ex.Reason == LobbyExceptionReason.LobbyNotFound || ex.Reason == LobbyExceptionReason.PlayerNotFound;
        }
    }
}
