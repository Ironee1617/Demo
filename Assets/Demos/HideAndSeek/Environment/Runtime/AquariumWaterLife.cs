using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    // R85: the feeling of being in water. Presentation only, local to this machine, nothing synced, no rules:
    //  * suspended matter: fine specks that drift with the tank's current round the camera and catch the light
    //    shafts, and now and then a larger bit (an algae strand, a cluster of specks, a food crumb, a tiny bubble).
    //    One static mesh that wraps round the camera in its shader ("Aquarium/Water Motes"), so the cost is the same
    //    everywhere and nothing is updated on the CPU;
    //  * ambient bubble streams: a dozen spots per map (the top of a rock or coral, or the sand; the same spots for
    //    the same map and scenery) release a burst of bubbles, pause, and release again. One small mesh per layout,
    //    animated in "Aquarium/Water Bubbles"; found a few spots per frame so starting a round never stalls;
    //  * bubbles shed by a diver or an undisguised alien at sprint speed (never by a fish or a disguised alien:
    //    that would give the alien away), 48 slots written into one vector array;
    //  * the swimmers the plants lean away from (the divers and this machine's own player only, for the same
    //    reason), and the current's clock for the sway (AquariumCurrent.cginc).
    // Per frame: a few global shader values, no allocation. "빠르게" graphics: fewer specks and bits, no shed
    // bubbles, the cheaper sway.
    public sealed class AquariumWaterLife : IDisposable
    {
        public static bool Fast;              // the pause menu's graphics option (AquariumGame.ApplyGraphicsChoice)
        public static bool Enabled = true;    // QA: everything here off (the sway stays)
        public static readonly List<Vector3> DebugSpots = new List<Vector3>();   // QA: where the bubble streams start
        // ---- tuning
        public const int Specks = 1000, SpecksFast = 380;      // specks in the box round the camera
        public const int Bits = 12, BitsFast = 5;              // larger bits in the same box, each shown about a third of the time
        public const float HalfWidth = 5f, HalfHeight = 3.5f;   // the box (m)
        public const float DriftSpeed = .045f;                 // how fast the current carries the specks (m/s)
        public const float Opacity = 1f;                       // overall strength of specks and bubbles
        public const int Streams = 12, TutorialStreams = 4;    // bubble spots per map
        public const int TrailSlots = 48, TrailPerSlot = 2;    // must match _AqTrail[48] in WaterBubbles.shader
        const float TrailInterval = .07f;

        static readonly int DriftId = Shader.PropertyToID("_AqDrift"), WaterId = Shader.PropertyToID("_AqWater"), PushId = Shader.PropertyToID("_AqPush"),
            TrailId = Shader.PropertyToID("_AqTrail"), CheapId = Shader.PropertyToID("_AqSwayCheap"), TintId = Shader.PropertyToID("_Tint");
        readonly GameObject holder;
        readonly Mesh moteMesh, streamMesh, trailMesh;
        readonly Material moteMaterial, bubbleMaterial;
        readonly MeshRenderer moteRenderer, streamRenderer, trailRenderer;
        readonly Material[] moteFull, moteFast;
        readonly Vector4[] push = new Vector4[4], pushNext = new Vector4[4]; int pushCount;
        readonly Vector4[] trail = new Vector4[TrailSlots]; int trailNext, emitted; readonly float[] trailAt = new float[16];
        Vector3 drift; float lastTime = -1, now, top = AquariumModel.WaterHeight; bool fastShown, shown = true, grottoShown; readonly bool ready;
        int buildMap = -1, buildVersion = -1, candidate, accepted, onRock, onSand; bool building;
        readonly List<Vector3> sv = new List<Vector3>(1024); readonly List<Vector2> sc = new List<Vector2>(1024);
        readonly List<Vector4> s1 = new List<Vector4>(1024), s2 = new List<Vector4>(1024); readonly List<int> st = new List<int>(1536);

        public AquariumWaterLife(Transform parent)
        {
            var moteShader = Shader.Find("Aquarium/Water Motes"); var bubbleShader = Shader.Find("Aquarium/Water Bubbles");
            if (moteShader == null || bubbleShader == null) return;
            holder = new GameObject("R85 • Water life (specks, bubble streams)") { hideFlags = HideFlags.DontSave }; holder.transform.SetParent(parent, false);
            moteMaterial = new Material(moteShader) { name = "Water motes", hideFlags = HideFlags.HideAndDontSave };
            moteMaterial.SetTexture("_Caustics", Resources.Load<Texture2D>("Aquarium/Caustics"));
            bubbleMaterial = new Material(bubbleShader) { name = "Ambient bubbles", hideFlags = HideFlags.HideAndDontSave };
            moteFull = new[] { moteMaterial, moteMaterial }; moteFast = new[] { moteMaterial };
            moteMesh = BuildMotes(); trailMesh = BuildTrail();
            streamMesh = new Mesh { name = "Ambient bubble streams", hideFlags = HideFlags.HideAndDontSave }; streamMesh.MarkDynamic();
            moteRenderer = Make("Suspended specks and bits", moteMesh, moteMaterial); moteRenderer.sharedMaterials = moteFull;
            streamRenderer = Make("Ambient bubble streams", streamMesh, bubbleMaterial);
            trailRenderer = Make("Swimmer bubbles", trailMesh, bubbleMaterial);
            for (int i = 0; i < TrailSlots; i++) trail[i] = new Vector4(0, -1000, 0, -100);
            for (int i = 0; i < 4; i++) push[i] = pushNext[i] = new Vector4(0, -1000, 0, 0);
            ready = true;
        }
        MeshRenderer Make(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave }; go.transform.SetParent(holder.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off; r.allowOcclusionWhenDynamic = false;
            return r;
        }
        static float H(int i, int salt) { unchecked { uint n = (uint)(i * 374761393 + salt * 668265263); n = (n ^ (n >> 13)) * 1274126177; return ((n ^ (n >> 16)) & 65535) / 65535f; } }
        static readonly Bounds Everywhere = new Bounds(Vector3.zero, Vector3.one * 4000);
        static void Quad(List<Vector3> v, List<Vector2> c, List<Vector4> a, List<Vector4> b, List<int> tri, Vector3 at, Vector4 d1, Vector4 d2)
        {
            int s = v.Count;
            for (int k = 0; k < 4; k++) { v.Add(at); c.Add(new Vector2(k == 0 || k == 3 ? -1 : 1, k < 2 ? -1 : 1)); a.Add(d1); if (b != null) b.Add(d2); }
            tri.Add(s); tri.Add(s + 1); tri.Add(s + 2); tri.Add(s); tri.Add(s + 2); tri.Add(s + 3);
        }

        // Specks (kind 0) and bits (1 algae strand, 2 speck cluster, 3 food crumb, 4 tiny bubble). Submesh 0 is what the
        // fast mode draws; submesh 1 the rest.
        static Mesh BuildMotes()
        {
            var v = new List<Vector3>(); var c = new List<Vector2>(); var d = new List<Vector4>(); var first = new List<int>(); var rest = new List<int>();
            for (int i = 0; i < Bits; i++)
            {
                int kind = 1 + i % 4; float r = H(i, 2);
                float size = kind == 1 ? .035f + .04f * r : kind == 2 ? .028f + .022f * r : kind == 3 ? .009f + .008f * r : .005f + .005f * r;
                Quad(v, c, d, null, i < BitsFast ? first : rest, new Vector3(H(i, 5), H(i, 6), H(i, 7)), new Vector4(kind, size, H(i, 3), H(i, 4)), default(Vector4));
            }
            for (int i = 0; i < Specks; i++)
            {
                float r = H(i, 12); float size = .005f + .010f * r * r + (H(i, 13) > .96f ? .008f : 0);
                Quad(v, c, d, null, i < SpecksFast ? first : rest, new Vector3(H(i, 15), H(i, 16), H(i, 17)), new Vector4(0, size, H(i, 18), H(i, 19)), default(Vector4));
            }
            var mesh = new Mesh { name = "Water motes", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32, subMeshCount = 2 };
            mesh.SetVertices(v); mesh.SetUVs(0, c); mesh.SetUVs(1, d); mesh.SetTriangles(first, 0, false); mesh.SetTriangles(rest, 1, false);
            mesh.bounds = Everywhere; mesh.UploadMeshData(true);
            return mesh;
        }
        static Mesh BuildTrail()
        {
            var v = new List<Vector3>(); var c = new List<Vector2>(); var a = new List<Vector4>(); var b = new List<Vector4>(); var tri = new List<int>();
            for (int s = 0; s < TrailSlots; s++)
                for (int k = 0; k < TrailPerSlot; k++)
                {
                    int i = s * TrailPerSlot + k;
                    Quad(v, c, a, b, tri, Vector3.zero, new Vector4(2, H(i, 31), .011f + .02f * H(i, 32) * H(i, 33), H(i, 34)), new Vector4(s, 0, 0, 0));
                }
            var mesh = new Mesh { name = "Swimmer bubbles", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(v); mesh.SetUVs(0, c); mesh.SetUVs(1, a); mesh.SetUVs(2, b); mesh.SetTriangles(tri, 0, false);
            mesh.bounds = Everywhere; mesh.UploadMeshData(true);
            return mesh;
        }

        // Every frame, with the reef's shader clock (frozen with the final scene).
        public void Present(float time)
        {
            if (!ready) return;
            now = time;
            float dt = lastTime < 0 ? 0 : Mathf.Clamp(time - lastTime, 0, .1f); lastTime = time;
            // AqCurrentDir (AquariumCurrent.cginc)
            float a = .6f + .55f * Mathf.Sin(time * .043f) + .35f * Mathf.Sin(time * .071f + 1.3f);
            drift += new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (DriftSpeed * dt);
            top = AquariumMaps.IsTutorial ? AquariumTutorialMap.Top : AquariumModel.WaterHeight;
            Shader.SetGlobalVector(DriftId, drift);
            Shader.SetGlobalVector(WaterId, new Vector4(top, Opacity, HalfWidth, HalfHeight));
            Shader.SetGlobalFloat(CheapId, Fast ? 1 : 0);
            for (int i = 0; i < 4; i++) { push[i] = i < pushCount ? pushNext[i] : new Vector4(0, -1000, 0, 0); }
            pushCount = 0;
            Shader.SetGlobalVectorArray(PushId, push);
            Shader.SetGlobalVectorArray(TrailId, trail);
            if (shown != Enabled) { shown = Enabled; moteRenderer.enabled = streamRenderer.enabled = shown; trailRenderer.enabled = shown && !Fast; }
            if (fastShown != Fast) { fastShown = Fast; moteRenderer.sharedMaterials = Fast ? moteFast : moteFull; trailRenderer.enabled = shown && !Fast; }
            bool grotto = AquariumMaps.IsGrotto;
            if (grotto != grottoShown) { grottoShown = grotto; moteMaterial.SetColor(TintId, grotto ? new Color(.66f, .6f, .86f, 1) : new Color(.86f, .95f, .93f, 1)); }
            StepStreams();
        }

        // A swimmer drawn this frame. Divers and undisguised aliens shed bubbles at sprint speed; plants lean away
        // from divers and from this machine's own player.
        public void Swimmer(int id, Vector3 position, Vector3 velocity, bool diver, bool alienForm, bool local)
        {
            if (!ready || position.y > top - .25f || position.y < -3) return;
            float speed = velocity.magnitude;
            if ((diver || local) && pushCount < 4) pushNext[pushCount++] = new Vector4(position.x, position.y, position.z, Mathf.Clamp01((speed - .3f) / 2.2f));
            if (Fast || !Enabled || !(diver || alienForm)) return;
            if (speed < (diver ? AquariumModel.SeekerSpeed : AquariumModel.HiderSpeed) * 1.06f) return;
            int slot = id & 15; if (now - trailAt[slot] < TrailInterval && now >= trailAt[slot]) return;
            trailAt[slot] = now; emitted++;
            var at = position - velocity / speed * (diver ? .9f : .45f) + new Vector3(H(emitted, 1) - .5f, H(emitted, 2) - .5f, H(emitted, 3) - .5f) * .3f;
            trail[trailNext] = new Vector4(at.x, at.y, at.z, now); trailNext = (trailNext + 1) % TrailSlots;
        }

        // ---- bubble streams: found a few candidates per frame whenever the map or its scenery changes
        void StepStreams()
        {
            int map = AquariumMaps.Current, version = AquariumSceneryLayout.Version;
            if (map != buildMap || version != buildVersion)
            {
                buildMap = map; buildVersion = version; candidate = accepted = onRock = onSand = 0; building = true;
                sv.Clear(); sc.Clear(); s1.Clear(); s2.Clear(); st.Clear(); DebugSpots.Clear();
            }
            if (!building) return;
            int want = AquariumMaps.IsTutorial ? TutorialStreams : Streams;
            for (int n = 0; n < 3 && building; n++)
            {
                if (candidate >= 90 || accepted >= want)
                {
                    streamMesh.Clear();
                    streamMesh.SetVertices(sv); streamMesh.SetUVs(0, sc); streamMesh.SetUVs(1, s1); streamMesh.SetUVs(2, s2); streamMesh.SetTriangles(st, 0, false);
                    streamMesh.bounds = Everywhere; building = false; return;
                }
                TryCandidate(candidate++, map, want);
            }
        }
        void TryCandidate(int i, int map, int want)
        {
            var p = AquariumMaps.Layout(-14 + 28 * H(i, 11 + map * 7), 0, -9 + 18 * H(i, 12 + map * 7));
            if (!AquariumModel.InsideTank(p, 1.6f)) return;
            float bed = AquariumSeabed.Height(p.x, p.z);
            var start = new Point(p.x, top - .4f, p.z); var end = new Point(p.x, bed + .04f, p.z);
            float hit = AquariumModel.SolidHit(start, end, 0), y = end.y; bool rock = false;
            if (hit <= 1) { y = start.y + (end.y - start.y) * hit; rock = y > bed + .25f; if (y > top - 2.5f) return; }
            if (rock ? onRock >= want * 2 / 3 : onSand >= (want + 1) / 2) return;
            if (rock) onRock++; else onSand++;
            accepted++;
            var origin = new Vector3(p.x, y + .04f, p.z); DebugSpots.Add(origin);
            bool stray = H(i, 21) < .25f;
            int count = stray ? 1 + (int)(H(i, 22) * 2) : 7 + (int)(H(i, 22) * 8);
            float burst = stray ? .6f : 2.5f + 2.5f * H(i, 23);
            float period = burst + (top - origin.y) / .5f + 1f + (stray ? 4 + 10 * H(i, 24) : 3 + 11 * H(i, 24)), phase = H(i, 25) * period;
            for (int k = 0; k < count; k++)
            {
                int j = i * 31 + k; float s = H(j, 26), size = .009f + .026f * s * s + (stray ? .008f : 0), speed = .5f + .45f * Mathf.Sqrt(s) + .15f * H(j, 27);
                var at = origin + new Vector3((H(j, 28) - .5f) * .16f, 0, (H(j, 29) - .5f) * .16f);
                var d2 = new Vector4(H(j, 31) * burst, period, phase, top);
                Quad(sv, sc, s1, s2, st, at, new Vector4(0, H(j, 30), size, speed), d2);
                if (size > .02f) Quad(sv, sc, s1, s2, st, at, new Vector4(1, H(j, 30), size, speed), d2);
            }
        }

        public void Dispose()
        {
            if (!ready) return;
            for (int i = 0; i < 4; i++) push[i] = new Vector4(0, -1000, 0, 0);
            Shader.SetGlobalVectorArray(PushId, push);
            Release(holder); Release(moteMesh); Release(streamMesh); Release(trailMesh); Release(moteMaterial); Release(bubbleMaterial);
        }
        static void Release(UnityEngine.Object value)
        { if (value == null) return; if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
    }
}
