using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Shared IMGUI look: night-blue panels framed in gold leaf, serif titles and engraved buttons. The frames, buttons,
    // meters and icons come from the kit baked by Tools/TowerArt/UIPainter into Resources/Tower/UI, two texture pixels
    // per UI unit; nine-slice borders below are given in texture pixels.
    public sealed partial class TowerGame
    {
        // brass is the theme accent (gold leaf); gold is kept for the gold reward tier.
        private readonly Color brass=new Color(.90f,.74f,.45f),crimson=new Color(.80f,.17f,.2f),silver=new Color(.76f,.81f,.88f),gold=new Color(.98f,.79f,.38f);
        private readonly Color rim=new Color(.2f,.22f,.3f),night=new Color(.02f,.028f,.058f);
        private Texture2D uiGradient,uiHalo,uiDiamond,uiGrain,uiVignette,uiBand;
        private float hudHealth=-1,hudTrail,hudDamageHold,hudHitFlash;
        private Fighter hudActor;
        private GUISkin uiSkin;
        private Texture2D uiScrollThumb;
        private int scrollDepth;
        private static readonly Rect Screenful=new Rect(-200,-200,2000,1300);
        private static readonly Dictionary<string,Texture2D> uiKit=new Dictionary<string,Texture2D>();
        private enum UIIcon{Sword,Hourglass,Target,Speed,Star,Heart,Shield,Skull,Crystal,Tower,Flame,Plus,Coin}
        private const int UIIconCount=13;
        private static Color Alpha(Color c,float a){c.a=a;return c;}
        private static Rect Inset(Rect r,float n){return new Rect(r.x+n,r.y+n,r.width-n*2,r.height-n*2);}
        private static float Hash(float seed){return Mathf.Repeat(Mathf.Sin(seed*12.9898f)*43758.5453f,1);}
        private void Round(Rect r,Color c,float radius=6)
        {GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,c,0,radius);}

        // Frames, rims and edges are empty in the middle, so their centre slice is skipped.
        private static readonly HashSet<Texture2D> hollowKit=new HashSet<Texture2D>();
        private static Texture2D Kit(string name)
        {
            Texture2D texture;
            if(!uiKit.TryGetValue(name,out texture)||texture==null)
            {
                texture=Resources.Load<Texture2D>("Tower/UI/"+name);uiKit[name]=texture;
                if(texture!=null&&(name.Contains("frame")||name.Contains("edge")||name.Contains("rim")))hollowKit.Add(texture);
            }
            return texture;
        }
        /// <summary>Draws a kit texture as a nine-slice; <paramref name="border"/> is in texture pixels and shows at half size.</summary>
        private static void Slice(Rect r,Texture2D texture,float border,Color tint)
        {
            if(texture==null||Event.current.type!=EventType.Repaint||r.width<=0||r.height<=0)return;
            float b=Mathf.Min(border*.5f,r.width*.5f,r.height*.5f),u=border/texture.width,v=border/texture.height;
            float[] xs={r.x,r.x+b,r.xMax-b,r.xMax},ys={r.y,r.y+b,r.yMax-b,r.yMax},us={0,u,1-u,1},vs={1,1-v,v,0};
            GUI.color=tint;bool hollow=hollowKit.Contains(texture);
            for(int j=0;j<3;j++)for(int i=0;i<3;i++)
            {
                float w=xs[i+1]-xs[i],h=ys[j+1]-ys[j];if(w<=.01f||h<=.01f||hollow&&i==1&&j==1)continue;
                GUI.DrawTextureWithTexCoords(new Rect(xs[i],ys[j],w,h),texture,new Rect(us[i],vs[j+1],us[i+1]-us[i],vs[j]-vs[j+1]),true);
            }
            GUI.color=Color.white;
        }
        // Warm golds use the gilded pieces as painted; every other accent tints the silver ones.
        private static bool Golden(Color c){return c.r>.75f&&c.g>.55f&&c.g<.86f&&c.b<.56f&&c.r-c.b>.3f;}
        private static Texture2D Metal(string piece,Color accent){return Kit(piece+(Golden(accent)?"-gold":"-silver"));}
        private static Color MetalTint(Color accent){return Golden(accent)?new Color(1,1,1,accent.a):Alpha(Color.Lerp(accent,Color.white,.3f),accent.a);}

        private void EnsureUITextures()
        {
            if(uiGradient)return;
            uiGradient=new Texture2D(1,64,TextureFormat.RGBA32,false){name="UI light falloff",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<64;y++)uiGradient.SetPixel(0,y,new Color(1,1,1,y/63f));
            uiGradient.Apply();
            uiHalo=new Texture2D(128,128,TextureFormat.RGBA32,false){name="UI portrait light",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<128;y++)for(int x=0;x<128;x++){float d=new Vector2((x-63.5f)/63.5f,(y-63.5f)/63.5f).magnitude;uiHalo.SetPixel(x,y,new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-d),2)));}
            uiHalo.Apply();
            uiDiamond=new Texture2D(32,32,TextureFormat.RGBA32,false){name="UI diamond",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)
                uiDiamond.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(16-Mathf.Abs(x-15.5f)-Mathf.Abs(y-15.5f))));
            uiDiamond.Apply();
            // Sparse film grain, tiled and shifted every frame on menus.
            var random=new System.Random(6066);
            uiGrain=new Texture2D(128,128,TextureFormat.RGBA32,false){name="UI film grain",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Point};
            for(int y=0;y<128;y++)for(int x=0;x<128;x++){double n=random.NextDouble();uiGrain.SetPixel(x,y,n>.93?new Color(.9f,.95f,1,(float)(n-.93)*9):n<.05?new Color(0,0,0,.6f):Color.clear);}
            uiGrain.Apply();
            uiVignette=new Texture2D(128,128,TextureFormat.RGBA32,false){name="UI vignette",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<128;y++)for(int x=0;x<128;x++){float d=new Vector2((x-63.5f)/63.5f,(y-63.5f)/63.5f).magnitude;uiVignette.SetPixel(x,y,new Color(1,1,1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1.35f,d))));}
            uiVignette.Apply();
            // Opaque across the middle, fading out over the outer fifth on each side.
            uiBand=new Texture2D(64,1,TextureFormat.RGBA32,false){name="UI band",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp};
            for(int x=0;x<64;x++){float u=x/63f;uiBand.SetPixel(x,0,new Color(1,1,1,Mathf.SmoothStep(0,1,Mathf.Min(u,1-u)/.2f)));}
            uiBand.Apply();
        }
        private void Light(Rect r,Color c)
        {EnsureUITextures();GUI.color=c;GUI.DrawTexture(r,uiGradient);GUI.color=Color.white;}
        private void Halo(Rect r,Color c)
        {EnsureUITextures();GUI.color=c;GUI.DrawTexture(r,uiHalo);GUI.color=Color.white;}
        private void Grain(Rect r,float alpha,bool moving=false)
        {
            EnsureUITextures();float shift=moving?Mathf.Floor(Time.unscaledTime*18)*.37f:0;
            GUI.color=new Color(1,1,1,alpha);GUI.DrawTextureWithTexCoords(r,uiGrain,new Rect(shift,shift*.61f,r.width/128,r.height/128));GUI.color=Color.white;
        }
        private void Vignette(Color c){EnsureUITextures();GUI.color=c;GUI.DrawTexture(new Rect(-40,-40,1680,980),uiVignette);GUI.color=Color.white;}
        private void Diamond(Vector2 center,float size,Color color)
        {
            EnsureUITextures();float radius=size*.707107f;GUI.color=color;
            GUI.DrawTexture(new Rect(center.x-radius,center.y-radius,radius*2,radius*2),uiDiamond);
            GUI.color=Color.white;
        }
        private void Icon(Rect r,UIIcon icon,float alpha=1)
        {
            var atlas=Kit("icons");if(atlas==null)return;
            GUI.color=new Color(1,1,1,alpha);
            GUI.DrawTextureWithTexCoords(r,atlas,new Rect((int)icon/(float)UIIconCount,0,1f/UIIconCount,1),true);GUI.color=Color.white;
        }

        // Containers: a shadowed night-blue body, a faint accent light and a gilded or tinted frame.
        private void PanelBody(Rect r,Color accent,float light=.06f)
        {
            Slice(new Rect(r.x-14,r.y-8,r.width+28,r.height+30),Kit("panel-shadow"),44,new Color(1,1,1,.8f));
            Slice(r,Kit("panel-fill"),28,Color.white);
            Light(new Rect(r.x+3,r.y+3,r.width-6,Mathf.Min(150,r.height-6)),Alpha(accent,light));
            Grain(Inset(r,3),.03f);
        }
        private void Panel(Rect r,Color accent)
        {PanelBody(r,accent);Slice(r,Metal("panel-frame",accent),64,MetalTint(accent));}
        /// <summary>The heavier engraved frame of cards and modal screens, with a winged crest on its top edge.</summary>
        private void Card(Rect r,Color accent,bool crest=true)
        {
            PanelBody(r,accent,.1f);
            Slice(r,Metal("card-frame",accent),88,MetalTint(accent));
            if(crest)Crest(new Vector2(r.center.x,r.y+1),accent,r.width<300?.8f:1);
        }
        private void Crest(Vector2 at,Color accent,float scale=1)
        {
            var texture=Metal("crest",accent);if(texture==null)return;float w=120*scale,h=48*scale;
            GUI.color=MetalTint(accent);GUI.DrawTexture(new Rect(at.x-w*.5f,at.y-h*.48f,w,h),texture);GUI.color=Color.white;
        }
        /// <summary>A recessed slot for rows, stat boxes and places still empty.</summary>
        private void Slot(Rect r,float alpha=1){Slice(r,Kit("inset"),20,new Color(1,1,1,alpha));}
        /// <summary>A slot with a frame that lights in <paramref name="accent"/> when active.</summary>
        private void Slot(Rect r,Color accent,bool lit)
        {
            Slot(r);
            if(lit){Light(new Rect(r.x+3,r.y+3,r.width-6,Mathf.Min(60,r.height-6)),Alpha(accent,.1f));Slice(r,Metal("panel-frame",accent),64,Alpha(MetalTint(accent),.85f));}
        }
        /// <summary>A clickable framed tile (portrait cards, party slots); its frame brightens on hover and when selected.</summary>
        private bool Tile(Rect r,Color accent,bool selected=false,bool enabled=true)
        {
            bool available=enabled&&GUI.enabled,hover=available&&r.Contains(Event.current.mousePosition);
            if(selected)Halo(new Rect(r.x-22,r.y-22,r.width+44,r.height+44),Alpha(accent,.2f));
            else if(hover)Halo(new Rect(r.x-14,r.y-14,r.width+28,r.height+28),Alpha(accent,.12f));
            PanelBody(r,accent,selected?.14f:hover?.09f:.04f);
            float frame=selected?1:hover?.85f:.5f;
            Slice(r,Metal("panel-frame",selected||hover?accent:silver),64,Alpha(MetalTint(selected||hover?accent:silver),frame));
            if(selected)Diamond(new Vector2(r.center.x,r.y+1),10,accent);
            bool previous=GUI.enabled;GUI.enabled=available;bool clicked=GUI.Button(r,"",buttonStyle);GUI.enabled=previous;return clicked;
        }
        /// <summary>Invisible click area over art that draws its own frame.</summary>
        private bool Hit(Rect r){return GUI.Button(r,"",buttonStyle);}
        /// <summary>A dark band that fades at both ends, edged with gilded hairlines; used for notices and synergy chips.</summary>
        private void Banner(Rect r,float alpha=1,Color? accent=null)
        {
            EnsureUITextures();Color c=accent??brass;
            GUI.color=Alpha(night,.9f*alpha);GUI.DrawTexture(r,uiBand);
            GUI.color=Alpha(c,.07f*alpha);GUI.DrawTexture(new Rect(r.x,r.y,r.width,r.height*.55f),uiBand);GUI.color=Color.white;
            Divider(new Rect(r.x,r.y-5,r.width,10),alpha*.9f);Divider(new Rect(r.x,r.yMax-5,r.width,10),alpha*.55f);
        }
        /// <summary>A gilded hairline across <paramref name="r"/> that fades at both ends, optionally with a centre gem.</summary>
        private void Divider(Rect r,float alpha=1,bool knot=false,Color? tint=null)
        {
            var line=Kit("divider");if(line==null)return;
            GUI.color=Alpha(tint??Color.white,alpha);GUI.DrawTexture(new Rect(r.x,r.center.y-3,r.width,6),line);
            if(knot){var k=Kit("knot");if(k!=null)GUI.DrawTexture(new Rect(r.center.x-28,r.center.y-8,56,16),k);}
            GUI.color=Color.white;
        }
        /// <summary>A flourish pointing right at <paramref name="r"/>'s far end, or mirrored to point left.</summary>
        private void Flourish(Rect r,bool mirrored,float alpha=1)
        {
            var texture=Kit("flourish");if(texture==null)return;
            GUI.color=new Color(1,1,1,alpha);
            GUI.DrawTextureWithTexCoords(r,texture,mirrored?new Rect(1,0,-1,1):new Rect(0,0,1,1),true);GUI.color=Color.white;
        }
        private bool Button(Rect r,string text,Color accent,bool enabled=true,bool primary=false)
        {
            bool available=enabled&&GUI.enabled,hover=available&&r.Contains(Event.current.mousePosition);
            bool pressed=hover&&Input.GetMouseButton(0);
            Color body=primary?Color.Lerp(accent,Color.black,.52f):new Color(.15f,.18f,.27f);
            if(!available)body=new Color(.1f,.11f,.14f);
            else if(hover)body=Color.Lerp(body,accent,primary?.24f:.2f);
            if(pressed)body=Color.Lerp(body,Color.black,.25f);
            if(hover)Halo(new Rect(r.x-20,r.y-18,r.width+40,r.height+36),Alpha(accent,.22f));
            else if(primary&&available)Halo(new Rect(r.x-12,r.y-12,r.width+24,r.height+24),Alpha(accent,.08f));
            // Buttons are small, so one stretched shadow reads the same as the panels' nine-slice one.
            var shade=Kit("panel-shadow");if(shade!=null){GUI.color=new Color(1,1,1,.55f);GUI.DrawTexture(new Rect(r.x-14,r.y-6,r.width+28,r.height+22),shade);GUI.color=Color.white;}
            Slice(r,Kit("button-body"),24,body);
            Color edge=!available?Alpha(Color.Lerp(muted,night,.3f),.6f):primary||hover?accent:Color.Lerp(accent,silver,.45f);
            Slice(r,Metal("button-edge",edge),24,MetalTint(edge));
            bool previous=GUI.enabled;GUI.enabled=available;
            bool clicked=GUI.Button(r,"",buttonStyle);GUI.enabled=previous;
            if(!string.IsNullOrEmpty(text))
            {
                Rect t=new Rect(r.x+8,r.y+(pressed?1:0),r.width-16,r.height);Color ink=available?paper:Alpha(muted,.75f);
                if(primary){Title(new Rect(t.x,t.y+2,t.width,t.height),text,19,new Color(0,0,0,.55f),TextAnchor.MiddleCenter);Title(t,text,19,available?new Color(1,.95f,.84f):ink,TextAnchor.MiddleCenter);}
                else Label(t,text,17,ink,TextAnchor.MiddleCenter,true);
            }
            return clicked;
        }
        private void Portrait(Rect r,int index,Color frame)
        {
            Slot(r);Halo(Inset(r,2),Alpha(frame,.3f));PortraitArt(Inset(r,Mathf.Max(3,r.width*.08f)),index);
            Slice(r,Metal("portrait-frame",frame),36,MetalTint(frame));
        }
        /// <summary>The hero's round gilded medallion; <paramref name="flash"/> tints its glow when struck.</summary>
        private void Medallion(Rect r,int index,Color flash)
        {
            var texture=Kit("medallion");
            Halo(new Rect(r.x-16,r.y-16,r.width+32,r.height+32),Alpha(flash,.22f));
            if(texture!=null)GUI.DrawTexture(r,texture);
            float art=r.width*.7f;PortraitArt(new Rect(r.center.x-art*.5f,r.center.y-art*.52f,art,art),index);
        }
        private void PortraitArt(Rect r,int index)
        {
            if(TowerMythArt.IsMythIndex(index))DrawMythPortrait(r,index);
            else{var texture=TowerActorArt.Portrait(index);if(texture)GUI.DrawTexture(r,texture,ScaleMode.ScaleToFit,true);}
        }
        /// <summary>Large serif title in pale gold over a soft shadow and glow.</summary>
        private void GildedTitle(Rect r,string text,int size,TextAnchor align=TextAnchor.MiddleLeft)
        {
            Title(new Rect(r.x+1,r.y+4,r.width,r.height),text,size,new Color(0,0,0,.7f),align);
            // Large titles carry a bronze under-edge; at small sizes it would only muddy the glyphs.
            if(size>=30)Title(new Rect(r.x,r.y+1,r.width,r.height),text,size,new Color(.55f,.38f,.14f,.9f),align);
            else Title(new Rect(r.x-1,r.y+2,r.width,r.height),text,size,new Color(0,0,0,.45f),align);
            Title(r,text,size,new Color(.99f,.92f,.76f),align);
        }
        // Full-screen modal backdrop: the painted night over the tower on the title, a deep veil over the world elsewhere.
        private void ScreenBackdrop(bool painted,float veil=.86f)
        {
            var art=painted?Kit("menu-backdrop"):null;
            if(art!=null)
            {
                Fill(Screenful,night);
                // Cover the 16:9 layout; wider screens extend it to the sides.
                float w=1600*1.02f,h=900*1.02f;GUI.DrawTexture(new Rect(800-w*.5f,450-h*.5f,w,h),art,ScaleMode.ScaleAndCrop);
                Light(new Rect(0,0,1600,320),new Color(.01f,.014f,.03f,.55f));
            }
            else
            {
                Fill(Screenful,new Color(.014f,.02f,.045f,veil));
                Light(new Rect(0,0,1600,260),new Color(.01f,.014f,.03f,.6f));
                Halo(new Rect(300,-380,1000,760),new Color(.5f,.4f,.22f,.08f));
            }
            Grain(Screenful,.035f,true);
            Vignette(new Color(0,0,0,.85f));
        }
        private void MenuBackdrop(string title,string eyebrow,float veil=.86f)
        {
            ScreenBackdrop(intro,veil);
            Label(new Rect(66,34,1100,23),eyebrow,13,brass,TextAnchor.MiddleLeft,true);
            GildedTitle(new Rect(62,62,1170,74),title,44);
            Divider(new Rect(64,146,1472,10),.85f,true);
        }
        private void Keycap(Rect r,string text)
        {Slice(r,Kit("keycap"),16,Color.white);Label(new Rect(r.x,r.y-1,r.width,r.height-1),text,13,paper,TextAnchor.MiddleCenter,true);}
        private void Meter(Rect r,float value,Color color,float trail=0,bool ticks=false)
        {
            Slice(r,Kit("meter-trough"),16,Color.white);
            Rect inner=new Rect(r.x+3,r.y+3,r.width-6,r.height-6);float radius=inner.height*.5f,fill=Mathf.Clamp01(value)*inner.width;
            if(trail>value)Round(new Rect(inner.x,inner.y,inner.width*Mathf.Clamp01(trail),inner.height),new Color(.96f,.88f,.72f,.42f),radius);
            var gloss=Kit("meter-fill");
            if(fill>0&&gloss!=null)
            {
                GUI.DrawTexture(new Rect(inner.x,inner.y,fill,inner.height),gloss,ScaleMode.StretchToFill,true,0,color,0,Mathf.Min(radius,fill*.5f));
                Fill(new Rect(inner.x+Mathf.Max(0,fill-2),inner.y+2,Mathf.Min(2,fill),inner.height-4),Alpha(Color.Lerp(color,Color.white,.55f),.85f));
            }
            if(ticks)for(int i=1;i<4;i++)Fill(new Rect(inner.x+inner.width*i/4,inner.y+1,1,inner.height-2),new Color(0,0,0,.4f));
            Slice(r,Kit("meter-rim-gold"),16,Color.white);
        }
        private void TickHUD(float dt)
        {
            if(hero==null||run==null)return;
            float current=Mathf.Clamp01(hero.hp/PlayerMaxHealth);
            if(hudActor!=hero||hudHealth<0){hudActor=hero;hudHealth=hudTrail=current;hudDamageHold=hudHitFlash=0;return;}
            if(current<hudHealth-.001f){hudDamageHold=.45f;hudHitFlash=.4f;}
            hudHealth=current;hudHitFlash=Mathf.Max(0,hudHitFlash-dt);
            if(hudTrail<current)hudTrail=current;
            hudDamageHold-=dt;if(hudDamageHold<=0)hudTrail=Mathf.MoveTowards(hudTrail,current,dt*.55f);
        }
        private static int IconFor(string stat)
        {
            switch(stat)
            {
                case "공격력": return (int)UIIcon.Sword;
                case "공격 간격": case "등반 시간": case "원정 시간": return (int)UIIcon.Hourglass;
                case "사거리": return (int)UIIcon.Target;
                case "이동 속도": return (int)UIIcon.Speed;
                case "스킬 위력": case "서약 연계": return (int)UIIcon.Star;
                case "최대 생명력": return (int)UIIcon.Heart;
                case "방어력": case "무피격 클리어": return (int)UIIcon.Shield;
                case "치유 배율": case "5초마다 회복": return (int)UIIcon.Plus;
                case "처치": return (int)UIIcon.Skull;
                case "모은 골드": return (int)UIIcon.Coin;
                case "스킬 포인트": return (int)UIIcon.Crystal;
                default: return -1;
            }
        }
        private void StatCell(Rect r,string name,string value)
        {
            int icon=IconFor(name);float x=r.x;
            if(icon>=0){Icon(new Rect(r.x-2,r.y+20,30,30),(UIIcon)icon);x+=34;}
            Label(new Rect(x,r.y,r.width-(x-r.x),22),name,14,muted);Title(new Rect(x,r.y+23,r.width-(x-r.x),38),value,26,paper,TextAnchor.UpperLeft);
        }
        private void FitLabel(Rect r,string text,int size,int minimum,Color color,TextAnchor align=TextAnchor.UpperLeft,bool bold=false)
        {
            textStyle.wordWrap=true;textStyle.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;
            textStyle.fontSize=size;
            while(size>minimum&&textStyle.CalcHeight(new GUIContent(text),r.width)>r.height)textStyle.fontSize=--size;
            Label(r,text,size,color,align,bold);
        }
        private static string ConciseDescription(string text)
        {return text.Replace(" · 반복 선택 시 누적","").Replace("현재 ","").Replace("선택 후 ","");}
        private void PrepareScrollStyle()
        {
            if(uiSkin)return;
            uiSkin=Instantiate(GUI.skin);uiSkin.hideFlags=HideFlags.HideAndDontSave;
            uiScrollThumb=new Texture2D(1,1){name="UI scroll thumb",hideFlags=HideFlags.HideAndDontSave};
            uiScrollThumb.SetPixel(0,0,new Color(.72f,.6f,.38f,.8f));uiScrollThumb.Apply();
            uiSkin.verticalScrollbar=new GUIStyle{fixedWidth=4,margin=new RectOffset(6,0,0,0)};
            uiSkin.verticalScrollbarThumb=new GUIStyle{fixedWidth=4};
            uiSkin.verticalScrollbarThumb.normal.background=uiSkin.verticalScrollbarThumb.hover.background=uiSkin.verticalScrollbarThumb.active.background=uiScrollThumb;
            uiSkin.verticalScrollbarUpButton=new GUIStyle{fixedHeight=0};
            uiSkin.verticalScrollbarDownButton=new GUIStyle{fixedHeight=0};
        }
        /// <summary>A scroll view in the UI skin; text inside it is drawn through the scaled GUI matrix.</summary>
        private Vector2 BeginScroll(Rect viewport,Vector2 scroll,Rect content)
        {
            PrepareScrollStyle();scrollSkin=GUI.skin;GUI.skin=uiSkin;scrollDepth++;
            return GUI.BeginScrollView(viewport,scroll,content,false,true);
        }
        private GUISkin scrollSkin;
        private void EndScroll(){GUI.EndScrollView();scrollDepth--;GUI.skin=scrollSkin;}
        private static readonly Rect ShopButtonRect=new Rect(1190,716,346,56);
        private bool HUDHit(Vector2 p)
        {
            if(village)return VillageHUDHit(p);
            if(new Rect(1280,28,256,42).Contains(p)||new Rect(28,18,444,104).Contains(p))return true;
            for(int i=0;i<run.Companions.Count;i++)if(new Rect(36+i*150,790,140,76).Contains(p))return true;
            if(run.FloorType==FloorType.Shop&&run.IsFloorCleared&&NearMerchant&&ShopButtonRect.Contains(p))return true;
            return run.IsFloorCleared&&!run.IsRewardPending&&new Rect(1190,790,346,64).Contains(p);
        }
    }
}
