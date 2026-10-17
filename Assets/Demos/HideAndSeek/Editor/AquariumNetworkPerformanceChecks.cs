using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumNetworkPerformanceChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static bool allocationCounterAvailable;

        public static void Run()
        {
            string output = Environment.GetEnvironmentVariable("AQUARIUM_NETWORK_QA");
            if (string.IsNullOrEmpty(output)) output = "Artifacts/NetworkQA";
            Directory.CreateDirectory(output);
            var result = new StringBuilder("baseline=3e12b65\n");
            try
            {
                bool recorderValid;
                int probeSamples;
                long probe = ProbeAllocationCounter(out recorderValid, out probeSamples);
                allocationCounterAvailable = probe >= 65536;
                result.AppendLine("managed_allocation_counter=GC.Alloc frame_aggregate recorder_valid=" + recorderValid +
                    " probe_samples=" + probeSamples + " probe_65536_observed_bytes=" + probe +
                    " allocation_comparison=" + (allocationCounterAvailable ? "supported" : "unsupported"));
                var model = new AquariumModel(73);
                model.AddPlayer(1, "Snapshot 검사", Team.Seeker);
                model.Fish[0].eaten = true;
                model.Fish[1].dead = true;
                model.Fish[2].dead = model.Fish[2].eaten = true;
                var snapshot = model.Snapshot();
                Require(snapshot.eaten == 2 && snapshot.killed == 1 && snapshot.remaining == model.Fish.Count - 3,
                    "Snapshot food counters must preserve eaten/dead precedence");
                Require(snapshot.target == model.Fish.Count - 1 && ReferenceEquals(snapshot.players[0], model.Players[0]) &&
                    ReferenceEquals(snapshot.fish[0], model.Fish[0]), "Snapshot payload and shallow-copy semantics changed");
                Require(model.Snapshot().sequence == snapshot.sequence + 1, "Snapshot sequence must increase once per capture");

                long baselineBytes, currentBytes;
                double baselineUs = Measure(() => BaselineSnapshot(model), out baselineBytes);
                double currentUs = Measure(() => model.Snapshot(), out currentBytes);
                result.AppendLine("snapshot_baseline_us=" + Number(baselineUs) + " current_us=" + Number(currentUs) +
                    " baseline_bytes=" + Bytes(baselineBytes) + " current_bytes=" + Bytes(currentBytes));
                if (baselineBytes >= 0 && currentBytes >= 0)
                    Require(currentBytes < baselineBytes, "Snapshot must remove temporary FindAll list allocations");

                CheckSession(result);
                CheckViewKeys(result);
                result.AppendLine("OK");
                File.WriteAllText(Path.Combine(output, "network-performance-checks.txt"), result.ToString());
            }
            catch (Exception ex)
            {
                result.AppendLine("FAIL " + ex);
                File.WriteAllText(Path.Combine(output, "network-performance-checks.txt"), result.ToString());
                throw;
            }
            UnityEngine.Debug.Log("AQUARIUM_NETWORK_PERFORMANCE_CHECKS_OK " + result);
        }

        private static void CheckSession(StringBuilder result)
        {
            var session = new AquariumSession();
            var root = new GameObject("Network snapshot checks");
            var type = typeof(AquariumSession);
            var networkField = type.GetField("network", Private);
            try
            {
                session.StartPractice("네트워크 검사", Team.Seeker);
                Require((int)type.GetField("Protocol", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue() == 6,
                    "Network protocol must remain compatible with v6 clients");
                var createMessage = (Func<string, int, object>)Delegate.CreateDelegate(typeof(Func<string, int, object>),
                    session, type.GetMethod("StateMessage", Private));
                object message = createMessage("state", 0);
                var payload = (AquariumSnapshot)message.GetType().GetField("state").GetValue(message);
                Require(ReferenceEquals(payload, session.State), "Broadcast must reuse the host render snapshot");
                Require(payload.sequence == session.State.sequence, "Host and transmitted state sequences differ");
                var writeState = (Action<FastBufferWriter>)Delegate.CreateDelegate(typeof(Action<FastBufferWriter>),
                    session, type.GetMethod("WriteState", Private));

                string json = JsonUtility.ToJson(message);
                using (var writer = new FastBufferWriter(60000, Allocator.Temp))
                {
                    writeState(writer);
                    int length = writer.Length;
                    for (int recipient = 0; recipient < 4; recipient++)
                    {
                        using (var reader = new FastBufferReader(writer, Allocator.Temp))
                        {
                            string actual;
                            reader.ReadValueSafe(out actual);
                            Require(actual == json && writer.Length == length,
                                "A shared state packet must retain the exact v6 string payload for every recipient");
                        }
                    }
                    result.AppendLine("four_recipient_packet_bytes=" + length + " host_sequence=" + payload.sequence);
                }

                // Joining between simulation ticks must publish a fresh welcome sequence.
                var helloType = type.GetNestedType("Hello", BindingFlags.NonPublic);
                object hello = Activator.CreateInstance(helloType, true);
                helloType.GetField("name").SetValue(hello, "관전자 검사");
                helloType.GetField("team").SetValue(hello, Team.Hider);
                var approved = (System.Collections.IDictionary)type.GetField("approved", Private).GetValue(session);
                approved.Add(1UL, hello);
                var beforeWelcome = session.State;
                type.GetMethod("OnConnected", Private).Invoke(session, new object[] { 1UL });
                Require(!ReferenceEquals(beforeWelcome, session.State) && session.State.sequence > beforeWelcome.sequence &&
                    session.Observers.Length == 1, "A mid-round arrival must capture fresh state and observer metadata");
                object welcome = createMessage("welcome", session.Observers[0].id);
                Require(ReferenceEquals(welcome.GetType().GetField("state").GetValue(welcome), session.State),
                    "Welcome must use the newly captured host state");
                result.AppendLine("welcome_captures_fresh_sequence=true mid_round_observer=true");

                // A disconnected/non-listening transport used to trigger another Snapshot anyway.
                networkField.SetValue(session, root.AddComponent<NetworkManager>());
                type.GetField("snapshotTimer", Private).SetValue(session, 1f);
                int before = session.State.sequence;
                session.Update(1f / 30f);
                Require(session.State.sequence == before + 1, "A host tick must capture state only once");
                Require(session.Model.Snapshot().sequence == session.State.sequence + 1,
                    "Periodic transmission still creates a hidden duplicate snapshot");
                result.AppendLine("host_tick_snapshots=1 serialized_state_reuses_host=true protocol=6");

                message = createMessage("state", 0);
                long baselineBytes, currentBytes;
                double baselineUs = Measure(() => SerializeRecipients(message, 4), out baselineBytes);
                double currentUs = Measure(() => PrepareState(writeState), out currentBytes);
                result.AppendLine("four_peer_state_preparation_baseline_us=" + Number(baselineUs) + " current_us=" + Number(currentUs) +
                    " baseline_bytes=" + Bytes(baselineBytes) + " current_bytes=" + Bytes(currentBytes));
                if (baselineBytes >= 0 && currentBytes >= 0)
                    Require(currentBytes < baselineBytes, "One shared packet must reduce four-peer serialization allocations");
            }
            finally
            {
                networkField.SetValue(session, null);
                session.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static AquariumSnapshot BaselineSnapshot(AquariumModel model)
        {
            int eaten = model.Fish.FindAll(f => f.eaten).Count;
            int killed = model.Fish.FindAll(f => f.dead && !f.eaten).Count;
            return new AquariumSnapshot { round = model.Round, time = model.Time, huntAt = model.HuntAt, endsAt = model.EndsAt,
                winner = model.Winner, phase = model.Phase, winReason = model.WinReason, reviewEndsAt = model.ReviewEndsAt,
                finishedAt = model.FinishedAt, target = model.Fish.Count - killed, killed = killed,
                remaining = model.Fish.Count - killed - eaten, eaten = eaten, players = model.Players.ToArray(),
                fish = model.Fish.ToArray(), harpoons = model.Harpoons.ToArray(), effects = model.Effects.ToArray(), eggs = model.Eggs.ToArray() };
        }

        private static void CheckViewKeys(StringBuilder result)
        {
            var type = typeof(AquariumView);
            var view = (AquariumView)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
            foreach (string name in new[] { "actors", "playerKeys", "fishKeys", "effects", "visible", "removedActors", "activeEffects", "expiredEffects" })
            {
                var field = type.GetField(name, Private);
                field.SetValue(view, Activator.CreateInstance(field.FieldType));
            }
            var playerKey = (Func<int, string>)Delegate.CreateDelegate(typeof(Func<int, string>), view, type.GetMethod("PlayerKey", Private));
            var fishKey = (Func<int, string>)Delegate.CreateDelegate(typeof(Func<int, string>), view, type.GetMethod("FishKey", Private));
            var fishKind = (Func<int, string>)Delegate.CreateDelegate(typeof(Func<int, string>),
                type.GetMethod("FishKind", BindingFlags.Static | BindingFlags.NonPublic));
            var fishKeys = (System.Collections.IDictionary)type.GetField("fishKeys", Private).GetValue(view);
            const int id = 7;
            string player = playerKey(id), fish = fishKey(id);
            Require(player == "p7" && fish == "f7" && player != fish, "Player and fish actor namespaces changed");
            Require(ReferenceEquals(player, playerKey(id)) && ReferenceEquals(fish, fishKey(id)), "Warm actor keys must be reused");
            foreach (int species in new[] { -1, 0, 1, 2, 3, 99, 100 })
            {
                Require(fishKind(species) == "fish" + species, "Cached/fallback fish kind changed for species " + species);
                if (species >= 0 && species <= 2)
                    Require(ReferenceEquals(fishKind(species), fishKind(species)), "Supported fish kinds must reuse their strings");
            }

            var root = new GameObject("Actor key cache checks");
            float previousTime = Shader.GetGlobalFloat("_AquariumPreviewTime");
            float previousEnabled = Shader.GetGlobalFloat("_AquariumPreviewTimeEnabled");
            try
            {
                type.GetField("root", Private).SetValue(view, root.transform);
                type.GetField("localRing", Private).SetValue(view, root.AddComponent<LineRenderer>());
                var state = new AquariumSnapshot { round = 1, time = 1, phase = RoundPhase.Playing,
                    players = new Swimmer[0], fish = new Prey[0], harpoons = new Harpoon[0], effects = new WaterEffect[0], eggs = new RevivalEgg[0] };
                view.Draw(state, 0, 1f / 60f);
                Require(fishKeys.Count == 0 && ReferenceEquals(player, playerKey(id)), "A round change must reset only fish keys");
                string firstRoundFish = fishKey(id);
                view.Draw(state, 0, 1f / 60f);
                Require(ReferenceEquals(firstRoundFish, fishKey(id)), "Same-round snapshots discarded the warm fish key");
                state.round = 2; state.time = 2;
                view.Draw(state, 0, 1f / 60f);
                Require(fishKeys.Count == 0 && fishKey(id) == firstRoundFish && ReferenceEquals(player, playerKey(id)),
                    "Rematch must preserve actor key values while resetting the fish cache");
                result.AppendLine("view_key_namespace=true warm_identity=true round_reset=true fish_kind_fallback=true");

                int measuredId = id;
                long baselineBytes, currentBytes;
                double baselineUs = Measure(() => "p" + measuredId, out baselineBytes);
                double currentUs = Measure(() => playerKey(measuredId), out currentBytes);
                result.AppendLine("warm_player_key_baseline_us=" + Number(baselineUs) + " current_us=" + Number(currentUs) +
                    " baseline_bytes=" + Bytes(baselineBytes) + " current_bytes=" + Bytes(currentBytes));
                if (baselineBytes >= 0 && currentBytes >= 0)
                    Require(currentBytes < baselineBytes, "Warm player keys must remove concatenation allocations");
            }
            finally
            {
                Shader.SetGlobalFloat("_AquariumPreviewTime", previousTime);
                Shader.SetGlobalFloat("_AquariumPreviewTimeEnabled", previousEnabled);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static object SerializeRecipients(object message, int serializations)
        {
            for (int recipient = 0; recipient < serializations; recipient++)
            {
                string json = JsonUtility.ToJson(message);
                using (var writer = new FastBufferWriter(60000, Allocator.Temp)) writer.WriteValueSafe(json);
            }
            return message;
        }

        private static object PrepareState(Action<FastBufferWriter> writeState)
        {
            using (var writer = new FastBufferWriter(60000, Allocator.Temp)) writeState(writer);
            return null;
        }

        private static double Measure(Func<object> action, out long bytes)
        {
            for (int i = 0; i < 20; i++) action();
            const int count = 300;
            var timer = new Stopwatch();
            timer.Start();
            for (int i = 0; i < count; i++) action();
            timer.Stop();
            // Some Editor versions do not flush a current-frame GC.Alloc sample on Stop.
            // Report unsupported instead of turning an unobserved counter into zero allocation.
            bytes = -1;
            if (allocationCounterAvailable)
            {
                using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1,
                    ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
                {
                    if (recorder.Valid)
                    {
                        for (int i = 0; i < count; i++) action();
                        recorder.Stop();
                        bytes = recorder.Count == 0 ? 0 : recorder.GetSample(0).Value / count;
                    }
                }
            }
            return timer.Elapsed.TotalMilliseconds * 1000 / count;
        }

        private static long ProbeAllocationCounter(out bool valid, out int samples)
        {
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1,
                ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
            {
                valid = recorder.Valid;
                samples = 0;
                if (!valid) return -1;
                var probe = new byte[65536];
                recorder.Stop();
                samples = recorder.Count;
                long measured = samples == 0 ? 0 : recorder.GetSample(0).Value;
                GC.KeepAlive(probe);
                return measured;
            }
        }

        private static string Number(double value) { return value.ToString("F3", CultureInfo.InvariantCulture); }
        private static string Bytes(long value) { return value < 0 ? "unsupported" : value.ToString(CultureInfo.InvariantCulture); }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
