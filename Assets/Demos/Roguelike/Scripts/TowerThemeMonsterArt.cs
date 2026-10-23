using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>How a borrowed creature is regraded: hell burns the ember board in blood, the abyss turns hides to raw flesh.</summary>
    public enum TowerCreatureGrade { None, Hellfire, Flesh }

    /// <summary>Where a theme's creature comes from: a painted board column, its regrade, or the final boss painting.</summary>
    public struct TowerCreatureSource
    {
        public string Sheet; public int Column; public TowerCreatureGrade Grade; public bool Final;
        /// <summary>Row of the board in the original board order, which keys the creature anatomy tables.</summary>
        public int Row { get { return TowerThemeMonsterArt.BoardRow(Sheet); } }
    }

    /// <summary>Measured creature cutouts and panoramas from the seven painted pixel atlases, plus the final boss.</summary>
    public static class TowerThemeMonsterArt
    {
        static readonly Dictionary<string,Sprite> Cache=new Dictionary<string,Sprite>();
        static readonly Dictionary<string,Sprite> Backgrounds=new Dictionary<string,Sprite>();
        // The painted boards keep their original file names; each mythology borrows the board whose
        // creatures fit it (TowerThemeId order): Olympus=cloud, Asgard=frozen, Dangun=rock, Eden=jungle,
        // Veda=water, Papyrus=ruins, Chinese=lava. Hell regrades the ember board's imps, hounds and warlocks,
        // and the abyss stitches its brood from several boards. The rig anatomy tables are indexed by board row.
        static readonly string[] Sheets={"cloud","frozen","rock","jungle","water","ruins","lava"};
        static readonly string[] BoardOrder={"lava","rock","jungle","frozen","water","cloud","ruins"};
        static readonly string[,] Names={
            {"MistSlime","StormBird","CloudRam","WindSprite","LightningKnight","SkyMage","TempestRoc"},
            {"SnowSlime","FrostWolf","IceSentinel","IcicleSpider","SnowShaman","GlacierYeti","FrostDragon"},
            {"PebbleCrawler","CrystalBeetle","StoneSentinel","CaveScorpion","ShardShaman","IronbackTroll","MountainTitan"},
            {"MossSlime","ThornMantis","VineGuardian","VenomFrog","JunglePanther","SporeShaman","AncientTreant"},
            {"TideSlime","ShellCrab","CoralGuard","SpearMerman","ElectricJellyfish","AbyssMage","AbyssKraken"},
            {"SandScarab","BrokenSentinel","TombShade","BoneArcher","CursedStatue","RuinPriest","FallenPharaoh"},
            {"EmberSlime","AshImp","BasaltGolem","FlameLizard","LavaHound","ObsidianMage","MoltenColossus"}};
        // The abyss slots: a flayed mantis, an eyed jellyfish, a flesh troll, an eye spider, the wailing wraith and the apostle.
        static readonly string[] AbyssSheets={"jungle","water","rock","rock","ruins","water"};
        static readonly int[] AbyssColumns={1,4,5,0,2,5};
        public const string FinalBossPath="Tower/Story/nihilos";

        /// <summary>File stem of the painted creature board a theme mostly borrows.</summary>
        public static string Sheet(TowerThemeId theme)
        {return theme==TowerThemeId.Hell?"lava":theme==TowerThemeId.Abyss?"water":theme==TowerThemeId.Village?"cloud":Sheets[Mathf.Clamp((int)theme,0,6)];}
        public static int BoardRow(string sheet){return Mathf.Max(0,Array.IndexOf(BoardOrder,sheet));}
        public static int BoardRow(TowerThemeId theme){return BoardRow(Sheet(theme));}
        public static TowerCreatureSource Source(TowerThemeId theme,int variant,bool boss)
        {
            int column=Mathf.Clamp(variant,0,5);
            if(theme==TowerThemeId.Abyss)
            {
                if(boss&&variant==TowerThemeCatalog.FinalVariant)return new TowerCreatureSource{Sheet="nihilos",Column=6,Final=true};
                return boss?new TowerCreatureSource{Sheet="water",Column=6,Grade=TowerCreatureGrade.Flesh}:
                    new TowerCreatureSource{Sheet=AbyssSheets[column],Column=AbyssColumns[column],Grade=TowerCreatureGrade.Flesh};
            }
            return new TowerCreatureSource{Sheet=Sheet(theme),Column=boss?6:column,Grade=theme==TowerThemeId.Hell?TowerCreatureGrade.Hellfire:TowerCreatureGrade.None};
        }
        [Serializable] sealed class LayoutFile { public AtlasLayout[] atlases; }
        [Serializable] sealed class AtlasLayout
        { public string id; public int sourceWidth,sourceHeight; public CreatureRect[] monsters; public CreatureRect background; }
        [Serializable] sealed class CreatureRect { public int x,y,width,height; }
        static Dictionary<string,AtlasLayout> layouts;

        public static string Name(TowerThemeId theme,int variant,bool boss)
        {
            var source=Source(theme,variant,boss);
            if(source.Final)return "Nihilos";
            string name=Names[Array.IndexOf(Sheets,source.Sheet),source.Column];
            return source.Grade==TowerCreatureGrade.Hellfire?"Infernal"+name:source.Grade==TowerCreatureGrade.Flesh?"Flayed"+name:name;
        }
        public static Sprite Get(TowerThemeId theme,int variant,bool boss)
        {
            var source=Source(theme,variant,boss);string key=source.Sheet+":"+source.Column+":"+source.Grade;
            Sprite sprite;if(Cache.TryGetValue(key,out sprite)&&sprite!=null)return sprite;
            if(source.Final)sprite=FinalBoss();
            else{var layout=Layout(source.Sheet);sprite=Slice(source.Sheet,layout,layout.monsters[source.Column],Name(theme,variant,boss),true,source.Grade);}
            Cache[key]=sprite;return sprite;
        }
        static Sprite FinalBoss()
        {
            var texture=Resources.Load<Texture2D>(FinalBossPath);
            if(texture==null)throw new InvalidOperationException("Missing final boss painting: "+FinalBossPath);
            texture.wrapMode=TextureWrapMode.Clamp;
            var sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),1,0,SpriteMeshType.FullRect);
            sprite.name="Nihilos";return sprite;
        }
        public static Sprite Background(TowerThemeId theme)
        {
            // Until a chapter has its own backdrop, hell shows the ember board's burning halls and the abyss the dark cavern.
            string sheet=theme==TowerThemeId.Abyss?"rock":Sheet(theme);Sprite sprite;
            if(Backgrounds.TryGetValue(sheet,out sprite))return sprite;
            var layout=Layout(sheet);
            sprite=Slice(sheet,layout,layout.background,sheet+" authored panorama");Backgrounds.Add(sheet,sprite);return sprite;
        }
        public static Texture2D SourceAtlas(TowerThemeId theme)
        {return Atlas(Sheet(theme));}
        static Texture2D Atlas(string sheet)
        {
            string path="Tower/Themes/"+sheet;var texture=Resources.Load<Texture2D>(path);
            if(texture==null)throw new InvalidOperationException("Missing theme artwork atlas: "+path);
            texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;return texture;
        }
        static Sprite Slice(string sheet,AtlasLayout layout,CreatureRect cut,string name,bool isolate=false,TowerCreatureGrade grade=TowerCreatureGrade.None)
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
            if(isolate)return CreatureCutout(texture,bounds,name,grade);
            var sprite=Sprite.Create(texture,bounds,new Vector2(.5f,.5f),1,0,SpriteMeshType.FullRect);
            sprite.name=name;return sprite;
        }
        static Sprite CreatureCutout(Texture2D atlas,Rect bounds,string name,TowerCreatureGrade grade)
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
            if(grade!=TowerCreatureGrade.None)for(int i=0;i<pixels.Length;i++)pixels[i]=Regrade(pixels[i],grade);
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
            {name=name+" isolated pixels",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(pixels);texture.Apply(false,true);
            var sprite=Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(.5f,.5f),1,0,SpriteMeshType.FullRect);
            sprite.name=name;return sprite;
        }
        // Lava oranges sink to blood crimson over blacker basalt; hides become raw flesh, bruise and bone.
        static readonly Color[] FleshRamp={new Color(.03f,.01f,.03f),new Color(.16f,.03f,.08f),new Color(.38f,.09f,.16f),new Color(.59f,.2f,.26f),new Color(.77f,.41f,.41f),new Color(.89f,.67f,.59f),new Color(.96f,.91f,.84f)};
        static Color32 Regrade(Color32 pixel,TowerCreatureGrade grade)
        {
            if(pixel.a==0)return pixel;
            Color c=pixel;float h,s,v;Color.RGBToHSV(c,out h,out s,out v);
            bool glow=s>.5f&&v>.78f;
            Color result;
            if(grade==TowerCreatureGrade.Hellfire)
            {
                if(h<.18f)h*=.28f;else if(h>.5f&&h<.92f)h=Mathf.Lerp(h,.97f,.6f);
                if(!glow){v=Mathf.Pow(v,1.18f);if(s<.35f)v*=.82f;}
                result=Color.HSVToRGB(h,Mathf.Clamp01(s*1.12f),v);
                if(glow)result=Color.Lerp(result,new Color(1,.62f,.42f),Mathf.InverseLerp(.85f,1,v)*.5f);
            }
            else
            {
                float luma=c.r*.299f+c.g*.587f+c.b*.114f,at=Mathf.Clamp01(Mathf.Pow(luma,.9f))*(FleshRamp.Length-1);
                int i=Mathf.Min(FleshRamp.Length-2,(int)at);
                result=Color.Lerp(FleshRamp[i],FleshRamp[i+1],at-i);
                // Bright glowing parts (eyes, crystals, lamps) burn a sick red instead.
                if(glow)result=Color.Lerp(result,new Color(1,.24f+(v-.78f)*1.6f,.16f),.8f);
            }
            result.a=c.a;return result;
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
