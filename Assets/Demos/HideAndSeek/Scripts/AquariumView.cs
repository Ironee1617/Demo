using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    // Procedural artwork stays separate from the host-authoritative game rules.
    public sealed class AquariumView : IDisposable
    {
        private sealed class Actor
        {
            public GameObject root, normalMouth, predatorMouth;
            public Transform tail;
            public Transform[] tentacles;
            public string kind;
            public AquariumCreature creature;
            public CapsuleCollider bodyCollider;
            public Vector3 previousPosition;
            public float moveSpeed;
        }
        private readonly Transform root;
        private readonly Dictionary<string, Actor> actors = new Dictionary<string, Actor>();
        private readonly Dictionary<int, GameObject> effects = new Dictionary<int, GameObject>();
        private readonly List<Material> materials = new List<Material>();


        private readonly List<Renderer> glassPanels = new List<Renderer>();
        private readonly HashSet<string> visible = new HashSet<string>();
        private readonly HashSet<int> activeEffects = new HashSet<int>();
        private readonly List<string> removedActors = new List<string>();
        private readonly List<int> expiredEffects = new List<int>();
        private Actor hiddenByCamera;
        private readonly Color[] colors = { new Color(1f, 0.49f, 0.12f), new Color(0.25f, 0.55f, 0.91f), new Color(0.94f, 0.45f, 0.62f) };
        private readonly Material sand, dark, teal, white, black, purple, steel, glass, mouthLining, eggPearl, eggGold;
        private readonly Material[] fishMaterials, finMaterials;
        private readonly Material lineMaterial;
        private readonly Mesh fanMesh, coneMesh;
        private readonly LineRenderer localRing;
        private readonly System.Random random = new System.Random(54);
        public Camera Camera { get; private set; }

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
            fishMaterials = new Material[3]; finMaterials = new Material[3];
            for (int i = 0; i < 3; i++) { fishMaterials[i] = Material(colors[i]); finMaterials[i] = Material(Color.Lerp(colors[i], Color.white, 0.38f)); }
            fanMesh = MakeFanMesh(); coneMesh = MakeConeMesh();
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
                Ball("Diving suit", t, Vector3.zero, new Vector3(0.72f, 0.64f, 1.16f), dark);
                Ball("Helmet", t, new Vector3(0, 0.1f, 0.49f), Vector3.one * 0.65f, teal);
                Ball("Visor rim", t, new Vector3(0, 0.2f, 0.65f), new Vector3(0.56f, 0.34f, 0.3f), eggGold);
                Ball("Visor", t, new Vector3(0, 0.23f, 0.71f), new Vector3(0.44f, 0.23f, 0.22f), steel);
                Box("Oxygen tank", t, new Vector3(0, 0.36f, -0.12f), new Vector3(0.37f, 0.29f, 0.65f), steel);
                Box("Tank strap", t, new Vector3(0, 0.38f, -0.12f), new Vector3(0.4f, 0.31f, 0.12f), dark);
                for (int side = -1; side <= 1; side += 2)
                    Box("Flipper", t, new Vector3(side * 0.24f, -0.05f, -0.79f), new Vector3(0.26f, 0.13f, 0.55f), eggGold);
                Box("Harpoon grip", t, new Vector3(0.5f, 0, 0.46f), new Vector3(0.14f, 0.16f, 0.8f), dark);
                Box("Harpoon barrel", t, new Vector3(0.5f, 0, 0.93f), new Vector3(0.065f, 0.065f, 0.6f), steel);
            }
            else if (kind == "harpoon")
            {
                Box("Spear shaft", t, Vector3.zero, new Vector3(0.055f, 0.055f, 0.95f), steel);
                var tip = Fin(t, new Vector3(0, 0, 0.65f), new Vector3(0.35f, 1, 0.4f), white); tip.localRotation = Quaternion.Euler(0, 180, 0);
            }
            else if (kind == "egg")
            {
                Ball("Pearl egg", t, new Vector3(0, 0.28f, 0), new Vector3(0.47f, 0.64f, 0.47f), eggPearl);
                Ball("Amber egg core", t, new Vector3(0, 0.2f, 0.18f), new Vector3(0.22f, 0.28f, 0.12f), eggGold);
                Ball("Egg glint", t, new Vector3(-0.1f, 0.43f, 0.18f), new Vector3(0.07f, 0.11f, 0.035f), white);
                var band = Ring("Shell seam", t, 0.23f, new Color(1f, 0.66f, 0.22f, 0.85f), 0.018f); band.transform.localPosition = new Vector3(0, 0.22f, 0);
            }
            if(kind=="alien" || kind=="diver" || kind.StartsWith("fish"))
            {
                actor.bodyCollider=actor.root.AddComponent<CapsuleCollider>();actor.bodyCollider.direction=2;
                var physicsBody=actor.root.AddComponent<Rigidbody>();physicsBody.isKinematic=true;physicsBody.useGravity=false;
            }
            return actor;
        }
        private Actor Place(string key, string kind, Point at, Point facing, float dt, float time, bool dead = false)
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
            actor.kind=kind;
            if(actor.bodyCollider!=null)
            {
                float radius=AquariumBodyCollision.Radius(kind=="diver",kind=="alien");
                actor.bodyCollider.radius=radius;actor.bodyCollider.height=2*(radius+AquariumBodyCollision.Half(kind=="diver",kind=="alien"));
            }
            float blend = fresh ? 1 : 1 - Mathf.Exp(-dt * 18);
            actor.moveSpeed = fresh || dt <= 0 ? 0 : Vector3.Distance(target,actor.previousPosition)/dt;
            actor.previousPosition=target;
            actor.root.transform.position = Vector3.Lerp(actor.root.transform.position, target, blend);
            var direction = new Vector3(facing.x, facing.y, facing.z);
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
            var rot = Quaternion.LookRotation(direction, Mathf.Abs(direction.normalized.y) > 0.99f ? Vector3.forward : Vector3.up);
            if (dead) rot *= Quaternion.Euler(0, 0, 180);
            actor.root.transform.rotation = fresh ? rot : Quaternion.Slerp(actor.root.transform.rotation, rot, blend);
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

        public void Draw(AquariumSnapshot state, int localId, float dt)
        {
            if (state == null) return;
            visible.Clear();
            Swimmer local = null;
            foreach (var p in state.players)
            {
                if (p.id == localId) local = p;
                float age=state.time-p.visualAt;
                bool deathPose=!p.alive && state.time-p.deathAt<2.4f;
                bool showAlien = state.phase == RoundPhase.Finished && p.team == Team.Hider && !deathPose;
                if ((!p.alive && !deathPose || p.swallowedBy != 0) && !showAlien) continue;
                bool morphing=state.time-p.morphAt<1.4f;
                int form=showAlien || p.species<0 || p.revealUntil>state.time && !morphing ? -1:p.species;
                string kind=p.team==Team.Seeker?"diver":form<0?"alien":"fish"+form;
                var actor=Place("p"+p.id,kind,p.position,p.facing,dt,state.time);
                if(actor.creature!=null)
                {
                    float actionLength=p.visualAction=="SpitRecoil"?AquariumModel.SpitAnimationSeconds:p.visualAction=="SpitOut"?AquariumModel.ReleaseKickSeconds:p.visualAction=="Bite"?AquariumModel.FishFeedingSeconds:p.visualAction=="TentacleEat"?AquariumModel.TentacleFeedingSeconds:2.4f;
                    string motion=age<actionLength && !string.IsNullOrEmpty(p.visualAction)?p.visualAction:"Locomotion";
                    if(motion=="Bite" && form<0)motion="TentacleEat";
                    if(deathPose)motion=p.deathReason=="harpoon"?"Harpoon":"Eaten";
                    if(motion=="Bite" && form>=0 && age<AquariumFeeding.ReturnToFish)form+=100;
                    actor.creature.Present(form,motion,p.visualAt,state.time,actor.moveSpeed,deathPose && p.deathReason=="harpoon");
                    actor.root.transform.localScale=Vector3.one;
                    actor.bodyCollider.enabled=true;
                }
            }
            // Resolve swallowed players after every predator mouth has its current
            // pose, independent of player roster order and network interpolation.
            foreach(var p in state.players)
            {
                float age=state.time-p.visualAt;
                if(p.swallowedBy==0 || age>=AquariumModel.SwallowVisualSeconds || state.phase==RoundPhase.Finished)continue;
                var actor=Place("p"+p.id,"alien",p.swallowedFrom,p.facing,dt,state.time);
                actor.root.transform.localScale=Vector3.one;
                actor.creature.Present(-1,"Eaten",p.visualAt,state.time,0);
                if(actors.TryGetValue("p"+p.swallowedBy,out var predator))IngestActor(actor,predator,age);
                actor.bodyCollider.enabled=false;
            }
            foreach (var f in state.fish)
            {
                float age=state.time-f.visualAt;
                if(f.eaten && age>=AquariumModel.SwallowVisualSeconds)continue;
                var actor=Place("f"+f.id,"fish"+f.species,f.position,f.facing,dt,state.time,f.dead && age>2.4f);
                actor.root.transform.localScale=Vector3.one;
                actor.creature.Present(f.species,f.eaten?"Eaten":f.dead?"Harpoon":"Locomotion",f.visualAt,state.time,actor.moveSpeed,f.dead);
                if(f.eaten && actors.TryGetValue("p"+f.eater,out var predator))IngestActor(actor,predator,age);
                actor.bodyCollider.enabled=!f.eaten;
            }
            foreach (var h in state.harpoons) Place("h" + h.id, "harpoon", h.position, h.direction, dt, state.time);
            if (state.eggs != null) foreach (var egg in state.eggs)
            {
                var actor = Place("e" + egg.id, "egg", egg.position, new Point(0, 1), dt, state.time);
                float urgency = Mathf.Clamp01(1 - (egg.hatchAt - state.time) / AquariumModel.HatchSeconds);
                float pulse = Mathf.Sin(state.time * (3 + urgency * 5) + egg.id) * (0.025f + urgency * 0.025f);
                actor.root.transform.localScale = new Vector3(1 - pulse, 1 + pulse, 1 - pulse);
            }
            removedActors.Clear();
            foreach (var pair in actors) if (!visible.Contains(pair.Key)) { DestroyActor(pair.Value); removedActors.Add(pair.Key); }
            foreach (var key in removedActors) actors.Remove(key);
            localRing.gameObject.SetActive(state.phase == RoundPhase.Playing && local != null && local.team == Team.Hider && local.alive && local.swallowedBy == 0);
            if (local != null) localRing.transform.position = new Vector3(local.position.x, 0.25f, local.position.z);
            activeEffects.Clear();
            foreach (var e in state.effects)
            {
                activeEffects.Add(e.id); GameObject fx;
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
                float age = Mathf.Clamp01((state.time - e.born) / Mathf.Max(0.01f, e.duration));
                if(AquariumInkCloud.IsInk(e.kind))
                {
                    float seconds=state.time-e.born-(e.kind=="burp"?0:.12f);
                    Vector3 source=new Vector3(e.position.x,e.position.y,e.position.z);
                    if(e.owner!=0 && actors.TryGetValue("p"+e.owner,out var feeder) && feeder.creature!=null)
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

        public Vector3 PlayerPosition(Swimmer player)
        {
            Actor actor;
            return actors.TryGetValue("p" + player.id, out actor) ? actor.root.transform.position : new Vector3(player.position.x, player.position.y, player.position.z);
        }
        public void CameraOcclusion(int targetId, Vector3? target, bool hideTarget = false)
        {
            if (hiddenByCamera != null && hiddenByCamera.root != null)
                foreach (var renderer in hiddenByCamera.root.GetComponentsInChildren<Renderer>()) renderer.enabled = true;
            hiddenByCamera = null;
            foreach (var panel in glassPanels)
            {
                panel.enabled = true;
                if (!target.HasValue) continue;
                var delta = target.Value - Camera.transform.position; float hit;
                if (panel.bounds.IntersectRay(new Ray(Camera.transform.position, delta.normalized), out hit) && hit < delta.magnitude)
                    panel.enabled = false;
            }
            Actor actor;
            if (target.HasValue && (hideTarget || Vector3.Distance(Camera.transform.position, target.Value) < 0.9f) && actors.TryGetValue("p" + targetId, out actor))
            {
                hiddenByCamera = actor;
                foreach (var renderer in actor.root.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            }
        }
        private void DestroyActor(Actor actor)
        { if(Application.isPlaying)UnityEngine.Object.Destroy(actor.root);else UnityEngine.Object.DestroyImmediate(actor.root); }
        public void Dispose()
        {
            foreach (var actor in actors.Values) DestroyActor(actor);
            foreach (var mat in materials) UnityEngine.Object.Destroy(mat);
            UnityEngine.Object.Destroy(fanMesh); UnityEngine.Object.Destroy(coneMesh);
            UnityEngine.Object.Destroy(root.gameObject);
        }
    }
}
