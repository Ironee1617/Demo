using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>Myth portraits and grounded joint animation using consistent idle artwork.</summary>
    public sealed class TowerMythArt : MonoBehaviour
    {
        public const int FirstArtIndex=100,CharacterCount=50,Columns=5,Rows=6;
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
        static readonly Dictionary<int,Sprite[]> frames=new Dictionary<int,Sprite[]>();
        static readonly Dictionary<int,Texture2D> portraits=new Dictionary<int,Texture2D>();
        static readonly Dictionary<int,Sprite> portraitSprites=new Dictionary<int,Sprite>();
        [Serializable] sealed class AtlasLayoutFile { public AtlasLayout[] atlases; }
        [Serializable] sealed class AtlasLayout
        { public string id; public int sourceWidth,sourceHeight; public int[] columnCuts,rowCuts,rowCutsByColumn; }
        static Dictionary<string,AtlasLayout> layouts;
        [Serializable] sealed class RigProfileFile { public TowerMythRig.Profile[] profiles; }
        static Dictionary<int,TowerMythRig.Profile> rigProfiles;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCaches()
        {
            frames.Clear(); portraits.Clear(); portraitSprites.Clear();
            layouts=null;rigProfiles=null;
        }
        TowerMythRig rig;
        TowerEdenArt eden;
        float lastPoseTime;
        int index;
        public int CurrentFrameRow { get; private set; }
        public float RenderedHeight
        {
            get
            {
                if(eden!=null)return eden.RenderedHeight;
                if(rig==null||rig.Renderer==null)return 0;
                // Health/name anchors follow the character, rather than an
                // overhead weapon's changing mesh bounds during a large swing.
                var top=rig.RestHeadPosition+Vector2.up*(rig.HeadRadii.y+.12f);
                return rig.Renderer.transform.TransformPoint(top).y-transform.position.y;
            }
        }
        public static bool IsMythIndex(int artIndex){return artIndex>=FirstArtIndex&&artIndex<FirstArtIndex+CharacterCount;}
        public static string ProfileName(int artIndex){return Names[Ordinal(artIndex)];}
        public static string SheetId(int artIndex){return Sheets[Ordinal(artIndex)/Columns];}
        static int Ordinal(int artIndex)
        {
            if(!IsMythIndex(artIndex))throw new ArgumentOutOfRangeException("artIndex",artIndex,"Myth art indices are 100 through 149.");
            return artIndex-FirstArtIndex;
        }
        public static void ValidateLibrary()
        {
            foreach(var sheet in Sheets)
            {
                var texture=Resources.Load<Texture2D>("Tower/Myths/"+sheet);
                if(texture==null)throw new InvalidOperationException("Missing humanoid myth atlas: Tower/Myths/"+sheet);
                if(texture.width<Columns||texture.height<Rows)throw new InvalidOperationException("Invalid 5 by 6 myth atlas: "+sheet);
            }
        }
        static AtlasLayout Layout(string sheet)
        {
            if(layouts==null)
            {
                layouts=new Dictionary<string,AtlasLayout>();
                var asset=Resources.Load<TextAsset>("Tower/Myths/AtlasLayout");
                if(asset!=null)foreach(var entry in JsonUtility.FromJson<AtlasLayoutFile>(asset.text).atlases)
                {
                    if(entry.sourceWidth<=0||entry.sourceHeight<=0||entry.columnCuts==null||entry.columnCuts.Length!=Columns+1||entry.rowCuts==null||entry.rowCuts.Length!=Rows+1)
                        throw new InvalidOperationException("Invalid myth atlas layout: "+entry.id);
                    if(entry.rowCutsByColumn!=null&&entry.rowCutsByColumn.Length!=0&&entry.rowCutsByColumn.Length!=Columns*(Rows+1))
                        throw new InvalidOperationException("Invalid myth column pose layout: "+entry.id);
                    layouts.Add(entry.id,entry);
                }
            }
            AtlasLayout result;return layouts.TryGetValue(sheet,out result)?result:null;
        }
        static Sprite[] Frames(int artIndex)
        {
            Sprite[] cached;
            if(frames.TryGetValue(artIndex,out cached))
            {
                bool valid=cached!=null&&cached.Length==Rows;
                if(valid)foreach(var sprite in cached)if(sprite==null||sprite.texture==null){valid=false;break;}
                if(valid)return cached;
                frames.Remove(artIndex);
            }
            int ordinal=Ordinal(artIndex),column=ordinal%Columns;
            string path="Tower/Myths/"+Sheets[ordinal/Columns];
            var texture=Resources.Load<Texture2D>(path);
            if(texture==null)throw new InvalidOperationException("Missing humanoid myth atlas: "+path);
            texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;
            cached=new Sprite[Rows];
            // Generated sheets can have taller casting rows. Measured cuts prevent adjacent poses from being clipped.
            var layout=Layout(Sheets[ordinal/Columns]);
            int left=layout==null?column*texture.width/Columns:Mathf.RoundToInt(layout.columnCuts[column]*texture.width/(float)layout.sourceWidth);
            int right=layout==null?(column+1)*texture.width/Columns:Mathf.RoundToInt(layout.columnCuts[column+1]*texture.width/(float)layout.sourceWidth);
            float pixelsPerUnit=texture.height/(float)Rows/CellHeight;
            for(int row=0;row<Rows;row++)
            {
                var rowCuts=layout==null?null:layout.rowCutsByColumn!=null&&layout.rowCutsByColumn.Length>0?layout.rowCutsByColumn:layout.rowCuts;
                int strip=layout!=null&&layout.rowCutsByColumn!=null&&layout.rowCutsByColumn.Length>0?column*(Rows+1):0;
                int bottom=layout==null?(Rows-row-1)*texture.height/Rows:texture.height-Mathf.RoundToInt(rowCuts[strip+row+1]*texture.height/(float)layout.sourceHeight);
                int top=layout==null?(Rows-row)*texture.height/Rows:texture.height-Mathf.RoundToInt(rowCuts[strip+row]*texture.height/(float)layout.sourceHeight);
                cached[row]=Sprite.Create(texture,new Rect(left,bottom,right-left,top-bottom),new Vector2(.5f,0),pixelsPerUnit,0,SpriteMeshType.FullRect);
                cached[row].name=Names[ordinal]+" "+new[]{"Idle","Run","Attack","Cast","Hurt","Death"}[row];
            }
            frames.Add(artIndex,cached);return cached;
        }
        public void Build(int artIndex)
        {
            index=artIndex;
            if(TowerEdenArt.Contains(index))
            {
                eden=gameObject.AddComponent<TowerEdenArt>();eden.Build(index);
                Pose("Idle",false,0,0,0,1,Color.white,true,0,0,0,0,.46f);
                return;
            }
            LoadRigProfiles();
            TowerMythRig.Profile anatomy;
            if(!rigProfiles.TryGetValue(index,out anatomy))throw new InvalidOperationException("Missing myth anatomy: "+index);
            var companion=TowerRun.Definitions[index-FirstArtIndex];
            rig=gameObject.AddComponent<TowerMythRig>();
            rig.Build(index,Frames(index)[0],anatomy,companion.Role);
            Pose("Idle",false,0,0,0,1,Color.white,true,0,0,0,0,.46f);
        }
        static void LoadRigProfiles()
        {
            if(rigProfiles==null)
            {
                rigProfiles=new Dictionary<int,TowerMythRig.Profile>();
                var asset=Resources.Load<TextAsset>("Tower/Myths/RigData");
                if(asset==null)throw new InvalidOperationException("Missing measured myth anatomy: Tower/Myths/RigData");
                foreach(var profile in JsonUtility.FromJson<RigProfileFile>(asset.text).profiles)rigProfiles.Add(profile.artIndex,profile);
            }
        }
        public void Pose(string motion,bool moving,float time,float actionProgress,float hurtTime,int facing,Color tint)
        {
            float dt=Mathf.Clamp(time-lastPoseTime,0,.05f);
            Pose(motion,moving,time,actionProgress,hurtTime,facing,tint,true,0,moving?5:0,moving?5*facing:0,dt,.46f);
        }
        public void Pose(string motion,bool moving,float time,float actionProgress,float hurtTime,int facing,Color tint,
            bool grounded,float verticalVelocity,float horizontalSpeed,float horizontalVelocity,float dt,float releaseFraction)
        {
            CurrentFrameRow=motion=="Death"?5:motion=="Hurt"?4:motion=="Cast"?3:motion=="Attack"?2:moving||!grounded?1:0;
            lastPoseTime=time;
            if(eden!=null)
            {
                eden.Pose(motion,moving,time,actionProgress,hurtTime,facing,tint,grounded,verticalVelocity,horizontalSpeed,dt,releaseFraction);
                return;
            }
            rig.ReleaseFraction=releaseFraction;
            rig.Pose(motion,moving,time,actionProgress,hurtTime,facing,tint,grounded,new Vector2(horizontalVelocity,verticalVelocity),horizontalSpeed,dt);
        }
        public static Sprite PortraitSprite(int artIndex)
        {
            if(TowerEdenArt.Contains(artIndex))return TowerEdenArt.Frames(artIndex)[0];
            Sprite cached;
            if(portraitSprites.TryGetValue(artIndex,out cached))
            {
                if(cached!=null&&cached.texture!=null)return cached;
                portraitSprites.Remove(artIndex);
            }
            var source=Frames(artIndex)[0];LoadRigProfiles();
            var profile=rigProfiles[artIndex];
            if(profile.sourceMaxX<=profile.sourceMinX)return source;
            // The rig retains the original cell coordinates. Portraits use a
            // separate bounded view so adjacent atlas fragments cannot reappear
            // in summon cards after being removed from the animated model.
            int left=Mathf.Clamp(Mathf.RoundToInt(profile.sourceMinX),0,Mathf.RoundToInt(source.rect.width));
            int right=Mathf.Clamp(Mathf.RoundToInt(profile.sourceMaxX),left+1,Mathf.RoundToInt(source.rect.width));
            var rect=new Rect(source.rect.x+left,source.rect.y,right-left,source.rect.height);
            cached=Sprite.Create(source.texture,rect,new Vector2(.5f,0),source.pixelsPerUnit,0,SpriteMeshType.FullRect);
            cached.name=source.name+" clean portrait";portraitSprites.Add(artIndex,cached);return cached;
        }
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
