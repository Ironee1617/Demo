using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // The practice range's screen: the hero's plate and the selected skill with its numbers and level bonus across the top,
    // the yard's settings on the left, the latest cast's damage and the target status (with the reaction it sets up) on the
    // right, and every skill grouped by build style in two rows along the bottom.
    public sealed partial class TowerGame
    {
        private static readonly Rect PracticeSettingsRect=new Rect(28,146,272,506),PracticeStatusRect=new Rect(1290,196,282,206);
        private const float PracticeTileWidth=74,PracticeTileHeight=60,PracticeTileGap=6,PracticeGroupGap=20;
        // Bar rows: melee, ranged and automatic skills on the first, spells and support on the second.
        private static readonly float[] PracticeRowY={708,800};
        private static int PracticeRowOf(int style){return style<=(int)SkillStyle.Auto?0:1;}
        // Icons by SkillEffect.
        private static readonly UIIcon[] PracticeIcons={UIIcon.Flame,UIIcon.Crystal,UIIcon.Star,UIIcon.Flame,UIIcon.Star,UIIcon.Sword,UIIcon.Sword,UIIcon.Speed,
            UIIcon.Target,UIIcon.Target,UIIcon.Target,UIIcon.Skull,UIIcon.Heart,UIIcon.Shield,UIIcon.Plus,UIIcon.Tower,
            UIIcon.Coin,UIIcon.Speed,UIIcon.Target,UIIcon.Sword,UIIcon.Tower,UIIcon.Crystal,UIIcon.Star,UIIcon.Flame,UIIcon.Hourglass,UIIcon.Skull,UIIcon.Shield,UIIcon.Plus};
        private static Rect[] practiceTiles;
        private static Rect PracticeTileRect(int index)
        {
            if(practiceTiles==null)
            {
                practiceTiles=new Rect[PracticeEffects.Length];
                for(int row=0;row<2;row++)
                {
                    float width=0;int last=-1;
                    for(int i=0;i<PracticeEffects.Length;i++)
                    {
                        int style=PracticeStyleOf(i);if(PracticeRowOf(style)!=row)continue;
                        if(last>=0)width+=style!=last?PracticeGroupGap+PracticeTileGap:PracticeTileGap;width+=PracticeTileWidth;last=style;
                    }
                    float x=800-width*.5f;last=-1;
                    for(int i=0;i<PracticeEffects.Length;i++)
                    {
                        int style=PracticeStyleOf(i);if(PracticeRowOf(style)!=row)continue;
                        if(last>=0)x+=style!=last?PracticeGroupGap+PracticeTileGap:PracticeTileGap;
                        practiceTiles[i]=new Rect(x,PracticeRowY[row],PracticeTileWidth,PracticeTileHeight);x+=PracticeTileWidth;last=style;
                    }
                }
            }
            return practiceTiles[index];
        }
        private static bool PracticeSupport(SkillEffect effect){return TowerSkillBuilds.Support(effect);}

        private void DrawPracticeHUD()
        {
            var effect=PracticeEffect;var skill=PracticeSkill();Color accent=SkillColor(effect);
            Vignette(new Color(0,0,0,.42f));
            Light(new Rect(0,0,1600,170),new Color(.01f,.014f,.03f,.62f));
            // The same falloff turned upside down shades the skill bar over the ground.
            EnsureUITextures();GUI.color=new Color(.01f,.014f,.03f,.82f);GUI.DrawTextureWithTexCoords(new Rect(0,650,1600,250),uiGradient,new Rect(0,1,1,-1));GUI.color=Color.white;
            Light(new Rect(60,668,1480,224),new Color(.01f,.014f,.03f,.5f));

            Panel(new Rect(92,32,400,80),brass);
            Title(new Rect(150,36,330,28),"스킬 연습장",19,paper,TextAnchor.MiddleLeft);
            Label(new Rect(150,64,330,20),"생명력 "+Mathf.CeilToInt(hero.hp)+" / "+Mathf.CeilToInt(PlayerMaxHealth)+(PlayerShield>0?"  ·  보호막 "+Mathf.CeilToInt(PlayerShield):""),13,PlayerShield>0?TowerWorld.Teal:muted);
            Label(new Rect(150,85,330,20),"공격력 "+run.Player.Damage.ToString("0")+"  ·  스킬 위력 "+run.Player.SkillPowerMultiplier.ToString("0.##")+"배",13,brass);
            if(Hit(new Rect(28,18,104,104)))OpenParty("player");
            Medallion(new Rect(26,16,108,108),0,brass);

            // The selected skill: its style and level, its numbers at that level and what that level adds.
            // Bright skies (Olympus) wash out the text lines under the name, so the shade reaches past their width.
            Halo(new Rect(300,-150,1000,430),new Color(.01f,.014f,.03f,.86f));
            string name=PracticeName(effect);titleStyle.fontSize=31;float half=Mathf.Min(250,titleStyle.CalcSize(new GUIContent(name)).x*.5f+18);
            Flourish(new Rect(800-half-112,32,112,22),false,.85f);Flourish(new Rect(800+half,32,112,22),true,.85f);
            GildedTitle(new Rect(500,16,600,50),name,31,TextAnchor.MiddleCenter);
            string line=TowerSkillBuilds.Tag(effect)+"  ·  Lv."+PracticeLevel+"  ·  "+skill.Definition.Description;
            Title(new Rect(381,66,840,24),line,16,new Color(0,0,0,.75f),TextAnchor.MiddleCenter);
            Title(new Rect(380,64,840,24),line,16,Color.Lerp(accent,paper,.45f),TextAnchor.MiddleCenter);
            ShadowLabel(new Rect(380,90,840,22),PracticeStats(skill),14,new Color(.84f,.86f,.92f),TextAnchor.MiddleCenter);
            string bonus=TowerSkillBuilds.LevelText(effect,PracticeLevel==2?2:3);
            ShadowLabel(new Rect(380,112,840,22),PracticeLevel==1?"레벨을 올리면  ·  "+bonus:bonus,15,PracticeLevel==3?TowerWorld.Gold:PracticeLevel==2?paper:new Color(.86f,.84f,.76f),TextAnchor.MiddleCenter,true);
            if(noticeTime>0&&!menu&&!party&&!help)
            {
                float a=Mathf.Clamp01(noticeTime*2);
                Banner(new Rect(400,146,800,44),a);
                Label(new Rect(420,148,760,40),notice,16,Alpha(paper,a),TextAnchor.MiddleCenter);
            }

            if(Button(new Rect(1280,28,132,42),"마을로",silver)){LeavePractice();return;}
            if(Button(new Rect(1424,28,112,42),"메뉴",muted))menu=!menu;Keycap(new Rect(1496,38,32,22),"Esc");
            // The latest cast's total, so levels, target counts and statuses can be compared.
            Halo(new Rect(1190,46,480,190),new Color(.01f,.014f,.03f,.62f));
            bool support=PracticeSupport(effect);
            ShadowLabel(new Rect(1236,84,300,20),PracticeCasts==0?"시전하면 피해 합계가 여기에 나옵니다":support?"회복·보호막 스킬  ·  머리 위 숫자":"이번 시전 피해",13,new Color(.84f,.86f,.92f),TextAnchor.MiddleRight,true);
            if(PracticeCasts>0&&!support)GildedTitle(new Rect(1236,102,300,46),Mathf.RoundToInt(PracticeDamage).ToString("#,0"),36,TextAnchor.MiddleRight);
            if(PracticeCasts>0&&!support)ShadowLabel(new Rect(1236,150,300,20),"적중 "+PracticeHits+"회",13,paper,TextAnchor.MiddleRight);

            DrawPracticeSettings(accent);
            DrawPracticeStatus(effect);

            for(int style=0;style<TowerSkillBuilds.StyleCount;style++)
            {
                int firstTile=-1,lastTile=-1;
                for(int i=0;i<PracticeEffects.Length;i++)if(PracticeStyleOf(i)==style){if(firstTile<0)firstTile=i;lastTile=i;}
                if(firstTile<0)continue;
                Rect a=PracticeTileRect(firstTile),b=PracticeTileRect(lastTile);Color c=StyleColor((SkillStyle)style);
                Divider(new Rect(a.x,a.y-9,b.xMax-a.x,8),.5f,false,Color.Lerp(c,Color.white,.4f));
                ShadowLabel(new Rect(a.x,a.y-30,b.xMax-a.x,21),TowerSkillBuilds.StyleName((SkillStyle)style),15,Color.Lerp(c,Color.white,.15f),TextAnchor.MiddleCenter,true);
            }
            bool winding=hero.castRemaining>0;
            for(int i=0;i<PracticeEffects.Length;i++)
            {
                var e=PracticeEffects[i];Rect r=PracticeTileRect(i);Color c=SkillColor(e);bool selected=i==PracticeIndex;
                if(selected&&winding)Halo(new Rect(r.x-26,r.y-26,r.width+52,r.height+52),Alpha(c,.3f+.15f*Mathf.Sin(Time.unscaledTime*18)));
                if(Tile(r,c,selected)){SelectPractice(i);QueuePractice();}
                Halo(new Rect(r.center.x-24,r.y+1,48,40),Alpha(c,selected?.5f:.24f));
                Icon(new Rect(r.center.x-14,r.y+6,28,28),PracticeIcons[(int)e],selected?1:.8f);
                FitLabel(new Rect(r.x+3,r.y+36,r.width-6,22),PracticeName(e),13,10,selected?paper:new Color(.8f,.82f,.88f),TextAnchor.MiddleCenter,selected);
            }
        }
        // Build styles keep one color each in the bar and the party panel.
        private static Color StyleColor(SkillStyle style)
        {
            switch(style)
            {
                case SkillStyle.Melee:return new Color(1,.68f,.36f);
                case SkillStyle.Ranged:return new Color(.98f,.86f,.44f);
                case SkillStyle.Auto:return new Color(.5f,.8f,1);
                case SkillStyle.Spell:return new Color(.74f,.6f,1);
                default:return new Color(.45f,.95f,.7f);
            }
        }

        private void DrawPracticeSettings(Color accent)
        {
            Rect p=PracticeSettingsRect;Panel(p,brass);
            float x=p.x+20,w=p.width-40,y=p.y+20,third=(w-16)/3;
            if(Button(new Rect(x,y,w,54),"시전",accent,!PracticeBusy,true))QueuePractice();Keycap(new Rect(x+w-46,y+16,32,22),"F");y+=72;

            Label(new Rect(x,y,w,20),"스킬 레벨",14,brass,TextAnchor.MiddleLeft,true);Label(new Rect(x,y,w,20),"1 · 2 · 3",12,muted,TextAnchor.MiddleRight);y+=26;
            for(int i=1;i<=3;i++)if(Button(new Rect(x+(i-1)*(third+8),y,third,38),"Lv."+i,i==PracticeLevel?brass:muted,true,i==PracticeLevel))SetPracticeLevel(i);
            y+=56;
            Label(new Rect(x,y,w,20),"표적 수",14,brass,TextAnchor.MiddleLeft,true);y+=26;
            for(int i=0;i<PracticeTargetCounts.Length;i++)
            {int n=PracticeTargetCounts[i];if(Button(new Rect(x+i*(third+8),y,third,38),n+"마리",n==PracticeTargetCount?brass:muted,true,n==PracticeTargetCount))SetPracticeTargets(n);}
            y+=56;
            Label(new Rect(x,y,w,20),"배경 신화",14,brass,TextAnchor.MiddleLeft,true);y+=26;
            if(Button(new Rect(x,y,46,38),"←",muted))ShiftPracticeTheme(-1);
            if(Button(new Rect(x+w-46,y,46,38),"→",muted))ShiftPracticeTheme(1);
            Title(new Rect(x+50,y,w-100,38),TowerThemeCatalog.Get(PracticeTheme).Name,18,paper,TextAnchor.MiddleCenter);
            y+=56;
            if(Button(new Rect(x,y,w,42),"자동 반복  ·  "+(PracticeAuto?"켜짐":"꺼짐"),PracticeAuto?TowerWorld.Teal:muted,true,PracticeAuto))PracticeAuto=!PracticeAuto;
            Keycap(new Rect(x+w-42,y+10,28,22),"T");y+=52;
            if(Button(new Rect(x,y,w,42),"제자리로",muted))ResetPracticeYard();
            Keycap(new Rect(x+w-42,y+10,28,22),"R");y+=56;
            Divider(new Rect(x,y-6,w,10),.4f);
            Keycap(new Rect(x,y+4,54,24),"Q / E");Label(new Rect(x+64,y+4,w-64,24),"스킬 고르기",13,muted,TextAnchor.MiddleLeft);y+=30;
            Label(new Rect(x,y+2,w,22),"아래 스킬 칸을 누르면 바로 시전",13,muted,TextAnchor.MiddleLeft);
        }

        // The status kept on the targets and the reaction the selected skill would set off against it.
        private void DrawPracticeStatus(SkillEffect effect)
        {
            Rect p=PracticeStatusRect;Panel(p,brass);
            float x=p.x+16,w=p.width-32,y=p.y+14;
            Label(new Rect(x,y,w,20),"표적 상태  ·  연계 시험",14,brass,TextAnchor.MiddleLeft,true);Keycap(new Rect(x+w-30,y-1,28,22),"G");y+=28;
            // Three over two, wide enough for each name on one line.
            float cell=(w-2*6)/3;
            for(int i=0;i<PracticeStatusNames.Length;i++)
                if(Button(new Rect(x+i%3*(cell+6),y+i/3*40,cell,34),PracticeStatusNames[i],i==PracticeStatus?brass:muted,true,i==PracticeStatus))SetPracticeStatus(i);
            y+=88;
            string hint;bool live=PracticeReaction(effect,PracticeStatus,out hint);
            FitLabel(new Rect(x,y,w,60),hint,14,11,live?TowerWorld.Gold:new Color(.8f,.82f,.88f),TextAnchor.UpperLeft,live);
        }
        /// <summary>What the selected skill does with the chosen status (true when it sets off a reaction), or what it sets up.</summary>
        private static bool PracticeReaction(SkillEffect effect,int status,out string text)
        {
            var element=TowerSkillBuilds.Element(effect);bool melee=TowerSkillBuilds.Style(effect)==SkillStyle.Melee;
            switch(status)
            {
                case 1:
                    if(element==SkillElement.Lightning){text="초전도!  번개가 둔화된 적에게 1.5배";return true;}
                    if(element==SkillElement.Fire){text="증기 폭발!  화염이 둔화된 적에게 1.4배 (둔화는 풀림)";return true;}
                    text="둔화된 적에게는 번개 1.5배(초전도), 화염 1.4배(증기 폭발)";return false;
                case 2:
                    if(element==SkillElement.Frost){text="증기 폭발!  냉기가 불타는 적에게 1.4배 (불은 꺼짐)";return true;}
                    if(element==SkillElement.Poison){text="독연!  독이 불타는 적에게 1.5배";return true;}
                    text="불타는 적에게는 냉기 1.4배(증기 폭발), 독 1.5배(독연)";return false;
                case 3:
                    if(element==SkillElement.Lightning){text="초전도!  번개가 기절한 적에게 1.5배";return true;}
                    if(melee){text="무방비!  근접 스킬이 기절한 적에게 1.35배";return true;}
                    text="기절한 적에게는 근접 스킬 1.35배(무방비), 번개 1.5배(초전도)";return false;
                case 4:text="약점 노출!  표식이 붙은 적은 모든 피해 +20%";return true;
            }
            switch(effect)
            {
                case SkillEffect.IceNova:case SkillEffect.PoisonCloud:text="둔화를 남깁니다  ·  번개·화염 스킬과 연계";break;
                case SkillEffect.Fireball:case SkillEffect.Meteor:text="화상을 남깁니다  ·  냉기·독 스킬과 연계";break;
                case SkillEffect.Hook:case SkillEffect.Quake:case SkillEffect.Vortex:text="기절을 남깁니다  ·  근접·번개 스킬과 연계";break;
                case SkillEffect.HuntMark:text="표식: 파티 전원의 피해 +20%";break;
                case SkillEffect.Curse:text="저주는 실전에서 대상이 쓰러지면 옆 적에게 번집니다";break;
                case SkillEffect.Bloom:text="씨앗은 피어날 때 주변을 치고 생명력을 회복합니다";break;
                case SkillEffect.WarBanner:text="깃발 곁의 영웅 공격 속도 +35% (Lv.2부터 +50%)";break;
                default:text="표적 상태를 골라 이 스킬의 연계를 시험하세요";break;
            }
            return false;
        }

        private string PracticeStats(SkillState skill)
        {
            var effect=skill.Definition.Effect;
            string amount=effect==SkillEffect.HealPulse?"회복":effect==SkillEffect.ShieldPulse?"보호막":"피해";
            return amount+" "+Mathf.RoundToInt(run.Player.Damage*skill.Power)+"  ·  범위 "+skill.Radius.ToString("0.#")+"  ·  수량 "+skill.Count+
                (skill.Duration>0?"  ·  지속 "+skill.Duration.ToString("0.#")+"초":"")+"  ·  원래 재사용 "+skill.Cooldown.ToString("0.#")+"초";
        }
        private bool PracticeHUDHit(Vector2 p)
        {
            return new Rect(28,18,464,104).Contains(p)||new Rect(1280,28,256,42).Contains(p)||PracticeSettingsRect.Contains(p)||PracticeStatusRect.Contains(p)||
                new Rect(100,PracticeRowY[0]-32,1400,PracticeRowY[1]+PracticeTileHeight+8-(PracticeRowY[0]-32)).Contains(p);
        }
    }
}
