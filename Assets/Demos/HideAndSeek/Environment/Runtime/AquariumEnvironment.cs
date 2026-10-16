using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    /// <summary>Deterministic mesh authoring. The editor bakes this into the CoralGallery prefab.</summary>
    public static partial class AquariumEnvironment
    {
        public const string ResourcePath = "Aquarium/CoralGallery";
        private const float DeckY = AquariumModel.WaterHeight+.24f;
        private static readonly Color Navy = C(0x132c40), Brass = C(0xb49660), Stone = C(0xe7d5ae);
        private static readonly Color[] Corals = { C(0xf6aa4f), C(0xf07479), C(0xa994de), C(0x51bfb1), C(0xdbc787), C(0xea9271) };

        public static AquariumEnvironmentRoot Ensure(Transform parent)
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<AquariumEnvironmentRoot>();
            if (existing != null) return existing;
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab != null) return UnityEngine.Object.Instantiate(prefab, parent).GetComponent<AquariumEnvironmentRoot>();
            return Create(parent);
        }

        public static void ConfigureCamera(Camera camera)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = C(0x15232c);
            camera.allowHDR = true; camera.allowMSAA = true;
            camera.nearClipPlane = .06f; camera.farClipPlane = Mathf.Max(160,90*AquariumMapOutline.HorizontalScale);
            if (camera.GetComponent<AquariumGalleryCamera>() == null) camera.gameObject.AddComponent<AquariumGalleryCamera>();
        }

        public static void ConfigureOverview(Camera camera)
        {
            float scale=AquariumMapOutline.HorizontalScale;
            camera.orthographic=false;camera.fieldOfView=43;
            camera.transform.position=new Vector3(0,8.3f+36.7f*scale,-20*scale);
            camera.transform.LookAt(new Vector3(0,5.5f,0));
        }

        public static Vector3 LayoutPosition(float x,float y,float z)
        {
            var p=AquariumMapOutline.LayoutPoint(x,y,z);return new Vector3(p.x,p.y,p.z);
        }

        public static void ConfigureLighting(Transform parent)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = C(0x788b96);
            RenderSettings.ambientEquatorColor = C(0x425b68);
            RenderSettings.ambientGroundColor = C(0x172e39);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = .003f/AquariumMapOutline.HorizontalScale; RenderSettings.fogColor = C(0x177d8d);
            RenderSettings.reflectionIntensity = .65f;
            Light sun = null;
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional && light.name != "Gallery cool fill") { sun = light; break; }
            if (sun == null) { var go = new GameObject("Gallery skylight"); go.transform.SetParent(parent); sun = go.AddComponent<Light>(); }
            sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(57, -34, 0);
            sun.color = C(0xffe6bd); sun.intensity = .9f; sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .68f; sun.shadowBias = .035f; sun.shadowNormalBias = .16f;
            RenderSettings.sun = sun;
            var fill = GameObject.Find("Gallery cool fill");
            if (fill == null) { fill = new GameObject("Gallery cool fill"); fill.transform.SetParent(parent); fill.AddComponent<Light>(); }
            fill.transform.rotation = Quaternion.Euler(32, 135, 0);
            var f = fill.GetComponent<Light>(); f.type = LightType.Directional; f.color = C(0x83c8d5); f.intensity = .2f; f.shadows = LightShadows.None;
            QualitySettings.shadows = ShadowQuality.All; QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowDistance = Mathf.Max(75,65*AquariumMapOutline.HorizontalScale); QualitySettings.antiAliasing = 4; QualitySettings.pixelLightCount = 4;
        }

        public static AquariumEnvironmentRoot Create(Transform parent = null)
        {
            var root = new GameObject("CORAL GALLERY | Environment"); root.transform.SetParent(parent, false);
            var marker = root.AddComponent<AquariumEnvironmentRoot>();
            var r = new System.Random(261016);
            var caustics = MakeCaustics();
            var sand = ScannedSurface("Lagoon sand / fine carbonate grains", "SandGrain", caustics, .48f, true);
            var rock = ScannedSurface("Reef stone / 4K mineral detail", "Rock", caustics, .55f, false);
            var coral = LivingMaterial("Living coral / six growth forms",caustics,0,0,.65f);
            var kelp = LivingMaterial("Mixed seaweed / anchored continuous current",caustics,1,0,.05f);
            var anemones = LivingMaterial("Anemones / gently moving soft tentacles",caustics,.42f,1,.1f);
            var architecture = Lit("Gallery • limestone, bronze & oak", caustics, .38f, 0);
            var interior = new Material(Shader.Find("Aquarium/Dim Visitor Gallery")){name="Visitor gallery / charcoal stone"};interior.SetFloat("_Exposure",.20f);
            var interiorFixtures = new Material(interior){name="Visitor gallery / dark wood and bronze"};interiorFixtures.SetFloat("_Exposure",.32f);
            var interiorFloor = new Material(interior){name="Visitor gallery / honed dark limestone"};interiorFloor.SetFloat("_Paving",1);interiorFloor.SetFloat("_Exposure",.28f);
            var interiorGlow = Lit("Visitor gallery / low warm guidance", caustics, .3f, 0); interiorGlow.SetFloat("_Emission", .35f);
            var monolith = ScannedSurface("Monumental reef / 4K scanned fractured rock", "Rock", caustics, .37f, false);
            var paving = Lit("Gallery • continuous limestone paving", caustics, .33f, 0); paving.SetFloat("_Paving", 1);
            var garden = Lit("Botanical planters", caustics, .3f, 0); garden.SetFloat("_Sway", .22f);
            var trim = Lit("Recessed light strips", caustics, .6f, 0); trim.SetFloat("_Emission", 2.5f);
            var glass = Fx("Low iron glass", C(0x6ad6db), .24f, 0);
            var walkGlass = Fx("Observation walk • full-depth structural glass", C(0x66cdd6), .42f, 0);
            var bubbles = Fx("Aeration • pearl bubbles", C(0xb9f5ff), .62f, 1);
            var rays = Fx("Sunlight shafts • moving refraction", C(0xb9eaff), .15f, 2);
            var water = new Material(Shader.Find("Aquarium/Lagoon Water")) { name = "Clear turquoise water" };
            water.SetColor("_Color", new Color(.025f, .58f, .66f, .075f));

            BuildBasin(root.transform, sand, architecture, trim, glass, water, r);
            BuildJoinedArchitecture(root.transform, architecture, paving, trim, glass, walkGlass, water, interiorFloor, sand, marker);
            BuildVisitorInterior(root.transform, interior, interiorFixtures, interiorGlow, marker);
            BuildMonumentalReefs(root.transform, monolith, coral, kelp, r, marker);
            BuildFurniture(root.transform, architecture, garden, trim, r);
            BuildReefs(root.transform, rock, coral, kelp, r, marker);
            BuildAnemoneGardens(root.transform, rock, kelp, anemones, marker);
            BuildAtmosphere(root.transform, bubbles, rays, r, marker);
            return marker;
        }

        public static Color C(uint rgb) { return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1); }
        private static float Range(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }
        private static Vector3 At(float a, float offset, float y)
        {
            var inner = AquariumMapOutline.PointOn(a / (Mathf.PI * 2));
            var outer = AquariumMapOutline.PointOn(a / (Mathf.PI * 2), true);
            var w = new Vector3(inner.x, y, inner.z); var o = new Vector3(outer.x, y, outer.z);
            return offset >= 0 ? Vector3.LerpUnclamped(w, o, offset / 2.43f) : w + (o-w).normalized * offset;
        }
        private static Material Lit(string name, Texture caustic, float smooth, float underwater)
        {
            var m = new Material(Shader.Find("Aquarium/Reef Lit")) { name = name };
            m.SetTexture("_Caustics", caustic); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Underwater", underwater); return m;
        }
        private static Material LivingMaterial(string name,Texture caustic,float sway,float soft,float detail)
        {
            var m=new Material(Shader.Find("Aquarium/Living Reef")){name=name};
            m.SetTexture("_Caustics",caustic);m.SetFloat("_Sway",sway);m.SetFloat("_Soft",soft);
            m.SetFloat("_PolypDetail",detail);m.SetFloat("_Smoothness",soft>.5f?.30f:.19f);return m;
        }
        private static Material Fx(string name, Color tint, float alpha, int mode)
        {
            var m = new Material(Shader.Find("Aquarium/Atmosphere")) { name = name }; tint.a = alpha;
            m.SetColor("_Color", tint); m.SetFloat("_Mode", mode); return m;
        }

        private static Texture2D MakeCaustics()
        {
            const int size = 1024, cells = 9;
            var points = new Vector2[cells, cells]; var random = new System.Random(62);
            for (int y = 0; y < cells; y++) for (int x = 0; x < cells; x++) points[x, y] = new Vector2(Range(random, .15f, .85f), Range(random, .15f, .85f));
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                var p = new Vector2((float)x / size * cells, (float)y / size * cells);
                p += new Vector2(Mathf.Sin(p.y * Mathf.PI * 2 / cells * 3), Mathf.Sin(p.x * Mathf.PI * 2 / cells * 2)) * .23f;
                int cx = Mathf.FloorToInt(p.x), cy = Mathf.FloorToInt(p.y); float nearest = 99, second = 99;
                for (int iy = -1; iy <= 1; iy++) for (int ix = -1; ix <= 1; ix++)
                {
                    int gx = cx + ix, gy = cy + iy;
                    Vector2 q = new Vector2(gx, gy) + points[(gx % cells + cells) % cells, (gy % cells + cells) % cells];
                    float d = (p - q).sqrMagnitude;
                    if (d < nearest) { second = nearest; nearest = d; } else if (d < second) second = d;
                }
                float value = Mathf.Pow(Mathf.Clamp01(1 - (Mathf.Sqrt(second) - Mathf.Sqrt(nearest)) / .12f), 2);
                pixels[y * size + x] = new Color(value, value, value, 1);
            }
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { name = "Seamless refracted light network", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            t.SetPixels(pixels); t.Apply(); return t;
        }

        private static void BuildBasin(Transform root, Material sand, Material architecture, Material trim, Material glass, Material water, System.Random r)
        {
            var rim = new Geo(); var lights = new Geo();
            const int count = 496;
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2 / count, b = (i + 1) * Mathf.PI * 2 / count;
                // Deep navy footing, brass coping and limestone gallery deck.
                Band(rim, a, b, 2.16f, 2.43f, DeckY, Elevated(8.0f), Navy);
                Band(rim, a, b, 2.13f, 2.46f, Elevated(7.99f), Elevated(8.06f), C(0x738b9c));
                Band(lights, a, b, 2.12f, 2.14f, Elevated(7.39f), Elevated(7.45f), C(0xffc77f));
            }
            rim.Emit("02 • Curved limestone gallery & bronze detailing", root, architecture);
            lights.Emit("03 • Continuous warm and turquoise light ribbons", root, trim, false);
        }

        private static Vector3 ToVector(Point p, float height) { return new Vector3(p.x,height,p.z); }
        private static void SandTriangle(Geo g, Vector3 a, Vector3 b, Vector3 c, int depth)
        {
            if (depth > 0)
            {
                var ab=(a+b)*.5f;var bc=(b+c)*.5f;var ca=(c+a)*.5f;
                SandTriangle(g,a,ab,ca,depth-1);SandTriangle(g,ab,b,bc,depth-1);SandTriangle(g,ca,bc,c,depth-1);SandTriangle(g,ab,bc,ca,depth-1);return;
            }
            int n=g.Count;
            foreach(var p0 in new[]{a,b,c})
            {
                var p=p0;p.y+=Mathf.Sin(p.x*.8f+p.z)*.018f+Mathf.PerlinNoise(p.x*.28f+31,p.z*.28f)*.02f;
                var layout=p/AquariumMapOutline.HorizontalScale;
                float bright=Mathf.Clamp01((layout.x-6)*.16f+.4f)*.7f+Mathf.PerlinNoise(layout.x*.19f+1,layout.z*.2f+13)*.25f;
                g.V(p,Color.Lerp(C(0x138cae),C(0xb9ded1),bright));
            }
            g.T(n,n+1,n+2);
        }

        private static void Band(Geo g, float a, float b, float inner, float outer, float low, float high, Color c)
        {
            var ai = At(a, inner, high); var bi = At(b, inner, high); var ao = At(a, outer, high); var bo = At(b, outer, high);
            g.Quad(ai, ao, bo, bi, c);
            g.Quad(At(a,inner,low),At(b,inner,low),At(b,outer,low),At(a,outer,low),c*.74f);
            g.Quad(ai, At(a, inner, low), At(b, inner, low), bi, c * .84f);
            g.Quad(ao, bo, At(b, outer, low), At(a, outer, low), c * .8f);
        }

        private static void BuildFurniture(Transform root, Material architecture, Material garden, Material glow, System.Random r)
        {
            var fixtures = new Geo(); var plants = new Geo(); var warm = new Geo();
            for (int i = 0; i < 22; i++)
            {
                float a = (i + .4f) * Mathf.PI * 2 / 22;
                Vector3 p = At(a, 1.25f, DeckY + .02f);
                if(AquariumEntryLayout.KeepPromenadeClear(new Point(p.x,p.z),.7f))continue;
                fixtures.Cylinder(p, p + Vector3.up * .32f, .49f, .59f, Navy, 18);
                fixtures.Cylinder(p + Vector3.up * .32f, p + Vector3.up * .35f, .6f, .6f, Brass, 18);
                fixtures.Ellipsoid(p + Vector3.up * .35f, new Vector3(.52f, .07f, .52f), C(0x344e32), 12, 5, 0);
                for (int leaf = 0; leaf < 16; leaf++)
                {
                    float direction = leaf * 2.4f + i;
                    Vector3 start = p + Vector3.up * .36f;
                    float length = Range(r, .65f, 1.15f);
                    Vector3 end = start + new Vector3(Mathf.Cos(direction) * length * .7f, Range(r, .4f, 1.1f), Mathf.Sin(direction) * length * .7f);
                    Leaf(plants, start, end, Range(r, .12f, .25f), Color.Lerp(C(0x346e3f), C(0xa4ba46), Range(r, 0, 1)), direction, .65f);
                }
            }
            for (int i = 0; i < 22; i++)
            {
                float a = (i + .9f) * Mathf.PI * 2 / 22; Vector3 p = At(a, 1.15f, DeckY + .02f);
                if(AquariumEntryLayout.KeepPromenadeClear(new Point(p.x,p.z),.9f))continue;
                var tangent = (At(a + .01f, 1.15f, 0) - At(a - .01f, 1.15f, 0)).normalized;
                var rot = Quaternion.LookRotation(new Vector3(-tangent.z, 0, tangent.x));
                for (int slat = 0; slat < 4; slat++) fixtures.Box(p + Vector3.up * .37f + rot * new Vector3(0, 0, (slat - 1.5f) * .12f), new Vector3(1.3f, .07f, .09f), C(0xac784b), rot);
                for (int leg = -1; leg <= 1; leg += 2)
                {
                    fixtures.Box(p + rot * new Vector3(leg * .48f, .18f, 0), new Vector3(.065f, .36f, .45f), Navy, rot);
                    fixtures.Box(p + rot * new Vector3(leg * .55f, .58f, .25f), new Vector3(.05f, .54f, .05f), Navy, rot);
                }
                fixtures.Box(p + rot * new Vector3(0, .72f, .25f), new Vector3(1.3f, .13f, .06f), C(0xba8857), rot);
                // Recessed, warm bollards frame the deck without spawning expensive per-tile lights.
                var lamp = At(a + .07f, .31f, DeckY);
                fixtures.Box(lamp + Vector3.up * .28f, new Vector3(.12f, .56f, .12f), Navy, Quaternion.identity);
                warm.Box(lamp + Vector3.up * .55f, new Vector3(.15f, .06f, .15f), C(0xffdc97), Quaternion.identity);
            }
            fixtures.Box(LayoutPosition(-1.3f,Elevated(6.57f),-11), new Vector3(3.8f, .72f, .12f), Navy, Quaternion.identity);
            fixtures.Emit("09 • Oak benches, bronze pots & bollards", root, architecture);
            plants.Emit("10 • Gallery botanical planting", root, garden);
            warm.Emit("11 • Warm gallery lanterns", root, glow, false);
            Label(root, "CORAL  /  GALLERY", LayoutPosition(-1.3f,Elevated(6.68f),-11)+Vector3.back*.07f, Quaternion.identity, .039f, C(0xe6d2a2));
            Label(root, "A Q U A R I U M", LayoutPosition(-1.3f,Elevated(6.39f),-11)+Vector3.back*.08f, Quaternion.identity, .023f, C(0x7fc5cd));
        }

        private static void Label(Transform parent, string text, Vector3 at, Quaternion rotation, float size, Color color)
        {
            var go = new GameObject(text); go.transform.SetParent(parent, false); go.transform.localPosition = at; go.transform.localRotation = rotation;
            var label = go.AddComponent<TextMesh>(); label.text = text; label.characterSize = size; label.fontSize = 90; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.color = color;
            var renderer=go.GetComponent<MeshRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;
            var fontMaterial=renderer.sharedMaterial;
            if(fontMaterial!=null)
            {
                var material=new Material(Shader.Find("Aquarium/World Label")){name="Gallery signage / depth-tested lettering"};
                material.mainTexture=fontMaterial.mainTexture;renderer.sharedMaterial=material;
            }
        }

        private static void BuildReefs(Transform root, Material rockMat, Material coralMat, Material kelpMat, System.Random r, AquariumEnvironmentRoot marker)
        {
            for (int zone = 0; zone < AquariumModel.Rocks.Length; zone++)
            {
                var stones = new Geo(); var coral = new Geo(); var kelp = new Geo();
                var centre = AquariumModel.Rocks[zone]; Vector3 p = new Vector3(centre.x, centre.y, centre.z);
                float radius = AquariumModel.RockRadii[zone];
                // Main rock uses the same ellipsoid as authority/camera collision; only shallow surface relief varies.
                stones.Ellipsoid(p, new Vector3(radius, AquariumModel.RockHalfHeight, radius), C(0x647a7d), 15, 10, .28f);
                for (int seam = 0; seam < 9; seam++)
                {
                    float a = seam * 2.399f + zone;
                    float vertical = .12f + (seam % 3) * .43f;
                    float spread = Mathf.Sqrt(1 - vertical * vertical * .64f) * radius;
                    Vector3 face = p + new Vector3(Mathf.Cos(a) * spread * .77f, vertical * .9f - .25f, Mathf.Sin(a) * spread * .77f);
                    stones.Ellipsoid(face, new Vector3(radius * .31f, .4f, radius * .3f), Color.Lerp(C(0x4d626a), C(0x8d9d99), (seam % 3) * .36f), 9, 7, .23f);
                }
                for (int n = 0; n < 20; n++)
                {
                    float a = n * 2.39996f + zone;
                    float rr = radius * Range(r, .55f, 1.25f);
                    Vector3 at = p + new Vector3(Mathf.Cos(a) * rr, 0, Mathf.Sin(a) * rr);
                    at.y = AquariumSeabed.Height(at.x,at.z)+.03f;
                    float size = Range(r, .14f, .34f);
                    stones.Ellipsoid(at, new Vector3(size, size * .55f, size * .85f), Color.Lerp(C(0x416d72), C(0x99b7a7), Range(r, 0, 1)), 9, 6, .15f);
                }
                // Coral colonies occupy the actual cover surface and its low rubble skirt.
                for (int n = 0; n < 19; n++)
                {
                    float a = n * 2.4f + zone * .8f;
                    float rr = radius * Range(r, .25f, .95f);
                    float h = Mathf.Sqrt(Mathf.Max(0, 1 - rr * rr / (radius * radius))) * AquariumModel.RockHalfHeight;
                    Vector3 at = p + new Vector3(Mathf.Cos(a) * rr, h - .03f, Mathf.Sin(a) * rr);
                    if(AquariumMonolithLayout.Distance(new Point(at.x,at.y,at.z))<.25f)continue;
                    Color c = Corals[(zone + n) % Corals.Length]; float scale = Range(r, .3f, .57f);
                    CoralColony(coral,at,scale,zone*19+n,r,marker);
                    marker.coralColonies++;
                }
                for (int n = 0; n < 11; n++)
                {
                    float a = Range(r, 0, Mathf.PI * 2); Vector3 at = p + new Vector3(Mathf.Cos(a) * radius * 1.15f, 0, Mathf.Sin(a) * radius * 1.15f); at.y = AquariumSeabed.Height(at.x,at.z)+.01f;
                    float height = Range(r, .85f, 3.5f);
                    if(AquariumMonolithLayout.Distance(new Point(at.x,at.y,at.z))>.55f&&!AquariumEntryLayout.KeepWaterClear(new Point(at.x,at.z),.8f)) { Kelp(kelp, at, height, a, r,marker); marker.kelpFronds++; }
                }
                // Separate small planting pockets extend each reef without stretching its boulder.
                // They keep the larger lagoon landscaped while leaving wide open-water lanes.
                for(int pocket=0;pocket<2;pocket++)
                {
                    float angle=zone*2.399f+pocket*2.3f;
                    var bed=p+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(radius+2.0f);bed.y=AquariumSeabed.Height(bed.x,bed.z)+.02f;
                    var test=new Point(bed.x,bed.z);
                    if(AquariumMonolithLayout.Distance(new Point(bed.x,bed.y,bed.z))<1)continue;
                    if(!AquariumMapOutline.Contains(test,1.3f)||AquariumWalkwayLayout.Contains(test,1.3f)||AquariumEntryLayout.KeepWaterClear(test,1.25f))continue;
                    for(int n=0;n<6;n++)
                    {
                        float direction=n*2.399f+angle;
                        var at=bed+new Vector3(Mathf.Cos(direction),0,Mathf.Sin(direction))*Range(r,.15f,.75f);
                        float size=Range(r,.15f,.3f);
                        stones.Ellipsoid(at,new Vector3(size,size*.5f,size*.8f),C(0x71958e),8,5,.12f);
                        Kelp(kelp,at,Range(r,1.1f,3.5f),direction,r,marker);marker.kelpFronds++;
                        if(n%2==0){CoralColony(coral,at+Vector3.up*.13f,Range(r,.22f,.4f),zone*7+n,r,marker);marker.coralColonies++;}
                    }
                }
                stones.Emit("12 • Reef " + (zone + 1).ToString("00") + " / weathered cover", root, rockMat);
                if(coral.Count>0)coral.Emit("13 • Reef " + (zone + 1).ToString("00") + " / coral colonies", root, coralMat);
                if(kelp.Count>0)kelp.Emit("14 • Reef " + (zone + 1).ToString("00") + " / living kelp", root, kelpMat);
            }
            var edgeStone = new Geo(); var edgeKelp = new Geo(); var edgeCoral = new Geo();
            // Longer shoreline gets more planting, while every plant keeps its original dimensions.
            const int shorelinePlants=200;
            for (int i = 0; i < shorelinePlants; i++)
            {
                float a = i * Mathf.PI * 2 / shorelinePlants;
                Vector3 p = At(a, Range(r, -.9f, -.24f), .15f);p.y=AquariumSeabed.Height(p.x,p.z)+.02f;
                float size = Range(r, .12f, .32f);
                if(AquariumMonolithLayout.Distance(new Point(p.x,p.y,p.z))<.6f)continue;
                if(AquariumEntryLayout.KeepWaterClear(new Point(p.x,p.z),1.0f)||AquariumWalkwayLayout.Contains(new Point(p.x,p.z),.6f))continue;
                edgeStone.Ellipsoid(p, new Vector3(size, .12f, size * .8f), Color.Lerp(C(0x3e7f80), C(0x9ebba0), Range(r, 0, 1)), 8, 5, .15f);
                if (i % 2 == 0) { Kelp(edgeKelp, p, Range(r, 1.3f, 4.7f), a, r,marker); marker.kelpFronds++; }
                if (i % 3 == 0) { CoralColony(edgeCoral,p+Vector3.up*.12f,Range(r,.18f,.32f),i/3,r,marker); marker.coralColonies++; }
            }
            for (int i = 0; i < 320; i++)
            {
                float a = Range(r, 0, Mathf.PI * 2), distance = Range(r, .1f, .96f);
                Vector3 at = At(a, 0, .14f); at.x *= distance; at.z *= distance;at.y=AquariumSeabed.Height(at.x,at.z)+.025f;
                float size = Range(r, .012f, .055f);
                if(AquariumEntryLayout.KeepWaterClear(new Point(at.x,at.z),.1f)||AquariumWalkwayLayout.Contains(new Point(at.x,at.z),.12f))continue;
                edgeStone.Ellipsoid(at, new Vector3(size * 1.5f, size * .6f, size), C(0xb6c6ac), 6, 4, .1f);
                if (i % 19 == 0) Starfish(edgeCoral, at + Vector3.up * .025f, Range(r, .16f, .25f), Corals[i % 6], a);
            }
            edgeStone.Emit("15 • Lagoon rubble & shells", root, rockMat);
            edgeKelp.Emit("16 • Peripheral eelgrass meadow", root, kelpMat);
            edgeCoral.Emit("17 • Shallow corals & sea stars", root, coralMat);
        }

        private static void Kelp(Geo g,Vector3 at,float height,float phase,System.Random r,AquariumEnvironmentRoot marker)
        {
            Seaweed(g,at,height,phase,r,marker);
        }

        private static void Leaf(Geo g, Vector3 start, Vector3 end, float width, Color c, float phase, float sway)
        {
            Vector3 dir = end - start;
            Vector3 side = Vector3.Cross(dir.normalized, Vector3.up).normalized;
            if (side.sqrMagnitude < .01f) side = Vector3.right;
            int begin = g.Count;
            for (int i = 0; i <= 7; i++)
            {
                float f = i / 7f; Vector3 p = Vector3.Lerp(start, end, f) + Vector3.up * Mathf.Sin(f * Mathf.PI) * dir.magnitude * .16f;
                float w = Mathf.Sin(f * Mathf.PI) * width;
                Color color = Color.Lerp(c * .72f, c * 1.12f, f); color.a = .65f + f * .35f;
                g.V(p - side * w, color, new Vector2(0, f), new Vector2(sway * (.5f + f * .5f), phase));
                g.V(p + Vector3.up * w * .18f, color * 1.07f, new Vector2(.5f, f), new Vector2(sway * (.5f + f * .5f), phase));
                g.V(p + side * w, color, new Vector2(1, f), new Vector2(sway * (.5f + f * .5f), phase));
                if (i < 7) { int v = begin + i * 3; g.Q(v, v + 1, v + 4, v + 3); g.Q(v + 1, v + 2, v + 5, v + 4); }
            }
        }

        private static void BranchCoral(Geo g, Vector3 at, Vector3 branch, float radius, int depth, Color c, System.Random r)
        {
            var end = at + branch;
            g.Cylinder(at, end, radius, radius * .65f, c, 7);
            g.Ellipsoid(end, Vector3.one * radius * .8f, Color.Lerp(c, Color.white, .4f), 7, 5, 0);
            if (depth <= 0) return;
            for (int j = 0; j < 3; j++)
            {
                float a = Range(r, 0, 6.28f);
                var direction = (branch.normalized * .42f + new Vector3(Mathf.Cos(a) * .75f, .5f, Mathf.Sin(a) * .75f)).normalized;
                BranchCoral(g, Vector3.Lerp(at, end, .7f + j * .12f), direction * branch.magnitude * .58f, radius * .6f, depth - 1, Color.Lerp(c, Color.white, .08f), r);
            }
        }

        private static void PlateCoral(Geo g,Vector3 p,float radius,Color c,int seed)
        {
            ScrollCoral(g,p,radius,c,seed);
        }

        private static void Starfish(Geo g, Vector3 at, float radius, Color c, float angle)
        {
            for (int i = 0; i < 10; i++)
            {
                float a = angle + i * Mathf.PI / 5, b = angle + (i + 1) * Mathf.PI / 5;
                var p = at + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius * (i % 2 == 0 ? 1 : .4f);
                var q = at + new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * radius * (i % 2 == 0 ? .4f : 1);
                g.Triangle(at + Vector3.up * .05f, q, p, c);
            }
        }

        private static void BuildAtmosphere(Transform root, Material bubbleMat, Material rayMat, System.Random r, AquariumEnvironmentRoot marker)
        {
            var bubbles = new Geo(); var beams = new Geo();
            Vector3[] vents = { LayoutPosition(-10,.2f,4),LayoutPosition(-6,.2f,-2),LayoutPosition(4,.2f,4),LayoutPosition(10,.2f,-1),LayoutPosition(0,.2f,-5) };
            foreach (var vent in vents) for (int i = 0; i < 28; i++)
            {
                float size = Range(r, .022f, .075f);
                Vector3 p = vent + new Vector3(Range(r,-.23f,.23f),0,Range(r,-.23f,.23f));p.y=AquariumSeabed.Height(p.x,p.z)+.06f;
                bubbles.Ellipsoid(p, Vector3.one * size, Color.white, 8, 6, 0, new Vector2(Range(r,0,1),Range(r,.065f,.13f)));
                marker.bubbleCount++;
            }
            var bubbleObject = bubbles.Emit("18 • Rising aeration bubbles / GPU animated", root, bubbleMat, false);
            bubbleObject.GetComponent<MeshFilter>().sharedMesh.bounds = new Bounds(Vector3.up * 6, new Vector3(29*AquariumMapOutline.HorizontalScale, AquariumModel.WaterHeight+1, 18*AquariumMapOutline.HorizontalScale));
            for (int i = 0; i < 26; i++)
            {
                // Interleave two clear-water rows so light reaches the enlarged swimming lanes.
                float x=-11+(i%13)*1.8f,z=(i<13?3.9f:-3.3f)+Mathf.Sin(i*1.7f)*1.4f;
                var location=AquariumWalkwayLayout.Constrain(AquariumMapOutline.Clamp(AquariumMapOutline.LayoutPoint(x,AquariumModel.WaterHeight,z),1.3f),1.1f);
                if(!AquariumMapOutline.Contains(location,1)||AquariumEntryLayout.DeckDistance(location)<.8f)continue;
                Vector3 top = new Vector3(location.x,AquariumModel.WaterHeight,location.z);
                Vector3 bottom = top + new Vector3(-3.7f, -AquariumModel.WaterHeight+.6f, -2.8f); float width = Range(r,.32f,.65f);
                float phase=Range(r,0,Mathf.PI*2);
                for (int plane = 0; plane < 2; plane++)
                {
                    Vector3 side = plane == 0 ? Vector3.right : Vector3.forward;
                    int n=beams.Count;
                    const int rows=20,columns=4;
                    for(int row=0;row<=rows;row++)for(int column=0;column<=columns;column++)
                    {
                        float depth=row/(float)rows,u=column/(float)columns;
                        var centre=Vector3.Lerp(top,bottom,depth);float spread=Mathf.Lerp(1,2.8f,depth);
                        beams.V(centre+side*(u*2-1)*width*spread,Color.white,new Vector2(u,1-depth),new Vector2(phase,0));
                        if(row<rows&&column<columns){int v=n+row*(columns+1)+column;beams.Q(v,v+1,v+columns+2,v+columns+1);}
                    }
                }
            }
            var shafts=beams.Emit("19 • Rippling refracted underwater sun shafts", root, rayMat, false);
            var shaftMesh=shafts.GetComponent<MeshFilter>().sharedMesh;var bounds=shaftMesh.bounds;bounds.Expand(2);shaftMesh.bounds=bounds;
        }

        // Geometry is grouped per zone/material. No per-leaf GameObjects or frame-by-frame mesh allocations.
        private sealed class Geo
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<Vector2> uv = new List<Vector2>(), data = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();
            public int Count { get { return vertices.Count; } }
            public void T(int a,int b,int c) { triangles.Add(a);triangles.Add(b);triangles.Add(c); }
            public void V(Vector3 p, Color c, Vector2 tex = default(Vector2), Vector2 payload = default(Vector2)) { vertices.Add(p); colors.Add(c); uv.Add(tex); data.Add(payload); }
            public void Q(int a,int b,int c,int d) { triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(a);triangles.Add(c);triangles.Add(d); }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color) { int n=Count;V(a,color,new Vector2(0,0));V(b,color,new Vector2(1,0));V(c,color,new Vector2(1,1));V(d,color,new Vector2(0,1));Q(n,n+1,n+2,n+3); }
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color) { int n=Count;V(a,color);V(b,color);V(c,color);triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2); }
            public void Box(Vector3 centre,Vector3 size,Color c,Quaternion rotation)
            {
                var v=new Vector3[8]; for(int i=0;i<8;i++)v[i]=centre+rotation*new Vector3((i&1)==0?-size.x*.5f:size.x*.5f,(i&2)==0?-size.y*.5f:size.y*.5f,(i&4)==0?-size.z*.5f:size.z*.5f);
                Quad(v[2],v[6],v[7],v[3],c);Quad(v[0],v[1],v[5],v[4],c);Quad(v[0],v[2],v[3],v[1],c);Quad(v[4],v[5],v[7],v[6],c);Quad(v[0],v[4],v[6],v[2],c);Quad(v[1],v[3],v[7],v[5],c);
            }
            public void Cylinder(Vector3 a,Vector3 b,float r0,float r1,Color c,int sides=8,bool open=false,float sway=0,float phase=0)
            {
                Vector3 axis=(b-a).normalized;Vector3 u=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.right:Vector3.up).normalized;Vector3 v=Vector3.Cross(axis,u);
                int n=Count;
                for(int j=0;j<=1;j++)for(int i=0;i<=sides;i++) { float t=i*Mathf.PI*2/sides;V((j==0?a:b)+(u*Mathf.Cos(t)+v*Mathf.Sin(t))*(j==0?r0:r1),j==0?c:Color.Lerp(c,Color.white,.08f),new Vector2((float)i/sides,j),new Vector2(sway,phase)); }
                for(int i=0;i<sides;i++)Q(n+i,n+i+1,n+i+sides+2,n+i+sides+1);
                int bottom=Count;V(a,c,Vector2.zero,new Vector2(sway,phase));
                for(int i=0;i<sides;i++)T(bottom,n+i+1,n+i);
                if(!open)
                {
                    int top=Count;V(b,c,Vector2.one,new Vector2(sway,phase));
                    for(int i=0;i<sides;i++)T(top,n+sides+1+i,n+sides+2+i);
                }
                if(open) { for(int i=0;i<sides;i++) { float aa=i*Mathf.PI*2/sides,bb=(i+1)*Mathf.PI*2/sides;Vector3 x=u*Mathf.Cos(aa)+v*Mathf.Sin(aa),y=u*Mathf.Cos(bb)+v*Mathf.Sin(bb);Quad(b+x*r1,b+y*r1,b+y*r1*.66f,b+x*r1*.66f,Color.Lerp(c,Color.white,.3f));Quad(b+x*r1*.66f,b+y*r1*.66f,b-axis*r1+y*r1*.55f,b-axis*r1+x*r1*.55f,c*.35f); } }
            }
            public void Ellipsoid(Vector3 at,Vector3 scale,Color color,int sides,int rings,float rough,Vector2 payload=default(Vector2))
            {
                int n=Count;
                for(int j=0;j<=rings;j++)for(int i=0;i<=sides;i++)
                {
                    float y=Mathf.Cos(j*Mathf.PI/rings),rr=Mathf.Sin(j*Mathf.PI/rings),a=i*Mathf.PI*2/sides;
                    Vector3 unit=new Vector3(Mathf.Cos(a)*rr,y,Mathf.Sin(a)*rr);
                    float noise=Mathf.PerlinNoise(unit.x*3+at.x*1.7f+20,unit.z*3+unit.y*2+at.z*1.4f+20);
                    Vector3 p=at+Vector3.Scale(unit,scale)*(1+(noise-.5f)*rough);
                    Color c=color*(1+(noise-.5f)*rough*3);c=Color.Lerp(c,C(0x9bb4a2),Mathf.Max(0,y)*rough*1.3f);c.a=.75f+Mathf.Max(0,y)*.25f;
                    V(p,c,new Vector2((float)i/sides,(float)j/rings),payload);
                    if(j<rings&&i<sides) { int v=n+j*(sides+1)+i;Q(v,v+1,v+sides+2,v+sides+1); }
                }
            }
            public GameObject Emit(string name,Transform parent,Material material,bool shadows=true)
            {
                var mesh=new Mesh { name=name,indexFormat=IndexFormat.UInt32 };
                if (name.Contains("weathered cover"))
                {
                    var points = new List<Vector3>(); var tint = new List<Color>(); var indices = new List<int>();
                    foreach (int index in triangles) { indices.Add(points.Count); points.Add(vertices[index]); tint.Add(colors[index]); }
                    mesh.SetVertices(points); mesh.SetColors(tint); mesh.SetTriangles(indices, 0);
                }
                else { mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetUVs(1,data);mesh.SetTriangles(triangles,0); }
                mesh.RecalculateNormals();SurfaceBasis(mesh);mesh.RecalculateBounds();
                var bounds=mesh.bounds;bounds.Expand(.7f);mesh.bounds=bounds;
                var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=shadows?ShadowCastingMode.On:ShadowCastingMode.Off;renderer.receiveShadows=shadows;return go;
            }
        }
    }
}
