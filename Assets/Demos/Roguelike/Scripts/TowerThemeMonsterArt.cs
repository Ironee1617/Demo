using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>Measured creature cutouts and panoramas from the seven painted pixel atlases.</summary>
    public static class TowerThemeMonsterArt
    {
        static readonly Dictionary<int,Sprite> Cache=new Dictionary<int,Sprite>();
        static readonly Dictionary<int,Sprite> Backgrounds=new Dictionary<int,Sprite>();
        static readonly string[] Sheets={"lava","rock","jungle","frozen","water","cloud","ruins"};
        static readonly string[,] Names={
            {"EmberSlime","AshImp","BasaltGolem","FlameLizard","LavaHound","ObsidianMage","MoltenColossus"},
            {"PebbleCrawler","CrystalBeetle","StoneSentinel","CaveScorpion","ShardShaman","IronbackTroll","MountainTitan"},
            {"MossSlime","ThornMantis","VineGuardian","VenomFrog","JunglePanther","SporeShaman","AncientTreant"},
            {"SnowSlime","FrostWolf","IceSentinel","IcicleSpider","SnowShaman","GlacierYeti","FrostDragon"},
            {"TideSlime","ShellCrab","CoralGuard","SpearMerman","ElectricJellyfish","AbyssMage","AbyssKraken"},
            {"MistSlime","StormBird","CloudRam","WindSprite","LightningKnight","SkyMage","TempestRoc"},
            {"SandScarab","BrokenSentinel","TombShade","BoneArcher","CursedStatue","RuinPriest","FallenPharaoh"}};
        [Serializable] sealed class LayoutFile { public AtlasLayout[] atlases; }
        [Serializable] sealed class AtlasLayout
        { public string id; public int sourceWidth,sourceHeight; public CreatureRect[] monsters; public CreatureRect background; }
        [Serializable] sealed class CreatureRect { public int x,y,width,height; }
        static Dictionary<string,AtlasLayout> layouts;

        public static string Name(TowerThemeId theme,int variant,bool boss)
        {return Names[Mathf.Clamp((int)theme,0,6),boss?6:Mathf.Clamp(variant,0,5)];}
        public static Sprite Get(TowerThemeId theme,int variant,bool boss)
        {
            int row=Mathf.Clamp((int)theme,0,6),column=boss?6:Mathf.Clamp(variant,0,5),key=row*7+column;
            Sprite sprite;if(Cache.TryGetValue(key,out sprite))return sprite;
            string sheet=Sheets[row];var layout=Layout(sheet);
            sprite=Slice(sheet,layout,layout.monsters[column],Name(theme,variant,boss),true);Cache.Add(key,sprite);return sprite;
        }
        public static Sprite Background(TowerThemeId theme)
        {
            int row=Mathf.Clamp((int)theme,0,6);Sprite sprite;
            if(Backgrounds.TryGetValue(row,out sprite))return sprite;
            string sheet=Sheets[row];var layout=Layout(sheet);
            sprite=Slice(sheet,layout,layout.background,((TowerThemeId)row)+" authored panorama");Backgrounds.Add(row,sprite);return sprite;
        }
        public static Texture2D SourceAtlas(TowerThemeId theme)
        {return Atlas(Sheets[Mathf.Clamp((int)theme,0,6)]);}
        static Texture2D Atlas(string sheet)
        {
            string path="Tower/Themes/"+sheet;var texture=Resources.Load<Texture2D>(path);
            if(texture==null)throw new InvalidOperationException("Missing theme artwork atlas: "+path);
            texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;return texture;
        }
        static Sprite Slice(string sheet,AtlasLayout layout,CreatureRect cut,string name,bool isolate=false)
        {
            if(cut==null||cut.width<=0||cut.height<=0)throw new InvalidOperationException("Missing theme artwork bounds: "+name);
            var texture=Atlas(sheet);
            // Measurements use the source image's top-left origin. Unity sprite rects start at the bottom left.
            int left=Mathf.RoundToInt(cut.x*texture.width/(float)layout.sourceWidth);
            int right=Mathf.RoundToInt((cut.x+cut.width)*texture.width/(float)layout.sourceWidth);
            int top=texture.height-Mathf.RoundToInt(cut.y*texture.height/(float)layout.sourceHeight);
            int bottom=texture.height-Mathf.RoundToInt((cut.y+cut.height)*texture.height/(float)layout.sourceHeight);
            if(left<0||bottom<0||right>texture.width||top>texture.height||right<=left||top<=bottom)
                throw new InvalidOperationException("Invalid theme artwork bounds: "+name);
            var bounds=new Rect(left,bottom,right-left,top-bottom);
            if(isolate)return CreatureCutout(texture,bounds,name);
            var sprite=Sprite.Create(texture,bounds,new Vector2(.5f,.5f),1,0,SpriteMeshType.FullRect);
            sprite.name=name;return sprite;
        }
        static Sprite CreatureCutout(Texture2D atlas,Rect bounds,string name)
        {
            int width=(int)bounds.width,height=(int)bounds.height,left=(int)bounds.x,bottom=(int)bounds.y;
            var source=atlas.GetPixels32();var pixels=new Color32[width*height];
            for(int y=0;y<height;y++)Array.Copy(source,(bottom+y)*atlas.width+left,pixels,y*width,width);
            // Tight atlas rectangles can overlap a neighboring silhouette. Keep the largest
            // eight-connected body without changing any retained source pixel's color or alpha.
            var labels=new int[pixels.Length];var queue=new int[pixels.Length];
            int component=0,largest=0,largestCount=0;
            for(int i=0;i<pixels.Length;i++)
            {
                if(pixels[i].a<8||labels[i]!=0)continue;
                component++;int head=0,tail=1;queue[0]=i;labels[i]=component;
                while(head<tail)
                {
                    int at=queue[head++],x=at%width,y=at/width;
                    for(int ny=Mathf.Max(0,y-1);ny<=Mathf.Min(height-1,y+1);ny++)
                    for(int nx=Mathf.Max(0,x-1);nx<=Mathf.Min(width-1,x+1);nx++)
                    {
                        int next=ny*width+nx;
                        if(labels[next]!=0||pixels[next].a<8)continue;
                        labels[next]=component;queue[tail++]=next;
                    }
                }
                if(tail>largestCount){largest=component;largestCount=tail;}
            }
            if(largestCount==0)throw new InvalidOperationException("Empty theme creature cutout: "+name);
            int minX=width,minY=height,maxX=0,maxY=0;
            for(int i=0;i<pixels.Length;i++)
            {
                if(labels[i]!=largest){pixels[i]=new Color32(0,0,0,0);continue;}
                int x=i%width,y=i/width;minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);
            }
            // Retire the removed fragments' empty margin as well, keeping the visible feet at the actor's ground anchor.
            int croppedWidth=maxX-minX+1,croppedHeight=maxY-minY+1;var cropped=new Color32[croppedWidth*croppedHeight];
            for(int y=0;y<croppedHeight;y++)Array.Copy(pixels,(minY+y)*width+minX,cropped,y*croppedWidth,croppedWidth);
            width=croppedWidth;height=croppedHeight;pixels=cropped;
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
            {name=name+" isolated pixels",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(pixels);texture.Apply(false,true);
            var sprite=Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(.5f,.5f),1,0,SpriteMeshType.FullRect);
            sprite.name=name;return sprite;
        }
        static AtlasLayout Layout(string sheet)
        {
            if(layouts==null)
            {
                var asset=Resources.Load<TextAsset>("Tower/Themes/AtlasLayout");
                if(asset==null)throw new InvalidOperationException("Missing measured theme artwork: Tower/Themes/AtlasLayout");
                layouts=new Dictionary<string,AtlasLayout>();
                foreach(var entry in JsonUtility.FromJson<LayoutFile>(asset.text).atlases)
                {
                    if(entry.sourceWidth<=0||entry.sourceHeight<=0||entry.monsters==null||entry.monsters.Length!=7)
                        throw new InvalidOperationException("Invalid theme creature layout: "+entry.id);
                    layouts.Add(entry.id,entry);
                }
            }
            AtlasLayout result;if(!layouts.TryGetValue(sheet,out result))throw new InvalidOperationException("Missing theme creature layout: "+sheet);
            return result;
        }
    }
}
