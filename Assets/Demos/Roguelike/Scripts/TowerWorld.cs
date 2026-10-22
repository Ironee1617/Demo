using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerWorld
    {
        public struct Ledge { public float x,y,width; public Ledge(float x,float y,float width){this.x=x;this.y=y;this.width=width;} }
        public Ledge[] Ledges { get; private set; } = new Ledge[0];
        public float HighestPlatform { get; private set; }
        public const float Ground=-3f,Left=-16f,Right=29f,ExitX=26f;
        private readonly Transform root;
        private Transform environment,portal;
        private SpriteRenderer gateOpening;
        private bool gateOpen;
        private readonly List<AmbientPixel> motes=new List<AmbientPixel>();
        private readonly List<SpriteRenderer> pulses=new List<SpriteRenderer>();
        private static readonly Dictionary<string,Sprite> themeSprites=new Dictionary<string,Sprite>();
        private static Sprite square,disc,glow;
        private System.Random decorationRandom;
        private TowerThemeId activeTheme;
        public TowerThemeId CurrentTheme { get { return activeTheme; } }
        public int CurrentFloor { get; private set; }
        public TowerPlatformPattern CurrentPattern { get; private set; }
        private Color accent;
        private float clock,weatherCeiling=8;
        private sealed class AmbientPixel { public Transform transform; public Vector2 origin; public float phase,speed; }
        public static readonly Color Gold=new Color(.91f,.72f,.36f),Teal=new Color(.34f,.88f,.79f);
        public static Sprite Disc {get{EnsureTextures();return disc;}}
        public static Sprite Glow {get{EnsureTextures();return glow;}}

        public TowerWorld(Transform parent)
        {
            EnsureTextures();root=new GameObject("Tower themed chamber").transform;root.SetParent(parent,false);
            SetTheme(TowerThemeId.Ruins,1);
        }
        public void SetTheme(TowerThemeId theme,int floor,int runSeed=0)
        {
            // Only the generated environment is retired; actors and their effects belong to the game.
            if(environment!=null){environment.gameObject.SetActive(false);Object.Destroy(environment.gameObject);}
            motes.Clear();pulses.Clear();clock=0;activeTheme=theme;CurrentFloor=floor;
            CurrentPattern=TowerPlatformCatalog.Select(runSeed,floor);
            Ledges=new Ledge[CurrentPattern.Platforms.Count];
            HighestPlatform=Ground;
            for(int i=0;i<Ledges.Length;i++){var p=CurrentPattern.Platforms[i];Ledges[i]=new Ledge(p.X,p.Y,p.Width);HighestPlatform=Mathf.Max(HighestPlatform,p.Y);}
            weatherCeiling=Mathf.Max(8,HighestPlatform+6);
            decorationRandom=new System.Random(unchecked(runSeed*397^floor*7411^(int)theme*57803));
            environment=new GameObject(theme+" environment · floor "+floor).transform;environment.SetParent(root,false);
            BuildTheme(theme);BuildGate();BuildWeather();
        }
        public void Tick(float dt,bool open)
        {
            clock+=dt;
            foreach(var mote in motes)
            {
                Vector2 p=mote.origin;float travel=clock*mote.speed;
                if(activeTheme==TowerThemeId.Frozen){p.x+=Mathf.Sin(clock*.5f+mote.phase)*.4f;p.y=weatherCeiling-Mathf.Repeat(weatherCeiling-p.y+travel,weatherCeiling+3);}
                else if(activeTheme==TowerThemeId.Water){p.x+=Mathf.Sin(clock*1.4f+mote.phase)*.22f;p.y+=Mathf.Sin(clock*2+mote.phase)*.035f;}
                else if(activeTheme==TowerThemeId.Cloud){p.x=-25+Mathf.Repeat(p.x+25+travel,67);p.y+=Mathf.Sin(clock*.4f+mote.phase)*.08f;}
                else {p.x+=Mathf.Sin(clock*.7f+mote.phase)*.18f;p.y=-2.6f+Mathf.Repeat(p.y+2.6f+travel,weatherCeiling+2.6f);}
                mote.transform.localPosition=new Vector3(Mathf.Round(p.x*32)/32,Mathf.Round(p.y*32)/32,0);
            }
            foreach(var pulse in pulses){var c=pulse.color;c.a=.4f+Mathf.Sin(clock*1.6f+pulse.transform.localPosition.x)*.12f;pulse.color=c;}
            if(gateOpening!=null&&gateOpen!=open)
            {gateOpen=open;var c=gateOpening.color;c.a=open?.88f:.12f;gateOpening.color=c;}
            TickFeatures(open);
        }
        public GameObject Box(string name,Vector2 pos,Vector2 size,Color color,int order,Sprite sprite=null)
        {return SpriteObject(environment!=null?environment:root,name,pos,size,color,order,sprite);}
        public static GameObject SpriteObject(Transform parent,string name,Vector2 pos,Vector2 size,Color color,int order,Sprite sprite=null)
        {
            EnsureTextures();var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=new Vector3(size.x,size.y,1);
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprite??square;renderer.color=color;renderer.sortingOrder=order;return go;
        }
        private float Range(float min,float max){return min+(float)decorationRandom.NextDouble()*(max-min);}
        private static Color Hex(string value){Color c;ColorUtility.TryParseHtmlString("#"+value,out c);return c;}
        private static void EnsureTextures()
        {
            if(square!=null)return;
            square=Make(1,(x,y)=>Color.white);
            disc=Make(32,(x,y)=>Vector2.Distance(new Vector2(x,y),new Vector2(.5f,.5f))<=.48f?Color.white:Color.clear);
            glow=Make(32,(x,y)=>{float d=Vector2.Distance(new Vector2(x,y),new Vector2(.5f,.5f))*2;return new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-d),2));});
        }
        private static Sprite Make(int n,System.Func<float,float,Color> pixel)
        {return PixelSprite(n,n,n,(x,y)=>pixel(x/(float)n,y/(float)n));}
        private static Sprite PixelSprite(int width,int height,float ppu,System.Func<int,int,Color> pixel)
        {
            var t=new Texture2D(width,height,TextureFormat.RGBA32,false);t.filterMode=FilterMode.Point;t.wrapMode=TextureWrapMode.Clamp;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)t.SetPixel(x,y,pixel(x,y));
            t.Apply();return Sprite.Create(t,new Rect(0,0,width,height),new Vector2(.5f,.5f),ppu);
        }
    }
}
