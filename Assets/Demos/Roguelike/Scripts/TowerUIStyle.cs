using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Shared IMGUI look: a dim, blood-stained gothic theme with bone text, grain and vignette.
    public sealed partial class TowerGame
    {
        // brass is the theme accent (dried blood); gold is kept for the gold reward tier.
        private readonly Color brass=new Color(.84f,.27f,.24f),crimson=new Color(.66f,.07f,.1f),silver=new Color(.64f,.68f,.71f),gold=new Color(.80f,.62f,.34f);
        private readonly Color blood=new Color(.36f,.03f,.05f),rim=new Color(.26f,.1f,.11f);
        private Texture2D uiGradient,uiHalo,uiDiamond,uiGrain,uiVignette;
        private float hudHealth=-1,hudTrail,hudDamageHold,hudHitFlash;
        private Fighter hudActor;
        private GUISkin uiSkin;
        private Texture2D uiScrollThumb;
        private static readonly Rect Screenful=new Rect(-200,-200,2000,1300);
        private static Color Alpha(Color c,float a){c.a=a;return c;}
        private static Rect Inset(Rect r,float n){return new Rect(r.x+n,r.y+n,r.width-n*2,r.height-n*2);}
        private static float Hash(float seed){return Mathf.Repeat(Mathf.Sin(seed*12.9898f)*43758.5453f,1);}
        private void Round(Rect r,Color c,float radius=6)
        {GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,c,0,radius);}
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
            for(int y=0;y<128;y++)for(int x=0;x<128;x++){double n=random.NextDouble();uiGrain.SetPixel(x,y,n>.93?new Color(1,.95f,.9f,(float)(n-.93)*9):n<.05?new Color(0,0,0,.6f):Color.clear);}
            uiGrain.Apply();
            uiVignette=new Texture2D(128,128,TextureFormat.RGBA32,false){name="UI vignette",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<128;y++)for(int x=0;x<128;x++){float d=new Vector2((x-63.5f)/63.5f,(y-63.5f)/63.5f).magnitude;uiVignette.SetPixel(x,y,new Color(1,1,1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1.35f,d))));}
            uiVignette.Apply();
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
        // A few dried drips hang from the top edge; positions come from the rect so they stay put.
        private void Drips(Rect r,Color accent,int count=-1)
        {
            if(count<0)count=1+(int)(r.width/260);
            Color c=Color.Lerp(blood,Alpha(accent,accent.a),.25f);c.a=.85f*accent.a;
            for(int i=0;i<count;i++)
            {
                float seed=r.x*.131f+r.y*.717f+r.width*.07f+i*3.71f;
                float x=r.x+22+Hash(seed)*(r.width-44),length=5+Hash(seed+1.3f)*(r.height>90?24:10),width=2+Hash(seed+2.1f)*2;
                Round(new Rect(x,r.y+1,width,length),c,width*.5f);
                Round(new Rect(x-1,r.y+length-1,width+2,width+2),c,(width+2)*.5f);
            }
        }
        private void CornerMarks(Rect r,Color c)
        {
            const float n=14,t=2,o=5;
            Fill(new Rect(r.x+o,r.y+o,n,t),c);Fill(new Rect(r.x+o,r.y+o,t,n),c);
            Fill(new Rect(r.xMax-o-n,r.y+o,n,t),c);Fill(new Rect(r.xMax-o-t,r.y+o,t,n),c);
            Fill(new Rect(r.x+o,r.yMax-o-t,n,t),c);Fill(new Rect(r.x+o,r.yMax-o-n,t,n),c);
            Fill(new Rect(r.xMax-o-n,r.yMax-o-t,n,t),c);Fill(new Rect(r.xMax-o-t,r.yMax-o-n,t,n),c);
        }
        private void Panel(Rect r,Color accent)
        {
            Round(new Rect(r.x,r.y+6,r.width,r.height),new Color(0,0,0,.5f),6);
            Round(r,rim,5);
            Round(Inset(r,1),ink,4);
            Light(new Rect(r.x+2,r.y+2,r.width-4,Mathf.Min(120,r.height-4)),Alpha(accent,.07f));
            Grain(Inset(r,2),.07f);
            Fill(new Rect(r.x+16,r.y+1,r.width-32,1),new Color(1,.8f,.75f,.06f));
            CornerMarks(r,Alpha(Color.Lerp(accent,paper,.35f),.35f));
            Drips(r,accent);
        }
        private void SelectionFrame(Rect r,Color accent,bool selected)
        {
            if(!selected)return;
            Halo(new Rect(r.x-18,r.y-18,r.width+36,r.height+36),Alpha(accent,.18f));
            Round(r,Alpha(accent,.9f),6);Round(Inset(r,2),new Color(.05f,.025f,.03f),4);
            Light(Inset(r,3),Alpha(accent,.12f));
            Diamond(new Vector2(r.xMax-22,r.y+23),5,accent);
        }
        private void Diamond(Vector2 center,float size,Color color)
        {
            EnsureUITextures();float radius=size*.707107f;GUI.color=color;
            GUI.DrawTexture(new Rect(center.x-radius,center.y-radius,radius*2,radius*2),uiDiamond);
            GUI.color=Color.white;
        }
        private bool Button(Rect r,string text,Color accent,bool enabled=true,bool primary=false)
        {
            bool available=enabled&&GUI.enabled,hover=available&&r.Contains(Event.current.mousePosition);
            bool pressed=hover&&Input.GetMouseButton(0);
            Color border=available?(primary?accent:new Color(.36f,.15f,.15f)):new Color(.15f,.09f,.1f);
            Color body=primary?Color.Lerp(accent,Color.black,.48f):new Color(.075f,.045f,.05f);
            if(!available)body=new Color(.045f,.03f,.035f);
            else if(hover)body=Color.Lerp(body,accent,primary?.22f:.16f);
            if(pressed)body*=.8f;
            if(hover)Halo(new Rect(r.x-14,r.y-12,r.width+28,r.height+24),Alpha(accent,.2f));
            Round(new Rect(r.x,r.y+3,r.width,r.height),new Color(0,0,0,.45f),4);
            Round(r,border,4);Round(Inset(r,1),body,3);
            if(available)Light(Inset(r,2),new Color(1,.85f,.8f,primary?.09f:.03f));
            if(primary&&available)Fill(new Rect(r.x+8,r.yMax-3,r.width-16,1),Alpha(accent,.6f));
            bool previous=GUI.enabled;GUI.enabled=available;
            buttonStyle.fontSize=18;buttonStyle.normal.textColor=buttonStyle.hover.textColor=buttonStyle.active.textColor=buttonStyle.focused.textColor=
                available?paper:Alpha(muted,.7f);
            bool clicked=GUI.Button(r,text,buttonStyle);GUI.enabled=previous;return clicked;
        }
        private void Portrait(Rect r,int index,Color frame)
        {
            Round(r,rim,6);Round(Inset(r,1),new Color(.05f,.03f,.035f),5);
            Halo(Inset(r,2),Alpha(frame,.3f));PortraitArt(Inset(r,3),index);
        }
        private void PortraitArt(Rect r,int index)
        {
            if(TowerMythArt.IsMythIndex(index))DrawMythPortrait(r,index);
            else{var texture=TowerActorArt.Portrait(index);if(texture)GUI.DrawTexture(r,texture,ScaleMode.ScaleToFit,true);}
        }
        // Title text with a low red bleed and a rare flicker.
        private void HauntedTitle(Rect r,string text,int size)
        {
            float flicker=Mathf.PerlinNoise(Time.unscaledTime*2.3f,.17f)>.86f?.55f:1;
            Label(new Rect(r.x+2,r.y+3,r.width,r.height),text,size,new Color(.55f,.04f,.06f,.55f*flicker),TextAnchor.MiddleLeft,true);
            Label(r,text,size,Alpha(paper,flicker),TextAnchor.MiddleLeft,true);
        }
        private void MenuBackdrop(string title,string eyebrow)
        {
            Fill(Screenful,new Color(.014f,.008f,.011f,1));
            Halo(new Rect(560,-460,1240,1040),new Color(.42f,.04f,.06f,.26f));
            Halo(new Rect(-260,520,900,700),new Color(.2f,.03f,.05f,.18f));
            Light(new Rect(0,0,1600,240),new Color(.32f,.05f,.07f,.16f));
            Grain(Screenful,.09f,true);
            Vignette(new Color(0,0,0,.92f));
            Fill(new Rect(64,151,1472,1),new Color(.55f,.12f,.12f,.35f));
            Drips(new Rect(64,151,1472,24),Alpha(brass,.8f),5);
            Label(new Rect(66,35,1100,23),eyebrow,13,brass,TextAnchor.MiddleLeft,true);
            HauntedTitle(new Rect(62,69,1170,66),title,43);
        }
        private void Keycap(Rect r,string text)
        {Round(r,new Color(.38f,.14f,.14f),4);Round(Inset(r,1),new Color(.06f,.035f,.04f),3);Label(r,text,13,paper,TextAnchor.MiddleCenter,true);}
        private void Meter(Rect r,float value,Color color,float trail=0,bool ticks=false)
        {
            Round(r,rim,4);Round(Inset(r,1),new Color(.02f,.01f,.012f),3);
            Rect inner=Inset(r,3);float fill=Mathf.Clamp01(value)*inner.width;
            if(trail>value)Round(new Rect(inner.x,inner.y,inner.width*Mathf.Clamp01(trail),inner.height),new Color(.85f,.78f,.68f,.45f),2);
            if(fill>0)
            {
                Round(new Rect(inner.x,inner.y,fill,inner.height),Color.Lerp(color,Color.black,.35f),2);
                Light(new Rect(inner.x,inner.y,fill,inner.height),Alpha(color,.9f));
                Fill(new Rect(inner.x,inner.y+1,fill,1),new Color(1,.8f,.75f,.18f));
                Fill(new Rect(inner.x+Mathf.Max(0,fill-2),inner.y,Mathf.Min(2,fill),inner.height),Alpha(Color.Lerp(color,Color.white,.45f),.9f));
            }
            if(ticks)for(int i=1;i<4;i++)Fill(new Rect(inner.x+inner.width*i/4,inner.y,1,inner.height),new Color(0,0,0,.45f));
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
        private void StatCell(Rect r,string name,string value)
        {Label(new Rect(r.x,r.y,r.width,22),name,14,muted);Label(new Rect(r.x,r.y+25,r.width,36),value,27,paper,TextAnchor.UpperLeft,true);}
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
            uiScrollThumb.SetPixel(0,0,new Color(.45f,.16f,.16f));uiScrollThumb.Apply();
            uiSkin.verticalScrollbar=new GUIStyle{fixedWidth=5,margin=new RectOffset(6,0,0,0)};
            uiSkin.verticalScrollbarThumb=new GUIStyle{fixedWidth=5};
            uiSkin.verticalScrollbarThumb.normal.background=uiSkin.verticalScrollbarThumb.hover.background=uiSkin.verticalScrollbarThumb.active.background=uiScrollThumb;
            uiSkin.verticalScrollbarUpButton=new GUIStyle{fixedHeight=0};
            uiSkin.verticalScrollbarDownButton=new GUIStyle{fixedHeight=0};
        }
        private static readonly Rect ShopButtonRect=new Rect(1190,716,346,56);
        private bool HUDHit(Vector2 p)
        {
            if(village)return VillageHUDHit(p);
            if(new Rect(1280,28,256,42).Contains(p)||new Rect(36,27,420,92).Contains(p))return true;
            for(int i=0;i<run.Companions.Count;i++)if(new Rect(36+i*150,790,140,76).Contains(p))return true;
            if(run.FloorType==FloorType.Shop&&run.IsFloorCleared&&NearMerchant&&ShopButtonRect.Contains(p))return true;
            return run.IsFloorCleared&&!run.IsRewardPending&&new Rect(1190,790,346,64).Contains(p);
        }
    }
}
