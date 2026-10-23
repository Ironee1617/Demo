using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerWorld
    {
        private sealed class ThemeLook { public Color sky,horizon,stone,edge,accent,frame; }
        private static ThemeLook Look(TowerThemeId theme)
        {
            switch(theme)
            {
                case TowerThemeId.Olympus: return new ThemeLook{sky=Hex("5f9bd8"),horizon=Hex("f1d6a8"),stone=Hex("a9a3ad"),edge=Hex("efe3c6"),accent=Hex("f4dfa1"),frame=Hex("d8ccb0")};
                case TowerThemeId.Asgard: return new ThemeLook{sky=Hex("131d33"),horizon=Hex("3a5577"),stone=Hex("4a5c74"),edge=Hex("c9e3ee"),accent=Hex("9ee9ef"),frame=Hex("a9c3d6")};
                case TowerThemeId.Dangun: return new ThemeLook{sky=Hex("9aa9b5"),horizon=Hex("d9c9b1"),stone=Hex("5a5d58"),edge=Hex("cfc6a8"),accent=Hex("e9c27a"),frame=Hex("9c8a70")};
                case TowerThemeId.Eden: return new ThemeLook{sky=Hex("cfe5ef"),horizon=Hex("f6edc8"),stone=Hex("5e7a52"),edge=Hex("d8ecaa"),accent=Hex("f2e39a"),frame=Hex("8aa66a")};
                case TowerThemeId.Veda: return new ThemeLook{sky=Hex("3c1f4e"),horizon=Hex("f0813c"),stone=Hex("7a5a46"),edge=Hex("e4c07a"),accent=Hex("ffb347"),frame=Hex("b08a52")};
                case TowerThemeId.Papyrus: return new ThemeLook{sky=Hex("2d5fa6"),horizon=Hex("f2c882"),stone=Hex("9c7f55"),edge=Hex("f0d9a0"),accent=Hex("f0c860"),frame=Hex("c9a86a")};
                case TowerThemeId.Hell: return new ThemeLook{sky=Hex("1a0806"),horizon=Hex("7a1e0c"),stone=Hex("2a2220"),edge=Hex("c8502a"),accent=Hex("ff7a2a"),frame=Hex("5a4a44")};
                case TowerThemeId.Abyss: return new ThemeLook{sky=Hex("0a0408"),horizon=Hex("3a0e1c"),stone=Hex("3a1820"),edge=Hex("8a2a3a"),accent=Hex("c02a4a"),frame=Hex("4a2a38")};
                case TowerThemeId.Village: return new ThemeLook{sky=Hex("2a2a5a"),horizon=Hex("e89a6a"),stone=Hex("6a5a4a"),edge=Hex("e8c890"),accent=Hex("ffd27a"),frame=Hex("b89a6a")};
                default: return new ThemeLook{sky=Hex("aab8bf"),horizon=Hex("e8dcc4"),stone=Hex("6a4c48"),edge=Hex("e6c98a"),accent=Hex("ff9a4a"),frame=Hex("b8462e")};
            }
        }
        private void BuildTheme(TowerThemeId theme)
        {
            var look=Look(theme);accent=look.accent;
            Box("Theme sky",new Vector2(6,3),new Vector2(110,38),look.sky,-100);
            for(int i=0;i<8;i++)Box("Pixel horizon band",new Vector2(6,-3+i*1.35f),new Vector2(100,1.38f),Color.Lerp(look.horizon,look.sky,i/8f),-98);
            using(TowerProfiling.Backdrop.Auto())BuildAuthoredBackdrop(theme);
            using(TowerProfiling.Terrain.Auto())BuildTerrain(theme,look);
            BuildForeground(theme);
        }
        // Baked 96ppu terrain (Tools/TowerArt/BackdropPainter): per theme a 384x384 column holding
        // props and end caps (row 0), the platform strip (96), buried strata (192) and ground (288).
        private const int AtlasPpu=96,AtlasColumn=384;
        private static Texture2D terrainAtlas;private static bool terrainAtlasChecked;
        private static Color32[] atlasPixels;private static int atlasWidth;
        private readonly List<Texture2D> terrainTextures=new List<Texture2D>();
        private readonly List<Sprite> terrainSprites=new List<Sprite>();
        private static Texture2D TerrainAtlas
        {
            get
            {
                if(!terrainAtlasChecked)
                {
                    terrainAtlasChecked=true;terrainAtlas=Resources.Load<Texture2D>("Tower/Themes/Terrain");
                    if(terrainAtlas!=null){terrainAtlas.filterMode=FilterMode.Point;terrainAtlas.wrapMode=TextureWrapMode.Clamp;}
                }
                return terrainAtlas;
            }
        }
        private static bool EnsureAtlasPixels()
        {
            if(TerrainAtlas==null)return false;
            if(atlasPixels==null){atlasPixels=TerrainAtlas.GetPixels32();atlasWidth=TerrainAtlas.width;}
            return true;
        }
        /// <summary>True when the baked atlas already holds this theme's column.</summary>
        private static bool AtlasHasTheme(TowerThemeId theme){return TerrainAtlas!=null&&((int)theme+1)*AtlasColumn<=TerrainAtlas.width;}
        private void ReleaseTerrainTextures()
        {
            foreach(var sprite in terrainSprites)if(sprite)Object.Destroy(sprite);
            foreach(var texture in terrainTextures)if(texture)Object.Destroy(texture);
            terrainSprites.Clear();terrainTextures.Clear();ReleaseLitTerrain();
        }
        private static Sprite AtlasSprite(string key,Rect rect,Vector2 pivot)
        {
            Sprite sprite;if(themeSprites.TryGetValue(key,out sprite))return sprite;
            sprite=Sprite.Create(TerrainAtlas,rect,pivot,AtlasPpu,0,SpriteMeshType.FullRect);sprite.name=key;themeSprites.Add(key,sprite);return sprite;
        }
        private static Color32 Blend(Color32 under,Color32 over)
        {
            if(over.a>=255)return over;if(over.a==0)return under;
            float a=over.a/255f,ua=under.a/255f,outA=a+ua*(1-a);
            if(outA<=0)return new Color32(0,0,0,0);
            float w=ua*(1-a);
            return new Color32((byte)((over.r*a+under.r*w)/outA),(byte)((over.g*a+under.g*w)/outA),(byte)((over.b*a+under.b*w)/outA),(byte)(outA*255));
        }
        private static Color32 Tint(Color32 c,Color32 tint){return new Color32((byte)(c.r*tint.r/255),(byte)(c.g*tint.g/255),(byte)(c.b*tint.b/255),c.a);}
        /// <summary>
        /// The rolling ground and every sloped platform are composed per texel column from the atlas strips,
        /// so hills and tilted slabs keep crisp 96ppu masonry with the surface exactly on the collision profile.
        /// </summary>
        private void BuildTerrain(TowerThemeId theme,ThemeLook look)
        {
            bool openBelow=theme==TowerThemeId.Olympus;
            if(!openBelow)Box("Bedrock",new Vector2(6,-8.5f),new Vector2(100,7.2f),Color.Lerp(look.stone,Color.black,.6f),-25);
            if(BuildLitTerrain(theme,openBelow))return;
            if(!EnsureAtlasPixels()||!AtlasHasTheme(theme)){BuildPlainTerrain(theme,look);return;}
            Color32 line=look.edge;line.a=128;
            for(float x0=-26;x0<42;x0+=4)BuildGroundChunk(theme,x0,4,openBelow,line);
            foreach(var ledge in Ledges)BuildPlatformSlab(theme,ledge,line);
        }
        private void BuildGroundChunk(TowerThemeId theme,float x0,float width,bool openBelow,Color32 line)
        {
            int columns=Mathf.RoundToInt(width*AtlasPpu);
            float minH=float.MaxValue,maxH=float.MinValue;
            for(int i=0;i<=columns;i++){float h=GroundAt(x0+i/(float)AtlasPpu);minH=Mathf.Min(minH,h);maxH=Mathf.Max(maxH,h);}
            // Every chunk shares one underside so hill chunks reach down to the bedrock instead of floating above it;
            // the open sky walkway keeps its shallower flat underside.
            float bottom=openBelow?Ground-1.5f:Mathf.Floor((Mathf.Min(minH,Ground)-2.25f)*AtlasPpu)/AtlasPpu;
            int rows=Mathf.CeilToInt((maxH-bottom)*AtlasPpu)+1;
            var pixels=new Color32[columns*rows];
            int column0=(int)theme*AtlasColumn;var strataTint=new Color32(161,161,173,255);
            for(int i=0;i<columns;i++)
            {
                float x=x0+(i+.5f)/AtlasPpu;float h=GroundAt(x);
                int jTop=Mathf.Clamp(Mathf.FloorToInt((h-bottom)*AtlasPpu),0,rows);
                int ix=((Mathf.FloorToInt(x*AtlasPpu)%AtlasColumn)+AtlasColumn)%AtlasColumn;
                for(int j=jTop-1;j>=0;j--)
                {
                    int d=jTop-1-j;Color32 c;
                    if(d<AtlasPpu)c=atlasPixels[(288+AtlasPpu-1-d)*atlasWidth+column0+ix];
                    else
                    {
                        int under=(d-AtlasPpu)%AtlasPpu;
                        c=Tint(atlasPixels[(192+AtlasPpu-1-under)*atlasWidth+column0+ix],strataTint);
                        if(openBelow)c.a=(byte)(c.a*Mathf.Clamp01(j/48f));
                    }
                    if(d<2&&c.a>0)c=Blend(c,line);
                    pixels[j*columns+i]=c;
                }
            }
            SlabObject(theme+" ground tile",x0,bottom,columns,rows,pixels,-20);
        }
        private void BuildPlatformSlab(TowerThemeId theme,Ledge ledge,Color32 line)
        {
            float left=Mathf.Floor(ledge.Left*AtlasPpu)/AtlasPpu;
            int columns=Mathf.Max(32,Mathf.RoundToInt((ledge.Right-left)*AtlasPpu));
            float bottom=Mathf.Floor((ledge.Bottom-1f)*AtlasPpu)/AtlasPpu;
            int rows=Mathf.CeilToInt((ledge.Top-bottom)*AtlasPpu)+1;
            var pixels=new Color32[columns*rows];int column0=(int)theme*AtlasColumn;
            for(int i=0;i<columns;i++)
            {
                float x=left+(i+.5f)/AtlasPpu;float h=ledge.HeightAt(x);
                int jTop=Mathf.Clamp(Mathf.FloorToInt((h-bottom)*AtlasPpu),0,rows);
                int ix=((Mathf.FloorToInt(x*AtlasPpu)%AtlasColumn)+AtlasColumn)%AtlasColumn;
                // The strip's own end caps close the cut slab on both sides.
                int cap=i<16?288+i:i>=columns-16?304+(i-(columns-16)):-1;
                for(int d=0;d<AtlasPpu;d++)
                {
                    int j=jTop-1-d;if(j<0||j>=rows)continue;
                    Color32 c=atlasPixels[(96+AtlasPpu-1-d)*atlasWidth+column0+ix];
                    if(cap>=0){var k=atlasPixels[(AtlasPpu-1-d)*atlasWidth+column0+cap];if(k.a>0)c=Blend(c,k);}
                    if(d<2&&c.a>0)c=Blend(c,line);
                    pixels[j*columns+i]=c;
                }
            }
            SlabObject(theme+" raised platform",left,bottom,columns,rows,pixels,-15);
        }
        private GameObject SlabObject(string name,float x,float y,int columns,int rows,Color32[] pixels,int order)
        {
            var texture=new Texture2D(columns,rows,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,name=name};
            texture.SetPixels32(pixels);texture.Apply(false,true);terrainTextures.Add(texture);
            var sprite=Sprite.Create(texture,new Rect(0,0,columns,rows),Vector2.zero,AtlasPpu,0,SpriteMeshType.FullRect);sprite.name=name;terrainSprites.Add(sprite);
            var go=new GameObject(name);go.transform.SetParent(environment,false);go.transform.localPosition=new Vector3(x,y,0);
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sortingOrder=order;return go;
        }
        /// <summary>Flat-colour stand-in used only when the baked atlas is missing.</summary>
        private void BuildPlainTerrain(TowerThemeId theme,ThemeLook look)
        {
            for(float x=-26;x<42;x+=.5f)
            {
                float h=GroundAt(x+.25f);
                Box(theme+" ground tile",new Vector2(x+.25f,h-1.25f),new Vector2(.5f,2.5f),look.stone,-20);
                Box(theme+" ground walkable surface",new Vector2(x+.25f,h-.03f),new Vector2(.5f,.06f),look.edge,-17);
            }
            foreach(var ledge in Ledges)
                for(float x=ledge.Left;x<ledge.Right;x+=.25f)
                {
                    float w=Mathf.Min(.25f,ledge.Right-x),h=ledge.HeightAt(x+w*.5f);
                    Box(theme+" raised platform",new Vector2(x+w*.5f,h-.25f),new Vector2(w,.5f),look.stone,-15);
                    Box(theme+" platform walkable surface",new Vector2(x+w*.5f,h-.03f),new Vector2(w,.06f),look.edge,-13);
                }
        }
        private void BuildAuthoredBackdrop(TowerThemeId theme)
        {
            // Baked high-resolution parallax planes; the painted creature board's panorama remains the fallback.
            if(TowerThemeBackdrop.Build(environment,theme))return;
            Sprite sprite=TowerThemeMonsterArt.Background(theme);
            if(sprite==null)return;
            float upperExtension=Mathf.Max(0,HighestPlatform-1);
            float scale=Mathf.Max(66/sprite.rect.width,(22+upperExtension*2)/sprite.rect.height);
            Box(theme+" painted panorama",new Vector2(6,3.5f+upperExtension*.5f),Vector2.one*scale,new Color(.78f,.78f,.81f,1),-94,sprite);
        }
        private void BuildForeground(TowerThemeId theme)
        {
            if(BuildLitProps(theme)||!AtlasHasTheme(theme))return;
            // Small props rest on the hills themselves, below knee height, leaving every landing surface readable.
            for(int i=0;i<19;i++)
            {
                float x=-23+i*3.7f+Range(-.5f,.5f);
                if(Mathf.Abs(x-ExitX)<1.8f)continue;
                float size=Range(.65f,.95f);
                var prop=Box(theme+" foreground detail",new Vector2(x,GroundAt(x)),Vector2.one*size,Color.white,-32,ForegroundSprite(theme,i%3));
                if(i%2==1)prop.transform.localScale=new Vector3(-size,size,1);
            }
        }
        private static Sprite ForegroundSprite(TowerThemeId theme,int variant)
        {return AtlasSprite("foreground atlas "+theme+variant,new Rect((int)theme*AtlasColumn+(variant%3)*AtlasPpu,0,AtlasPpu,AtlasPpu),new Vector2(.5f,0));}
        private void BuildGate()
        {
            portal=new GameObject("Ascent gate").transform;portal.SetParent(environment,false);portal.localPosition=new Vector3(ExitX,GroundAt(ExitX),0);
            SpriteObject(portal,"Carved ascent arch",new Vector2(0,1.625f),Vector2.one,Color.white,-10,GateSprite(activeTheme,Look(activeTheme).frame));
            gateOpen=false;gateOpening=SpriteObject(portal,"Gate opening",new Vector2(0,1.4f),new Vector2(1.5f,2.8f),new Color(accent.r,accent.g,accent.b,.12f),-9,Glow).GetComponent<SpriteRenderer>();
        }
        private const int GateWidth=224,GateHeight=312;
        private static readonly Dictionary<TowerThemeId,System.Threading.Tasks.Task<Color32[]>> gatePixels=new Dictionary<TowerThemeId,System.Threading.Tasks.Task<Color32[]>>();
        private static Sprite GateSprite(TowerThemeId theme,Color frame)
        {
            // 96ppu: the same 2.33 x 3.25 unit silhouette as before, with carved mouldings, voussoirs and fluting.
            string key="carved ascent arch hd "+theme;Sprite sprite;if(themeSprites.TryGetValue(key,out sprite))return sprite;
            System.Threading.Tasks.Task<Color32[]> drawn;
            var pixels=gatePixels.TryGetValue(theme,out drawn)?drawn.Result:GatePixels(new GatePalette(frame));gatePixels.Remove(theme);
            sprite=PixelSprite(GateWidth,GateHeight,96,pixels);themeSprites.Add(key,sprite);return sprite;
        }
        /// <summary>Draws a theme's arch on a worker thread ahead of the floor that first shows it.</summary>
        private static void PrefetchGate(TowerThemeId theme)
        {
            if(themeSprites.ContainsKey("carved ascent arch hd "+theme)||gatePixels.ContainsKey(theme))return;
            var palette=new GatePalette(Look(theme).frame);
            gatePixels[theme]=System.Threading.Tasks.Task.Run(()=>GatePixels(palette));
        }
        private struct GatePalette
        {
            public Color frame,outline,shadow,light,glint,backDark,backLit,sill,sillLit;
            public GatePalette(Color frame)
            {
                this.frame=frame;outline=Color.Lerp(frame,Hex("111c2a"),.7f);shadow=Color.Lerp(frame,Hex("29303f"),.4f);light=Color.Lerp(frame,Hex("fff0ca"),.38f);glint=Color.Lerp(frame,Hex("fff6e0"),.62f);
                backDark=Hex("0e1824");backLit=Hex("2c3a38");sill=Hex("38453e");sillLit=Hex("4a5a50");
            }
        }
        /// <summary>The arch's texels. Pure arithmetic on the palette, so it may run off the main thread.</summary>
        private static Color32[] GatePixels(GatePalette p)
        {
            Color frame=p.frame,outline=p.outline,shadow=p.shadow,light=p.light,glint=p.glint,backDark=p.backDark,backLit=p.backLit,sill=p.sill,sillLit=p.sillLit;
            System.Func<int,int,Color> texel=(x,y)=>
            {
                // Geometry is measured in the original 48ppu units; shading works per 96ppu texel.
                float u=(x+.5f)*.5f,v=(y+.5f)*.5f,dx=u-56,dy=v-105,rad=Mathf.Sqrt(dx*dx+dy*dy);
                uint h=(uint)(x*73856093)^(uint)(y*19349663);h^=h>>13;h*=0x5bd1e995;h^=h>>15;float grain=(h&1023)/1023f;
                bool arch=v>=105&&rad<48&&rad>32;
                bool pillar=v>=7&&v<106&&((u>=8&&u<25)||(u>87&&u<=104));
                bool foot=v<11&&v>1&&((u>=3&&u<30)||(u>82&&u<=109));
                bool capital=v>=99&&v<111&&((u>=4&&u<29)||(u>83&&u<=108));
                bool steps=v<8&&u>19&&u<93;
                bool recess=(v>=8&&v<105&&u>=25&&u<=87)||(v>=105&&rad<=32);
                if(recess)
                {
                    // A deep doorway: faint coursing in the back wall, lit from the threshold.
                    float lift=Mathf.Clamp01((40-v)/32f);
                    Color back=Color.Lerp(backDark,backLit,lift*.8f);
                    if(Mathf.Repeat(v,9)<.5f||Mathf.Repeat(u+(Mathf.Floor(v/9)%2)*7,14)<.5f)back=Color.Lerp(back,Color.black,.35f);
                    return v<15?Color.Lerp(sill,sillLit,grain*.4f):back;
                }
                if(!arch&&!pillar&&!foot&&!capital&&!steps)return Color.clear;
                Color c=frame;
                if(arch)
                {
                    float t=(rad-32)/16,angle=Mathf.Atan2(dy,dx)*Mathf.Rad2Deg;
                    if(rad>47.5f||rad<32.5f)c=outline;
                    else if(t>.86f)c=t>.93f?glint:light;
                    else if(t<.2f)c=t<.1f?Color.Lerp(shadow,outline,.5f):shadow;
                    else if(Mathf.Abs(t-.55f)<.04f)c=shadow;
                    if(Mathf.Repeat(angle+6,11.25f)<.9f&&rad>33&&rad<47)c=outline;
                    else if(Mathf.Repeat(angle+6,11.25f)<1.8f&&rad>33&&rad<47&&c==frame)c=light;
                    if(Mathf.Abs(dx)<5.5f&&v>138){c=Mathf.Abs(dx)>5||rad>47.5f?outline:dx<-2.5f?glint:dx>2.5f?shadow:light;}
                }
                else if(capital)
                {
                    float r=v-99;
                    c=r<.5f||r>11.5f?outline:r>10.5f?glint:r>8?light:r<3?shadow:frame;
                    if(Mathf.Abs(r-6)<.5f)c=outline;
                    float cx=u<56?u-4:108-u;if(cx<.5f||cx>24.5f)c=outline;
                }
                else if(pillar)
                {
                    float local=u<56?u-8:104-u;
                    if(local<.5f||local>16.5f)c=outline;
                    else if(local<3.5f)c=local<1.5f?glint:light;
                    else if(local>13)c=shadow;
                    else if(Mathf.Abs(local-6.5f)<.5f||Mathf.Abs(local-9.5f)<.5f||Mathf.Abs(local-12)<.5f)c=shadow;
                    else if(Mathf.Abs(local-7.5f)<.5f||Mathf.Abs(local-10.5f)<.5f)c=light;
                    float drum=Mathf.Repeat(v,24);
                    if(drum<.5f)c=outline;else if(drum>23&&local>.5f&&local<16.5f)c=light;
                    if(drum>7&&drum<15&&Mathf.Abs(local-8.5f)<1f)c=drum>8&&drum<14?glint:light;
                }
                else
                {
                    float r=Mathf.Repeat(v,4);
                    c=r<.5f?outline:r>3?light:r>2?frame:Color.Lerp(frame,shadow,.5f);
                    if(u<6||u>106)c=shadow;
                }
                // Weathered stone grain.
                if(c==frame&&grain>.86f)c=Color.Lerp(frame,shadow,.6f);
                else if(c==frame&&grain<.06f)c=light;
                return c;
            };
            var pixels=new Color32[GateWidth*GateHeight];
            for(int y=0;y<GateHeight;y++)for(int x=0;x<GateWidth;x++)pixels[y*GateWidth+x]=texel(x,y);
            return pixels;
        }
        private void BuildWeather()
        {
            // Each mythology has its own drifting particles: snow, petals, lamp sparks, dust, mist, cloud wisps.
            int count=activeTheme==TowerThemeId.Asgard?38:activeTheme==TowerThemeId.Olympus?8:activeTheme==TowerThemeId.Eden||activeTheme==TowerThemeId.Chinese?26:20;
            for(int i=0;i<count;i++)
            {
                Vector2 pos=new Vector2(Range(-25,42),Range(-2.5f,weatherCeiling));
                Color color=accent;float size=Range(.03125f,.07f),speed=Range(.06f,.2f);
                switch(activeTheme)
                {
                    case TowerThemeId.Asgard: color=Hex("d8e8f4");speed=Range(.35f,.9f);break;
                    case TowerThemeId.Eden: color=i%3==0?Hex("f6d2e0"):Hex("fff2b8");size=Range(.05f,.09f);speed=Range(.25f,.6f);break;
                    case TowerThemeId.Chinese: color=i%4==0?Hex("ffd27a"):Hex("f7b8c8");size=Range(.05f,.09f);speed=Range(.25f,.6f);break;
                    case TowerThemeId.Veda: color=i%3==0?Hex("ffe2a0"):Hex("ff9a4a");speed=Range(.12f,.45f);break;
                    case TowerThemeId.Papyrus: color=Hex("f0d8a0");speed=Range(.08f,.25f);break;
                    case TowerThemeId.Dangun: color=Hex("e4e8ea");size=Range(.06f,.12f);speed=Range(.05f,.15f);break;
                    case TowerThemeId.Hell: color=i%3==0?Hex("ffb050"):Hex("ff6a2a");speed=Range(.3f,.8f);break;
                    case TowerThemeId.Abyss: color=i%2==0?Hex("a01a30"):Hex("6a2a7a");size=Range(.05f,.1f);speed=Range(.1f,.3f);break;
                    case TowerThemeId.Village: color=i%3==0?Hex("fff2b0"):Hex("ffd27a");size=Range(.05f,.08f);speed=Range(.1f,.3f);break;
                    case TowerThemeId.Olympus: AddMote(Cloud(pos.x,pos.y,Range(.6f,1.5f),new Color(1,.96f,.9f,.32f),-39),Range(.1f,.3f));continue;
                }
                color.a=Range(.22f,.52f);
                AddMote(Box("Ambient "+activeTheme+" pixel",pos,Vector2.one*size,color,-30),speed);
            }
        }
        private void AddMote(GameObject go,float speed)
        {motes.Add(new AmbientPixel{transform=go.transform,origin=go.transform.localPosition,phase=Range(0,6.28f),speed=speed});}
        private GameObject Cloud(float x,float y,float width,Color color,int order)
        {
            Sprite s;if(!themeSprites.TryGetValue("cloud",out s))
            {
                s=PixelSprite(64,24,32,(px,py)=>
                {
                    if(!CloudSilhouette(px,py))return Color.clear;
                    float a=((px-15)*(px-15)+(py-12)*(py-12))/100f;
                    float b=((px-31)*(px-31)+(py-14)*(py-14))/169f;
                    float c=((px-48)*(px-48)+(py-12)*(py-12))/100f;
                    float cx=a<=b&&a<=c?15:b<=c?31:48;
                    float cy=cx==31?14:12,radius=cx==31?13:10;
                    float nx=(px-cx)/radius,ny=(py-cy)/radius;
                    float light=-nx*.4f+ny*.7f-(nx*nx+ny*ny)*.2f;
                    float value=light>.15f?1:light>-.2f?.85f:.70f;
                    if(!CloudSilhouette(px,py-1))value=.52f;
                    else if(!CloudSilhouette(px+1,py))value=.70f;
                    else if(py==23||!CloudSilhouette(px,py+1)||!CloudSilhouette(px-1,py))value=1;
                    return new Color(value,value,value,1);
                });themeSprites.Add("cloud",s);
            }
            return Box("Layered pixel cloud",new Vector2(x,y),new Vector2(width/2,width/2),color,order,s);
        }
        private static bool CloudSilhouette(int x,int y)
        {return (x>4&&x<60&&y>4&&y<13)||Circle(x,y,15,12,10)||Circle(x,y,31,14,13)||Circle(x,y,48,12,10);}
        private static bool Circle(int x,int y,int cx,int cy,int r){return (x-cx)*(x-cx)+(y-cy)*(y-cy)<r*r;}
    }
}
