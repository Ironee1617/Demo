using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // All masonry, windows, banners and torches are regions of the supplied castle pixel atlases.
    public sealed class TowerWorld
    {
        public struct Ledge { public float x,y,width; public Ledge(float x,float y,float width){this.x=x;this.y=y;this.width=width;} }
        public readonly Ledge[] Ledges={new Ledge(-3,-1.2f,5),new Ledge(6,.1f,5),new Ledge(15,-.5f,5)};
        public const float Ground=-3f,Left=-16f,Right=29f,ExitX=26f;
        private readonly Transform root,portal;
        private readonly List<SpriteRenderer> torches=new List<SpriteRenderer>();
        private readonly List<Transform> motes=new List<Transform>();
        private readonly Sprite[] torchFrames=new Sprite[4];
        private readonly Dictionary<string,Sprite> atlasRegions=new Dictionary<string,Sprite>();
        private static Sprite square,disc,glow;
        public static readonly Color Gold=new Color(.91f,.72f,.36f),Teal=new Color(.34f,.88f,.79f);
        public static Sprite Disc {get{EnsureTextures();return disc;}}
        public static Sprite Glow {get{EnsureTextures();return glow;}}
        private float clock;
        public TowerWorld(Transform parent)
        {
            EnsureTextures();root=new GameObject("Original castle pixel chamber").transform;root.SetParent(parent);
            Box("Night",new Vector2(6,2),new Vector2(110,38),new Color(.035f,.045f,.065f),-95);
            Sprite brick=Region("CastleInterior",0,160,64,64);
            Sprite floor=Region("CastleInterior",32,1152,32,32);
            Sprite lip=Region("CastleInterior",32,160,32,16);
            Sprite window=Region("CastleInterior",224,288,96,128);
            Sprite pillar=Region("CastleInterior",416,112,32,160);
            Sprite banner=Region("CastleDecor",0,224,96,96);
            for(int row=0;row<6;row++)for(int col=0;col<26;col++)
                Original("Castle masonry",brick,new Vector2(-18+col*2,-2+row*2),new Color(.57f,.62f,.7f),-85);
            for(int i=0;i<9;i++)
            {
                float x=-18+i*6;
                Original("Original arched window",window,new Vector2(x+3,.9f),new Color(.8f,.85f,.92f),-75);
                Original("Original pillar lower",pillar,new Vector2(x,-.5f),Color.white,-60);
                Original("Original pillar upper",pillar,new Vector2(x,4.5f),Color.white,-60);
                if(i%2==0)Original("Original hanging banner",banner,new Vector2(x+1.6f,4),Color.white,-58);
            }
            // A native 32px stone tile remains one world unit, including platform collision surfaces.
            for(int i=0;i<80;i++)
            {
                float x=-34+i;
                for(int row=0;row<3;row++)Original("Original floor tile",floor,new Vector2(x+.5f,Ground-.5f-row),Color.white,-20);
                Original("Original floor edge",lip,new Vector2(x+.5f,Ground-.25f),Color.white,-18);
            }
            foreach(var ledge in Ledges)
                for(int i=0;i<(int)ledge.width;i++)
                {
                    float x=ledge.x-ledge.width*.5f+i+.5f;
                    var tile=Original("Original platform stone",floor,new Vector2(x,ledge.y-.5f),Color.white,-15);
                    tile.transform.localScale=new Vector3(1,.5f,1);tile.transform.localPosition=new Vector3(x,ledge.y-.25f,0);
                    Original("Original platform edge",lip,new Vector2(x,ledge.y-.25f),Color.white,-14);
                }
            for(int i=0;i<4;i++)torchFrames[i]=Region("Torch",i*32,0,32,64);
            for(int i=0;i<9;i++)
            {var t=Original("Original animated torch",torchFrames[0],new Vector2(-18+i*6,.2f),Color.white,-48);torches.Add(t.GetComponent<SpriteRenderer>());}
            portal=new GameObject("Ascent gate").transform;portal.SetParent(root);portal.localPosition=new Vector3(ExitX,Ground,0);
            var door=Original("Original tower door",Region("CastleInterior",576,448,64,96),Vector2.zero,Color.white,-10);
            door.transform.SetParent(portal,false);door.transform.localPosition=new Vector3(0,1.5f,0);
            var veil=SpriteObject(portal,"Gate opening",new Vector2(0,1.5f),new Vector2(1.3f,2.6f),new Color(.2f,.9f,.8f,.25f),-9,glow);
            for(int i=0;i<24;i++)
            {var m=Box("Dust mote",new Vector2(Random.Range(-18f,31f),Random.Range(-2f,6f)),Vector2.one/32,new Color(.8f,.78f,.63f,.45f),-30);motes.Add(m.transform);}
        }
        private Sprite Region(string atlas,int left,int top,int width,int height)
        {
            string key=atlas+left+","+top+","+width+","+height;Sprite s;if(atlasRegions.TryGetValue(key,out s))return s;
            var texture=Resources.Load<Texture2D>("Tower/Environment/"+atlas);
            if(texture==null)throw new System.InvalidOperationException("Missing original castle atlas: "+atlas);
            s=Sprite.Create(texture,new Rect(left,texture.height-top-height,width,height),new Vector2(.5f,.5f),32);atlasRegions.Add(key,s);return s;
        }
        private GameObject Original(string name,Sprite sprite,Vector2 position,Color color,int order)
        {var go=SpriteObject(root,name,position,Vector2.one,color,order,sprite);return go;}
        public void Tick(float dt,bool open)
        {
            clock+=dt;int frame=(int)(clock*8)%torchFrames.Length;foreach(var t in torches)t.sprite=torchFrames[frame];
            foreach(var m in motes){var p=m.position;p.y+=dt*.08f;if(p.y>7)p.y=-2.5f;m.position=p;}
            foreach(var r in portal.GetComponentsInChildren<SpriteRenderer>())
            {var c=r.color;c.a=r.name=="Gate opening"?(open?.85f:.07f):1;r.color=c;}
        }
        public GameObject Box(string name,Vector2 pos,Vector2 size,Color color,int order,Sprite sprite=null)
        {return SpriteObject(root,name,pos,size,color,order,sprite);}
        public static GameObject SpriteObject(Transform parent,string name,Vector2 pos,Vector2 size,Color color,int order,Sprite sprite=null)
        {
            EnsureTextures();var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=new Vector3(size.x,size.y,1);
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprite??square;renderer.color=color;renderer.sortingOrder=order;return go;
        }
        private static void EnsureTextures()
        {
            if(square!=null)return;
            square=Make(1,(x,y)=>Color.white);
            disc=Make(32,(x,y)=>Vector2.Distance(new Vector2(x,y),new Vector2(.5f,.5f))<=.48f?Color.white:Color.clear);
            glow=Make(32,(x,y)=>{float d=Vector2.Distance(new Vector2(x,y),new Vector2(.5f,.5f))*2;return new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-d),2));});
        }
        private static Sprite Make(int n,System.Func<float,float,Color> pixel)
        {var t=new Texture2D(n,n,TextureFormat.RGBA32,false);t.filterMode=FilterMode.Point;for(int y=0;y<n;y++)for(int x=0;x<n;x++)t.SetPixel(x,y,pixel(x/(float)n,y/(float)n));t.Apply();return Sprite.Create(t,new Rect(0,0,n,n),new Vector2(.5f,.5f),n);}
    }
}
