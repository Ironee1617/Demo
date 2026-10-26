using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>
    /// Lit terrain baked by Tools/TowerArt/BackdropPainter: per theme a horizontally tiling surface band, a body tile
    /// that repeats both ways, a platform band, its end cap and a props sheet. Meshes follow the generated hills and
    /// sloped platforms and sample the textures through tiling UVs, so building a floor composes no pixels.
    /// </summary>
    public sealed partial class TowerWorld
    {
        private const string LitTerrainFolder="Tower/Themes/Terrain/";
        // Band geometry in world units; it must match the bake (Tools/TowerArt/BackdropPainter/TerrainBake.cs).
        internal const float TopTile=12,TopAbove=.25f,TopBelow=1.25f,BodyTile=6,BodyOverlap=.85f;
        internal const float SlabTile=6,SlabAbove=.25f,SlabBelow=1f,CapHalf=.375f,PropCell=1.5f,PropBase=.1f;
        private const float StripStep=.125f;
        private sealed class LitTerrainSet { public Material top,body,slab,cap; public Texture2D props; }
        private static Shader terrainStripShader;
        private static TowerThemeId litTheme;private static LitTerrainSet litSet;private static bool litChecked;
        private readonly List<Mesh> terrainMeshes=new List<Mesh>();

        private static LitTerrainSet LitTerrain(TowerThemeId theme)
        {
            if(litChecked&&litTheme==theme)return litSet;
            ReleaseLitSet();litChecked=true;litTheme=theme;
            string id=LitTerrainFolder+theme.ToString().ToLowerInvariant();
            var top=Resources.Load<Texture2D>(id+"-top");var body=Resources.Load<Texture2D>(id+"-body");
            var slab=Resources.Load<Texture2D>(id+"-slab");var cap=Resources.Load<Texture2D>(id+"-cap");
            if(top==null||body==null||slab==null||cap==null)return litSet=null;
            if(terrainStripShader==null)terrainStripShader=Resources.Load<Shader>("Tower/TerrainStrip");
            if(terrainStripShader==null)return litSet=null;
            top.wrapMode=TextureWrapMode.Repeat;body.wrapMode=TextureWrapMode.Repeat;slab.wrapMode=TextureWrapMode.Repeat;cap.wrapMode=TextureWrapMode.Clamp;
            // Tiles repeat only across u for bands; clamp v keeps the transparent edges clean.
            top.wrapModeV=TextureWrapMode.Clamp;slab.wrapModeV=TextureWrapMode.Clamp;
            litSet=new LitTerrainSet{top=StripMaterial(top),body=StripMaterial(body),slab=StripMaterial(slab),cap=StripMaterial(cap),props=Resources.Load<Texture2D>(id+"-props")};
            return litSet;
        }
        private static Material StripMaterial(Texture2D texture){return new Material(terrainStripShader){mainTexture=texture,name=texture.name};}

        private static readonly List<ResourceRequest> artPrefetch=new List<ResourceRequest>();
        private static TowerThemeId artPrefetchTheme;
        /// <summary>Starts reading the next theme's terrain and backdrop textures in the background, so the floor that
        /// switches to it finds them resident instead of stalling on disk reads and uploads.</summary>
        private static void PrefetchThemeArt(TowerThemeId current,TowerThemeId next)
        {
            if(next!=current&&artPrefetch.Count>0&&artPrefetchTheme==next)return;
            // A prediction that did not come true leaves its textures unused.
            if(artPrefetchTheme!=current)foreach(var request in artPrefetch)if(request.isDone&&request.asset!=null)Resources.UnloadAsset(request.asset);
            artPrefetch.Clear();
            if(next==current)return;
            artPrefetchTheme=next;
            string id=LitTerrainFolder+next.ToString().ToLowerInvariant();
            foreach(var part in new[]{"-top","-body","-slab","-cap","-props"})artPrefetch.Add(Resources.LoadAsync<Texture2D>(id+part));
            TowerThemeBackdrop.Prefetch(next,artPrefetch);
            PrefetchGate(next);
        }
        private static void ReleaseLitSet()
        {
            if(litSet==null)return;
            foreach(var m in new[]{litSet.top,litSet.body,litSet.slab,litSet.cap})if(m!=null){var t=m.mainTexture;Object.Destroy(m);if(t!=null)Resources.UnloadAsset(t);}
            if(litSet.props!=null)Resources.UnloadAsset(litSet.props);
            litSet=null;
        }
        private void ReleaseLitTerrain()
        {
            foreach(var mesh in terrainMeshes)if(mesh)Object.Destroy(mesh);
            terrainMeshes.Clear();
        }

        /// <summary>Builds the ground and platforms from the baked lit set; false when this theme has none yet.</summary>
        private bool BuildLitTerrain(TowerThemeId theme,bool openBelow)
        {
            var set=LitTerrain(theme);if(set==null)return false;
            // Body: world-aligned tiles from just under the surface band down to the bedrock line.
            float bottom=openBelow?Ground-1.6f:Ground-6f;
            var body=new StripBuilder();
            for(float x=DrawLeft;x<=DrawRight+.001f;x+=StripStep)
            {
                float h=GroundAt(x),top=h-TopBelow+BodyOverlap;
                byte fade=openBelow?(byte)0:(byte)255;
                body.Column(new Vector3(x,top,0),new Vector2(x/BodyTile,top/BodyTile),new Color32(255,255,255,255),
                            new Vector3(x,bottom,0),new Vector2(x/BodyTile,bottom/BodyTile),new Color32(255,255,255,fade));
            }
            StripObject(theme+" ground body",body,set.body,-21);
            // Surface band: sheared along the hill profile so its walkable edge sits exactly on the collision line.
            var band=new StripBuilder();
            for(float x=DrawLeft;x<=DrawRight+.001f;x+=StripStep)
            {
                float h=GroundAt(x),u=x/TopTile;
                band.Column(new Vector3(x,h+TopAbove,0),new Vector2(u,1),new Color32(255,255,255,255),new Vector3(x,h-TopBelow,0),new Vector2(u,0),new Color32(255,255,255,255));
            }
            StripObject(theme+" ground tile",band,set.top,-20);
            foreach(var ledge in Ledges)BuildLitPlatform(theme,ledge,set);
            return true;
        }

        private void BuildLitPlatform(TowerThemeId theme,Ledge ledge,LitTerrainSet set)
        {
            // Each slab starts at a different spot of the tiling texture so neighbours do not look stamped.
            float offset=(float)decorationRandom.NextDouble();
            var slab=new StripBuilder();
            int steps=Mathf.Max(2,Mathf.CeilToInt((ledge.Right-ledge.Left)/StripStep));
            for(int i=0;i<=steps;i++)
            {
                float x=Mathf.Lerp(ledge.Left,ledge.Right,i/(float)steps),h=ledge.HeightAt(x),u=offset+(x-ledge.Left)/SlabTile;
                slab.Column(new Vector3(x,h+SlabAbove,0),new Vector2(u,1),new Color32(255,255,255,255),new Vector3(x,h-SlabBelow,0),new Vector2(u,0),new Color32(255,255,255,255));
            }
            StripObject(theme+" raised platform",slab,set.slab,-15);
            // End caps close both cut ends; the left one is the right cap mirrored.
            var caps=new StripBuilder();
            float hr=ledge.HeightAt(ledge.Right),hl=ledge.HeightAt(ledge.Left);
            caps.Quad(new Vector3(ledge.Right-CapHalf,hr-SlabBelow,0),new Vector3(ledge.Right+CapHalf,hr+SlabAbove,0),0,1);
            caps.Quad(new Vector3(ledge.Left-CapHalf,hl-SlabBelow,0),new Vector3(ledge.Left+CapHalf,hl+SlabAbove,0),1,0);
            StripObject(theme+" platform caps",caps,set.cap,-14);
        }

        /// <summary>Ground props from the lit props sheet; false when the theme has no sheet.</summary>
        private bool BuildLitProps(TowerThemeId theme)
        {
            var set=litChecked&&litTheme==theme?litSet:null;
            if(set==null||set.props==null)return false;
            int cells=Mathf.Max(1,set.props.width/set.props.height);float ppu=set.props.height/PropCell;
            for(int i=0;-23+i*3.7f<DrawRight+2;i++)
            {
                float x=-23+i*3.7f+Range(-.5f,.5f);
                if(Mathf.Abs(x-ExitX)<1.8f||Mathf.Abs(x+12)<1.2f||OnHazard(x))continue;
                string key="lit prop "+theme+(i%cells);Sprite sprite;
                if(!themeSprites.TryGetValue(key,out sprite)||sprite==null||sprite.texture!=set.props)
                {
                    sprite=Sprite.Create(set.props,new Rect((i%cells)*set.props.height,0,set.props.height,set.props.height),new Vector2(.5f,PropBase/PropCell),ppu,0,SpriteMeshType.FullRect);
                    sprite.name=key;themeSprites[key]=sprite;
                }
                float size=Range(.7f,1f);
                var prop=Box(theme+" foreground detail",new Vector2(x,GroundAt(x)),Vector2.one*size,Color.white,-32,sprite);
                if(i%2==1)prop.transform.localScale=new Vector3(-size,size,1);
            }
            return true;
        }

        private sealed class StripBuilder
        {
            public readonly List<Vector3> v=new List<Vector3>();public readonly List<Vector2> uv=new List<Vector2>();
            public readonly List<Color32> c=new List<Color32>();public readonly List<int> t=new List<int>();
            /// <summary>Appends a top/bottom vertex pair and joins it to the previous pair.</summary>
            public void Column(Vector3 top,Vector2 topUv,Color32 topColor,Vector3 bottom,Vector2 bottomUv,Color32 bottomColor)
            {
                int k=v.Count;v.Add(top);uv.Add(topUv);c.Add(topColor);v.Add(bottom);uv.Add(bottomUv);c.Add(bottomColor);
                if(k>=2&&k%2==0&&!quadStart.Contains(k)){t.Add(k-2);t.Add(k);t.Add(k-1);t.Add(k-1);t.Add(k);t.Add(k+1);}
            }
            private readonly HashSet<int> quadStart=new HashSet<int>();
            /// <summary>Appends a standalone axis-aligned quad from min to max with u running u0..u1.</summary>
            public void Quad(Vector3 min,Vector3 max,float u0,float u1)
            {
                quadStart.Add(v.Count);
                Column(new Vector3(min.x,max.y,0),new Vector2(u0,1),new Color32(255,255,255,255),new Vector3(min.x,min.y,0),new Vector2(u0,0),new Color32(255,255,255,255));
                Column(new Vector3(max.x,max.y,0),new Vector2(u1,1),new Color32(255,255,255,255),new Vector3(max.x,min.y,0),new Vector2(u1,0),new Color32(255,255,255,255));
            }
        }
        private GameObject StripObject(string name,StripBuilder strip,Material material,int order)
        {
            var mesh=new Mesh{name=name};
            mesh.SetVertices(strip.v);mesh.SetUVs(0,strip.uv);mesh.SetColors(strip.c);mesh.SetTriangles(strip.t,0);mesh.RecalculateBounds();
            terrainMeshes.Add(mesh);
            var go=new GameObject(name);go.transform.SetParent(environment,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=order;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            return go;
        }
    }
}
