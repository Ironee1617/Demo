using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerGame
    {
        private GUIStyle textStyle,buttonStyle;
        private TowerSmokeDriver captureDriver;
        private Font uiFont;
        private float uiScale=1; private Vector2 uiOffset;
        private string inspectedOwner="player";
        private Vector2 detailScroll;
        private readonly Color ink=new Color(.035f,.055f,.08f,.96f),paper=new Color(.90f,.92f,.91f),muted=new Color(.55f,.64f,.68f);
        private void OnGUI()
        {
            if(hero==null)return;
            if(captureDriver==null)captureDriver=GetComponent<TowerSmokeDriver>();
            bool captureHud=captureDriver!=null&&captureDriver.BeginHudCapture();
            if(textStyle==null)
            {
                uiFont=Resources.Load<Font>("Tower/Font");
                textStyle=new GUIStyle{font=uiFont,wordWrap=true,richText=false};
                buttonStyle=new GUIStyle{font=uiFont,alignment=TextAnchor.MiddleCenter,fontSize=19};buttonStyle.normal.textColor=paper;buttonStyle.hover.textColor=Color.white;
            }
            uiScale=Mathf.Min(Screen.width/1600f,Screen.height/900f);uiOffset=new Vector2((Screen.width-1600*uiScale)*.5f,(Screen.height-900*uiScale)*.5f);
            var oldMatrix=GUI.matrix;GUI.matrix=Matrix4x4.TRS(uiOffset,Quaternion.identity,Vector3.one*uiScale);
            DrawWorldLabels();
            if(intro){GUI.enabled=!help;DrawIntro();GUI.enabled=true;}
            else
            {
                GUI.enabled=!menu&&!party&&!help&&!run.IsRewardPending&&hero.hp>0&&!run.IsComplete;
                DrawHUD();
                GUI.enabled=!party&&!help;
                if(run.IsRewardPending)DrawRewards();
                GUI.enabled=!help;
                if(party)DrawParty();
                else if(menu)DrawMenu();
                else if(hero.hp<=0)DrawEnding(false);
                else if(run.IsComplete)DrawEnding(true);
            }
            GUI.enabled=true;if(help)DrawHelp();GUI.matrix=oldMatrix;GUI.color=Color.white;
            if(captureHud)captureDriver.EndHudCapture();
        }
        private void Fill(Rect rect,Color color){GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=Color.white;}
        private void Border(Rect r,Color c,float thickness=1)
        {Fill(new Rect(r.x,r.y,r.width,thickness),c);Fill(new Rect(r.x,r.yMax-thickness,r.width,thickness),c);Fill(new Rect(r.x,r.y,thickness,r.height),c);Fill(new Rect(r.xMax-thickness,r.y,thickness,r.height),c);}
        private void Panel(Rect r,Color edge)
        {Fill(r,ink);Border(r,new Color(edge.r,edge.g,edge.b,.45f));Fill(new Rect(r.x+14,r.y,48,2),edge);Fill(new Rect(r.xMax-62,r.yMax-2,48,2),edge);}
        private void Label(Rect r,string text,int size,Color color,TextAnchor align=TextAnchor.UpperLeft,bool bold=false)
        {textStyle.fontSize=size;textStyle.alignment=align;textStyle.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;textStyle.normal.textColor=color;GUI.Label(r,text,textStyle);}
        private bool Button(Rect r,string text,Color accent,bool enabled=true)
        {
            bool hover=r.Contains(Event.current.mousePosition);Fill(r,enabled?(hover?new Color(accent.r*.32f,accent.g*.32f,accent.b*.32f,.98f):new Color(.08f,.12f,.16f,.97f)):new Color(.06f,.08f,.1f,.8f));
            Border(r,enabled?new Color(accent.r,accent.g,accent.b,hover?1:.55f):new Color(.2f,.24f,.27f));
            bool previous=GUI.enabled;GUI.enabled=previous&&enabled;buttonStyle.fontSize=19;bool clicked=GUI.Button(r,text,buttonStyle);GUI.enabled=previous;return clicked;
        }
        private void Portrait(Rect r,int index,Color frame)
        {
            Fill(r,new Color(.10f,.14f,.18f));var t=TowerActorArt.Portrait(index);
            if(t!=null)GUI.DrawTexture(new Rect(r.x+3,r.y+3,r.width-6,r.height-6),t,ScaleMode.ScaleToFit,true);
            Border(r,new Color(frame.r,frame.g,frame.b,.6f));
        }
        private Color TierColor(RewardTier tier){return tier==RewardTier.Silver?new Color(.70f,.78f,.86f):tier==RewardTier.Gold?TowerWorld.Gold:new Color(.55f,.80f,1f);}
        private string TierName(RewardTier tier){return tier==RewardTier.Silver?"실버":tier==RewardTier.Gold?"골드":"다이아";}
        private string RoleName(CompanionRole role){return role==CompanionRole.Vanguard?"전사":role==CompanionRole.Archer?"궁수":role==CompanionRole.Mage?"마법사":"치유사";}
        private string BossAttackLabel(Fighter boss)
        {string pattern=BossPatternName(boss);return pattern=="CrownVolley"?"왕관 연사":pattern=="TwinSpiral"?"쌍나선 탄막":pattern=="LeapingSlam"?"도약 강타":"전투 준비";}
        private string CategoryName(RewardOffer offer){return offer.Kind==RewardKind.Recruit?"동료 영입":offer.Kind==RewardKind.Trait?"특성":offer.IsUpgrade?"스킬 강화":"스킬 습득";}
        internal void OpenParty(string owner)
        {party=true;menu=false;dismissId="";inspectedOwner=owner;detailScroll=Vector2.zero;}
        private Vector2 WorldGui(Vector2 p)
        {Vector3 s=view.WorldToScreenPoint(p);return new Vector2((s.x-uiOffset.x)/uiScale,(Screen.height-s.y-uiOffset.y)/uiScale);}
        private void DrawWorldLabels()
        {
            foreach(var e in enemies)
            {
                var p=WorldGui(e.pos+Vector2.up*(e.isBoss?3.1f:e.kind==3?1.8f:e.kind==2?1.6f:1.2f));Fill(new Rect(p.x-31,p.y,62,4),new Color(.05f,.04f,.06f,.9f));Fill(new Rect(p.x-31,p.y,62*e.hp/e.maxHp,4),e.isBoss?TowerWorld.Gold:e.airborne?TowerWorld.Teal:new Color(.82f,.36f,.32f));
                if(e.isBoss)Label(new Rect(p.x-105,p.y-24,210,22),"왕관의 수호자",15,TowerWorld.Gold,TextAnchor.MiddleCenter,true);
                if(e.airborne)Label(new Rect(p.x-84,p.y-24,168,22),"공중 · 원거리 전용",12,TowerWorld.Teal,TextAnchor.MiddleCenter);
                if(e.windup>0){Label(new Rect(p.x-105,p.y-49,210,24),(e.isBoss?BossAttackLabel(e):EnemyPatternName(e)=="MeleeSlam"?"내려치기":"탄막 경고")+" "+e.windup.ToString("0.0")+"초",14,TowerWorld.Gold,TextAnchor.MiddleCenter,true);}
            }
            int companionLabel=0;
            foreach(var a in allies)
            {
                var state=run.GetCompanion(a.id);if(state==null)continue;var p=WorldGui(a.pos+Vector2.up*1.7f);
                Label(new Rect(p.x-66,p.y-companionLabel++*15,132,25),state.Definition.Name+" · "+FormationName(state.Definition.Role),13,TowerWorld.Teal,TextAnchor.MiddleCenter);
            }
            foreach(var n in numbers){var p=WorldGui(n.pos);var c=n.color;c.a=Mathf.Min(1,n.life*3);Label(new Rect(p.x-50,p.y-15,100,35),n.text,23,c,TextAnchor.MiddleCenter,true);}
            if(!intro&&run.IsFloorCleared&&!run.IsRewardPending&&!run.IsComplete)
            {var p=WorldGui(new Vector2(TowerWorld.ExitX,TowerWorld.Ground+3.6f));Label(new Rect(p.x-95,p.y-25,190,50),"다음 층  ↑\n[E] 등반",18,TowerWorld.Teal,TextAnchor.MiddleCenter,true);}
        }
        private void DrawHUD()
        {
            Panel(new Rect(24,22,410,103),TowerWorld.Gold);Portrait(new Rect(37,34,76,76),0,TowerWorld.Gold);
            Label(new Rect(129,31,280,29),"서약의 탑",22,paper,TextAnchor.UpperLeft,true);
            if(PlayerShield>0)Label(new Rect(307,36,106,24),"보호막 "+Mathf.CeilToInt(PlayerShield),13,TowerWorld.Teal,TextAnchor.MiddleRight);
            Label(new Rect(130,67,285,23),"HP  "+Mathf.CeilToInt(hero.hp)+" / "+run.Player.MaxHealth.ToString("0")+"   방어 "+run.Player.Armor.ToString("0"),15,paper);
            Fill(new Rect(130,100,282,7),new Color(.18f,.13f,.16f));Fill(new Rect(130,100,282*Mathf.Clamp01(hero.hp/run.Player.MaxHealth),7),new Color(.77f,.33f,.31f));
            Panel(new Rect(647,22,306,103),TowerWorld.Gold);
            Label(new Rect(670,31,260,43),run.Floor.ToString("00")+" F",31,TowerWorld.Gold,TextAnchor.MiddleCenter,true);
            Label(new Rect(670,79,260,26),run.IsFloorCleared?"층 클리어":TowerDifficulty.IsBossFloor(run.Floor)?"BOSS · 3층 주기":"클리어하면 증강 1회",15,TowerDifficulty.IsBossFloor(run.Floor)?TowerWorld.Gold:muted,TextAnchor.MiddleCenter);
            if(Button(new Rect(1240,22,200,43),"영웅 · 동료 [Tab]",TowerWorld.Teal))OpenParty("player");
            if(Button(new Rect(1452,22,122,43),"메뉴 [Esc]",muted))menu=!menu;
            Label(new Rect(1260,82,314,30),run.IsFloorCleared?"CLEAR  ·  생명력 +18":"남은 수호자  "+enemies.Count,19,run.IsFloorCleared?TowerWorld.Teal:paper,TextAnchor.UpperRight);
            for(int i=0;i<run.MaxFloor;i++)
            {
                var c=i<run.ClearedFloors?TowerWorld.Gold:i==run.Floor-1?TowerWorld.Teal:new Color(.2f,.27f,.32f);
                bool bossMark=TowerDifficulty.IsBossFloor(i+1);
                Fill(new Rect(594+i*17,bossMark?138:140,8,bossMark?12:8),bossMark&&i>=run.ClearedFloors?new Color(.7f,.48f,.24f):c);
            }
            var activeBoss=enemies.Find(enemy=>enemy.isBoss&&enemy.hp>0);
            if(activeBoss!=null)
            {
                Panel(new Rect(450,158,700,62),TowerWorld.Gold);
                Label(new Rect(466,163,382,22),"왕관의 수호자 · "+BossAttackLabel(activeBoss),16,TowerWorld.Gold,TextAnchor.MiddleLeft,true);
                Label(new Rect(848,163,286,22),Mathf.CeilToInt(activeBoss.hp)+" / "+Mathf.CeilToInt(activeBoss.maxHp),14,paper,TextAnchor.MiddleRight);
                Fill(new Rect(467,196,666,9),new Color(.22f,.11f,.1f));Fill(new Rect(467,196,666*Mathf.Clamp01(activeBoss.hp/activeBoss.maxHp),9),new Color(.92f,.47f,.27f));
            }
            if(noticeTime>0&&!run.IsRewardPending)Label(new Rect(450,activeBoss!=null?226:179,700,46),notice,23,paper,TextAnchor.MiddleCenter,true);
            if(run.IsFloorCleared&&!run.IsRewardPending&&!run.IsComplete)
            {
                bool near=Mathf.Abs(hero.pos.x-TowerWorld.ExitX)<2;
                if(Button(new Rect(1150,707,390,56),near?"[E]  다음 층으로 올라가기":"오른쪽의 빛나는 문으로 이동  →",TowerWorld.Teal,near))NextFloor();
            }
            Panel(new Rect(24,790,1550,88),muted);
            for(int i=0;i<4;i++)
            {
                HeroState state=i==0?run.Player:i-1<run.Companions.Count?run.Companions[i-1]:null;
                float x=38+i*235;
                if(state==null)
                {Border(new Rect(x,801,221,64),new Color(.21f,.29f,.32f));Label(new Rect(x+13,811,197,46),"빈 동료 자리\n매 층 영입 기회",15,muted);continue;}
                var companion=i==0?null:run.Companions[i-1];Color c=companion==null?TowerWorld.Gold:TierColor(companion.Definition.Tier);
                if(Button(new Rect(x,800,223,66),"",c))OpenParty(state.Id);
                Portrait(new Rect(x+4,803,60,60),companion==null?0:companion.Definition.ArtIndex,c);
                Label(new Rect(x+75,808,144,23),state.Name+" Lv."+state.Level,17,paper,TextAnchor.UpperLeft,true);
                Label(new Rect(x+75,840,144,22),(companion==null?"":""+FormationName(companion.Definition.Role)+" · ")+"스킬 "+state.Skills.Count+" / 특성 "+state.Traits.Count,12,c);
            }
            Label(new Rect(996,807,550,27),"A/D 이동   Space 점프   J / 클릭 공격   Shift 회피",16,paper,TextAnchor.MiddleRight);
            Label(new Rect(996,842,550,24),"Ctrl 정밀 이동 · E 등반 · Tab 능력  |  "+(dashCooldown>0?"회피 "+dashCooldown.ToString("0.0")+"초":"회피 준비"),13,dashCooldown>0?muted:TowerWorld.Teal,TextAnchor.MiddleRight);
        }
        private void DrawIntro()
        {
            Fill(new Rect(0,0,1600,900),new Color(.015f,.025f,.04f,.43f));Panel(new Rect(100,158,740,575),TowerWorld.Gold);
            Label(new Rect(144,191,650,35),"T O W E R   O F   C O M P A N I O N S",17,TowerWorld.Gold);
            Label(new Rect(140,242,660,97),"서약의 탑",67,paper,TextAnchor.UpperLeft,true);
            Label(new Rect(145,352,640,74),"혼자 시작해, 함께 오른다.\n영웅과 동료의 스킬을 조합하는 횡스크롤 로그라이크",22,muted);
            Fill(new Rect(145,451,630,1),new Color(.39f,.35f,.25f));
            Label(new Rect(145,476,640,80),"매 층마다 세 가지 증강  ·  영입, 스킬, 특성\n15명의 동료  ·  최대 3명과 완성하는 나만의 파티",20,paper);
            if(Button(new Rect(145,590,355,64),"등반 시작  [Enter]",TowerWorld.Gold))StartClimb();
            if(Button(new Rect(518,590,260,64),"조작 · 규칙",TowerWorld.Teal))help=true;
            Label(new Rect(145,683,650,27),"PROTOTYPE 05   /   층별 성장 · 3층마다 보스",13,muted);
            Label(new Rect(1010,300,490,110),"한 층을 오를 때마다,\n새로운 가능성이 열린다.",31,paper,TextAnchor.MiddleRight,true);
            Label(new Rect(1015,453,485,108),"실버 40%    골드 40%    다이아 20%\n플레이어와 영입한 동료의 능력이 함께 등장합니다\n각 칸은 한 번씩 새로고침할 수 있습니다",17,TowerWorld.Gold,TextAnchor.UpperRight);
        }
        private void DrawRewards()
        {
            Fill(new Rect(0,0,1600,900),new Color(.015f,.025f,.045f,.94f));
            Label(new Rect(350,53,900,35),"FLOOR "+run.Floor.ToString("00")+"  /  OATH CHAMBER",17,TowerWorld.Gold,TextAnchor.MiddleCenter);
            Label(new Rect(240,104,1120,67),"이번 층의 증강을 선택하세요",39,paper,TextAnchor.MiddleCenter,true);
            Label(new Rect(200,183,1200,40),"영입 · 스킬 습득 · 스킬 강화 · 특성    |    실버 40% · 골드 40% · 다이아 20%",18,muted,TextAnchor.MiddleCenter);
            for(int i=0;i<run.Offers.Count;i++)
            {
                var o=run.Offers[i];var d=TowerRun.GetDefinition(o.OwnerId);Color c=TierColor(o.Tier);float x=202+i*408;
                string owner=d==null?"플레이어":d.Name;
                Panel(new Rect(x,246,380,485),c);Fill(new Rect(x,246,380,4),c);
                Label(new Rect(x+20,263,340,28),TierName(o.Tier)+"  /  "+CategoryName(o),17,c,TextAnchor.MiddleCenter,true);
                Portrait(new Rect(x+132,306,116,111),d==null?0:d.ArtIndex,c);
                Label(new Rect(x+20,428,340,27),owner+(o.Kind==RewardKind.Recruit?" · "+RoleName(d.Role)+" · "+FormationName(d.Role):" 전용"),18,c,TextAnchor.MiddleCenter);
                Label(new Rect(x+22,468,336,56),o.Title,22,paper,TextAnchor.MiddleCenter,true);
                string badge=o.Kind==RewardKind.Recruit?"고유 특성 · 영입 즉시 적용":o.Kind==RewardKind.Trait?"Lv."+o.NextLevel+"  ·  지속 효과":"Lv."+o.NextLevel+"  ·  재사용 "+o.Cooldown.ToString("0.0")+"초";
                Fill(new Rect(x+30,530,320,29),new Color(c.r*.17f,c.g*.17f,c.b*.17f,.8f));
                Label(new Rect(x+34,533,312,23),badge,15,c,TextAnchor.MiddleCenter);
                Label(new Rect(x+29,568,322,o.Kind==RewardKind.Recruit?85:105),o.Description,17,muted,TextAnchor.UpperCenter);
                if(o.Kind==RewardKind.Recruit)
                    Label(new Rect(x+26,649,328,29),"공격 "+d.Damage.ToString("0")+" · 간격 "+d.AttackInterval.ToString("0.00")+"초 · 사거리 "+d.Range.ToString("0.0"),14,c,TextAnchor.MiddleCenter);
                if(Button(new Rect(x+28,684,324,32),o.Kind==RewardKind.Recruit?"이 동료 영입":"이 증강 선택",c)){SelectOffer(o.Id);return;}
                if(Button(new Rect(x,749,380,43),run.CanReroll(i)?"↻  이 칸 새로고침  1 / 1":"새로고침 사용 완료  0 / 1",c,run.CanReroll(i))){run.Reroll(i);Sound(chimeSound,.25f);return;}
            }
            Label(new Rect(210,819,920,48),"동료 "+run.Companions.Count+" / 3  ·  빈자리가 있으면 영입도 등장합니다.\n영입한 동료와 플레이어의 스킬·특성은 함께 추첨됩니다.",16,muted);
            if(Button(new Rect(1160,820,264,45),"영웅 · 동료 상세",TowerWorld.Teal))OpenParty("player");
        }
        private void DrawParty()
        {
            Fill(new Rect(0,0,1600,900),new Color(.015f,.025f,.04f,.96f));
            Label(new Rect(160,66,1100,60),"영웅과 동료의 능력",37,paper,TextAnchor.UpperLeft,true);
            Label(new Rect(160,139,1280,39),"스킬은 전투 중 자동 발동합니다. 영웅을 선택하면 능력치, 습득 스킬과 남은 재사용 시간을 확인합니다.",18,muted);
            if(run.GetHero(inspectedOwner)==null)inspectedOwner="player";
            for(int i=0;i<4;i++)
            {
                HeroState s=i==0?run.Player:i-1<run.Companions.Count?run.Companions[i-1]:null;float x=160+i*322;
                if(s==null){Panel(new Rect(x,199,305,73),muted);Label(new Rect(x+15,214,275,44),"빈 동료 자리\n매 층 영입 증강에서 선택",15,muted,TextAnchor.MiddleCenter);continue;}
                var d=i==0?null:run.Companions[i-1].Definition;Color c=d==null?TowerWorld.Gold:TierColor(d.Tier);
                if(Button(new Rect(x,199,305,73),"",c)){inspectedOwner=s.Id;detailScroll=Vector2.zero;dismissId="";}
                if(inspectedOwner==s.Id)Border(new Rect(x,199,305,73),c,2);
                Portrait(new Rect(x+9,205,61,61),d==null?0:d.ArtIndex,c);
                Label(new Rect(x+84,209,207,27),s.Name+"  Lv."+s.Level,19,paper,TextAnchor.UpperLeft,true);
                Label(new Rect(x+84,244,207,23),d==null?"검의 서약자":FormationName(d.Role)+" · "+RoleName(d.Role)+" · "+TierName(d.Tier),14,c);
            }
            HeroState selected=run.GetHero(inspectedOwner)??run.Player;var companion=run.GetCompanion(selected.Id);var definition=companion==null?null:companion.Definition;
            Color accent=definition==null?TowerWorld.Gold:TierColor(definition.Tier);
            Panel(new Rect(160,300,436,444),accent);Portrait(new Rect(183,324,112,112),definition==null?0:definition.ArtIndex,accent);
            Label(new Rect(312,332,262,41),selected.Name,28,paper,TextAnchor.UpperLeft,true);
            Label(new Rect(312,383,262,47),definition==null?"플레이어 · 검의 서약자":definition.Title+"\n"+RoleName(definition.Role),17,accent);
            string stats;
            if(companion==null)
                stats="공격력 "+selected.Damage.ToString("0.0")+"    방어력 "+selected.Armor.ToString("0.0")+"\n최대 생명력 "+selected.MaxHealth.ToString("0")+"    이동 속도 "+selected.MoveSpeed.ToString("0.0")+"\n공격 간격 "+selected.AttackInterval.ToString("0.00")+"초    사거리 "+selected.Range.ToString("0.0");
            else
                stats="공격력 "+selected.Damage.ToString("0.0")+"    공격 간격 "+selected.AttackInterval.ToString("0.00")+"초\n사거리 "+selected.Range.ToString("0.0")+"    이동 속도 "+selected.MoveSpeed.ToString("0.0")+"\n스킬 위력 "+selected.SkillPowerMultiplier.ToString("0.00")+"배    "+(companion.HealAmount>0?"치유 "+companion.HealAmount.ToString("0.0"):"재사용 "+selected.CooldownMultiplier.ToString("0.00")+"배");
            Label(new Rect(185,464,383,84),stats,18,paper);
            if(companion!=null)Label(new Rect(185,549,385,20),FormationName(companion.Definition.Role)+" · "+(CanHitAirborne(companion)?"대공 가능":"지상 공격")+" · 피해는 플레이어만 받습니다",13,muted);
            Label(new Rect(185,573,385,28),"고유 특성",17,accent,TextAnchor.UpperLeft,true);
            Label(new Rect(185,614,385,102),definition==null?"검으로 가까운 적을 공격합니다. 점프와 무적 회피로 적의 공격을 피할 수 있습니다.\n증강으로 습득한 능력은 아래 상세 목록에 누적됩니다.":definition.Description,18,muted);
            Panel(new Rect(619,300,821,444),accent);
            DrawAbilities(new Rect(639,317,782,408),selected,accent);
            if(definition!=null)
            {
                bool confirm=dismissId==definition.Id;
                if(Button(new Rect(160,772,436,51),confirm?"해고 확정 · 습득한 능력도 제거":"이 동료 해고",confirm?new Color(1,.4f,.35f):muted))
                {if(confirm){Dismiss(definition.Id);inspectedOwner="player";detailScroll=Vector2.zero;return;}dismissId=definition.Id;}
            }
            else Label(new Rect(170,777,427,52),"플레이어는 해고할 수 없습니다.\n모든 능력은 이번 등반 동안 유지됩니다.",16,muted);
            Label(new Rect(620,780,520,52),"해고하면 해당 동료의 능력도 사라지며,\n다음 증강부터 다시 영입 기회가 생깁니다.",16,muted);
            if(Button(new Rect(1160,773,280,51),"돌아가기 [Tab]",TowerWorld.Gold)){party=false;dismissId="";}
        }
        private void DrawAbilities(Rect viewport,HeroState owner,Color accent)
        {
            int lockedCount=0;
            foreach(var augment in TowerRun.Augments)
                if(augment.OwnerId==owner.Id&&augment.Kind==RewardKind.Skill&&owner.GetSkill(augment.Id)==null)lockedCount++;
            float height=125+(owner.Skills.Count>0?owner.Skills.Count*148:53)+(owner.Traits.Count>0?owner.Traits.Count*136:56)+(lockedCount>0?58+lockedCount*119:0);
            detailScroll=GUI.BeginScrollView(viewport,detailScroll,new Rect(0,0,752,Mathf.Max(viewport.height-2,height)),false,true);
            float y=3;Label(new Rect(10,y,718,29),"습득한 스킬  "+owner.Skills.Count,21,accent,TextAnchor.UpperLeft,true);y+=43;
            if(owner.Skills.Count==0){Label(new Rect(12,y,718,43),"아직 습득한 스킬이 없습니다. 매 층 증강에서 새 스킬을 선택하세요.",17,muted);y+=53;}
            foreach(var skill in owner.Skills)
            {
                Fill(new Rect(5,y,725,137),new Color(.07f,.11f,.15f));Border(new Rect(5,y,725,137),new Color(.20f,.29f,.34f));
                Label(new Rect(19,y+10,410,28),skill.Definition.Name+"  Lv."+skill.Level,19,paper,TextAnchor.UpperLeft,true);
                float remaining=SkillRemaining(owner.Id,skill.Definition.Id);
                Label(new Rect(420,y+11,294,24),(remaining>0?"남음 "+remaining.ToString("0.0")+"초  /  ":"준비 완료  /  ")+skill.Cooldown.ToString("0.0")+"초",15,remaining>0?muted:TowerWorld.Teal,TextAnchor.UpperRight);
                Label(new Rect(20,y+44,690,80),skill.Description,17,muted);
                y+=148;
            }
            y+=11;Label(new Rect(10,y,718,29),"습득한 특성  "+owner.Traits.Count,21,accent,TextAnchor.UpperLeft,true);y+=43;
            if(owner.Traits.Count==0){Label(new Rect(12,y,718,49),"아직 추가 특성이 없습니다. 동료의 고유 특성은 영입 즉시 적용됩니다.",17,muted);y+=56;}
            foreach(var trait in owner.Traits)
            {
                Fill(new Rect(5,y,725,125),new Color(.07f,.11f,.15f));Border(new Rect(5,y,725,125),new Color(.20f,.29f,.34f));
                Label(new Rect(19,y+10,490,28),trait.Definition.Name+"  Lv."+trait.Level,19,paper,TextAnchor.UpperLeft,true);
                Label(new Rect(516,y+12,196,24),"지속 효과",15,accent,TextAnchor.UpperRight);
                Label(new Rect(20,y+44,690,74),trait.Description,17,muted);y+=136;
            }
            if(lockedCount>0)
            {
                y+=11;Label(new Rect(10,y,718,29),"아직 배우지 않은 전용 스킬  "+lockedCount,20,muted,TextAnchor.UpperLeft,true);y+=43;
                foreach(var augment in TowerRun.Augments)
                {
                    if(augment.OwnerId!=owner.Id||augment.Kind!=RewardKind.Skill||owner.GetSkill(augment.Id)!=null)continue;
                    Fill(new Rect(5,y,725,108),new Color(.045f,.075f,.10f));Border(new Rect(5,y,725,108),new Color(.16f,.23f,.27f));
                    Label(new Rect(19,y+10,402,28),augment.Name,18,muted,TextAnchor.UpperLeft,true);
                    Label(new Rect(412,y+12,302,24),"미습득 · 증강으로 해금",14,muted,TextAnchor.UpperRight);
                    Label(new Rect(20,y+43,690,57),augment.Description+"\n기본 재사용 "+augment.Cooldown.ToString("0.0")+"초",16,muted);
                    y+=119;
                }
            }
            GUI.EndScrollView();
        }
        private void DrawMenu()
        {
            Fill(new Rect(0,0,1600,900),new Color(.01f,.02f,.035f,.79f));Panel(new Rect(540,185,520,525),TowerWorld.Gold);
            Label(new Rect(575,222,450,60),"잠시 쉬어가기",35,paper,TextAnchor.MiddleCenter,true);
            if(Button(new Rect(600,322,400,56),"등반 계속 [Esc]",TowerWorld.Gold))menu=false;
            if(Button(new Rect(600,399,400,56),"조작 · 규칙",TowerWorld.Teal))help=true;
            if(Button(new Rect(600,476,400,56),"새로운 등반 시작",muted)){NewRun();intro=false;inspectedOwner="player";}
            if(Button(new Rect(600,553,400,56),"데모 선택으로",muted))BackToHub();
            Label(new Rect(580,654,440,30),"진행 시간 "+(int)elapsed/60+":"+((int)elapsed%60).ToString("00"),16,muted,TextAnchor.MiddleCenter);
        }
        private void DrawEnding(bool won)
        {
            Fill(new Rect(0,0,1600,900),new Color(.015f,.025f,.04f,.87f));Panel(new Rect(420,200,760,500),won?TowerWorld.Gold:new Color(.8f,.4f,.35f));
            Label(new Rect(470,251,660,74),won?"탑의 정상에 도달했습니다":"이번 서약은 여기까지",39,paper,TextAnchor.MiddleCenter,true);
            Label(new Rect(480,352,640,100),"도달 층  "+run.Floor+" / "+run.MaxFloor+"    ·    처치  "+kills+"\n동료  "+run.Companions.Count+" / 3    ·    등반 시간  "+(int)elapsed/60+":"+((int)elapsed%60).ToString("00"),23,muted,TextAnchor.MiddleCenter);
            if(Button(new Rect(490,508,620,58),"다시 등반하기",TowerWorld.Gold)){NewRun();intro=false;inspectedOwner="player";}
            if(Button(new Rect(490,589,620,48),"처음 화면으로",TowerWorld.Teal)){NewRun();intro=true;inspectedOwner="player";}
        }
        private void DrawHelp()
        {
            Fill(new Rect(0,0,1600,900),new Color(.015f,.025f,.04f,.97f));Panel(new Rect(230,100,1140,700),TowerWorld.Gold);
            Label(new Rect(280,140,1040,60),"조작과 서약의 규칙",35,paper,TextAnchor.UpperLeft,true);
            Label(new Rect(285,237,460,390),"A / D 또는 ← / →    이동\nSpace    점프\nJ 또는 마우스 왼쪽    검 공격\nShift    무적 회피 · 공격 중에도 사용 가능\nCtrl    정밀 이동 · 피격 중심 표시\nS / ↓    발판 아래로 내려가기\nE    클리어 후 문에서 등반\nTab    영웅 · 동료 능력 확인 / 해고\nEsc    일시정지 메뉴",20,paper);
            Label(new Rect(790,237,520,435),"적이 보여주는 점선은 발사 전 고정한 방향입니다. 옆으로 이동하거나 점프·회피로 피하세요. 청록색 선은 탄막의 빈틈입니다.\n\n전위는 주인공 앞, 중위 궁수는 약간 뒤, 후위 마법사·치유사는 더 뒤에 배치됩니다. 공중 적은 원거리 동료만 공격합니다. 마지막 원거리 동료를 해고하면 공중 적이 착지합니다.\n\n매 층 몬스터를 모두 처치하면 생명력 18을 회복하고 증강 세 장 중 하나를 선택합니다. 동료는 최대 3명이며 스킬은 자동 발동합니다.\n\n각 증강 칸은 한 번씩 새로고침할 수 있습니다.",18,muted);
            if(Button(new Rect(980,706,330,54),"알겠습니다",TowerWorld.Gold))help=false;
        }
    }
}
