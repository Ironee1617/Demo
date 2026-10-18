using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    // Procedural artwork stays separate from the host-authoritative game rules.
    public sealed class AquariumView : IDisposable
    {
        // Keep value samples: host snapshots share mutable Swimmer/Prey objects.
        private sealed class PositionHistory
        {
            private struct Sample { public Vector3 position; public float time; }
            private readonly Sample[] samples = new Sample[6];
            private int count;
            public void Clear() { count = 0; }
            public Vector3 Present(Vector3 position, float sampleTime, float renderTime, bool reset, out bool snapped)
            {
                snapped = reset || count == 0 || sampleTime < samples[0].time;
                if (!snapped)
                {
                    float gap = sampleTime - samples[0].time;
                    snapped = Vector3.Distance(position, samples[0].position) > Mathf.Max(3, gap * 10);
                }
                if (snapped)
                {
                    count = 1; samples[0] = new Sample { position = position, time = sampleTime };
                    return position;
                }
                if (sampleTime > samples[0].time)
                {
                    for (int i = Mathf.Min(count, samples.Length - 1); i > 0; i--) samples[i] = samples[i - 1];
                    count = Mathf.Min(count + 1, samples.Length);
                }
                samples[0] = new Sample { position = position, time = sampleTime };
                // Hold at the newest authoritative position instead of predicting through a wall.
                if (renderTime >= samples[0].time) return samples[0].position;
                for (int i = 1; i < count; i++)
                    if (renderTime >= samples[i].time)
                        return Vector3.Lerp(samples[i].position, samples[i - 1].position,
                            Mathf.InverseLerp(samples[i].time, samples[i - 1].time, renderTime));
                return samples[count - 1].position;
            }
        }
        private sealed class Actor
        {
            public GameObject root, normalMouth, predatorMouth;
            public Transform tail;
            public Transform[] tentacles;
            public string kind;
            public AquariumCreature creature;
            public AquariumRig diver;
            public AquariumHarpoonGun gun;
            public LineRenderer tether;
            public CapsuleCollider bodyCollider;
            public float moveSpeed,bank;
            public readonly PositionHistory positions = new PositionHistory();
            public bool dead, carried;
        }
        private sealed class OutlinePart { public Renderer source, shell; }
        private readonly List<OutlinePart> outlineParts = new List<OutlinePart>();
        private Actor outlinedActor;
        private readonly Material outlineMaterial;
        private bool worldFrozen, freezeExistingPoses;
        private int frozenRound;
        private float environmentTime,presentationTime=-1,lastStateTime=-1;
        private int presentationRound;
        private RoundPhase presentationPhase;
        private readonly List<SkinnedMeshRenderer> outlineSkins=new List<SkinnedMeshRenderer>();
        private readonly List<MeshFilter> outlineFilters=new List<MeshFilter>();
        private readonly List<Renderer> cameraRenderers=new List<Renderer>();
        private readonly Transform root;
        private readonly Dictionary<string, Actor> actors = new Dictionary<string, Actor>();
        private readonly Dictionary<int,string> playerKeys=new Dictionary<int,string>();
        private readonly Dictionary<int,string> fishKeys=new Dictionary<int,string>();
        private static readonly string[] fishKinds={"fish0","fish1","fish2"};
        private string PlayerKey(int id)=>ActorKey(playerKeys,"p",id);
        private string FishKey(int id)=>ActorKey(fishKeys,"f",id);
        private static string ActorKey(Dictionary<int,string> cache,string prefix,int id)
        {if(!cache.TryGetValue(id,out var key)){key=prefix+id;cache.Add(id,key);}return key;}
        private static string FishKind(int species)=>species>=0 && species<fishKinds.Length?fishKinds[species]:"fish"+species;
        private readonly Dictionary<int, GameObject> effects = new Dictionary<int, GameObject>();
        private readonly List<Material> materials = new List<Material>();


        private readonly List<Renderer> glassPanels = new List<Renderer>();
        private readonly HashSet<string> visible = new HashSet<string>();
        private readonly HashSet<int> activeEffects = new HashSet<int>();
        private readonly List<string> removedActors = new List<string>();
        private readonly List<int> expiredEffects = new List<int>();
        private readonly HashSet<Actor> hiddenByCamera = new HashSet<Actor>();
        private readonly Dictionary<Renderer, bool> hiddenRendererStates = new Dictionary<Renderer, bool>();
        private readonly Color[] colors = { new Color(1f, 0.49f, 0.12f), new Color(0.25f, 0.55f, 0.91f), new Color(0.94f, 0.45f, 0.62f) };
        private readonly Material sand, dark, teal, white, black, purple, steel, glass, mouthLining, eggPearl, eggGold;
        private readonly Material[] fishMaterials, finMaterials;
        private readonly Material lineMaterial;
        private readonly Mesh fanMesh, coneMesh;
        private readonly LineRenderer localRing;
        private readonly System.Random random = new System.Random(54);
        public Camera Camera { get; private set; }
        private AquariumFirstPersonEquipment equipment;

        public AquariumView(Transform parent)
        {
            root = new GameObject("Aquarium visuals").transform; root.SetParent(parent);
            sand = Material(new Color(0.56f, 0.53f, 0.4f)); sand.SetFloat("_Glossiness", 0.08f);
            dark = Material(new Color(0.025f, 0.09f, 0.16f)); teal = Material(new Color(0.045f, 0.32f, 0.39f));
            white = Material(new Color(0.96f, 0.99f, 1f)); black = Material(new Color(0.012f, 0.015f, 0.045f));
            purple = Material(new Color(0.81f, 0.68f, 0.97f)); steel = Material(new Color(0.66f, 0.83f, 0.9f));
            mouthLining = Material(new Color(0.89f, 0.28f, 0.5f));
            eggPearl = Material(new Color(1f, 0.9f, 0.69f)); eggGold = Material(new Color(1f, 0.63f, 0.18f));
            eggGold.EnableKeyword("_EMISSION"); eggGold.SetColor("_EmissionColor", new Color(0.3f, 0.12f, 0.015f));
            glass = Material(new Color(0.15f, 0.48f, 0.56f, 0.055f), true);
            lineMaterial = new Material(Shader.Find("Sprites/Default")); materials.Add(lineMaterial);
            outlineMaterial = new Material(Resources.Load<Shader>("AquariumTargetOutline"));
            outlineMaterial.SetColor("_OutlineColor", new Color(1f, 0.86f, 0.25f)); outlineMaterial.SetFloat("_OutlineWidth", 0.035f); materials.Add(outlineMaterial);
            fishMaterials = new Material[3]; finMaterials = new Material[3];
            for (int i = 0; i < 3; i++) { fishMaterials[i] = Material(colors[i]); finMaterials[i] = Material(Color.Lerp(colors[i], Color.white, 0.38f)); }
            fanMesh = MakeFanMesh(); coneMesh = MakeConeMesh();
            AquariumCreature.Prewarm();
            BuildEnvironment();
            localRing = Ring("Your swimmer", root, 0.68f, new Color(0.59f, 1f, 0.81f, 0.85f), 0.035f);
            Camera = UnityEngine.Camera.main;
            if (Camera == null) { var go = new GameObject("Aquarium Camera"); go.tag = "MainCamera"; Camera = go.AddComponent<Camera>(); go.AddComponent<AudioListener>(); }
            Camera.depthTextureMode|=DepthTextureMode.Depth;
            Camera.transform.position = new Vector3(0, 23, -20);
            Camera.transform.rotation = Quaternion.Euler(49, 0, 0);
            Camera.orthographic = false; Camera.fieldOfView = 68; Camera.nearClipPlane = 0.06f; Camera.farClipPlane = 100;
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = new Color(0.025f, 0.12f, 0.18f);
            AquariumEnvironment.ConfigureCamera(Camera);
            AquariumEnvironment.ConfigureLighting(root);
            equipment = new AquariumFirstPersonEquipment(Camera);
        }

        private Material Material(Color color, bool transparent = false)
        {
            // This built-in alpha shader is also used by the rings and included in the player build.
            // It keeps procedural glass translucent without depending on a stripped Standard variant.
            var mat = new Material(Shader.Find(transparent ? "Sprites/Default" : "Standard")); mat.color = color;
            if (transparent) mat.renderQueue = 3000;
            else mat.SetFloat("_Glossiness", 0.28f);
            materials.Add(mat); return mat;
        }
        private GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 at, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = at; go.transform.localScale = size;
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = mat;
            if (mat == glass)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                if (type == PrimitiveType.Cube) glassPanels.Add(renderer);
            }
            return go;
        }
        private GameObject Box(string name, Transform parent, Vector3 at, Vector3 size, Material mat)
        { return Shape(name, PrimitiveType.Cube, parent, at, size, mat); }
        private GameObject Ball(string name, Transform parent, Vector3 at, Vector3 size, Material mat)
        { return Shape(name, PrimitiveType.Sphere, parent, at, size, mat); }
        private float Range(float a, float b) { return a + (float)random.NextDouble() * (b - a); }

        private void BuildEnvironment()
        {
            var environment = AquariumEnvironment.Ensure(root);
            foreach (var renderer in environment.GetComponentsInChildren<Renderer>())
                if (renderer.name == "04 • Curved aquarium glass") glassPanels.Add(renderer);
        }

        private Mesh MakeFanMesh()
        {
            var mesh = new Mesh { name = "Shared aquarium fan" };
            mesh.vertices = new[] { new Vector3(0, 0, 0.2f), new Vector3(-0.45f, 0, -0.6f), new Vector3(0.45f, 0, -0.6f), new Vector3(0, 0.13f, -0.36f) };
            mesh.triangles = new[] { 0, 1, 3, 0, 3, 2, 1, 2, 3, 0, 2, 1 }; mesh.RecalculateNormals(); return mesh;
        }
        private Mesh MakeConeMesh()
        {
            const int sides = 8;
            var mesh = new Mesh { name = "Shared aquarium tooth" }; var vertices = new Vector3[sides + 2]; var triangles = new int[sides * 6];
            vertices[0] = Vector3.up; vertices[sides + 1] = Vector3.zero;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2 / sides; vertices[i + 1] = new Vector3(Mathf.Sin(angle) * 0.5f, 0, Mathf.Cos(angle) * 0.5f);
                int next = (i + 1) % sides + 1;
                triangles[i * 6] = 0; triangles[i * 6 + 1] = i + 1; triangles[i * 6 + 2] = next;
                triangles[i * 6 + 3] = sides + 1; triangles[i * 6 + 4] = next; triangles[i * 6 + 5] = i + 1;
            }
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); return mesh;
        }
        private Transform MeshShape(string name, Mesh mesh, Transform parent, Vector3 at, Vector3 size, Material mat)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = at; go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = mat; return go.transform;
        }
        private Transform Fin(Transform parent, Vector3 at, Vector3 size, Material mat)
        { return MeshShape("Fan fin", fanMesh, parent, at, size, mat); }
        private void PredatorMouth(Actor actor, Vector3 at)
        {
            actor.predatorMouth = new GameObject("Feeding mouth"); actor.predatorMouth.transform.SetParent(actor.root.transform, false); actor.predatorMouth.transform.localPosition = at;
            var t = actor.predatorMouth.transform;
            Ball("Mouth lip", t, Vector3.zero, new Vector3(0.75f, 0.73f, 0.25f), mouthLining);
            Ball("Mouth cavity", t, new Vector3(0, 0, 0.08f), new Vector3(0.61f, 0.59f, 0.2f), black);
            Ball("Tongue", t, new Vector3(0, -0.18f, 0.18f), new Vector3(0.27f, 0.12f, 0.13f), mouthLining);
            for (int side = -1; side <= 1; side += 2)
                for (int j = 0; j < 4; j++)
                {
                    float x = (j - 1.5f) * 0.13f;
                    var tooth = MeshShape("Predator tooth", coneMesh, t, new Vector3(x, side * (0.24f - Mathf.Abs(x) * 0.3f), 0.19f), new Vector3(0.095f, 0.14f, 0.09f), white);
                    if (side > 0) tooth.localRotation = Quaternion.Euler(0, 0, 180);
                }
            actor.predatorMouth.SetActive(false);
        }
        private Actor MakeActor(string kind)
        {
            var actor = new Actor { root = new GameObject(kind), kind = kind }; actor.root.transform.SetParent(root);
            var t = actor.root.transform;
            if (kind.StartsWith("fish") || kind == "alien")
            {
                var visual = new GameObject("Rigged creature");visual.transform.SetParent(t,false);
                actor.creature=visual.AddComponent<AquariumCreature>();actor.creature.Initialize(kind=="alien"?-1:int.Parse(kind.Substring(4)),random.Next(10000));
            }
            else if (kind == "diver")
            {
                actor.diver = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("AquariumEquipment/Diver"), t).GetComponent<AquariumRig>();
                actor.gun = AquariumFirstPersonEquipment.CreateGun(t);
            }
            else if (kind == "harpoon")
            {
                UnityEngine.Object.Instantiate(Resources.Load<GameObject>("AquariumEquipment/HarpoonHead"), t);
                actor.tether = new GameObject("Braided retrieval line").AddComponent<LineRenderer>(); actor.tether.transform.SetParent(t, false);
                actor.tether.sharedMaterial = Resources.Load<Material>("AquariumEquipment/Rope");
                actor.tether.positionCount = 25; actor.tether.useWorldSpace = true; actor.tether.widthMultiplier = .014f;
                actor.tether.numCapVertices = 3; actor.tether.numCornerVertices = 2; actor.tether.textureMode = LineTextureMode.Tile;
                actor.tether.generateLightingData = true; actor.tether.shadowCastingMode = ShadowCastingMode.Off;
            }
            else if (kind == "egg")
            {
                Ball("Pearl egg", t, new Vector3(0, 0.32f - AquariumModel.EggRadius, 0), new Vector3(0.47f, 0.64f, 0.47f), eggPearl);
                Ball("Amber egg core", t, new Vector3(0, 0.24f - AquariumModel.EggRadius, 0.18f), new Vector3(0.22f, 0.28f, 0.12f), eggGold);
                Ball("Egg glint", t, new Vector3(-0.1f, 0.47f - AquariumModel.EggRadius, 0.18f), new Vector3(0.07f, 0.11f, 0.035f), white);
                var band = Ring("Shell seam", t, 0.23f, new Color(1f, 0.66f, 0.22f, 0.85f), 0.018f); band.transform.localPosition = new Vector3(0, 0.26f - AquariumModel.EggRadius, 0);
            }
            if(kind=="alien" || kind=="diver" || kind.StartsWith("fish"))
            {
                actor.bodyCollider=actor.root.AddComponent<CapsuleCollider>();actor.bodyCollider.direction=2;
                var physicsBody=actor.root.AddComponent<Rigidbody>();physicsBody.isKinematic=true;physicsBody.useGravity=false;
            }
            return actor;
        }
        private Actor Place(string key, string kind, Point at, Point facing, float dt, float time, bool dead = false,
            float sampleTime = float.NaN, float renderTime = float.NaN)
        {
            visible.Add(key); Actor actor;
            bool creatureKind=kind=="alien" || kind.StartsWith("fish");
            bool fresh = !actors.TryGetValue(key, out actor) || (actor.kind != kind && (actor.creature == null || !creatureKind));
            if (fresh)
            {
                if (actor != null) DestroyActor(actor);
                actor = MakeActor(kind); actors[key] = actor;
            }
            Vector3 target = new Vector3(at.x, at.y, at.z);
            if((fresh || actor.kind!=kind) && key.StartsWith("p") && actor.creature!=null && kind.StartsWith("fish"))actor.creature.PrepareFeedingForms(int.Parse(kind.Substring(4)));
            actor.kind=kind;
            if(actor.bodyCollider!=null)
            {
                float radius=AquariumBodyCollision.Radius(kind=="diver",kind=="alien");
                actor.bodyCollider.radius=radius;actor.bodyCollider.height=2*(radius+AquariumBodyCollision.Half(kind=="diver",kind=="alien"));
            }
            if (actor.dead != dead) actor.positions.Clear();
            actor.dead = dead;
            if (float.IsNaN(sampleTime)) sampleTime = time;
            if (float.IsNaN(renderTime)) renderTime = time;
            Vector3 before = actor.root.transform.position;
            Vector3 displayed = actor.positions.Present(target, sampleTime, renderTime, fresh || dead, out bool snapped);
            if (fresh || !freezeExistingPoses) actor.root.transform.position = displayed;
            float measured = snapped || dt <= 0 ? 0 : Vector3.Distance(actor.root.transform.position, before) / dt;
            actor.moveSpeed = snapped ? 0 : Mathf.Lerp(actor.moveSpeed, measured, 1 - Mathf.Exp(-Mathf.Max(0, dt) * 12));
            var direction = new Vector3(facing.x, facing.y, facing.z);
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
            var rot = Quaternion.LookRotation(direction, Mathf.Abs(direction.normalized.y) > 0.99f ? Vector3.forward : Vector3.up);
            if (dead && actor.creature == null) rot *= Quaternion.Euler(0, 0, 180);
            float turn=Vector3.SignedAngle(actor.root.transform.forward,direction,Vector3.up);
            float wantedBank=actor.moveSpeed>.15f?Mathf.Clamp(-turn*.35f,-10,10):0;
            actor.bank=Mathf.Lerp(actor.bank,wantedBank,1-Mathf.Exp(-Mathf.Max(0,dt)*6));
            rot*=Quaternion.Euler(0,0,actor.bank);
            if (fresh || !freezeExistingPoses) actor.root.transform.rotation = fresh || dead ? rot : Quaternion.Slerp(actor.root.transform.rotation, rot, 1-Mathf.Exp(-dt*8));
            if (actor.tail != null) actor.tail.localRotation = Quaternion.Euler(0, dead ? 0 : Mathf.Sin(time * 10 + at.z) * 22, 90);
            if (actor.tentacles != null) for (int i = 0; i < actor.tentacles.Length; i++)
                actor.tentacles[i].localRotation = Quaternion.Euler(Mathf.Sin(time * 3 + i * 1.8f) * 15, 0, Mathf.Cos(time * 3 + i) * 12);
            return actor;
        }
        private LineRenderer Ring(string name, Transform parent, float radius, Color color, float width)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = lineMaterial;
            line.useWorldSpace = false; line.loop = true; line.positionCount = 48; line.widthMultiplier = width;
            line.startColor = line.endColor = color; line.shadowCastingMode = ShadowCastingMode.Off;
            for (int i = 0; i < 48; i++) { float a = i * Mathf.PI * 2 / 48; line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius)); }
            return line;
        }

        public void Draw(AquariumSnapshot state, int localId, float dt, float renderTime = float.NaN)
        {
            if (state == null) return;
            bool finished = state.phase == RoundPhase.Finished;
            // Share the gallery shader clock so water and reef motion freeze with the final scene.
            if (!finished) environmentTime = Time.time;
            Shader.SetGlobalFloat("_AquariumPreviewTime", environmentTime);
            Shader.SetGlobalFloat("_AquariumPreviewTimeEnabled", 1);
            if (finished && worldFrozen && frozenRound == state.round) { CleanupFrozenWorld(state); return; }
            worldFrozen = finished; frozenRound = state.round; freezeExistingPoses = finished;
            bool resetMotion = lastStateTime < 0 || state.time < lastStateTime || state.round != presentationRound || state.phase != presentationPhase;
            if (resetMotion)
                foreach (var actor in actors.Values) { actor.positions.Clear(); actor.moveSpeed = 0; }
            if(presentationTime<0 || resetMotion)presentationTime=state.time;
            else presentationTime=Mathf.Min(state.time+.05f,Mathf.Max(state.time-.1f,presentationTime+Mathf.Clamp(dt,0,.1f)));
            if(presentationRound!=state.round)fishKeys.Clear();
            lastStateTime=state.time;presentationRound=state.round;presentationPhase=state.phase;
            float visualTime = finished ? state.finishedAt : presentationTime;
            float renderAt = float.IsNaN(renderTime) ? presentationTime - .05f : renderTime;
            if (finished) SetMorphTarget(null);
            visible.Clear();
            Swimmer local = null;
            foreach (var p in state.players)
            {
                if (p.id == localId) local = p;
                if (p.swallowedBy != 0 || (!p.alive && (finished || p.team != Team.Hider))) continue;
                float age=visualTime-p.visualAt;
                bool deathPose=!p.alive;
                bool morphing=visualTime-p.morphAt<1.4f;
                int form=deathPose?p.deathSpecies:p.species<0 || p.revealUntil>visualTime && !morphing ? -1:p.species;
                string kind=p.team==Team.Seeker?"diver":form<0?"alien":FishKind(form);
                bool frozenPose=finished && actors.ContainsKey(PlayerKey(p.id));
                if (actors.TryGetValue(PlayerKey(p.id), out var released) && released.carried)
                { released.positions.Clear(); released.carried = false; }
                var actor=Place(PlayerKey(p.id),kind,p.position,p.facing,dt,visualTime,deathPose,state.time,renderAt);
                if (actor.diver != null && !frozenPose)
                {
                    actor.diver.SampleLocomotion(actor.moveSpeed,visualTime,p.id*.37f);
                    actor.gun.SetLoaded(visualTime >= p.nextAction);
                }
                if(actor.creature!=null && !frozenPose)
                {
                    float actionLength=p.visualAction=="SpitRecoil"?AquariumModel.SpitAnimationSeconds:p.visualAction=="SpitOut"?AquariumModel.ReleaseKickSeconds:p.visualAction=="Bite"?AquariumModel.FishFeedingSeconds:p.visualAction=="TentacleEat"?AquariumModel.TentacleFeedingSeconds:2.4f;
                    string motion=age<actionLength && !string.IsNullOrEmpty(p.visualAction)?p.visualAction:"Locomotion";
                    if(motion=="Bite" && form<0)motion="TentacleEat";
                    if(deathPose)motion=p.deathReason=="harpoon"?"Harpoon":"Eaten";
                    if(motion=="Bite" && form>=0 && age<AquariumFeeding.ReturnToFish)form+=100;
                    float poseTime=deathPose?Mathf.Min(visualTime,p.deathAt+2.4f):visualTime;
                    actor.creature.Present(form,motion,p.visualAt,poseTime,deathPose?0:actor.moveSpeed,deathPose && p.deathReason=="harpoon");
                    actor.root.transform.localScale=Vector3.one;
                    actor.bodyCollider.enabled=!deathPose || visualTime-p.deathAt<2.4f;
                }
            }
            // Resolve swallowed players after every predator mouth has its current
            // pose, independent of player roster order and network interpolation.
            foreach(var p in state.players)
            {
                float age=visualTime-p.visualAt;
                if(p.swallowedBy==0 || age>=AquariumModel.SwallowVisualSeconds || state.phase==RoundPhase.Finished)continue;
                var actor=Place(PlayerKey(p.id),"alien",p.swallowedFrom,p.facing,dt,visualTime,false,state.time,renderAt);
                actor.carried = true;
                actor.root.transform.localScale=Vector3.one;
                actor.creature.Present(-1,"Eaten",p.visualAt,visualTime,0);
                if(actors.TryGetValue(PlayerKey(p.swallowedBy),out var predator))IngestActor(actor,predator,age);
                actor.bodyCollider.enabled=false;
            }
            foreach (var f in state.fish)
            {
                float age=visualTime-f.visualAt;
                if(finished && (f.eaten || f.dead) || f.eaten && age>=AquariumModel.SwallowVisualSeconds)continue;
                bool frozenPose=finished && actors.ContainsKey(FishKey(f.id));
                var actor=Place(FishKey(f.id),FishKind(f.species),f.position,f.facing,dt,visualTime,f.dead && age>2.4f,state.time,renderAt);
                if(frozenPose)continue;
                actor.root.transform.localScale=Vector3.one;
                actor.creature.Present(f.species,f.eaten?"Eaten":f.dead?"Harpoon":"Locomotion",f.visualAt,visualTime,actor.moveSpeed,f.dead);
                if(f.eaten && actors.TryGetValue(PlayerKey(f.eater),out var predator))IngestActor(actor,predator,age);
                actor.bodyCollider.enabled=!f.eaten;
            }
            foreach (var h in state.harpoons) Place("h" + h.id, "harpoon", h.position, h.direction, dt, visualTime,false,state.time,renderAt);
            if (state.eggs != null) foreach (var egg in state.eggs)
            {
                var actor = Place("e" + egg.id, "egg", egg.position, new Point(0, 1), dt, visualTime,false,state.time,renderAt);
                float urgency = Mathf.Clamp01(1 - (egg.hatchAt - visualTime) / AquariumModel.HatchSeconds);
                float pulse = Mathf.Sin(visualTime * (3 + urgency * 5) + egg.id) * (0.025f + urgency * 0.025f);
                // Pulse sideways while keeping the shell's foot planted on the collision surface.
                actor.root.transform.localScale = new Vector3(1 - pulse, 1, 1 - pulse);
            }
            removedActors.Clear();
            foreach (var pair in actors) if (!visible.Contains(pair.Key)) { DestroyActor(pair.Value); removedActors.Add(pair.Key); }
            foreach (var key in removedActors) actors.Remove(key);
            localRing.gameObject.SetActive(state.phase == RoundPhase.Playing && local != null && local.team == Team.Hider && local.alive && local.swallowedBy == 0);
            if (local != null)
            { var displayed = PlayerPosition(local); localRing.transform.position = new Vector3(displayed.x, 0.25f, displayed.z); }
            activeEffects.Clear();
            foreach (var e in state.effects)
            {
                activeEffects.Add(e.id); GameObject fx;
                if(finished && effects.ContainsKey(e.id))continue;
                if (!effects.TryGetValue(e.id, out fx))
                {
                    if (AquariumInkCloud.IsInk(e.kind))
                    {
                        fx=new GameObject(e.kind+" volumetric ink");fx.transform.SetParent(root,false);
                        fx.AddComponent<AquariumInkCloud>().Initialize(e.id,e.kind!="eat");
                    }
                    else
                    {
                        Color color = e.kind == "morph" ? new Color(0.86f, 0.63f, 1) : e.kind == "caught" || e.kind == "miss" ? new Color(1, 0.4f, 0.3f) : e.kind == "hatch" || e.kind == "egg" ? new Color(1, 0.8f, 0.4f) : new Color(0.55f, 1, 0.76f);
                        fx = Ring(e.kind, root, 1, color, 0.055f).gameObject;
                        if (e.kind == "feed") for (int i = 0; i < 10; i++)
                            Ball("Food pellet", fx.transform, new Vector3(Mathf.Sin(i * 2.4f) * 0.5f, Mathf.Cos(i * 1.7f) * 0.25f, Mathf.Cos(i * 2.4f) * 0.5f), Vector3.one * 0.08f, fishMaterials[0]);
                    }
                    effects[e.id] = fx;
                }
                float age = Mathf.Clamp01((visualTime - e.born) / Mathf.Max(0.01f, e.duration));
                if(AquariumInkCloud.IsInk(e.kind))
                {
                    float seconds=visualTime-e.born-(e.kind=="burp"?0:.12f);
                    Vector3 source=new Vector3(e.position.x,e.position.y,e.position.z);
                    if(e.owner!=0 && actors.TryGetValue(PlayerKey(e.owner),out var feeder) && feeder.creature!=null)
                        source=feeder.creature.FeedingMouth+feeder.root.transform.forward*.08f;
                    fx.GetComponent<AquariumInkCloud>().Present(seconds,e.duration-(e.kind=="burp"?0:.12f),source,new Vector3(e.direction.x,e.direction.y,e.direction.z));
                }
                else
                {
                    fx.transform.position=new Vector3(e.position.x,e.position.y+age*.3f,e.position.z);
                    fx.transform.localScale=Vector3.one*(e.kind=="feed"?1:.15f+age*1.3f);
                    var line=fx.GetComponent<LineRenderer>();var c=line.startColor;c.a=1-age;line.startColor=line.endColor=c;
                }
            }
            expiredEffects.Clear();
            foreach (var e in effects) if (!activeEffects.Contains(e.Key)) { UnityEngine.Object.Destroy(e.Value); expiredEffects.Add(e.Key); }
            foreach (int id in expiredEffects) effects.Remove(id);

        }

        private void IngestActor(Actor prey,Actor predator,float age)
        {
            if(predator.creature==null)return;
            prey.creature.PresentIngestion(predator.creature,age);
        }

        public void PresentEquipment(AquariumSnapshot state, int followId, bool firstPerson)
        {
            Actor followed; actors.TryGetValue(PlayerKey(followId), out followed);
            equipment.Present(state, followId, firstPerson, followed == null ? 0 : followed.moveSpeed);
            if (state == null || state.phase == RoundPhase.Finished) return;
            foreach (var shot in state.harpoons)
            {
                Actor projectile, owner;
                if (!actors.TryGetValue("h" + shot.id, out projectile) || !actors.TryGetValue(PlayerKey(shot.owner), out owner) || owner.gun == null) continue;
                var gun = equipment.Visible && equipment.Owner == shot.owner ? equipment.gun : owner.gun;
                var tip = new Vector3(shot.position.x, shot.position.y, shot.position.z);
                var direction = new Vector3(shot.direction.x, shot.direction.y, shot.direction.z).normalized;
                var dockedTip = gun.muzzle.position + gun.muzzle.forward * gun.headLength * gun.transform.lossyScale.z;
                if (!shot.returning) tip = Vector3.Lerp(dockedTip, tip, Mathf.Clamp01((state.time - shot.born) / .07f));
                else
                {
                    float dock = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(shot.retrievedAt - .3f, shot.retrievedAt, state.time));
                    tip = Vector3.Lerp(tip, dockedTip, dock); direction = Vector3.Slerp(direction, gun.muzzle.forward, dock);
                }
                projectile.root.transform.SetPositionAndRotation(tip - direction * gun.headLength, Quaternion.LookRotation(direction));
                var from = gun.ropeExit.position; var to = projectile.root.transform.position;
                float distance = Vector3.Distance(from, to);
                float sag = Mathf.Min(.45f, distance * .035f) * (shot.returning ? 1 : .35f);
                var side = Vector3.Cross((to - from).normalized, Vector3.up);
                for (int i = 0; i < projectile.tether.positionCount; i++)
                {
                    float t = i / (float)(projectile.tether.positionCount - 1); float arc = Mathf.Sin(t * Mathf.PI);
                    var ripple = side * (Mathf.Sin(t * 9 - state.time * 2.5f) * Mathf.Min(.045f, distance * .009f));
                    projectile.tether.SetPosition(i, Vector3.Lerp(from, to, t) + (Vector3.down * sag + ripple) * arc);
                }
            }
        }

        // Render both passes for editor reviews and hidden-window smoke captures.
        public void RenderTo(RenderTexture target)
        {
            var previous = Camera.targetTexture;
            try
            {
                Camera.targetTexture = target; Camera.Render();
                foreach (var overlay in Camera.GetComponentsInChildren<Camera>()) if (overlay != Camera && overlay.enabled)
                {
                    var old = overlay.targetTexture;
                    try { overlay.targetTexture = target; overlay.Render(); }
                    finally { overlay.targetTexture = old; }
                }
            }
            finally { Camera.targetTexture = previous; }
        }

        public MorphTarget FindMorphTarget(AquariumSnapshot state, Swimmer caster, Point aim)
        {
            if (state == null || caster == null || !caster.alive || caster.team != Team.Hider || caster.swallowedBy != 0 || worldFrozen) return null;
            MorphTarget best = null; float screenDistance = float.MaxValue, worldDistance = float.MaxValue;
            foreach (var fish in state.fish) if (!fish.dead && !fish.eaten)
                ConsiderVisibleMorph(caster, aim, fish.id, fish.species, false, fish.position, ref best, ref screenDistance, ref worldDistance);
            foreach (var player in state.players)
                if (AquariumModel.CopyableAlly(caster, player, state.time))
                    ConsiderVisibleMorph(caster, aim, player.id, player.species, true, player.position, ref best, ref screenDistance, ref worldDistance);
            return best;
        }
        private void ConsiderVisibleMorph(Swimmer caster, Point aim, int id, int species, bool player, Point position,
            ref MorphTarget best, ref float bestScreenDistance, ref float bestWorldDistance)
        {
            if (!AquariumModel.CanMorphTarget(caster, aim, position)) return;
            Actor actor;
            if (!actors.TryGetValue((player?PlayerKey(id):FishKey(id)), out actor) || !actor.kind.StartsWith("fish")) return;
            var rendered = actor.root.transform.position;
            var screen = Camera.WorldToViewportPoint(rendered);
            if (screen.z <= 0 || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1) return;
            var cameraPosition = Camera.transform.position;
            if (AquariumModel.ObstacleHit(new Point(cameraPosition.x, cameraPosition.y, cameraPosition.z), new Point(rendered.x, rendered.y, rendered.z)) <= 1) return;
            float x = (screen.x - 0.5f) * Camera.aspect, y = screen.y - 0.5f;
            float screenDistance = x * x + y * y, worldDistance = (position - caster.position).Length;
            if (best != null && (screenDistance > bestScreenDistance + 0.000001f || (Mathf.Abs(screenDistance - bestScreenDistance) <= 0.000001f && worldDistance >= bestWorldDistance))) return;
            best = new MorphTarget { id = id, species = species, player = player, position = position };
            bestScreenDistance = screenDistance; bestWorldDistance = worldDistance;
        }
        // The same silhouette treatment is used for ordinary fish and disguised players.
        public void SetMorphTarget(MorphTarget target)
        {
            Actor actor = null;
            if (!worldFrozen && target != null) actors.TryGetValue((target.player?PlayerKey(target.id):FishKey(target.id)), out actor);
            if (actor != null && !actor.kind.StartsWith("fish")) actor = null;
            if (actor == outlinedActor) { EnsureOutlineParts(); UpdateOutlineVisibility(); return; }
            ClearOutline();
            outlinedActor = actor;
            EnsureOutlineParts();
            UpdateOutlineVisibility();
        }
        private bool HasOutline(Renderer source)
        { foreach(var part in outlineParts)if(part.source==source)return true;return false; }
        private void EnsureOutlineParts()
        {
            if(outlinedActor==null)return;
            // Forms and their bridge are created lazily; discover each skin once.
            outlinedActor.root.GetComponentsInChildren(true,outlineSkins);
            foreach(var skin in outlineSkins)
            {
                if(skin.sharedMesh==null || skin.name=="Candidate silhouette" || HasOutline(skin))continue;
                var shell=new GameObject("Candidate silhouette");shell.transform.SetParent(skin.transform,false);
                var renderer=shell.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=skin.sharedMesh;
                renderer.bones=skin.bones;renderer.rootBone=skin.rootBone;renderer.localBounds=skin.localBounds;
                renderer.sharedMaterial=outlineMaterial;renderer.updateWhenOffscreen=true;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                outlineParts.Add(new OutlinePart{source=skin,shell=renderer});
            }
            outlinedActor.root.GetComponentsInChildren(true,outlineFilters);
            foreach (var filter in outlineFilters)
            {
                if (filter.sharedMesh == null || filter.gameObject.name == "Candidate silhouette") continue;
                var sourceRenderer = filter.GetComponent<Renderer>();
                if (sourceRenderer == null || HasOutline(sourceRenderer)) continue;
                var shell = new GameObject("Candidate silhouette"); shell.transform.SetParent(filter.transform, false);
                shell.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = shell.AddComponent<MeshRenderer>(); renderer.sharedMaterial = outlineMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                outlineParts.Add(new OutlinePart { source = sourceRenderer, shell = renderer });
            }
        }
        private void UpdateOutlineVisibility()
        {
            foreach (var part in outlineParts) if (part.shell != null)
            {
                part.shell.enabled = part.source != null && part.source.enabled && part.source.gameObject.activeInHierarchy && !hiddenByCamera.Contains(outlinedActor);
                var source=part.source as SkinnedMeshRenderer;var shell=part.shell as SkinnedMeshRenderer;
                if(source!=null && shell!=null && part.shell.enabled)
                {
                    shell.sharedMesh=source.sharedMesh;shell.localBounds=source.localBounds;
                    for(int i=0;i<source.sharedMesh.blendShapeCount;i++)shell.SetBlendShapeWeight(i,source.GetBlendShapeWeight(i));
                }
            }
        }
        private void ClearOutline()
        {
            foreach (var part in outlineParts) if (part.shell != null)
            { part.shell.gameObject.SetActive(false); UnityEngine.Object.Destroy(part.shell.gameObject); }
            outlineParts.Clear(); outlinedActor = null;
        }
        private void CleanupFrozenWorld(AquariumSnapshot state)
        {
            // Keep the captured poses and effects untouched while removing objects absent from the snapshot.
            visible.Clear();
            foreach (var fish in state.fish) if (!fish.eaten && !fish.dead) visible.Add(FishKey(fish.id));
            foreach (var player in state.players) if (player.alive && player.swallowedBy == 0) visible.Add(PlayerKey(player.id));
            foreach (var harpoon in state.harpoons) visible.Add("h" + harpoon.id);
            if (state.eggs != null) foreach (var egg in state.eggs) visible.Add("e" + egg.id);
            removedActors.Clear();
            foreach (var pair in actors) if (!visible.Contains(pair.Key)) { DestroyActor(pair.Value); removedActors.Add(pair.Key); }
            foreach (var key in removedActors) actors.Remove(key);
            activeEffects.Clear();
            foreach (var effect in state.effects) activeEffects.Add(effect.id);
            expiredEffects.Clear();
            foreach (var effect in effects) if (!activeEffects.Contains(effect.Key)) { UnityEngine.Object.Destroy(effect.Value); expiredEffects.Add(effect.Key); }
            foreach (int id in expiredEffects) effects.Remove(id);
            localRing.gameObject.SetActive(false);
        }
        public Vector3 PlayerPosition(Swimmer player)
        {
            Actor actor;
            return actors.TryGetValue(PlayerKey(player.id), out actor) ? actor.root.transform.position : new Vector3(player.position.x, player.position.y, player.position.z);
        }
        public void CameraOcclusion(int targetId, Vector3? target, bool hideTarget = false)
        {
            foreach (var pair in hiddenRendererStates) if (pair.Key != null) pair.Key.enabled = pair.Value;
            hiddenRendererStates.Clear(); hiddenByCamera.Clear();
            foreach (var panel in glassPanels)
            {
                panel.enabled = true;
                if (!target.HasValue) continue;
                var delta = target.Value - Camera.transform.position; float hit;
                if (panel.bounds.IntersectRay(new Ray(Camera.transform.position, delta.normalized), out hit) && hit < delta.magnitude)
                    panel.enabled = false;
            }
            if (target.HasValue)
            {
                Actor actor;
                if ((hideTarget || Vector3.Distance(Camera.transform.position, target.Value) < 0.9f) && actors.TryGetValue(PlayerKey(targetId), out actor)) HideNearCamera(actor);
                // Fish that brush past the lens must not cover a death review or the free-view result scene.
                foreach (var pair in actors)
                    if ((pair.Key.StartsWith("p") || pair.Key.StartsWith("f")) && (Camera.transform.position - pair.Value.root.transform.position).sqrMagnitude < 1.44f)
                        HideNearCamera(pair.Value);
            }
            UpdateOutlineVisibility();
        }
        private void HideNearCamera(Actor actor)
        {
            if (!hiddenByCamera.Add(actor)) return;
            // Only active renderers are touched: inactive feeding-mouth children keep their original state.
            actor.root.GetComponentsInChildren(false,cameraRenderers);
            foreach (var renderer in cameraRenderers)
            { hiddenRendererStates[renderer] = renderer.enabled; renderer.enabled = false; }
        }
        private void DestroyActor(Actor actor)
        { if (actor == outlinedActor) ClearOutline(); if(Application.isPlaying)UnityEngine.Object.Destroy(actor.root);else UnityEngine.Object.DestroyImmediate(actor.root); }
        public void Dispose()
        {
            equipment?.Dispose();
            Shader.SetGlobalFloat("_AquariumPreviewTimeEnabled", 0);
            ClearOutline();
            foreach (var actor in actors.Values) DestroyActor(actor);
            foreach (var mat in materials) UnityEngine.Object.Destroy(mat);
            UnityEngine.Object.Destroy(fanMesh); UnityEngine.Object.Destroy(coneMesh);
            UnityEngine.Object.Destroy(root.gameObject);
        }
    }
}
