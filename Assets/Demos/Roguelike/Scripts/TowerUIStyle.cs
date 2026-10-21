using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerGame
    {
        private readonly Color brass=new Color(.82f,.70f,.47f),crimson=new Color(.79f,.23f,.28f),silver=new Color(.65f,.76f,.84f);
        private Texture2D uiGradient,uiHalo,uiDiamond;
        private float hudHealth=-1,hudTrail,hudDamageHold,hudHitFlash;
        private Fighter hudActor;
        private GUISkin uiSkin;
        private Texture2D uiScrollThumb;
        private static Color Alpha(Color c,float a){c.a=a;return c;}
        private static Rect Inset(Rect r,float n){return new Rect(r.x+n,r.y+n,r.width-n*2,r.height-n*2);}
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
        }
        private void Light(Rect r,Color c)
        {EnsureUITextures();GUI.color=c;GUI.DrawTexture(r,uiGradient);GUI.color=Color.white;}
        private void Halo(Rect r,Color c)
        {EnsureUITextures();GUI.color=c;GUI.DrawTexture(r,uiHalo);GUI.color=Color.white;}
        private void Panel(Rect r,Color accent)
        {
            Round(new Rect(r.x,r.y+5,r.width,r.height),new Color(0,0,0,.3f),9);
            Round(r,new Color(.22f,.27f,.32f,.9f),8);
            Round(Inset(r,1),ink,7);
            Light(new Rect(r.x+2,r.y+2,r.width-4,Mathf.Min(100,r.height-4)),Alpha(accent,.045f));
            Fill(new Rect(r.x+16,r.y+1,r.width-32,1),new Color(1,1,1,.045f));
        }
        private void SelectionFrame(Rect r,Color accent,bool selected)
        {
            if(!selected)return;
            Round(r,Alpha(accent,.85f),8);Round(Inset(r,2),new Color(.065f,.09f,.115f),6);
            Light(Inset(r,3),Alpha(accent,.11f));
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
            Color border=available?(primary?accent:new Color(.28f,.33f,.38f)):new Color(.16f,.19f,.23f);
            Color body=primary?Color.Lerp(accent,new Color(.43f,.32f,.19f),.23f):new Color(.105f,.135f,.17f);
            if(!available)body=new Color(.07f,.09f,.115f);
            else if(hover)body=Color.Lerp(body,primary?Color.white:accent,primary?.15f:.18f);
            if(pressed)body*=.82f;
            Round(new Rect(r.x,r.y+3,r.width,r.height),new Color(0,0,0,.35f),5);
            Round(r,border,5);Round(Inset(r,1),body,4);
            if(available)Light(Inset(r,2),new Color(1,1,1,primary?.12f:.035f));
            bool previous=GUI.enabled;GUI.enabled=available;
            buttonStyle.fontSize=18;buttonStyle.normal.textColor=buttonStyle.hover.textColor=buttonStyle.active.textColor=buttonStyle.focused.textColor=
                available?(primary?new Color(.13f,.105f,.065f):paper):muted;
            bool clicked=GUI.Button(r,text,buttonStyle);GUI.enabled=previous;return clicked;
        }
        private void Portrait(Rect r,int index,Color frame)
        {
            Round(r,new Color(.22f,.27f,.31f),8);Round(Inset(r,1),new Color(.065f,.085f,.11f),7);
            Halo(Inset(r,2),Alpha(frame,.32f));PortraitArt(Inset(r,3),index);
        }
        private void PortraitArt(Rect r,int index)
        {
            if(TowerMythArt.IsMythIndex(index))DrawMythPortrait(r,index);
            else{var texture=TowerActorArt.Portrait(index);if(texture)GUI.DrawTexture(r,texture,ScaleMode.ScaleToFit,true);}
        }
        private void MenuBackdrop(string title,string eyebrow)
        {
            Fill(new Rect(-200,-200,2000,1300),new Color(.018f,.027f,.043f,1));
            Halo(new Rect(660,-350,1100,1100),new Color(.19f,.27f,.34f,.45f));
            Light(new Rect(0,0,1600,230),new Color(.16f,.20f,.27f,.18f));
            Fill(new Rect(64,151,1472,1),new Color(.5f,.56f,.63f,.15f));
            Label(new Rect(66,35,1100,23),eyebrow,13,brass,TextAnchor.MiddleLeft,true);
            Label(new Rect(62,69,1170,66),title,43,paper,TextAnchor.MiddleLeft,true);
        }
        private void Keycap(Rect r,string text)
        {Round(r,new Color(.24f,.29f,.34f),4);Round(Inset(r,1),new Color(.08f,.105f,.135f),3);Label(r,text,13,paper,TextAnchor.MiddleCenter,true);}
        private void Meter(Rect r,float value,Color color,float trail=0,bool ticks=false)
        {
            Round(r,new Color(.28f,.30f,.33f,.9f),4);Round(Inset(r,1),new Color(.025f,.032f,.045f),3);
            Rect inner=Inset(r,3);float fill=Mathf.Clamp01(value)*inner.width;
            if(trail>value)Round(new Rect(inner.x,inner.y,inner.width*Mathf.Clamp01(trail),inner.height),Alpha(brass,.65f),2);
            if(fill>0)
            {
                Round(new Rect(inner.x,inner.y,fill,inner.height),Color.Lerp(color,Color.black,.25f),2);
                Light(new Rect(inner.x,inner.y,fill,inner.height),Alpha(color,.85f));
                Fill(new Rect(inner.x,inner.y+1,fill,1),new Color(1,1,1,.22f));
                Fill(new Rect(inner.x+Mathf.Max(0,fill-2),inner.y,Mathf.Min(2,fill),inner.height),Alpha(Color.Lerp(color,Color.white,.5f),.9f));
            }
            if(ticks)for(int i=1;i<4;i++)Fill(new Rect(inner.x+inner.width*i/4,inner.y,1,inner.height),new Color(0,0,0,.3f));
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
            uiScrollThumb.SetPixel(0,0,new Color(.32f,.39f,.45f));uiScrollThumb.Apply();
            uiSkin.verticalScrollbar=new GUIStyle{fixedWidth=5,margin=new RectOffset(6,0,0,0)};
            uiSkin.verticalScrollbarThumb=new GUIStyle{fixedWidth=5};
            uiSkin.verticalScrollbarThumb.normal.background=uiSkin.verticalScrollbarThumb.hover.background=uiSkin.verticalScrollbarThumb.active.background=uiScrollThumb;
            uiSkin.verticalScrollbarUpButton=new GUIStyle{fixedHeight=0};
            uiSkin.verticalScrollbarDownButton=new GUIStyle{fixedHeight=0};
        }
        private bool HUDHit(Vector2 p)
        {
            if(new Rect(1280,28,256,42).Contains(p)||new Rect(36,27,420,92).Contains(p))return true;
            for(int i=0;i<run.Companions.Count;i++)if(new Rect(36+i*150,790,140,76).Contains(p))return true;
            return run.IsFloorCleared&&!run.IsRewardPending&&new Rect(1190,790,346,64).Contains(p);
        }
    }
}
