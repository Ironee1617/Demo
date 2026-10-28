using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerGame
    {
        private GUIStyle textStyle,titleStyle,buttonStyle;
        private TowerSmokeDriver captureDriver;
        private Font uiFont,uiTitleFont;
        private float uiScale=1;private Vector2 uiOffset;
        private string inspectedOwner="player";
        private Vector2 detailScroll;
        private readonly Color ink=new Color(.03f,.04f,.08f,.97f),paper=new Color(.95f,.92f,.85f),muted=new Color(.64f,.67f,.75f);
        private void OnGUI()
        {
            if(hero==null)return;
            using(TowerProfiling.Hud.Auto())DrawGUI();
        }
        private void DrawGUI()
        {
            if(captureDriver==null)captureDriver=GetComponent<TowerSmokeDriver>();
            bool captureHud=captureDriver!=null&&captureDriver.BeginHudCapture();
            if(textStyle==null)
            {
                uiFont=Resources.Load<Font>("Tower/Font");uiTitleFont=Resources.Load<Font>("Tower/TitleFont")??uiFont;
                textStyle=new GUIStyle{font=uiFont,wordWrap=true,richText=false};
                titleStyle=new GUIStyle{font=uiTitleFont,wordWrap=true,richText=false};
                buttonStyle=new GUIStyle{font=uiFont,alignment=TextAnchor.MiddleCenter,fontSize=18};
            }
            uiScale=Mathf.Min(Screen.width/1600f,Screen.height/900f);uiOffset=new Vector2((Screen.width-1600*uiScale)*.5f,(Screen.height-900*uiScale)*.5f);
            var oldMatrix=GUI.matrix;GUI.matrix=Matrix4x4.TRS(uiOffset,Quaternion.identity,Vector3.one*uiScale);scrollDepth=0;
            // Full-screen menus cover the play HUD entirely, so it is not drawn beneath them.
            bool covered=Paused&&!menu;
            if(!intro&&!covered)DrawWorldLabels();
            if(intro){GUI.enabled=!help;DrawIntro();GUI.enabled=true;}
            else
            {
                GUI.enabled=!menu&&!party&&!help&&!shopOpen&&!VillageBlocking&&!run.IsRewardPending&&hero.hp>0&&!run.IsComplete;
                if(!covered){if(village)DrawVillageHUD();else DrawHUD();}GUI.enabled=!party&&!help&&!StoryOpen;
                if(run.IsRewardPending)DrawRewards();else if(shopOpen)DrawShop();
                else if(villagePanel==VillagePanel.Gate)DrawGatePanel();else if(villagePanel==VillagePanel.Shrine)DrawShrinePanel();
                else if(villagePanel==VillagePanel.SkillTree)DrawSkillTreePanel();
                GUI.enabled=!help&&!StoryOpen;
                if(party)DrawParty();else if(menu)DrawMenu();else if(hero.hp<=0)DrawEnding(false);else if(run.IsComplete){if(run.IsCampaign)DrawChapterComplete();else DrawEnding(true);}
            }
            GUI.enabled=!help;if(StoryOpen)DrawStory();
            GUI.enabled=true;if(help)DrawHelp();DrawToasts();GUI.matrix=oldMatrix;GUI.color=Color.white;
            if(captureHud)captureDriver.EndHudCapture();
        }
        private void Fill(Rect r,Color c){GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=Color.white;}
        private void Border(Rect r,Color c,float n=1)
        {Fill(new Rect(r.x,r.y,r.width,n),c);Fill(new Rect(r.x,r.yMax-n,r.width,n),c);Fill(new Rect(r.x,r.y,n,r.height),c);Fill(new Rect(r.x,r.yMax-n,n,r.height),c);}
        private void Label(Rect r,string text,int size,Color color,TextAnchor align=TextAnchor.UpperLeft,bool bold=false)
        {DrawText(r,text,textStyle,size,color,align,bold?FontStyle.Bold:FontStyle.Normal);}
        /// <summary>Serif display text for titles, names and large numbers.</summary>
        private void Title(Rect r,string text,int size,Color color,TextAnchor align=TextAnchor.UpperLeft)
        {DrawText(r,text,titleStyle,size,color,align,FontStyle.Normal);}
        /// <summary>Text with a soft dark drop so it reads over the world.</summary>
        private void ShadowLabel(Rect r,string text,int size,Color color,TextAnchor align=TextAnchor.UpperLeft,bool bold=false)
        {Label(new Rect(r.x+1,r.y+2,r.width,r.height),text,size,new Color(0,0,0,.75f*color.a),align,bold);Label(r,text,size,color,align,bold);}
        /// <summary>
        /// Lays text out at the screen's own pixel size, so glyphs stay sharp however far the 1600×900 layout is enlarged.
        /// Layouts at or below their own size, and text inside scroll views (whose clip keeps the scaled matrix), are drawn
        /// at layout size.
        /// </summary>
        private void DrawText(Rect r,string text,GUIStyle style,int size,Color color,TextAnchor align,FontStyle fontStyle)
        {
            style.alignment=align;style.fontStyle=fontStyle;style.normal.textColor=color;
            float s=uiScale;
            if(scrollDepth>0||s<1.02f){style.fontSize=size;GUI.Label(r,text,style);return;}
            style.fontSize=Mathf.Max(1,Mathf.RoundToInt(size*s));
            var saved=GUI.matrix;GUI.matrix=saved*Matrix4x4.Scale(new Vector3(1/s,1/s,1));
            GUI.Label(new Rect(r.x*s,r.y*s,r.width*s,r.height*s),text,style);
            GUI.matrix=saved;
        }
        private Color TierColor(RewardTier tier){return tier==RewardTier.Silver?silver:tier==RewardTier.Gold?gold:new Color(.5f,.84f,.92f);}
        private string TierName(RewardTier tier){return tier==RewardTier.Silver?"실버":tier==RewardTier.Gold?"골드":"다이아";}
        private string RoleName(CompanionRole role){return role==CompanionRole.Vanguard?"전사":role==CompanionRole.Archer?"궁수":role==CompanionRole.Mage?"마법사":"치유사";}
        private string CategoryName(RewardOffer offer){return offer.Kind==RewardKind.Oath?"서약 변형":offer.Kind==RewardKind.Recruit?"동료":offer.Kind==RewardKind.Trait?"능력치":offer.IsUpgrade?"스킬 강화":"스킬";}
        internal void OpenParty(string owner){party=true;menu=false;dismissId="";inspectedOwner=owner;detailScroll=Vector2.zero;}
        private Vector2 WorldGui(Vector2 p){var s=view.WorldToScreenPoint(p);return new Vector2((s.x-uiOffset.x)/uiScale,(Screen.height-s.y-uiOffset.y)/uiScale);}
        /// <summary>A small chamfered plate over the world for names, doors and prompts; brighter while in reach.</summary>
        private void Tag(Rect r,Color accent,bool active)
        {
            Slice(r,Kit("button-body"),24,new Color(.07f,.085f,.13f,active?.95f:.82f));
            Slice(r,Metal("button-edge",accent),24,Alpha(MetalTint(accent),active?1:.45f));
        }
        private void DrawWorldLabels()
        {
            foreach(var e in enemies)
            {
                if(e.isBoss||e.hp>=e.maxHp&&!e.isElite)continue;
                var p=WorldGui(e.pos+Vector2.up*(e.kind==3?1.8f:e.kind==2?1.6f:1.2f)*(e.isElite?TowerDifficulty.EliteScale:1));
                Round(new Rect(p.x-30,p.y-1,60,8),new Color(.01f,.012f,.025f,.9f),4);
                Round(new Rect(p.x-28,p.y+1,56*Mathf.Clamp01(e.hp/e.maxHp),4),e.isElite?brass:e.airborne?silver:crimson,2);
                Fill(new Rect(p.x-28,p.y+1,56*Mathf.Clamp01(e.hp/e.maxHp),1),new Color(1,1,1,.25f));
                if(e.isElite)Diamond(new Vector2(p.x-36,p.y+3),9,brass);
            }
            if(!intro&&run.FloorType!=FloorType.Normal&&run.FloorType!=FloorType.Elite)
            {
                bool shop=run.FloorType==FloorType.Shop,near=shop&&NearMerchant&&run.IsFloorCleared;
                var p=WorldGui(TowerWorld.FeaturePosition+Vector2.up*(shop?4.3f:2.2f));
                string text=!shop?"모닥불":run.ShopTraded?"거래 종료":near?"상인  [E] 거래":"상인";
                Color c=!shop?TowerWorld.Teal:run.ShopTraded?muted:gold;float w=near?156:124;
                Tag(new Rect(p.x-w*.5f,p.y-16,w,32),c,near&&!run.ShopTraded);
                Label(new Rect(p.x-w*.5f,p.y-16,w,32),text,15,c,TextAnchor.MiddleCenter,true);
            }
            foreach(var n in numbers)
            {
                var p=WorldGui(n.pos);var c=n.color;c.a=Mathf.Min(1,n.life*3);float w=Mathf.Max(90,n.text.Length*24+20);
                Title(new Rect(p.x-w*.5f+1,p.y-13,w,35),n.text,24,new Color(0,0,0,c.a*.75f),TextAnchor.MiddleCenter);
                Title(new Rect(p.x-w*.5f-1,p.y-15,w,35),n.text,24,new Color(0,0,0,c.a*.4f),TextAnchor.MiddleCenter);
                Title(new Rect(p.x-w*.5f,p.y-16,w,35),n.text,24,c,TextAnchor.MiddleCenter);
            }
            if(village){DrawVillageLabels();return;}
            if(!intro&&run.IsFloorCleared&&!run.IsRewardPending&&!run.IsComplete)
            {
                var near=NearestGate();
                if(!run.IsBranchFloor){var p=WorldGui(new Vector2(TowerWorld.ExitX,TowerWorld.Ground+3.4f));Keycap(new Rect(p.x-18,p.y-16,36,30),"E");}
                else foreach(var type in TowerWorld.BranchGates)
                {
                    // Each doorway names its destination; the one in reach shows the key.
                    var p=WorldGui(new Vector2(TowerWorld.GateX(type),TowerWorld.Ground+4.35f));Color c=TowerWorld.GateColor(type);bool active=near.HasValue&&near.Value==type;
                    Tag(new Rect(p.x-82,p.y-28,164,58),c,active);
                    Label(new Rect(p.x-74,p.y-23,148,24),FloorTypeName(type)+(active?"  [E]":""),16,c,TextAnchor.MiddleCenter,true);
                    Label(new Rect(p.x-74,p.y+1,148,20),FloorTypeSummary(type),12,muted,TextAnchor.MiddleCenter);
                }
            }
        }
        private void DrawHUD()
        {
            // A dark vignette frames the arena; under 35% health it pulses blood red.
            Vignette(new Color(0,0,0,.55f));
            float health=hero.hp/PlayerMaxHealth;
            if(health<.35f&&hero.hp>0)Vignette(new Color(.55f,0,.03f,(.25f+.2f*Mathf.Sin(Time.unscaledTime*5))*(1-health/.35f*.6f)));
            Light(new Rect(0,0,1600,175),new Color(.01f,.014f,.03f,.7f));
            // Soft shade behind the floor counter and the right-hand readouts keeps them legible over bright skies.
            Halo(new Rect(500,-90,600,280),new Color(.01f,.014f,.03f,.75f));
            Halo(new Rect(1180,40,440,150),new Color(.01f,.014f,.03f,.6f));
            // The hero's plate: a gilded medallion overlapping a framed bar of health and shield.
            Panel(new Rect(92,32,380,80),brass);
            Icon(new Rect(150,40,20,20),UIIcon.Heart);
            Label(new Rect(175,38,110,24),"생명력",14,muted,TextAnchor.MiddleLeft);
            Title(new Rect(260,34,192,30),Mathf.CeilToInt(hero.hp)+" / "+Mathf.CeilToInt(PlayerMaxHealth),20,paper,TextAnchor.MiddleRight);
            Meter(new Rect(146,66,310,24),hero.hp/PlayerMaxHealth,hudHitFlash>0?Color.Lerp(crimson,Color.white,hudHitFlash):crimson,hudTrail,true);
            if(PlayerShield>0)
            {
                Round(new Rect(154,93,294*Mathf.Clamp01(PlayerShield/(PlayerMaxHealth*.65f)),4),TowerWorld.Teal,2);
                ShadowLabel(new Rect(330,116,140,22),"보호막 "+Mathf.CeilToInt(PlayerShield),13,TowerWorld.Teal,TextAnchor.MiddleRight);
            }
            if(Hit(new Rect(28,18,104,104)))OpenParty("player");
            Medallion(new Rect(26,16,108,108),0,hudHitFlash>0?crimson:brass);
            // Expeditions climb from floor 1, so the HUD names the chapter of the floor underfoot.
            var here=run.IsCampaign?TowerCampaign.ForFloor(run.Floor):null;
            string place=here!=null?here.Numeral+"  ·  "+here.Title:run.Theme.Name;
            string line=place+(run.FloorType!=FloorType.Normal?"  ·  "+FloorTypeName(run.FloorType)+" 층":"");
            Color lineColor=run.FloorType==FloorType.Normal?new Color(.93f,.86f,.7f):TowerWorld.GateColor(run.FloorType);
            titleStyle.fontSize=16;float half=Mathf.Min(260,titleStyle.CalcSize(new GUIContent(line)).x*.5f+14);
            Flourish(new Rect(800-half-112,26,112,21),false,.85f);Flourish(new Rect(800+half,26,112,21),true,.85f);
            Title(new Rect(500,24,600,26),line,16,lineColor,TextAnchor.MiddleCenter);
            GildedTitle(new Rect(640,46,166,58),run.Floor.ToString("00"),46,TextAnchor.MiddleRight);
            Title(new Rect(812,68,90,28),"/ "+run.MaxFloor,18,muted);
            // Chapters show their own ten floors; long free climbs the current five-floor block; the oath trial its full track.
            int first=here!=null?here.FirstFloor:run.MaxFloor<=25?1:(run.Floor-1)/TowerRun.RewardInterval*TowerRun.RewardInterval+1;
            int shown=run.IsCampaign?TowerCampaign.FloorsPerChapter:run.MaxFloor<=25?run.MaxFloor:TowerRun.RewardInterval;
            float spacing=run.IsCampaign?22:run.MaxFloor<=25?14:26;
            for(int i=0;i<shown;i++)
            {
                int floor=first+i;bool done=floor<run.Floor||floor==run.Floor&&run.IsFloorCleared,current=floor==run.Floor;
                Color c=done?brass:current?paper:new Color(.36f,.4f,.52f,.7f);float size=TowerDifficulty.IsBossFloor(floor)?9:5;
                var at=new Vector2(800-(shown-1)*spacing*.5f+i*spacing,112);
                if(current)Halo(new Rect(at.x-14,at.y-14,28,28),Alpha(paper,.35f));
                if(TowerDifficulty.IsBossFloor(floor))Diamond(at,size+3,new Color(0,0,0,.6f));
                Diamond(at,size,c);
            }
            var boss=enemies.Find(e=>e.isBoss&&e.hp>0);
            // A boss's name takes the line where the next augment floor is announced.
            if(!run.IsOathTrial&&boss==null)
            {
                int nextRecruit=-1;if(!run.IsCampaign)foreach(int floor in TowerRun.RecruitFloors)if(floor>=run.Floor&&run.Companions.Count<TowerRun.MaxCompanions){nextRecruit=floor;break;}
                // The next fifth floor deals skill cards unless a free climb's recruit takes its place.
                int nextSkill=((run.Floor-(run.IsFloorCleared?0:1))/TowerRun.RewardInterval+1)*TowerRun.RewardInterval;
                string skillText=nextSkill<=run.MaxFloor&&nextSkill!=nextRecruit?"다음 스킬 증강  "+nextSkill+"층":"";
                string recruitText=nextRecruit>0?"다음 동료 영입  "+nextRecruit+"층":"";
                ShadowLabel(new Rect(500,124,600,22),skillText+(skillText!=""&&recruitText!=""?"   ·   ":"")+recruitText,13,muted,TextAnchor.MiddleCenter);
            }
            if(Button(new Rect(1280,28,132,42),"동료",silver))OpenParty("player");Keycap(new Rect(1372,38,32,22),"Tab");
            if(Button(new Rect(1424,28,112,42),"메뉴",muted))menu=!menu;Keycap(new Rect(1496,38,32,22),"Esc");
            string foes=run.IsFloorCleared?"층 클리어":"남은 적  "+RemainingEnemies;
            if(!run.IsFloorCleared){textStyle.fontSize=15;textStyle.fontStyle=FontStyle.Normal;Icon(new Rect(1536-textStyle.CalcSize(new GUIContent(foes)).x-28,84,22,22),UIIcon.Skull,.9f);}
            ShadowLabel(new Rect(1300,83,236,24),foes,15,run.IsFloorCleared?brass:paper,TextAnchor.MiddleRight,run.IsFloorCleared);
            if(LootActive)DrawGold(new Rect(1300,108,236,22),15,brass);
            if(!village&&TowerWorld.Right>TowerTerrainGenerator.CompactExitX+TowerTerrainGenerator.ExitMargin)DrawFloorTrack(new Rect(1300,140,236,7));
            if(boss!=null)
            {
                bool final=IsFinalBoss(boss);
                Halo(new Rect(470,112,660,120),new Color(.01f,.014f,.03f,.8f));Halo(new Rect(560,128,480,90),Alpha(crimson,.14f));
                GildedTitle(new Rect(500,138,600,34),boss.title??(boss.monster==null?"왕관의 수호자":boss.monster.Name),final?24:21,TextAnchor.MiddleCenter);
                Flourish(new Rect(392,176,112,24),false);Flourish(new Rect(1096,176,112,24),true);
                Meter(new Rect(504,177,592,24),boss.hp/boss.maxHp,crimson,0,true);
            }
            if(noticeTime>0&&!run.IsRewardPending&&!menu&&!party&&!help)
            {
                float a=Mathf.Clamp01(noticeTime*2);float y=boss!=null?226:158;
                Banner(new Rect(440,y,720,46),a);
                Label(new Rect(458,y+3,684,40),notice,18,Alpha(paper,a),TextAnchor.MiddleCenter);
            }
            for(int i=0;i<run.Companions.Count;i++)
            {
                var c=run.Companions[i];float x=36+i*150;bool sworn=run.Oath!=null&&run.Oath.CompanionId==c.Id;Color accent=sworn?brass:TierColor(c.Definition.Tier);
                if(Tile(new Rect(x,790,140,76),accent))OpenParty(c.Id);
                Portrait(new Rect(x+8,798,54,60),c.Definition.ArtIndex,accent);
                Title(new Rect(x+68,800,70,26),c.Name,16,paper,TextAnchor.MiddleLeft);
                Label(new Rect(x+68,830,68,21),sworn?"서약":"Lv. "+c.Level,13,sworn?brass:muted);
            }
            var synergies=run.ActiveSynergies;
            for(int i=0;i<synergies.Count;i++)
            {
                var s=synergies[i];Rect r=new Rect(36,738-i*48,446,40);
                Banner(r,1);
                Diamond(new Vector2(r.x+22,r.y+20),11,brass);Diamond(new Vector2(r.x+22,r.y+20),5,night);
                Label(new Rect(r.x+40,r.y+2,400,20),s.Definition.Name+"  ·  "+TowerMythCatalog.Get(s.Definition.MythId).Name+" "+s.Members+"명",14,brass,TextAnchor.MiddleLeft,true);
                FitLabel(new Rect(r.x+40,r.y+20,398,18),s.Description,12,10,paper);
            }
            DrawOathGauge();
            if(run.IsFloorCleared&&!run.IsRewardPending&&!run.IsComplete)
            {
                var gate=NearestGate();
                if(run.FloorType==FloorType.Shop&&NearMerchant&&Button(ShopButtonRect,run.ShopTraded?"거래 종료":"상인과 거래  [E]",gold,CanOpenShop))InteractMerchant();
                string label=gate.HasValue?(gate.Value==FloorType.Normal?"다음 층으로":FloorTypeName(gate.Value)+" 층으로")+"  [E]":run.IsBranchFloor?"문을 골라 이동  →":"출구로 이동  →";
                if(Button(new Rect(1190,790,346,64),label,brass,gate.HasValue,true))NextFloor(gate.Value);
            }
            else if(!run.IsFloorCleared)
            {
                bool counter=CounterRemaining>0;Color c=counter?TowerWorld.Teal:brass;
                Panel(new Rect(1290,786,246,80),c);Keycap(new Rect(1306,804,56,34),"Shift");
                Title(new Rect(1376,796,146,30),counter?"반격 준비":dashCooldown>0?dashCooldown.ToString("0.0")+"초":"회피",20,counter?c:paper,TextAnchor.MiddleLeft);
                Label(new Rect(1376,828,146,20),counter?"다음 검격 +50%":dashCooldown>0?"재사용 중":"준비 완료",13,counter?paper:muted);
                float ready=counter?CounterRemaining/DodgeCounterDuration:1-Mathf.Clamp01(dashCooldown/(1.15f*run.Player.DodgeCooldownMultiplier));
                Round(new Rect(1306,852,214,5),new Color(0,0,0,.6f),2);Round(new Rect(1306,852,214*ready,5),c,2);
            }
        }
        private void DrawIntro(){if(oathLobby)DrawOathSelection();else DrawTitle();}
        private void DrawRewards()
        {
            // A chapter opens with its own companion pick screen; free climbs and the oath trial keep recruit cards.
            if(run.IsCompanionPick){DrawCompanionPick();return;}
            bool recruit=run.IsRecruitOffer,opening=recruit&&!run.IsFloorCleared;
            // Ordinary floors deal stat cards, every fifth floor skill cards and elite floors gold-or-better spoils; shop and oath cards mix both.
            bool stats=!recruit&&run.Draft==RewardDraft.Stats,skills=!recruit&&run.Draft==RewardDraft.Skills,elite=run.IsEliteReward;
            string heading=recruit?"동료를 영입하세요":stats?"능력치를 강화하세요":skills?"새 스킬을 고르세요":"힘을 선택하세요";
            if(elite)heading="엘리트 전리품  ·  "+heading;
            MenuBackdrop(heading,opening?"CLIMB START  /  RECRUIT":"FLOOR "+run.Floor.ToString("00")+(recruit?"  /  RECRUIT":elite?"  /  ELITE SPOILS":stats?"  /  STAT AUGMENT":skills?"  /  SKILL AUGMENT":"  /  REWARD"));
            if(!opening&&run.IsFloorCleared)Label(new Rect(1130,83,400,36),CombatTime(LastFloorSeconds)+(LastFloorFlawless?"  ·  무피격":""),18,LastFloorFlawless?TowerWorld.Teal:muted,TextAnchor.MiddleRight);
            // A draft from a small pool can show fewer cards and have no spare god or skill to reroll into.
            bool spare=recruit?run.OpenRecruits>run.Offers.Count:!skills||run.OpenSkills>run.Offers.Count;
            for(int i=0;i<run.Offers.Count;i++)
            {
                var o=run.Offers[i];var d=TowerRun.GetDefinition(o.OwnerId);Color c=TierColor(o.Tier);float x=800-(run.Offers.Count*432-36)*.5f+i*432;
                Rect card=new Rect(x,202,396,534);Card(card,c);
                Label(new Rect(x+30,228,254,23),CategoryName(o),15,c,TextAnchor.MiddleLeft,true);
                Diamond(new Vector2(x+356,240),10,c);Label(new Rect(x+250,228,94,23),TierName(o.Tier),13,muted,TextAnchor.MiddleRight);
                Halo(new Rect(x+58,240,280,220),Alpha(c,.3f));
                Slot(new Rect(x+128,262,140,140),.7f);Slice(new Rect(x+128,262,140,140),Metal("portrait-frame",c),36,Alpha(MetalTint(c),.8f));
                PortraitArt(new Rect(x+137,268,122,128),d==null?0:d.ArtIndex);
                string owner=d==null?"플레이어":d.Name;
                string title=o.Title.StartsWith(owner+" · ")?o.Title.Substring(owner.Length+3):o.Title;
                if(o.Kind==RewardKind.Recruit)title=d.Name;
                else if(o.Kind==RewardKind.Skill)title=title.Replace(" 습득","").Replace(" 강화","");
                var myth=d==null?null:TowerMythCatalog.Get(d.MythId);
                Label(new Rect(x+30,414,340,23),o.Kind==RewardKind.Recruit?RoleName(d.Role)+"  ·  "+myth.Name:owner,15,muted);
                Title(new Rect(x+28,446,344,40),title,title.Length>12?22:27,paper,TextAnchor.MiddleLeft);
                Divider(new Rect(x+28,528,340,10),.7f,false,c);
                if(o.Kind==RewardKind.Recruit)
                {
                    // Recruit cards show the god's own passive, then the mythology synergy this companion would complete.
                    var passive=d.Passive;
                    Label(new Rect(x+30,490,340,24),d.Title,16,muted);
                    Label(new Rect(x+30,541,340,22),"고유 패시브  ·  "+passive.Name,15,c,TextAnchor.MiddleLeft,true);
                    FitLabel(new Rect(x+30,566,338,52),passive.Description,15,12,paper);
                    var synergy=TowerMythCatalog.GetSynergy(d.MythId);int after=run.MythCount(d.MythId)+1;
                    Slot(new Rect(x+26,620,344,46));
                    Label(new Rect(x+38,622,324,20),"「"+synergy.Name+"」  "+Mathf.Min(after,3)+" / 3"+(after>=2?"  ·  발동":""),14,after>=2?brass:muted,TextAnchor.MiddleLeft,true);
                    FitLabel(new Rect(x+38,642,324,22),after>=2?synergy.Describe(after):"2명부터  "+synergy.Describe(2),12,10,after>=2?paper:muted);
                }
                else FitLabel(new Rect(x+30,552,338,112),ConciseDescription(o.Description),18,16,new Color(.82f,.83f,.88f));
                if(Button(new Rect(x+28,676,340,44),o.Kind==RewardKind.Recruit?"영입":"선택",c,true,true)){SelectOffer(o.Id);return;}
                if(spare&&Button(new Rect(x+94,756,208,39),run.CanReroll(i)?"다시 뽑기":"사용 완료",muted,run.CanReroll(i))){run.Reroll(i);Sound(chimeSound,.25f);return;}
            }
            bool oathOffer=false;foreach(var offer in run.Offers)if(offer.Kind==RewardKind.Oath)oathOffer=true;
            string note=recruit?"모든 신화의 동료가 후보입니다. 같은 신화를 모으면 시너지가 발동합니다.":
                stats?"층마다 능력치 증강, "+TowerRun.RewardInterval+"층마다 스킬 증강을 고릅니다. 같은 능력치는 누적됩니다.":
                skills?"스킬을 새로 배우거나 한 단계 강화합니다 (최대 Lv.3).":"하나의 보상만 선택할 수 있습니다.";
            Label(new Rect(170,827,1000,29),oathOffer?"서약 변형은 함께 적용됩니다.":note,16,muted);
            if(Button(new Rect(1226,818,240,44),"능력 확인",silver))OpenParty("player");
        }
        private void DrawShop()
        {
            MenuBackdrop("떠도는 상인","FLOOR "+run.Floor.ToString("00")+"  /  SHOP");
            DrawGold(new Rect(1130,83,400,36),24,gold);
            for(int i=0;i<run.ShopOffers.Count;i++)
            {
                var o=run.ShopOffers[i];float x=170+i*432;
                Color c=o.Item==ShopItem.Augment?silver:o.Item==ShopItem.Heal?TowerWorld.Teal:gold;
                Rect card=new Rect(x,202,396,470);Card(card,c);
                Label(new Rect(x+30,228,254,23),o.Item==ShopItem.Augment?"증강":o.Item==ShopItem.Heal?"회복":"강화",15,c,TextAnchor.MiddleLeft,true);
                Icon(new Rect(x+312,226,26,26),UIIcon.Coin);Title(new Rect(x+200,226,108,26),o.Price+" G",17,gold,TextAnchor.MiddleRight);
                Halo(new Rect(x+78,240,240,210),Alpha(c,.3f));
                Icon(new Rect(x+138,280,120,120),o.Item==ShopItem.Augment?UIIcon.Star:o.Item==ShopItem.Heal?UIIcon.Heart:UIIcon.Shield);
                Title(new Rect(x+30,450,340,40),o.Name,26,paper,TextAnchor.MiddleLeft);
                Divider(new Rect(x+28,496,340,10),.7f,false,c);
                FitLabel(new Rect(x+30,514,338,80),o.Description,17,14,new Color(.82f,.83f,.88f));
                bool heal=o.Item==ShopItem.Heal,full=heal&&hero.hp>=PlayerMaxHealth-.5f;
                bool can=run.CanBuy(o.Item,progress.Gold)&&!full;
                string text=full?"생명력이 가득함":progress.Gold<o.Price?o.Price+" G  ·  골드 부족":o.Price+" G  구매";
                if(Button(new Rect(x+28,606,340,46),text,c,can,true)){BuyShopItem(o.Item);return;}
            }
            Icon(new Rect(168,701,26,26),UIIcon.Heart);
            Title(new Rect(202,700,700,30),"생명력  "+Mathf.CeilToInt(hero.hp)+" / "+Mathf.CeilToInt(PlayerMaxHealth),18,paper);
            Label(new Rect(170,736,900,28),"상인은 한 번만 거래합니다. 무엇을 사든 거래가 끝나면 노점이 닫힙니다.",15,brass);
            Label(new Rect(170,764,900,28),"골드는 층마다 놓인 항아리와 상자를 부숴 모으며 등반이 끝나도 남습니다.",14,muted);
            if(Button(new Rect(1226,818,240,44),"떠나기  [Esc]",silver))shopOpen=false;
        }
        private void DrawParty()
        {
            MenuBackdrop("동료와 능력","COMPANIONS");
            if(Button(new Rect(1320,76,216,48),"돌아가기  [Tab]",brass)){party=false;dismissId="";}
            if(run.GetHero(inspectedOwner)==null)inspectedOwner="player";
            for(int i=0;i<=run.Companions.Count;i++)
            {
                HeroState s=i==0?run.Player:run.Companions[i-1];var d=i==0?null:run.Companions[i-1].Definition;float x=96+i*358;
                var r=new Rect(x,176,334,80);Color c=d==null?brass:TierColor(d.Tier);
                if(Tile(r,c,inspectedOwner==s.Id)){inspectedOwner=s.Id;detailScroll=Vector2.zero;dismissId="";}
                Portrait(new Rect(x+12,184,60,64),d==null?0:d.ArtIndex,c);
                Title(new Rect(x+86,188,230,28),s.Name,21,paper,TextAnchor.MiddleLeft);
                Label(new Rect(x+86,222,220,22),d==null?"플레이어":"Lv. "+s.Level+"  ·  "+RoleName(d.Role),14,muted);
            }
            var selected=run.GetHero(inspectedOwner)??run.Player;var companion=run.GetCompanion(selected.Id);var definition=companion==null?null:companion.Definition;
            Color accent=definition==null?brass:TierColor(definition.Tier);
            Card(new Rect(96,284,428,510),accent);Halo(new Rect(120,293,380,220),Alpha(accent,.28f));
            PortraitArt(new Rect(234,304,150,150),definition==null?0:definition.ArtIndex);
            GildedTitle(new Rect(123,452,374,42),selected.Name,30,TextAnchor.MiddleCenter);
            Label(new Rect(123,498,374,26),definition==null?"루멘의 유일신 · 검의 서약자":definition.Title,16,accent,TextAnchor.MiddleCenter);
            Divider(new Rect(124,536,372,10),.8f,true,accent);
            StatCell(new Rect(128,560,170,68),"공격력",selected.Damage.ToString("0.#"));
            StatCell(new Rect(318,560,172,68),"공격 간격",selected.AttackInterval.ToString("0.00")+"초");
            StatCell(new Rect(128,639,170,68),companion==null?"최대 생명력":"스킬 위력",companion==null?selected.MaxHealth.ToString("0"):selected.SkillPowerMultiplier.ToString("0.##")+"배");
            StatCell(new Rect(318,639,172,68),companion==null?"방어력":"치유 배율",companion==null?selected.Armor.ToString("0"):selected.HealingMultiplier.ToString("0.##")+"배");
            StatCell(new Rect(128,718,170,68),"이동 속도",selected.MoveSpeed.ToString("0.#"));
            StatCell(new Rect(318,718,172,68),"사거리",selected.Range.ToString("0.#"));
            Panel(new Rect(552,284,952,510),accent);DrawAbilities(new Rect(576,305,908,466),selected,accent);
            bool bound=definition!=null&&run.Oath!=null&&definition.Id==run.Oath.CompanionId;
            if(bound)Title(new Rect(109,819,415,30),"이번 등반의 서약 동료",17,brass);
            else if(definition!=null)
            {
                bool confirm=dismissId==definition.Id;
                if(Button(new Rect(96,814,428,46),confirm?"능력을 잃고 동료와 작별":"동료와 작별",confirm?crimson:muted))
                {if(confirm){Dismiss(definition.Id);inspectedOwner="player";detailScroll=Vector2.zero;return;}dismissId=definition.Id;}
            }
        }
        private void DrawAbilities(Rect viewport,HeroState owner,Color accent)
        {
            float width=viewport.width-25;float height=OathDetailHeight(owner)+184+owner.Skills.Count*143+owner.Traits.Count*115;
            var companion=run.GetCompanion(owner.Id);if(companion!=null)height+=224;
            detailScroll=BeginScroll(viewport,detailScroll,new Rect(0,0,width,Mathf.Max(viewport.height-2,height)));
            float y=DrawOathDetails(owner,3);
            if(companion!=null)
            {
                // The passive with its live state, the legend it comes from, then the companion's base numbers.
                var passive=companion.Definition.Passive;string status=run.Passives.Status(companion);
                Title(new Rect(12,y,width-270,28),"고유 패시브  ·  "+passive.Name,20,accent,TextAnchor.MiddleLeft);
                if(status!="")Label(new Rect(width-258,y+2,244,24),status,15,TowerWorld.Teal,TextAnchor.MiddleRight);
                FitLabel(new Rect(12,y+34,width-24,48),passive.Description,18,15,paper);
                Label(new Rect(12,y+86,width-24,24),passive.Lore,15,muted);
                Label(new Rect(12,y+114,width-24,24),companion.Definition.InnateDescription,14,Alpha(muted,.8f));y+=150;
                var myth=TowerMythCatalog.Get(companion.Definition.MythId);var synergy=TowerMythCatalog.GetSynergy(myth.Id);int members=run.MythCount(myth.Id);
                Slot(new Rect(3,y,width-6,62),brass,members>=2);
                Label(new Rect(18,y+8,width-40,24),myth.Name+"  ·  「"+synergy.Name+"」  "+members+" / 3"+(members>=2?"  발동":""),17,members>=2?brass:paper,TextAnchor.MiddleLeft,true);
                FitLabel(new Rect(18,y+34,width-40,24),members>=2?synergy.Describe(members):"같은 신화 2명부터  "+synergy.Describe(2),15,12,muted);y+=74;
            }
            Title(new Rect(12,y,width-24,30),"스킬  "+owner.Skills.Count,21,paper,TextAnchor.MiddleLeft);Divider(new Rect(12,y+33,width-24,8),.5f);y+=45;
            if(owner.Skills.Count==0){Label(new Rect(12,y,width-24,38),"습득한 스킬 없음",17,muted);y+=44;}
            foreach(var skill in owner.Skills)
            {
                Slot(new Rect(3,y,width-6,131));
                Diamond(new Vector2(24,y+26),10,accent);
                Title(new Rect(40,y+12,width-265,29),skill.Definition.Name+"  "+skill.Level,20,paper);
                float remaining=SkillRemaining(owner.Id,skill.Definition.Id);
                Label(new Rect(width-224,y+13,202,25),remaining>0?remaining.ToString("0.0")+"초":"준비",15,remaining>0?muted:TowerWorld.Teal,TextAnchor.MiddleRight);
                FitLabel(new Rect(18,y+49,width-40,78),skill.Description,17,16,new Color(.8f,.82f,.88f));y+=143;
            }
            y+=14;Title(new Rect(12,y,width-24,30),"능력치  "+owner.Traits.Count,21,paper,TextAnchor.MiddleLeft);Divider(new Rect(12,y+33,width-24,8),.5f);y+=44;
            if(owner.Traits.Count==0)Label(new Rect(12,y,width-24,35),"강화한 능력치 없음",17,muted);
            foreach(var trait in owner.Traits)
            {
                Slot(new Rect(3,y,width-6,103));
                Diamond(new Vector2(24,y+24),8,brass);
                Title(new Rect(40,y+10,width-60,28),trait.Definition.Name+"  "+trait.Level,20,paper);
                Label(new Rect(18,y+44,width-40,58),ConciseDescription(trait.Description),17,new Color(.8f,.82f,.88f));y+=115;
            }
            EndScroll();
        }
        private void DrawMenu()
        {
            bool expedition=run.IsCampaign&&!village;float top=expedition?136:166;
            Fill(Screenful,new Color(.01f,.014f,.03f,.8f));Vignette(new Color(0,0,0,.85f));Grain(Screenful,.04f,true);
            Card(new Rect(540,top,520,expedition?632:572),brass);
            Label(new Rect(586,top+44,428,24),"PAUSED",13,brass,TextAnchor.MiddleCenter,true);
            GildedTitle(new Rect(560,top+76,480,60),"숨을 고르는 중",36,TextAnchor.MiddleCenter);
            Divider(new Rect(620,top+146,360,10),.8f,true);
            float y=top+184;
            if(Button(new Rect(600,y,400,56),"계속하기",brass,true,true))menu=false;y+=73;
            if(Button(new Rect(600,y,400,48),"조작과 규칙",silver))help=true;y+=64;
            if(Button(new Rect(600,y,400,48),"화면 흔들림  ·  "+(ScreenShakeEnabled?"켜짐":"꺼짐"),muted))ScreenShakeEnabled=!ScreenShakeEnabled;y+=64;
            if(expedition){if(Button(new Rect(600,y,400,48),"원정 포기  ·  마을로",crimson)){EnterVillage();inspectedOwner="player";}y+=64;}
            if(Button(new Rect(600,y,400,48),"처음 화면",muted)){if(village||run.IsCampaign)EnterVillage();else NewRun();intro=true;inspectedOwner="player";}y+=64;
            if(Button(new Rect(600,y,400,48),"데모 선택",muted))BackToHub();y+=69;
            Label(new Rect(600,y,400,27),village?TowerCampaign.Village:run.Floor+"층  ·  "+CombatTime(elapsed),16,muted,TextAnchor.MiddleCenter);
        }
        private void DrawEnding(bool won)
        {
            if(run.IsCampaign&&!won){DrawExpeditionLost();return;}
            MenuBackdrop(won?(run.IsOathTrial?"서약을 완수했습니다":"탑의 정상에 도달했습니다"):"등반이 끝났습니다",won?"OATH FULFILLED":"JOURNEY ENDED");
            Color c=won?brass:silver;
            Halo(new Rect(450,135,700,520),Alpha(c,.20f));
            if(won)GildedTitle(new Rect(600,202,400,110),run.Floor.ToString("00"),88,TextAnchor.MiddleCenter);
            else Title(new Rect(600,202,400,110),run.Floor.ToString("00"),88,c,TextAnchor.MiddleCenter);
            Title(new Rect(600,318,400,32),"/ "+run.MaxFloor+"층",22,muted,TextAnchor.MiddleCenter);
            float x=240;
            StatCell(new Rect(x,420,240,75),"처치",kills.ToString());StatCell(new Rect(x+290,420,240,75),"등반 시간",CombatTime(elapsed));
            StatCell(new Rect(x+580,420,240,75),run.IsOathTrial?"서약 연계":"모은 골드",run.IsOathTrial?run.Oath.Activations+"회":"+"+GoldCollected+" G");
            StatCell(new Rect(x+870,420,240,75),"무피격 클리어",FlawlessFloors+"층");
            Divider(new Rect(240,529,1120,10),.8f,true,c);
            Label(new Rect(240,565,1120,64),"순간 회피  "+PerfectDodges+"     ·     반격 적중  "+CounterHits+"     ·     돌진 베기  "+DashSlashes+"     ·     마무리 일격  "+Finishers+"     ·     받은 피해  "+Mathf.CeilToInt(DamageTaken),18,muted,TextAnchor.MiddleCenter);
            if(Button(new Rect(555,689,490,60),run.IsOathTrial?"다른 서약으로 도전":"다시 등반하기",brass,true,true))
            {bool trial=run.IsOathTrial;EnterVillage();intro=true;oathLobby=trial;inspectedOwner="player";}
            if(Button(new Rect(660,771,280,44),"데모 선택",muted))BackToHub();
        }
        private void DrawHelp()
        {
            // The guide opens over other menus, so its veil hides them completely.
            MenuBackdrop("조작과 규칙","FIELD GUIDE",.98f);
            if(Button(new Rect(1320,76,216,48),"닫기",brass))help=false;
            string[] keys={"A / D","Space","J","Shift","Shift → J","Ctrl","S / ↓","E","Esc","Tab"};
            string[] actions={"이동","점프 · 공중에서 한 번 더 · 짧게 누르면 낮게","검 공격 · 3타째 마무리 일격 · 항아리·상자 부수기","무적 회피 · 공중에서도 높이 유지","돌진 베기 · 회피 중이나 직후 공격","정밀 이동","발판 아래로","가까운 문으로 이동 · 상인과 거래","메뉴 · 상점 나가기","동료와 능력"};
            for(int i=0;i<keys.Length;i++)
            {
                float y=192+i*58;if(i%2==0)Slot(new Rect(84,y-8,664,52),.55f);
                Keycap(new Rect(100,y,120,36),keys[i]);Label(new Rect(238,y,500,36),actions[i],18,paper,TextAnchor.MiddleLeft);
            }
            bool trial=run.IsOathTrial||oathLobby;
            Card(new Rect(772,198,712,574),brass);
            GildedTitle(new Rect(808,232,640,37),trial?"서약 연계":"회피와 반격",27);
            Divider(new Rect(808,276,640,10),.6f);
            string rules=trial?"검격 적중 +1 · 순간 회피 +2\n기본 4충전 후 다음 검격 적중에 연계가 발동합니다. 헛스윙은 충전을 소모하지 않습니다.\n\n회피 시작 0.12초 안에 탄환을 통과하면 순간 회피입니다. 다음 검격의 반격 효과는 3초간 유지됩니다.\n\n여러 서약 변형은 함께 적용되며 충전은 다음 층에도 유지됩니다.\n\n5·10층 보스 · 10층 클리어로 종료\n부서지는 항아리와 상자, 골드는 나오지 않습니다.":
                "회피 시작 0.12초 안에 탄환을 통과하면 반격을 준비합니다. 3초 안에 맞힌 다음 검격은 피해가 50% 증가합니다.\n\n탑은 열 층마다 한 신화를 가둔 장입니다. 원정을 떠나면 되찾은 신화와 그 장 신화의 신들 가운데 함께 오를 동료를 직접 고릅니다. 30층을 넘으면 둘, 60층을 넘으면 셋을 데려갑니다.\n\n층마다 능력치 증강, 5층마다 보스와 스킬 증강이 나오고 그 장의 마지막 층에는 각성한 보스가 있습니다. 장을 끝내면 마을로 돌아와 다음 열 층이 열리고, 증강은 원정마다 처음부터 다시 쌓습니다. 층을 넘길 때마다 얻는 스킬 포인트는 실패해도 남으며, 마을의 스킬트리(K)에서 영구 능력을 배웁니다.";
            Label(new Rect(808,298,640,360),rules,18,new Color(.8f,.82f,.88f));
            Label(new Rect(808,675,640,65),trial?"적을 모두 처치하면 생명력 18 회복\n보상은 하나만 선택 · 각 칸 다시 뽑기 1회":"5층 갈림길에서 휴식·상점·엘리트 문 · 항아리·상자에서 하트와 골드\n보상은 하나만 선택 · 각 칸 다시 뽑기 1회",18,paper);
        }
    }
}
