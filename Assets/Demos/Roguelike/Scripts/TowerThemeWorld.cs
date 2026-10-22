using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerWorld
    {
        private void BuildTheme(TowerThemeId theme)
        {
            Color sky,horizon,stone,edge;
            switch(theme)
            {
                case TowerThemeId.Lava: sky=Hex("190f20");horizon=Hex("5d2630");stone=Hex("34303c");edge=Hex("b67a61");accent=Hex("ffad48");break;
                case TowerThemeId.Rock: sky=Hex("101725");horizon=Hex("263749");stone=Hex("42465b");edge=Hex("9294b3");accent=Hex("b5a5ff");break;
                case TowerThemeId.Jungle: sky=Hex("10252c");horizon=Hex("244d46");stone=Hex("47423b");edge=Hex("8aaf51");accent=Hex("bfe571");break;
                case TowerThemeId.Frozen: sky=Hex("243751");horizon=Hex("59798d");stone=Hex("50798d");edge=Hex("d1eef0");accent=Hex("9ee9ef");break;
                case TowerThemeId.Water: sky=Hex("203f5b");horizon=Hex("4d8090");stone=Hex("70594c");edge=Hex("d9bb7d");accent=Hex("8de0d2");break;
                case TowerThemeId.Cloud: sky=Hex("3b4f7f");horizon=Hex("7f94b0");stone=Hex("686482");edge=Hex("e7d8b7");accent=Hex("f4dfa1");break;
                default: sky=Hex("1c2430");horizon=Hex("384647");stone=Hex("55565a");edge=Hex("aaa58b");accent=Hex("8bd9be");break;
            }
            Box("Theme sky",new Vector2(6,3),new Vector2(110,38),sky,-100);
            for(int i=0;i<8;i++)Box("Pixel horizon band",new Vector2(6,-3+i*1.35f),new Vector2(100,1.38f),Color.Lerp(horizon,sky,i/8f),-98);
            bool authoredBackdrop=BuildAuthoredBackdrop(theme);
            if(!authoredBackdrop)BuildDistantLandscape(theme,sky,horizon);
            BuildTerrain(theme,stone,edge);
            if(authoredBackdrop){BuildForeground(theme);return;}
            switch(theme)
            {
                case TowerThemeId.Lava: BuildLava();break;
                case TowerThemeId.Rock: BuildCavern();break;
                case TowerThemeId.Jungle: BuildJungle();break;
                case TowerThemeId.Frozen: BuildFrozen();break;
                case TowerThemeId.Water: BuildWater();break;
                case TowerThemeId.Cloud: BuildClouds();break;
                default: BuildRuins();break;
            }
        }
        private void BuildTerrain(TowerThemeId theme,Color stone,Color edge)
        {
            bool openBelow=theme==TowerThemeId.Water||theme==TowerThemeId.Cloud;
            if(!openBelow)Box("Bedrock",new Vector2(6,Ground-3),new Vector2(100,5),Color.Lerp(stone,Color.black,.55f),-25);
            for(int i=0;i<80;i++)
            {
                float x=-34+i+.5f;
                Box(theme+" ground tile",new Vector2(x,Ground-.5f),Vector2.one,Color.white,-20,TerrainTile(theme,i%4));
                if(!openBelow)for(int j=0;j<2;j++)
                    Box("Buried strata",new Vector2(x,Ground-1.5f-j),Vector2.one,new Color(.63f,.63f,.68f),-22,TerrainTile(theme,(i+j+1)%4,true));
            }
            // A continuous edge shows the exact landing plane, including the boardwalk's transparent tiles.
            // The 96ppu atlas paints its own lit lip, so the marker becomes a thin translucent line.
            bool atlas=TerrainAtlas!=null;
            float line=atlas?2f/AtlasPpu:.0625f;Color lineColor=atlas?new Color(edge.r,edge.g,edge.b,.5f):edge;
            Box(theme+" ground walkable surface",new Vector2(6,Ground-line*.5f),new Vector2(80,line),lineColor,-17);
            foreach(var ledge in Ledges)
            {
                float left=ledge.x-ledge.width*.5f;
                for(int i=0;i<Mathf.CeilToInt(ledge.width);i++)
                {
                    float tileWidth=Mathf.Min(1,ledge.width-i);
                    Sprite tile=TerrainTile(theme,i%4,false,tileWidth,true);
                    // Crop the last tile before scaling so fractional-width ledges retain their full surface.
                    float tileScale=tileWidth/(tile.rect.width/tile.pixelsPerUnit);
                    Box(theme+" raised platform",new Vector2(left+i+tileWidth*.5f,ledge.y-.25f),new Vector2(tileScale,1),Color.white,-15,tile);
                }
                Box(theme+" platform walkable surface",new Vector2(ledge.x,ledge.y-line*.5f),new Vector2(ledge.width,line),lineColor,-13);
                if(atlas)
                {
                    // The atlas strip carries each theme's underside; only the ends and boardwalk posts are added.
                    float cap=16f/AtlasPpu;
                    Box(theme+" platform left end",new Vector2(left+cap*.5f,ledge.y-.25f),Vector2.one,Color.white,-14,EndCap(theme,false));
                    Box(theme+" platform right end",new Vector2(left+ledge.width-cap*.5f,ledge.y-.25f),Vector2.one,Color.white,-14,EndCap(theme,true));
                    if(theme==TowerThemeId.Water)BoardwalkSupports(ledge,left);
                    continue;
                }
                Box(theme+" platform front seam",new Vector2(ledge.x,ledge.y-.13f),new Vector2(ledge.width,.03125f),Color.Lerp(edge,stone,.6f),-14);
                int hangingCount=Mathf.Max(2,Mathf.FloorToInt(ledge.width/1.05f));
                if(theme==TowerThemeId.Water)BoardwalkSupports(ledge,left);
                else if(theme==TowerThemeId.Cloud)
                {
                    var cloud=Cloud(ledge.x,ledge.y-.64f,ledge.width*.94f,Hex("939bb6"),-24);
                    cloud.transform.localScale=new Vector3(cloud.transform.localScale.x,.85f,1);
                    Box("Sky platform gold inlay",new Vector2(ledge.x,ledge.y-.22f),new Vector2(ledge.width*.82f,.03125f),Hex("c3b995"),-14);
                }
                else if(theme==TowerThemeId.Jungle)
                {
                    Box("Root-bound platform base",new Vector2(ledge.x,ledge.y-.43f),new Vector2(ledge.width-.18f,.13f),Hex("514635"),-16);
                    for(int i=0;i<hangingCount;i++)Vine(left+.3f+(ledge.width-.6f)*(i+.5f)/hangingCount,ledge.y-.5f,Range(.35f,.85f),Hex("436648"),-24);
                }
                else if(theme==TowerThemeId.Frozen)
                    for(int i=0;i<hangingCount;i++)Spire("Platform icicle",left+(i+.5f)*ledge.width/hangingCount,ledge.y-.45f,Range(.2f,.48f),Hex("95cfda"),-23,true,.18f);
                else
                {
                    for(int i=0;i<hangingCount;i++)
                    {
                        float x=left+(i+.5f)*ledge.width/hangingCount;
                        if(theme==TowerThemeId.Lava)
                        {
                            Spire("Platform underside",x,ledge.y-.45f,Range(.25f,.55f),Color.Lerp(stone,Color.black,.28f),-23,true,.4f);
                            if(i%2==0)Box("Basalt platform ember seam",new Vector2(x,ledge.y-.3f),new Vector2(.18f,.03125f),Hex("aa6345"),-14);
                        }
                        else
                            Box(theme==TowerThemeId.Rock?"Cavern platform stone tooth":"Ruined platform corbel",new Vector2(x,ledge.y-.53f),new Vector2(.32f,.2f),Color.Lerp(stone,Color.black,.28f),-23);
                    }
                }
            }
        }
        private void BoardwalkSupports(Ledge ledge,float left)
        {
            int supportCount=Mathf.Max(2,Mathf.CeilToInt(ledge.width/2.4f));
            for(int i=0;i<supportCount;i++)
            {
                float x=Mathf.Lerp(left+.25f,left+ledge.width-.25f,i/(float)(supportCount-1));
                float height=ledge.y-Ground+.1f;
                Box("Raised boardwalk support",new Vector2(x,Ground+height*.5f-.2f),new Vector2(.16f,height),Hex("584b49"),-30);
                Box("Boardwalk support binding",new Vector2(x,ledge.y-.27f),new Vector2(.24f,.1f),Hex("a5946b"),-16);
            }
        }
        // Baked 96ppu terrain (Tools/TowerArt/BackdropPainter): per theme a 384x384 column holding
        // props and end caps (row 0), the platform strip (96), buried strata (192) and ground (288).
        private const int AtlasPpu=96,AtlasColumn=384;
        private static Texture2D terrainAtlas;private static bool terrainAtlasChecked;
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
        private static Sprite AtlasSprite(string key,Rect rect,Vector2 pivot)
        {
            Sprite sprite;if(themeSprites.TryGetValue(key,out sprite))return sprite;
            sprite=Sprite.Create(TerrainAtlas,rect,pivot,AtlasPpu,0,SpriteMeshType.FullRect);sprite.name=key;themeSprites.Add(key,sprite);return sprite;
        }
        private static Sprite EndCap(TowerThemeId theme,bool right)
        {return AtlasSprite("terrain end "+theme+right,new Rect((int)theme*AtlasColumn+(right?304:288),0,16,AtlasPpu),new Vector2(.5f,.75f));}
        private static Sprite TerrainTile(TowerThemeId theme,int variant,bool buried=false,float visibleWidth=1,bool raised=false)
        {
            if(TerrainAtlas!=null)
            {
                // Raised strips are one unit tall: the slab above, underside detail below, pivot on the slab centre.
                int columns=Mathf.Clamp(Mathf.CeilToInt(visibleWidth*AtlasPpu),1,AtlasPpu),row=raised?96:buried?192:288;
                return AtlasSprite("terrain atlas "+theme+variant+buried+raised+"-"+columns,
                    new Rect((int)theme*AtlasColumn+(variant%4)*AtlasPpu,row,columns,AtlasPpu),raised?new Vector2(.5f,.75f):new Vector2(.5f,.5f));
            }
            const int density=48;
            int pixels=Mathf.Clamp(Mathf.CeilToInt(visibleWidth*density),1,density),height=raised?24:density;
            string key="terrain authored "+theme+variant+buried+raised+"-"+pixels;Sprite sprite;if(themeSprites.TryGetValue(key,out sprite))return sprite;
            Color deep,shade,body,light,cap;
            switch(theme)
            {
                case TowerThemeId.Lava: deep=Hex("1e1b29");shade=Hex("342c3b");body=Hex("4c3c4a");light=Hex("74535c");cap=Hex("aa7770");break;
                case TowerThemeId.Rock: deep=Hex("1b2536");shade=Hex("303f55");body=Hex("4e5e78");light=Hex("79869d");cap=Hex("a7afc5");break;
                case TowerThemeId.Jungle: deep=Hex("202d2a");shade=Hex("3c4233");body=Hex("64634b");light=Hex("929071");cap=Hex("b5c879");break;
                case TowerThemeId.Frozen: deep=Hex("28435b");shade=Hex("3d6a83");body=Hex("6598ad");light=Hex("a0cbd1");cap=Hex("e4f2ed");break;
                case TowerThemeId.Water: deep=Hex("322f39");shade=Hex("685146");body=Hex("9a7657");light=Hex("c09b6b");cap=Hex("dfc691");break;
                case TowerThemeId.Cloud: deep=Hex("494762");shade=Hex("777189");body=Hex("a7a0ad");light=Hex("d0c7c3");cap=Hex("f0e4cd");break;
                default: deep=Hex("252f35");shade=Hex("414e51");body=Hex("64716c");light=Hex("949b88");cap=Hex("c0bda2");break;
            }
            sprite=PixelSprite(pixels,height,density,(x,y)=>
            {
                int px=x+variant*48,top=height-1-y;
                int row=y/13,blockX=(px+(row%2)*17)/29,localX=(px+(row%2)*17)%29,localY=y%13;
                int block=(blockX*7+row*11+variant*3)%5;
                Color c=block==0?shade:block==1?Color.Lerp(body,light,.28f):body;
                // Large stone faces, two-pixel bevels and sparse chips read at game scale.
                if(localX<2||localY<2)c=deep;
                else if(localY==12||localX==2)c=light;
                else if(localY<4)c=shade;
                if(localY==10&&localX>7&&localX<13&&block==1)c=light;
                if(top<4&&!buried)c=top==0?cap:top==3?shade:light;
                if(theme==TowerThemeId.Water)
                {
                    if(top>13)return Color.clear;
                    int board=px%12;
                    c=board==0?deep:board==1?light:board==11?shade:body;
                    if(top==0||top==11)c=cap;if(top==1)c=light;if(top>11)c=shade;
                    if(top>3&&top<9&&board==4+(top/3)%2)c=shade;
                    if((board==3||board==8)&&(top==3||top==9))c=deep;
                    if((board==3||board==8)&&top==2)c=cap;
                    return c;
                }
                if(theme==TowerThemeId.Cloud)
                {
                    if(raised&&top>18+(px/7)%4)return Color.clear;
                    if(top<5)c=top==0?cap:light;
                    if(top==6||top==9)c=Hex("b09a73");
                    if(top==7||top==8)c=Hex("dfc99b");
                    if(top>12&&px%24>8&&px%24<15)c=shade;
                    if(top>13&&top<17&&px%24>9&&px%24<14)c=light;
                }
                if(theme==TowerThemeId.Jungle)
                {
                    int mossDepth=3+((px/5)*7+variant)%7;
                    if(!buried&&top<mossDepth)c=top==0?cap:top<mossDepth-2?Hex("759451"):Hex("49693e");
                    if(!buried&&top==1&&px%11<4)c=Hex("d3dc8a");
                    if(top>5&&(px+top/2+variant*7)%37<3)c=Hex("6b523c");
                    if(top>5&&(px+top/2+variant*7)%37==3)c=Hex("a18355");
                }
                else if(theme==TowerThemeId.Lava)
                {
                    int fracture=(px+top/4*3+variant*11)%43;
                    if(top>5&&fracture<2)c=top%13<4?Hex("ef9c52"):Hex("aa5746");
                    if(top>5&&fracture==2)c=Hex("6d3a3d");
                    if(top==1&&px%19<7)c=Hex("ce9a7d");
                }
                else if(theme==TowerThemeId.Frozen)
                {
                    int facet=(px+top/2+variant*9)%36;
                    c=facet<4?light:facet<14?body:facet<27?Color.Lerp(body,shade,.35f):shade;
                    if(facet==4&&top>7)c=cap;
                    if(!buried&&top<4+(px/6)%3)c=top==0?cap:Hex("c5e4e2");
                    if(top>8&&top<12&&facet>18&&facet<26)c=light;
                }
                else if(theme==TowerThemeId.Ruins&&top>5)
                {
                    if(localX==14&&localY>5&&localY<10&&block==2)c=light;
                    if(localY==7&&localX>11&&localX<18&&block==2)c=light;
                    if(localY==2&&localX>19&&localX<25)c=Hex("54705b");
                }
                if(raised&&y==0)c=deep;
                return buried?Color.Lerp(c,deep,.23f):c;
            });themeSprites.Add(key,sprite);return sprite;
        }
        private void BuildDistantLandscape(TowerThemeId theme,Color sky,Color horizon)
        {
            for(int layer=0;layer<2;layer++)for(int i=0;i<11;i++)
            {
                float x=-29+i*7+layer*2.5f;Color tint=Color.Lerp(sky,horizon,layer==0?.32f:.55f);
                if(theme==TowerThemeId.Jungle)
                {
                    Box("Distant jungle trunk",new Vector2(x,1.8f),new Vector2(.7f,10),tint,-90+layer);
                    Cloud(x,4.4f+Range(-.3f,1),8,tint,-89+layer);
                }
                else if(theme==TowerThemeId.Cloud||theme==TowerThemeId.Water)
                    Cloud(x,3.1f+layer*2.4f+Range(-.4f,.5f),Range(4,8),Color.Lerp(horizon,sky,.4f+layer*.15f),-90+layer);
                else
                    Spire("Distant jagged ridge",x,-3,Range(5,9),tint,-90+layer,false,Range(5,9));
            }
            if(theme==TowerThemeId.Frozen||theme==TowerThemeId.Cloud||theme==TowerThemeId.Water)
            {
                Box("Hazy moon halo",new Vector2(17,5.1f),Vector2.one*5,new Color(accent.r,accent.g,accent.b,.16f),-94,Glow);
                Box("Pixel moon",new Vector2(17,5.1f),Vector2.one*1.7f,Color.Lerp(accent,Color.white,.35f),-93,Disc);
            }
        }
        private bool BuildAuthoredBackdrop(TowerThemeId theme)
        {
            // Baked high-resolution parallax planes; the atlas panorama remains the fallback.
            if(TowerThemeBackdrop.Build(environment,theme))return true;
            Sprite sprite=TowerThemeMonsterArt.Background(theme);
            if(sprite==null)return false;
            // Cover the full camera height as well as the room width without stretching the painting.
            float upperExtension=Mathf.Max(0,HighestPlatform-1);
            float scale=Mathf.Max(66/sprite.rect.width,(22+upperExtension*2)/sprite.rect.height);
            Color tint=theme==TowerThemeId.Cloud?new Color(.70f,.72f,.77f,1):
                theme==TowerThemeId.Frozen?new Color(.76f,.79f,.82f,1):
                theme==TowerThemeId.Jungle?new Color(.74f,.78f,.75f,1):new Color(.78f,.78f,.81f,1);
            Box(theme+" painted panorama",new Vector2(6,3.5f+upperExtension*.5f),Vector2.one*scale,tint,-94,sprite);
            return true;
        }
        private void BuildForeground(TowerThemeId theme)
        {
            // Keep silhouettes below knee height, leaving every landing surface readable.
            for(int i=0;i<19;i++)
            {
                float x=-23+i*3.7f+Range(-.5f,.5f);
                if(Mathf.Abs(x-ExitX)<1.8f)continue;
                float size=Range(.65f,.95f);
                var prop=Box(theme+" foreground detail",new Vector2(x,TerrainAtlas!=null?Ground:Ground+.4f*size),Vector2.one*size,Color.white,-32,ForegroundSprite(theme,i%3));
                if(i%2==1)prop.transform.localScale=new Vector3(-size,size,1);
            }
            if(theme==TowerThemeId.Water)
            {
                // Translucent, so the baked lagoon reflections read through beneath the walkway.
                Box("Open lagoon",new Vector2(6,-5.1f),new Vector2(100,4),new Color(.137f,.298f,.408f,TowerThemeBackdrop.Bands(theme).Count>0?.5f:1),-47);
                for(int i=0;i<19;i++)
                {
                    float x=-27+i*4;
                    Box("Boardwalk piling",new Vector2(x,-4.03f),new Vector2(.18f,2.1f),Hex("59453d"),-31);
                    Box("Piling cut light",new Vector2(x-.045f,-4.03f),new Vector2(.035f,2.1f),Hex("9d805b"),-30);
                    var ripple=Box("Moving lagoon ripple",new Vector2(x+Range(-1,1),Range(-5.6f,-3.5f)),new Vector2(Range(.4f,.9f),.03125f),Hex("618c9d"),-45);
                    AddMote(ripple,Range(.6f,1));
                }
            }
        }
        private static Sprite ForegroundSprite(TowerThemeId theme,int variant)
        {
            if(TerrainAtlas!=null)return AtlasSprite("foreground atlas "+theme+variant,new Rect((int)theme*AtlasColumn+(variant%3)*AtlasPpu,0,AtlasPpu,AtlasPpu),new Vector2(.5f,0));
            string key="foreground cluster "+theme+variant;Sprite sprite;if(themeSprites.TryGetValue(key,out sprite))return sprite;
            sprite=PixelSprite(48,40,48,(x,y)=>
            {
                if(theme==TowerThemeId.Jungle||theme==TowerThemeId.Water)
                {
                    Color c=Color.clear;
                    for(int i=-3;i<=3;i++)
                    {
                        float dx=i*5.8f,dy=29-Mathf.Abs(i)*3,px=x-24,py=y-2;
                        float t=(px*dx+py*dy)/(dx*dx+dy*dy);
                        if(t<0||t>1)continue;
                        float side=px*dy-py*dx,distance=Mathf.Abs(side)/Mathf.Sqrt(dx*dx+dy*dy);
                        float leaf=theme==TowerThemeId.Water?1.2f:(Mathf.Repeat(t*6,1)<.6f?3.6f*(1-t)+1:1.2f);
                        if(distance<leaf+1)c=Hex("1e3c36");
                        if(distance<leaf)c=side<0?Hex("4c7652"):Hex("7e9c61");
                        if(distance<.6f)c=Hex("a6b376");
                    }
                    if(theme==TowerThemeId.Jungle&&variant==1)
                    {
                        int dx=x-31,dy=y-25;
                        if(Mathf.Abs(dx)+Mathf.Abs(dy)<6)c=Hex("7e435b");
                        if((Mathf.Abs(dx)<2&&Mathf.Abs(dy)<5)||(Mathf.Abs(dy)<2&&Mathf.Abs(dx)<5))c=Hex("e4b093");
                        if(Mathf.Abs(dx)+Mathf.Abs(dy)<2)c=Hex("efdf9d");
                    }
                    return c;
                }
                if(theme==TowerThemeId.Rock||theme==TowerThemeId.Frozen)
                {
                    Color c=Color.clear;
                    for(int i=0;i<3;i++)
                    {
                        int cx=12+i*11,peak=i==1?33:21+(variant+i)%3*3,w=i==1?8:6,baseY=2+i%2;
                        int side=Mathf.Abs(x-cx),tip=peak-(side*9/w);
                        if(y<baseY||y>tip||side>w)continue;
                        c=Hex("253b52");
                        if(side<w-1&&y>baseY+1&&y<tip-1)
                            c=theme==TowerThemeId.Rock?(x<cx?Hex("b8a0d1"):Hex("666b9f")):(x<cx?Hex("c9e5e4"):Hex("74acbc"));
                        if(x==cx-2&&y>baseY+5&&y<tip-3)c=theme==TowerThemeId.Rock?Hex("ded0e8"):Hex("f0f5ed");
                    }
                    return c;
                }
                int edgeX=Mathf.Abs(x-24);
                bool inside=y>1&&y<18-edgeX/3+(x/9)%2&&edgeX<21;
                if(!inside)return Color.clear;
                Color dark=theme==TowerThemeId.Lava?Hex("27222f"):theme==TowerThemeId.Cloud?Hex("555268"):Hex("2f3d3c");
                Color body=theme==TowerThemeId.Lava?Hex("68505a"):theme==TowerThemeId.Cloud?Hex("bcb2b0"):Hex("798777");
                Color light=theme==TowerThemeId.Lava?Hex("a27874"):theme==TowerThemeId.Cloud?Hex("e5d8bd"):Hex("b0b59a");
                Color result=y<4?dark:(x/11+y/6+variant)%3==0?body:Color.Lerp(body,dark,.3f);
                if(y==16-edgeX/3+(x/9)%2||x%13==3)result=light;
                if((x+y/2)%19==0&&y<13)result=dark;
                if(theme==TowerThemeId.Lava&&(x-y/2+48)%23==0&&y>5)result=Hex("d98a56");
                if(theme==TowerThemeId.Cloud&&(y==8||y==10)&&x>13&&x<34)result=y==10?Hex("c4a977"):Hex("8a795f");
                if(theme==TowerThemeId.Ruins&&y<7&&(x/5)%3==0)result=Hex("617b50");
                return result;
            });themeSprites.Add(key,sprite);return sprite;
        }
        private void BuildLava()
        {
            Box("Distant magma river",new Vector2(6,-2.5f),new Vector2(100,.4f),Hex("bf5a36"),-72);
            for(int i=0;i<11;i++)
            {
                float x=-22+i*6;float h=Range(5,8);
                Spire("Basalt ceiling",x,8.5f,h,Hex("291f2d"),-68,true,Range(2.4f,4.8f));
                Spire("Basalt column",x+2.4f,Ground,Range(2.2f,4.6f),Hex("372b37"),-64,false,Range(1.2f,2.8f));
                if(i%2==0)
                {
                    Box("Distant lava fall",new Vector2(x+1.1f,2.5f),new Vector2(.38f,9),Hex("9b4337"),-76);
                    Box("Bright lava current",new Vector2(x+1.05f,2.5f),new Vector2(.1f,9),Hex("b97549"),-75);
                }
                float px=x+Range(-.5f,1);var heat=Box("Magma glow",new Vector2(px,-2.3f),new Vector2(3,2),new Color(1,.35f,.12f,.4f),-32,Glow).GetComponent<SpriteRenderer>();pulses.Add(heat);
                for(int j=0;j<3;j++)Box("Lava crust fissure",new Vector2(px+j*.28f,Ground-.3f-j*.25f),new Vector2(.25f,.0625f),Hex("f18c40"),-19);
            }
        }
        private void BuildCavern()
        {
            for(int i=0;i<13;i++)
            {
                float x=-24+i*5.2f;
                Spire("Cavern stalactite",x,8,Range(3,6),Hex("263143"),-68,true,Range(1.8f,3));
                Spire("Cavern stalagmite",x+1,Ground,Range(1.6f,4),Hex("2b3447"),-65,false,Range(1.2f,2.5f));
                Crystal(x,Ground,Range(.45f,.9f),i%2==0?Hex("6877a2"):Hex("548c91"),-38);
                if(i%3==0)Crystal(x+2,2.3f,1,Hex("627ab0"),-66);
            }
        }
        private void BuildJungle()
        {
            for(int i=0;i<11;i++)
            {
                float x=-23+i*6;float top=Range(5.2f,7);
                Box("Ancient jungle trunk",new Vector2(x,(top+Ground)*.5f),new Vector2(.65f,top-Ground),Hex("2e4136"),-62);
                Box("Bark light",new Vector2(x-.15f,(top+Ground)*.5f),new Vector2(.1f,top-Ground),Hex("3a5140"),-61);
                Branch(x,1.6f,2.6f,i%2==0?1:-1,Hex("314b3a"));
                Cloud(x,top,Range(4,6),Hex("285342"),-58);
                Cloud(x-1,top+.7f,4,Hex("376449"),-57);
                for(int j=0;j<2;j++)Vine(x-1.3f+j*2.2f,top,Range(1.2f,2.6f),Hex("466749"),-55);
                Fern(x+.7f,Ground,Range(.4f,.8f),Hex("537648"));
                if(i%2==0)
                {Box("Jungle flower",new Vector2(x+1.6f,Ground+.2f),Vector2.one*.18f,Hex("dd9c86"),-31,Disc);Box("Flower stem",new Vector2(x+1.6f,Ground+.1f),new Vector2(.04f,.25f),Hex("64834d"),-32);}
            }
        }
        private void BuildFrozen()
        {
            for(int i=0;i<13;i++)
            {
                float x=-24+i*5.2f;
                Spire("Frozen cavern roof",x,8.5f,Range(3,5.2f),Hex("3a526d"),-67,true,3.7f);
                for(int j=0;j<3;j++)Spire("Hanging icicle",x-.9f+j*.7f,5.4f+Range(0,1),Range(.4f,1.1f),Hex("668b9e"),-60,true,.25f);
                Crystal(x+1.3f,Ground,Range(.45f,.85f),Hex("88b2c4"),-36);
                if(i%2==0)Pine(x,Ground,Range(2,3.7f));
                Box("Snow drift",new Vector2(x+.4f,Ground-.11f),new Vector2(1.7f,.2f),Hex("d4e7e9"),-18,Disc);
            }
        }
        private void BuildWater()
        {
            Box("Distant lagoon",new Vector2(6,-2.8f),new Vector2(100,2),Hex("3d7283"),-71);
            Box("Open lagoon",new Vector2(6,-5.1f),new Vector2(100,4),Hex("235775"),-47);
            Box("Lagoon shallows",new Vector2(6,-3.9f),new Vector2(100,.75f),Hex("357d91"),-46);
            for(int i=0;i<18;i++)
            {
                float x=-30+i*4;
                Box("Boardwalk piling",new Vector2(x,-4.05f),new Vector2(.23f,2.1f),Hex("554747"),-31);
                Box("Piling highlight",new Vector2(x-.06f,-3.95f),new Vector2(.05f,1.8f),Hex("a18a65"),-30);
                if(i%3==0)
                {
                    Box("Small lagoon island",new Vector2(x+1,-1.9f),new Vector2(3,.3f),Hex("457561"),-61,Disc);
                    Palm(x+1.4f,-1.8f,Range(2,3.2f));
                }
                for(int j=0;j<3;j++)
                {
                    var ripple=Box("Moving lagoon ripple",new Vector2(x+Range(-1.4f,1.4f),Range(-5.7f,-3.5f)),new Vector2(Range(.3f,1.3f),.0625f),Hex("75b5bd"),-45);
                    AddMote(ripple,Range(.6f,1.2f));
                }
            }
            for(int i=0;i<7;i++)
            {
                float x=-15+i*7;
                Box("Dock post",new Vector2(x,Ground+.3f),new Vector2(.18f,.65f),Hex("82694e"),-35);
                Box("Dock rope binding",new Vector2(x,Ground+.5f),new Vector2(.24f,.12f),Hex("d1c08d"),-34);
                Box("Water lily",new Vector2(x+1.8f,-3.7f),new Vector2(.5f,.12f),Hex("79a67b"),-44,Disc);
            }
        }
        private void BuildClouds()
        {
            for(int i=0;i<14;i++)
            {
                float x=-28+i*5.2f;
                Cloud(x,-4.3f+Range(-.6f,.3f),Range(4,7),Hex("a0abc6"),-48);
                Cloud(x+1,Range(.8f,4.8f),Range(2.2f,3.7f),Hex("8193b7"),-65);
                if(i%3==0)
                {
                    Box("Distant floating island",new Vector2(x,5.1f),new Vector2(2,.2f),Hex("7b829c"),-67);
                    Spire("Distant island underside",x,5,1,Hex("65708e"),-68,true,1.5f);
                    Box("Distant sky obelisk",new Vector2(x,5.6f),new Vector2(.18f,.85f),Hex("919db5"),-66);
                }
                if(i%2==0)
                {
                    Box("Sky shrine plinth",new Vector2(x,Ground-.06f),new Vector2(.65f,.12f),Hex("85859d"),-35);
                    Crystal(x,Ground,.4f,Hex("b9af9d"),-34);
                }
            }
        }
        private void BuildRuins()
        {
            for(int i=0;i<11;i++)
            {
                float x=-23+i*6;float height=Range(3.6f,5.5f);
                Column(x,Ground,height,Hex("4b5157"),-62);
                if(i%2==0)
                {
                    Column(x+3.6f,Ground,height,Hex("4b5157"),-62);
                    Arch(x+1.8f,Ground+height-.3f,3.6f,Hex("626761"),-61);
                    Vine(x+.4f,Ground+height,Range(1.3f,2.8f),Hex("4f6b54"),-58);
                }
                for(int j=0;j<3;j++)
                {
                    float px=x+1.5f+j*.38f;var rubble=Box("Fallen carved stone",new Vector2(px,Ground+.08f),new Vector2(Range(.3f,.55f),Range(.12f,.26f)),Hex("565e59"),-34);
                    rubble.transform.localRotation=Quaternion.Euler(0,0,decorationRandom.Next(-2,3)*10);
                }
                Fern(x-.7f,Ground,.5f,Hex("667954"));
            }
        }
        private void BuildGate()
        {
            portal=new GameObject("Ascent gate").transform;portal.SetParent(environment,false);portal.localPosition=new Vector3(ExitX,Ground,0);
            Color frame=activeTheme==TowerThemeId.Water?Hex("b09468"):activeTheme==TowerThemeId.Jungle?Hex("6a8052"):activeTheme==TowerThemeId.Frozen?Hex("a9d6df"):activeTheme==TowerThemeId.Lava?Hex("8e6665"):Hex("a2a59a");
            SpriteObject(portal,"Carved ascent arch",new Vector2(0,1.625f),Vector2.one,Color.white,-10,GateSprite(activeTheme,frame));
            gateOpen=false;gateOpening=SpriteObject(portal,"Gate opening",new Vector2(0,1.4f),new Vector2(1.5f,2.8f),new Color(accent.r,accent.g,accent.b,.12f),-9,Glow).GetComponent<SpriteRenderer>();
        }
        private static Sprite GateSprite(TowerThemeId theme,Color frame)
        {
            // 96ppu: the same 2.33 x 3.25 unit silhouette as before, with carved mouldings, voussoirs and fluting.
            string key="carved ascent arch hd "+theme;Sprite sprite;if(themeSprites.TryGetValue(key,out sprite))return sprite;
            Color outline=Color.Lerp(frame,Hex("111c2a"),.7f),shadow=Color.Lerp(frame,Hex("29303f"),.4f),light=Color.Lerp(frame,Hex("fff0ca"),.38f),glint=Color.Lerp(frame,Hex("fff6e0"),.62f);
            sprite=PixelSprite(224,312,96,(x,y)=>
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
                    Color back=Color.Lerp(Hex("0e1824"),Hex("2c3a38"),lift*.8f);
                    if(Mathf.Repeat(v,9)<.5f||Mathf.Repeat(u+(Mathf.Floor(v/9)%2)*7,14)<.5f)back=Color.Lerp(back,Color.black,.35f);
                    return v<15?Color.Lerp(Hex("38453e"),Hex("4a5a50"),grain*.4f):back;
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
            });themeSprites.Add(key,sprite);return sprite;
        }
        private void BuildWeather()
        {
            if(activeTheme==TowerThemeId.Water)return;
            int count=activeTheme==TowerThemeId.Frozen?38:activeTheme==TowerThemeId.Cloud?8:20;
            for(int i=0;i<count;i++)
            {
                Vector2 pos=new Vector2(Range(-25,42),Range(-2.5f,weatherCeiling));
                Color color=accent;float size=Range(.03125f,.07f),speed=Range(.06f,.2f);
                if(activeTheme==TowerThemeId.Frozen){color=Hex("b3ccd8");speed=Range(.35f,.9f);}
                else if(activeTheme==TowerThemeId.Lava){color=i%3==0?Hex("ffe2a0"):Hex("e88956");speed=Range(.12f,.45f);}
                else if(activeTheme==TowerThemeId.Jungle){color=Hex("c2d780");}
                else if(activeTheme==TowerThemeId.Cloud)
                {AddMote(Cloud(pos.x,pos.y,Range(.6f,1.5f),new Color(.65f,.73f,.85f,.32f),-39),Range(.1f,.3f));continue;}
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
        private void Spire(string name,float x,float baseY,float height,Color color,int order,bool down=false,float width=0)
        {
            Sprite s;if(!themeSprites.TryGetValue("spire",out s))
            {
                s=PixelSprite(32,64,32,(px,py)=>
                {
                    int extent=2+(63-py)*13/64;bool inside=Mathf.Abs(px-16)<extent+((py/7)%3==0?1:0);
                    if(!inside)return Color.clear;return px<13?new Color(.84f,.84f,.88f):Color.white;
                });themeSprites.Add("spire",s);
            }
            var go=Box(name,new Vector2(x,baseY+(down?-height:height)*.5f),new Vector2(width>0?width:height*.45f,height*.5f),color,order,s);
            if(down)go.transform.localScale=new Vector3(go.transform.localScale.x,-go.transform.localScale.y,1);
        }
        private void Crystal(float x,float y,float height,Color color,int order)
        {
            Spire("Crystal left",x-.2f,y,height*.65f,color,order,false,.3f);
            Spire("Crystal prism",x,y,height,Color.Lerp(color,Color.white,.18f),order+1,false,.38f);
            Spire("Crystal right",x+.25f,y,height*.75f,color,order,false,.29f);
            Box("Crystal glint",new Vector2(x-.035f,y+height*.72f),new Vector2(.0625f,.2f),Color.Lerp(color,Color.white,.68f),order+2);
            var light=Box("Crystal halo",new Vector2(x,y+height*.4f),new Vector2(height*2.8f,height*2.3f),new Color(color.r,color.g,color.b,.4f),order-1,Glow).GetComponent<SpriteRenderer>();pulses.Add(light);
        }
        private void Vine(float x,float top,float length,Color color,int order)
        {
            int steps=Mathf.CeilToInt(length/.3f);
            for(int i=0;i<steps;i++)
            {
                float px=x+Mathf.Sin(i*.7f)*.09f,py=top-i*.3f;
                Box("Hanging vine",new Vector2(px,py),new Vector2(.0625f,.34f),color,order);
                if(i%2==0)Box("Vine leaf",new Vector2(px+(i%4==0?-.12f:.12f),py),new Vector2(.22f,.1f),Color.Lerp(color,accent,.17f),order+1);
            }
        }
        private void Branch(float x,float y,float length,int side,Color color)
        {var branch=Box("Jungle branch",new Vector2(x+side*length*.4f,y+.3f),new Vector2(length,.22f),color,-60);branch.transform.localRotation=Quaternion.Euler(0,0,side*17);}
        private void Fern(float x,float y,float height,Color color)
        {
            for(int i=-2;i<=2;i++)
            {
                var leaf=Box("Fern frond",new Vector2(x+i*.12f,y+height*.4f),new Vector2(.12f,height*(1-Mathf.Abs(i)*.12f)),color,-33);
                leaf.transform.localRotation=Quaternion.Euler(0,0,-i*22);
            }
        }
        private void Pine(float x,float y,float height)
        {
            Box("Frosted pine trunk",new Vector2(x,y+height*.4f),new Vector2(.16f,height*.8f),Hex("485769"),-55);
            for(int j=0;j<3;j++)
            {float h=height*(.65f-j*.13f);Spire("Pine bough",x,y+j*height*.23f,h,Hex("506b7b"),-54+j,false,height*(.6f-j*.12f));Spire("Snowy bough",x,y+j*height*.23f+.15f,h*.8f,Hex("829eae"),-53+j,false,height*(.48f-j*.1f));}
        }
        private void Palm(float x,float y,float height)
        {
            Box("Lagoon palm trunk",new Vector2(x,y+height*.48f),new Vector2(.2f,height),Hex("677366"),-60);
            for(int i=-2;i<=2;i++)
            {var leaf=Box("Palm canopy",new Vector2(x+i*.26f,y+height-.15f),new Vector2(1.35f,.18f),Hex("638c70"),-59);leaf.transform.localRotation=Quaternion.Euler(0,0,i*24);}
        }
        private void Column(float x,float y,float height,Color color,int order)
        {
            Box("Ruined column",new Vector2(x,y+height*.5f),new Vector2(.65f,height),color,order);
            Box("Column flute",new Vector2(x-.15f,y+height*.5f),new Vector2(.08f,height-.3f),Color.Lerp(color,Color.white,.12f),order+1);
            Box("Column capital",new Vector2(x,y+height-.1f),new Vector2(1,.3f),Color.Lerp(color,Color.white,.12f),order+1);
            Box("Column base",new Vector2(x,y+.15f),new Vector2(1,.3f),color,order+1);
            for(int i=1;i<height;i++)Box("Cracked column seam",new Vector2(x,y+i),new Vector2(.67f,.0625f),Color.Lerp(color,Color.black,.35f),order+2);
        }
        private void Arch(float x,float y,float width,Color color,int order)
        {
            for(int i=0;i<9;i++)
            {
                float a=Mathf.PI*i/8;var stone=Box("Ancient arch voussoir",new Vector2(x+Mathf.Cos(a)*width*.5f,y+Mathf.Sin(a)*1.2f),new Vector2(.65f,.45f),Color.Lerp(color,Color.black,i%2*.13f),order);
                stone.transform.localRotation=Quaternion.Euler(0,0,(a*Mathf.Rad2Deg)-90);
            }
        }
    }
}
