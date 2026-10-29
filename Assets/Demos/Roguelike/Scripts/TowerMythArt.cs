using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>Myth portraits and animation. Every companion is a chibi pixel god (TowerGodPixelArt)
    /// drawn in the style of chuchuking's Zeus.</summary>
    public sealed class TowerMythArt : MonoBehaviour
    {
        public const int FirstArtIndex=100,CharacterCount=50,Columns=5;
        /// <summary>Height of the square that frames one companion in review captures.</summary>
        public const float CellHeight=2.2f;
        static readonly string[] Sheets={"eden","olympus","asgard","india","egypt","china","korea","solomon","ireland","zodiac"};
        static readonly string[] Names={
            "Michael","Gabriel","Raphael","Uriel","Metatron",
            "Zeus","Poseidon","Hades","Athena","Artemis",
            "Odin","Thor","Loki","Freyja","Tyr",
            "Shiva","Vishnu","Brahma","Ganesha","Hanuman",
            "Ra","Osiris","Isis","Anubis","Horus",
            "Sun Wukong","Nezha","Erlang Shen","Guan Yu","Nuwa",
            "Dangun","Jumong","Hwanung","Princess Bari","Jacheongbi",
            "Bael","Agares","Vassago","Paimon","Asmodeus",
            "Cu Chulainn","Fionn mac Cumhaill","Lugh","Morrigan","Dagda",
            "Rat Champion","Ox Champion","Tiger Champion","Rabbit Champion","Dragon Champion"};
        static readonly Dictionary<int,Texture2D> portraits=new Dictionary<int,Texture2D>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCaches(){portraits.Clear();}
        TowerGodPixelArt pixel;
        public int CurrentFrameRow { get; private set; }
        public float RenderedHeight { get { return pixel==null?0:pixel.RenderedHeight; } }
        public TowerGodPixelArt Pixel { get { return pixel; } }
        public static bool IsMythIndex(int artIndex){return artIndex>=FirstArtIndex&&artIndex<FirstArtIndex+CharacterCount;}
        public static string ProfileName(int artIndex){return Names[Ordinal(artIndex)];}
        /// <summary>The mythology a companion belongs to.</summary>
        public static string SheetId(int artIndex){return Sheets[Ordinal(artIndex)/Columns];}
        static int Ordinal(int artIndex)
        {
            if(!IsMythIndex(artIndex))throw new ArgumentOutOfRangeException("artIndex",artIndex,"Myth art indices are 100 through 149.");
            return artIndex-FirstArtIndex;
        }
        public static void ValidateLibrary()
        {
            TowerGodPixelArt.ValidateLibrary();
            for(int index=FirstArtIndex;index<FirstArtIndex+CharacterCount;index++)
                if(!TowerGodPixelArt.Contains(index))throw new InvalidOperationException("Missing pixel god: "+index);
        }
        public void Build(int artIndex)
        {
            Ordinal(artIndex);
            pixel=gameObject.AddComponent<TowerGodPixelArt>();pixel.Build(artIndex);
        }
        public void Pose(string motion,bool moving,float time,float actionProgress,float hurtTime,int facing,Color tint,
            bool grounded,float verticalVelocity,float horizontalSpeed,float horizontalVelocity,float dt,float releaseFraction)
        {
            CurrentFrameRow=motion=="Death"?5:motion=="Hurt"?4:motion=="Cast"?3:motion=="Attack"?2:moving||!grounded?1:0;
            pixel.Pose(motion,moving,time,actionProgress,hurtTime,facing,tint,grounded,verticalVelocity,horizontalSpeed,dt,releaseFraction);
        }
        public static Sprite PortraitSprite(int artIndex){return TowerGodPixelArt.CardSprite(artIndex);}
        public static Texture2D Portrait(int artIndex)
        {
            Texture2D cached;
            if(portraits.TryGetValue(artIndex,out cached))
            {
                if(cached!=null)return cached;
                portraits.Remove(artIndex);
            }
            var root=new GameObject("Myth portrait "+ProfileName(artIndex)){hideFlags=HideFlags.HideAndDontSave};root.layer=31;root.transform.position=new Vector3(10000,10000,0);
            var sprite=root.AddComponent<SpriteRenderer>();sprite.sprite=PortraitSprite(artIndex);
            var cameraObject=new GameObject("Myth portrait camera"){hideFlags=HideFlags.HideAndDontSave};var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            var bounds=sprite.sprite.bounds;float extent=Mathf.Max(bounds.size.x,bounds.size.y);
            camera.orthographic=true;camera.orthographicSize=extent*.5f;camera.aspect=1;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<31;camera.allowHDR=false;camera.allowMSAA=false;
            camera.transform.position=root.transform.position+new Vector3(bounds.center.x,bounds.center.y,-10);
            var target=new RenderTexture(128,128,0,RenderTextureFormat.ARGB32){filterMode=FilterMode.Point,antiAliasing=1};target.Create();camera.targetTexture=target;
            var previous=RenderTexture.active;camera.Render();RenderTexture.active=target;
            cached=new Texture2D(128,128,TextureFormat.RGBA32,false){name="Myth portrait "+ProfileName(artIndex),filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};cached.ReadPixels(new Rect(0,0,128,128),0,0,false);cached.Apply(false,false);
            RenderTexture.active=previous;camera.targetTexture=null;root.SetActive(false);target.Release();Destroy(target);Destroy(cameraObject);Destroy(root);
            portraits.Add(artIndex,cached);return cached;
        }
    }
}
