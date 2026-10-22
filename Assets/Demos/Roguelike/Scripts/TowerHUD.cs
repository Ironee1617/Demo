using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerGame
    {
        private GUIStyle textStyle,buttonStyle;
        private TowerSmokeDriver captureDriver;
        private Font uiFont;
        private float uiScale=1;private Vector2 uiOffset;
        private string inspectedOwner="player";
        private Vector2 detailScroll;
        private readonly Color ink=new Color(.032f,.018f,.022f,.97f),paper=new Color(.88f,.83f,.75f),muted=new Color(.6f,.54f,.52f);
        private void OnGUI()
        {
            if(hero==null)return;
            if(captureDriver==null)captureDriver=GetComponent<TowerSmokeDriver>();
            bool captureHud=captureDriver!=null&&captureDriver.BeginHudCapture();
            if(textStyle==null)
            {
                uiFont=Resources.Load<Font>("Tower/Font");
                textStyle=new GUIStyle{font=uiFont,wordWrap=true,richText=false};
                buttonStyle=new GUIStyle{font=uiFont,alignment=TextAnchor.MiddleCenter,fontSize=18};
            }
            uiScale=Mathf.Min(Screen.width/1600f,Screen.height/900f);uiOffset=new Vector2((Screen.width-1600*uiScale)*.5f,(Screen.height-900*uiScale)*.5f);
            var oldMatrix=GUI.matrix;GUI.matrix=Matrix4x4.TRS(uiOffset,Quaternion.identity,Vector3.one*uiScale);
            if(!intro)DrawWorldLabels();
            if(intro){GUI.enabled=!help;DrawIntro();GUI.enabled=true;}
            else
            {
                GUI.enabled=!menu&&!party&&!help&&!shopOpen&&!run.IsRewardPending&&hero.hp>0&&!run.IsComplete;
                DrawHUD();GUI.enabled=!party&&!help;
                if(run.IsRewardPending)DrawRewards();else if(shopOpen)DrawShop();
                GUI.enabled=!help;
                if(party)DrawParty();else if(menu)DrawMenu();else if(hero.hp<=0)DrawEnding(false);else if(run.IsComplete)DrawEnding(true);
            }
            GUI.enabled=true;if(help)DrawHelp();DrawToasts();GUI.matrix=oldMatrix;GUI.color=Color.white;
            if(captureHud)captureDriver.EndHudCapture();
        }
        private void Fill(Rect r,Color c){GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=Color.white;}
        private void Border(Rect r,Color c,float n=1)
        {Fill(new Rect(r.x,r.y,r.width,n),c);Fill(new Rect(r.x,r.yMax-n,r.width,n),c);Fill(new Rect(r.x,r.y,n,r.height),c);Fill(new Rect(r.xMax-n,r.y,n,r.height),c);}
        private void Label(Rect r,string text,int size,Color color,TextAnchor align=TextAnchor.UpperLeft,bool bold=false)
        {textStyle.fontSize=size;textStyle.alignment=align;textStyle.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;textStyle.normal.textColor=color;GUI.Label(r,text,textStyle);}
        private Color TierColor(RewardTier tier){return tier==RewardTier.Silver?silver:tier==RewardTier.Gold?gold:new Color(.5f,.82f,.86f);}
        private string TierName(RewardTier tier){return tier==RewardTier.Silver?"실버":tier==RewardTier.Gold?"골드":"다이아";}
        private string RoleName(CompanionRole role){return role==CompanionRole.Vanguard?"전사":role==CompanionRole.Archer?"궁수":role==CompanionRole.Mage?"마법사":"치유사";}
        private string CategoryName(RewardOffer offer){return offer.Kind==RewardKind.Oath?"서약 변형":offer.Kind==RewardKind.Recruit?"동료":offer.Kind==RewardKind.Trait?"특성":offer.IsUpgrade?"스킬 강화":"스킬";}
        internal void OpenParty(string owner){party=true;menu=false;dismissId="";inspectedOwner=owner;detailScroll=Vector2.zero;}
        private Vector2 WorldGui(Vector2 p){var s=view.WorldToScreenPoint(p);return new Vector2((s.x-uiOffset.x)/uiScale,(Screen.height-s.y-uiOffset.y)/uiScale);}
        private void DrawWorldLabels()
        {
            foreach(var e in enemies)
            {
                if(e.isBoss||e.hp>=e.maxHp&&!e.isElite)continue;
                var p=WorldGui(e.pos+Vector2.up*(e.kind==3?1.8f:e.kind==2?1.6f:1.2f)*(e.isElite?TowerDifficulty.EliteScale:1));
                Round(new Rect(p.x-28,p.y,56,5),new Color(.02f,.025f,.04f,.9f),2);
                Round(new Rect(p.x-27,p.y+1,54*Mathf.Clamp01(e.hp/e.maxHp),3),e.isElite?brass:e.airborne?silver:crimson,1);
                if(e.isElite)Diamond(new Vector2(p.x-34,p.y+2.5f),6,brass);
            }
            if(!intro&&run.FloorType!=FloorType.Normal&&run.FloorType!=FloorType.Elite)
            {
                bool shop=run.FloorType==FloorType.Shop,near=shop&&NearMerchant&&run.IsFloorCleared;
                var p=WorldGui(TowerWorld.FeaturePosition+Vector2.up*(shop?4.3f:2.2f));
                string text=!shop?"모닥불":run.ShopTraded?"거래 종료":near?"상인  [E] 거래":"상인";
                Color c=!shop?TowerWorld.Teal:run.ShopTraded?muted:gold;float w=near?150:120;
                Round(new Rect(p.x-w*.5f,p.y-15,w,30),new Color(.03f,.015f,.02f,near?.92f:.78f),5);
                if(near&&!run.ShopTraded)Border(new Rect(p.x-w*.5f,p.y-15,w,30),Alpha(c,.8f));
                Label(new Rect(p.x-w*.5f,p.y-15,w,30),text,15,c,TextAnchor.MiddleCenter,true);
            }
            foreach(var n in numbers)
            {
                var p=WorldGui(n.pos);var c=n.color;c.a=Mathf.Min(1,n.life*3);float w=Mathf.Max(90,n.text.Length*23+20);
                Label(new Rect(p.x-w*.5f+1,p.y-14,w,35),n.text,22,new Color(0,0,0,c.a*.65f),TextAnchor.MiddleCenter,true);
                Label(new Rect(p.x-w*.5f,p.y-16,w,35),n.text,22,c,TextAnchor.MiddleCenter,true);
            }
            if(!intro&&run.IsFloorCleared&&!run.IsRewardPending&&!run.IsComplete)
            {
                var near=NearestGate();
                if(!run.IsBranchFloor){var p=WorldGui(new Vector2(TowerWorld.ExitX,TowerWorld.Ground+3.4f));Keycap(new Rect(p.x-18,p.y-16,36,30),"E");}
                else foreach(var type in TowerWorld.BranchGates)
                {
                    // Each doorway names its destination; the one in reach shows the key.
                    var p=WorldGui(new Vector2(TowerWorld.GateX(type),TowerWorld.Ground+4.35f));Color c=TowerWorld.GateColor(type);bool active=near.HasValue&&near.Value==type;
                    Round(new Rect(p.x-78,p.y-24,156,48),new Color(.03f,.015f,.02f,active?.92f:.72f),5);
                    if(active)Border(new Rect(p.x-78,p.y-24,156,48),Alpha(c,.8f));
                    Label(new Rect(p.x-74,p.y-22,148,24),FloorTypeName(type)+(active?"  [E]":""),16,c,TextAnchor.MiddleCenter,true);
                    Label(new Rect(p.x-74,p.y,148,20),FloorTypeSummary(type),12,muted,TextAnchor.MiddleCenter);
                }
            }
        }
        private void DrawHUD()
        {
            // A dark vignette frames the arena; under 35% health it pulses blood red.
            Vignette(new Color(0,0,0,.6f));
            float health=hero.hp/PlayerMaxHealth;
            if(health<.35f&&hero.hp>0)Vignette(new Color(.55f,0,.03f,(.25f+.2f*Mathf.Sin(Time.unscaledTime*5))*(1-health/.35f*.6f)));
            Light(new Rect(0,0,1600,175),new Color(.02f,.004f,.008f,.72f));
            Panel(new Rect(36,27,420,92),brass);
            if(Button(new Rect(45,35,74,74),"",brass))OpenParty("player");
            Portrait(new Rect(45,35,74,74),0,hudHitFlash>0?crimson:brass);
            Label(new Rect(135,36,110,24),"생명력",15,muted);
            Label(new Rect(246,35,190,27),Mathf.CeilToInt(hero.hp)+" / "+Mathf.CeilToInt(PlayerMaxHealth),20,paper,TextAnchor.MiddleRight,true);
            Meter(new Rect(134,68,302,25),hero.hp/PlayerMaxHealth,hudHitFlash>0?Color.Lerp(crimson,Color.white,hudHitFlash):crimson,hudTrail,true);
            if(PlayerShield>0)
            {
                Round(new Rect(137,98,296*Mathf.Clamp01(PlayerShield/(PlayerMaxHealth*.65f)),4),TowerWorld.Teal,2);
                Label(new Rect(324,122,120,22),"보호막 "+Mathf.CeilToInt(PlayerShield),13,TowerWorld.Teal,TextAnchor.MiddleRight);
            }
            Label(new Rect(615,26,370,23),run.Theme.Name+(run.FloorType!=FloorType.Normal?"  ·  "+FloorTypeName(run.FloorType)+" 층":""),16,run.FloorType==FloorType.Normal?paper:TowerWorld.GateColor(run.FloorType),TextAnchor.MiddleCenter);
            Label(new Rect(675,50,142,52),run.Floor.ToString("00"),43,brass,TextAnchor.MiddleRight,true);
            Label(new Rect(827,72,90,25),"/ "+run.MaxFloor,18,muted);
            // Long climbs show the current five-floor block; the oath trial keeps its full track.
            int first=run.MaxFloor<=25?1:(run.Floor-1)/TowerRun.RewardInterval*TowerRun.RewardInterval+1,shown=run.MaxFloor<=25?run.MaxFloor:TowerRun.RewardInterval;
            float spacing=run.MaxFloor<=25?14:26;
            for(int i=0;i<shown;i++)
            {
                int floor=first+i;
                Color c=floor<run.Floor||floor==run.Floor&&run.IsFloorCleared?brass:floor==run.Floor?paper:new Color(.32f,.24f,.25f,.7f);
                Diamond(new Vector2(800-(shown-1)*spacing*.5f+i*spacing,111),TowerDifficulty.IsBossFloor(floor)?7:4,c);
            }
            if(!run.IsOathTrial)
            {
                int nextRecruit=-1;foreach(int floor in TowerRun.RecruitFloors)if(floor>=run.Floor&&run.Companions.Count<TowerRun.MaxCompanions){nextRecruit=floor;break;}
                Label(new Rect(600,124,400,22),nextRecruit>0?"다음 동료 영입  "+nextRecruit+"층":"",13,muted,TextAnchor.MiddleCenter);
                Label(new Rect(1300,108,236,22),"보유 "+progress.Points+" P",14,brass,TextAnchor.MiddleRight,true);
            }
            if(Button(new Rect(1280,28,132,42),"동료",silver))OpenParty("player");Keycap(new Rect(1375,39,28,20),"Tab");
            if(Button(new Rect(1424,28,112,42),"메뉴",muted))menu=!menu;Keycap(new Rect(1499,39,28,20),"Esc");
            Label(new Rect(1300,84,236,24),run.IsFloorCleared?"층 클리어":"남은 적  "+enemies.Count,15,run.IsFloorCleared?brass:muted,TextAnchor.MiddleRight);
            var boss=enemies.Find(e=>e.isBoss&&e.hp>0);
            if(boss!=null)
            {
                Label(new Rect(500,141,600,30),boss.monster==null?"왕관의 수호자":boss.monster.Name,20,paper,TextAnchor.MiddleCenter,true);
                Diamond(new Vector2(486,189),9,brass);Diamond(new Vector2(1114,189),9,brass);
                Meter(new Rect(504,179,592,19),boss.hp/boss.maxHp,crimson,0,true);
            }
            if(noticeTime>0&&!run.IsRewardPending&&!menu&&!party&&!help)
            {
                float a=Mathf.Clamp01(noticeTime*2);float y=boss!=null?222:156;
                Round(new Rect(520,y,560,45),new Color(.03f,.012f,.016f,a*.88f),5);
                Fill(new Rect(540,y+44,520,1),Alpha(brass,.5f*a));
                Label(new Rect(538,y+3,524,39),notice,18,Alpha(paper,a),TextAnchor.MiddleCenter);
            }
            for(int i=0;i<run.Companions.Count;i++)
            {
                var c=run.Companions[i];float x=36+i*150;bool sworn=run.Oath!=null&&run.Oath.CompanionId==c.Id;
                if(Button(new Rect(x,790,140,76),"",sworn?brass:silver))OpenParty(c.Id);
                Portrait(new Rect(x+8,800,50,56),c.Definition.ArtIndex,sworn?brass:silver);
                Label(new Rect(x+66,803,70,25),c.Name,16,paper,TextAnchor.MiddleLeft,true);
                Label(new Rect(x+66,833,66,21),sworn?"서약":"Lv. "+c.Level,13,sworn?brass:muted);
            }
            var synergies=run.ActiveSynergies;
            for(int i=0;i<synergies.Count;i++)
            {
                var s=synergies[i];Rect r=new Rect(36,740-i*46,446,40);
                Round(r,new Color(.03f,.012f,.016f,.85f),5);Border(r,Alpha(brass,.45f));
                Diamond(new Vector2(r.x+18,r.y+20),7,brass);
                Label(new Rect(r.x+34,r.y+2,400,20),s.Definition.Name+"  ·  "+TowerMythCatalog.Get(s.Definition.MythId).Name+" "+s.Members+"명",14,brass,TextAnchor.MiddleLeft,true);
                FitLabel(new Rect(r.x+34,r.y+20,404,18),s.Description,12,10,muted);
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
                Panel(new Rect(1290,786,246,80),c);Keycap(new Rect(1306,804,53,35),"Shift");
                Label(new Rect(1374,797,146,28),counter?"반격 준비":dashCooldown>0?dashCooldown.ToString("0.0")+"초":"회피",20,c,TextAnchor.MiddleLeft,true);
                Label(new Rect(1374,831,146,20),counter?"다음 검격 +50%":dashCooldown>0?"재사용 중":"준비 완료",13,muted);
                float ready=counter?CounterRemaining/DodgeCounterDuration:1-Mathf.Clamp01(dashCooldown/(1.15f*run.Player.DodgeCooldownMultiplier));
                Round(new Rect(1306,858,214*ready,3),c,1);
            }
        }
        private void DrawIntro(){if(oathLobby)DrawOathSelection();else DrawTitle();}
        private void DrawRewards()
        {
            bool recruit=run.IsRecruitOffer,opening=recruit&&run.Floor==1&&!run.IsFloorCleared;
            MenuBackdrop(recruit?"동료를 영입하세요":"힘을 선택하세요",opening?"CLIMB START  /  RECRUIT":"FLOOR "+run.Floor.ToString("00")+(recruit?"  /  RECRUIT":"  /  REWARD"));
            if(!opening&&run.IsFloorCleared)Label(new Rect(1130,83,400,36),CombatTime(LastFloorSeconds)+(LastFloorFlawless?"  ·  무피격":""),18,LastFloorFlawless?TowerWorld.Teal:muted,TextAnchor.MiddleRight);
            for(int i=0;i<run.Offers.Count;i++)
            {
                var o=run.Offers[i];var d=TowerRun.GetDefinition(o.OwnerId);Color c=TierColor(o.Tier);float x=170+i*432;
                Rect card=new Rect(x,202,396,534);Panel(card,c);
                Light(new Rect(x+2,204,392,190),Alpha(c,.10f));
                Label(new Rect(x+28,226,254,23),CategoryName(o),15,c,TextAnchor.MiddleLeft,true);
                Label(new Rect(x+280,226,87,23),TierName(o.Tier),13,muted,TextAnchor.MiddleRight);
                Halo(new Rect(x+65,247,266,200),Alpha(c,.28f));PortraitArt(new Rect(x+137,265,122,132),d==null?0:d.ArtIndex);
                string owner=d==null?"플레이어":d.Name;
                string title=o.Title.StartsWith(owner+" · ")?o.Title.Substring(owner.Length+3):o.Title;
                if(o.Kind==RewardKind.Recruit)title=d.Name;
                else if(o.Kind==RewardKind.Skill)title=title.Replace(" 습득","").Replace(" 강화","");
                var myth=d==null?null:TowerMythCatalog.Get(d.MythId);
                Label(new Rect(x+28,414,340,23),o.Kind==RewardKind.Recruit?RoleName(d.Role)+"  ·  "+myth.Name:owner,15,muted);
                FitLabel(new Rect(x+26,449,344,70),title,26,22,paper,TextAnchor.UpperLeft,true);
                Fill(new Rect(x+28,533,340,1),Alpha(c,.2f));
                if(o.Kind==RewardKind.Recruit)
                {
                    // Recruit cards preview the mythology synergy this companion would complete.
                    FitLabel(new Rect(x+28,546,340,72),ConciseDescription(o.Description),17,14,muted);
                    var synergy=TowerMythCatalog.GetSynergy(d.MythId);int after=run.MythCount(d.MythId)+1;
                    Round(new Rect(x+24,620,348,46),new Color(1,1,1,after>=2?.06f:.025f),5);
                    Label(new Rect(x+34,622,330,20),"「"+synergy.Name+"」  "+Mathf.Min(after,3)+" / 3"+(after>=2?"  ·  발동":""),14,after>=2?brass:muted,TextAnchor.MiddleLeft,true);
                    FitLabel(new Rect(x+34,642,330,22),after>=2?synergy.Describe(after):"2명부터  "+synergy.Describe(2),12,10,after>=2?paper:muted);
                }
                else FitLabel(new Rect(x+28,552,340,112),ConciseDescription(o.Description),18,16,muted);
                if(Button(new Rect(x+26,674,344,42),o.Kind==RewardKind.Recruit?"영입":"선택",c,true,true)){SelectOffer(o.Id);return;}
                if(Button(new Rect(x+94,754,208,39),run.CanReroll(i)?"다시 뽑기":"사용 완료",muted,run.CanReroll(i))){run.Reroll(i);Sound(chimeSound,.25f);return;}
            }
            bool oathOffer=false;foreach(var offer in run.Offers)if(offer.Kind==RewardKind.Oath)oathOffer=true;
            Label(new Rect(170,827,850,29),oathOffer?"서약 변형은 함께 적용됩니다.":recruit?"모든 신화의 동료가 후보입니다. 같은 신화를 모으면 시너지가 발동합니다.":"하나의 보상만 선택할 수 있습니다.",16,muted);
            if(Button(new Rect(1226,818,240,44),"능력 확인",silver))OpenParty("player");
        }
        private void DrawShop()
        {
            MenuBackdrop("떠도는 상인","FLOOR "+run.Floor.ToString("00")+"  /  SHOP");
            Label(new Rect(1130,83,400,36),"보유 "+progress.Points+" P",24,gold,TextAnchor.MiddleRight,true);
            for(int i=0;i<run.ShopOffers.Count;i++)
            {
                var o=run.ShopOffers[i];float x=170+i*432;
                Color c=o.Item==ShopItem.Augment?silver:o.Item==ShopItem.Heal?TowerWorld.Teal:gold;
                Rect card=new Rect(x,202,396,470);Panel(card,c);Light(new Rect(x+2,204,392,190),Alpha(c,.08f));
                Label(new Rect(x+28,226,254,23),o.Item==ShopItem.Augment?"증강":o.Item==ShopItem.Heal?"회복":"강화",15,c,TextAnchor.MiddleLeft,true);
                Label(new Rect(x+230,226,137,23),o.Price+" P",15,gold,TextAnchor.MiddleRight,true);
                Halo(new Rect(x+88,250,220,190),Alpha(c,.26f));Diamond(new Vector2(x+198,345),56,Alpha(c,.8f));Diamond(new Vector2(x+198,345),30,new Color(.05f,.02f,.03f,.75f));Diamond(new Vector2(x+198,345),12,Alpha(c,.9f));
                Label(new Rect(x+28,452,340,40),o.Name,27,paper,TextAnchor.MiddleLeft,true);
                Fill(new Rect(x+28,500,340,1),Alpha(c,.2f));
                FitLabel(new Rect(x+28,514,340,80),o.Description,17,14,muted);
                bool heal=o.Item==ShopItem.Heal,full=heal&&hero.hp>=PlayerMaxHealth-.5f;
                bool can=run.CanBuy(o.Item,progress.Points)&&!full;
                string text=full?"생명력이 가득함":progress.Points<o.Price?o.Price+" P  ·  포인트 부족":o.Price+" P  구매";
                if(Button(new Rect(x+26,608,344,46),text,c,can,true)){BuyShopItem(o.Item);return;}
            }
            Label(new Rect(170,700,700,30),"생명력  "+Mathf.CeilToInt(hero.hp)+" / "+Mathf.CeilToInt(PlayerMaxHealth),18,paper);
            Label(new Rect(170,734,900,28),"상인은 한 번만 거래합니다. 무엇을 사든 거래가 끝나면 노점이 닫힙니다.",15,brass);
            Label(new Rect(170,762,900,28),"포인트는 층을 클리어할 때마다 1 P씩 쌓이며 등반이 끝나도 남습니다.",14,muted);
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
                var r=new Rect(x,180,334,76);Color c=d==null?brass:TierColor(d.Tier);
                if(Button(r,"",c)){inspectedOwner=s.Id;detailScroll=Vector2.zero;dismissId="";}
                SelectionFrame(r,c,inspectedOwner==s.Id);Portrait(new Rect(x+12,190,55,55),d==null?0:d.ArtIndex,c);
                Label(new Rect(x+84,191,220,27),s.Name,21,paper,TextAnchor.MiddleLeft,true);
                Label(new Rect(x+84,224,220,22),d==null?"플레이어":"Lv. "+s.Level+"  ·  "+RoleName(d.Role),14,muted);
            }
            var selected=run.GetHero(inspectedOwner)??run.Player;var companion=run.GetCompanion(selected.Id);var definition=companion==null?null:companion.Definition;
            Color accent=definition==null?brass:TierColor(definition.Tier);
            Panel(new Rect(96,284,428,510),accent);Halo(new Rect(120,293,380,220),Alpha(accent,.25f));
            PortraitArt(new Rect(234,302,150,150),definition==null?0:definition.ArtIndex);
            Label(new Rect(123,453,374,42),selected.Name,30,paper,TextAnchor.MiddleCenter,true);
            Label(new Rect(123,498,374,26),definition==null?"검의 서약자":definition.Title,16,accent,TextAnchor.MiddleCenter);
            Fill(new Rect(124,541,372,1),Alpha(accent,.20f));
            StatCell(new Rect(126,562,170,68),"공격력",selected.Damage.ToString("0.#"));
            StatCell(new Rect(320,562,172,68),"공격 간격",selected.AttackInterval.ToString("0.00")+"초");
            StatCell(new Rect(126,641,170,68),companion==null?"최대 생명력":"스킬 위력",companion==null?selected.MaxHealth.ToString("0"):selected.SkillPowerMultiplier.ToString("0.##")+"배");
            StatCell(new Rect(320,641,172,68),companion==null?"방어력":"치유 배율",companion==null?selected.Armor.ToString("0"):selected.HealingMultiplier.ToString("0.##")+"배");
            StatCell(new Rect(126,720,170,68),"이동 속도",selected.MoveSpeed.ToString("0.#"));
            StatCell(new Rect(320,720,172,68),"사거리",selected.Range.ToString("0.#"));
            Panel(new Rect(552,284,952,510),accent);DrawAbilities(new Rect(576,305,908,466),selected,accent);
            bool bound=definition!=null&&run.Oath!=null&&definition.Id==run.Oath.CompanionId;
            if(bound)Label(new Rect(109,819,415,30),"이번 등반의 서약 동료",16,brass);
            else if(definition!=null)
            {
                bool confirm=dismissId==definition.Id;
                if(Button(new Rect(96,814,428,46),confirm?"능력을 잃고 동료와 작별":"동료와 작별",confirm?crimson:muted))
                {if(confirm){Dismiss(definition.Id);inspectedOwner="player";detailScroll=Vector2.zero;return;}dismissId=definition.Id;}
            }
        }
        private void DrawAbilities(Rect viewport,HeroState owner,Color accent)
        {
            PrepareScrollStyle();var previousSkin=GUI.skin;GUI.skin=uiSkin;
            float width=viewport.width-25;float height=OathDetailHeight(owner)+184+owner.Skills.Count*143+owner.Traits.Count*115;
            var companion=run.GetCompanion(owner.Id);if(companion!=null)height+=196;
            detailScroll=GUI.BeginScrollView(viewport,detailScroll,new Rect(0,0,width,Mathf.Max(viewport.height-2,height)),false,true);
            float y=DrawOathDetails(owner,3);
            if(companion!=null)
            {
                Label(new Rect(12,y,width-24,28),companion.Definition.InnateTrait,20,accent,TextAnchor.MiddleLeft,true);
                Label(new Rect(12,y+36,width-24,65),companion.Definition.InnateDescription,18,muted);y+=122;
                var myth=TowerMythCatalog.Get(companion.Definition.MythId);var synergy=TowerMythCatalog.GetSynergy(myth.Id);int members=run.MythCount(myth.Id);
                Round(new Rect(3,y,width-6,62),new Color(.072f,.095f,.123f),5);
                Label(new Rect(18,y+8,width-40,24),myth.Name+"  ·  「"+synergy.Name+"」  "+members+" / 3"+(members>=2?"  발동":""),17,members>=2?brass:paper,TextAnchor.MiddleLeft,true);
                FitLabel(new Rect(18,y+34,width-40,24),members>=2?synergy.Describe(members):"같은 신화 2명부터  "+synergy.Describe(2),15,12,muted);y+=74;
            }
            Label(new Rect(12,y,width-24,30),"스킬  "+owner.Skills.Count,21,paper,TextAnchor.MiddleLeft,true);y+=45;
            if(owner.Skills.Count==0){Label(new Rect(12,y,width-24,38),"습득한 스킬 없음",17,muted);y+=44;}
            foreach(var skill in owner.Skills)
            {
                Round(new Rect(3,y,width-6,131),new Color(.072f,.095f,.123f),5);
                Label(new Rect(18,y+12,width-245,29),skill.Definition.Name+"  "+skill.Level,20,paper,TextAnchor.UpperLeft,true);
                float remaining=SkillRemaining(owner.Id,skill.Definition.Id);
                Label(new Rect(width-224,y+13,202,25),remaining>0?remaining.ToString("0.0")+"초":"준비",15,remaining>0?muted:TowerWorld.Teal,TextAnchor.MiddleRight);
                FitLabel(new Rect(18,y+49,width-40,78),skill.Description,17,16,muted);y+=143;
            }
            y+=14;Label(new Rect(12,y,width-24,30),"특성  "+owner.Traits.Count,21,paper,TextAnchor.MiddleLeft,true);y+=44;
            if(owner.Traits.Count==0)Label(new Rect(12,y,width-24,35),"습득한 특성 없음",17,muted);
            foreach(var trait in owner.Traits)
            {
                Round(new Rect(3,y,width-6,103),new Color(.072f,.095f,.123f),5);
                Label(new Rect(18,y+10,width-40,28),trait.Definition.Name+"  "+trait.Level,20,paper,TextAnchor.UpperLeft,true);
                Label(new Rect(18,y+44,width-40,58),ConciseDescription(trait.Description),17,muted);y+=115;
            }
            GUI.EndScrollView();GUI.skin=previousSkin;
        }
        private void DrawMenu()
        {
            Fill(Screenful,new Color(.012f,.004f,.007f,.86f));Vignette(new Color(0,0,0,.8f));Grain(Screenful,.07f,true);Panel(new Rect(540,166,520,572),brass);
            Label(new Rect(586,208,428,29),"PAUSED",13,brass,TextAnchor.MiddleCenter,true);
            Label(new Rect(584,254,436,59),"숨을 고르는 중",35,new Color(.55f,.04f,.06f,.5f),TextAnchor.MiddleCenter,true);
            Label(new Rect(582,251,436,59),"숨을 고르는 중",35,paper,TextAnchor.MiddleCenter,true);
            if(Button(new Rect(600,350,400,56),"계속하기",brass,true,true))menu=false;
            if(Button(new Rect(600,423,400,48),"조작과 규칙",silver))help=true;
            if(Button(new Rect(600,487,400,48),"화면 흔들림  ·  "+(ScreenShakeEnabled?"켜짐":"꺼짐"),muted))ScreenShakeEnabled=!ScreenShakeEnabled;
            if(Button(new Rect(600,551,400,48),"처음 화면",muted)){NewRun();intro=true;inspectedOwner="player";}
            if(Button(new Rect(600,615,400,48),"데모 선택",muted))BackToHub();
            Label(new Rect(600,684,400,27),run.Floor+"층  ·  "+CombatTime(elapsed),16,muted,TextAnchor.MiddleCenter);
        }
        private void DrawEnding(bool won)
        {
            MenuBackdrop(won?(run.IsOathTrial?"서약을 완수했습니다":"탑의 정상에 도달했습니다"):"등반이 끝났습니다",won?"OATH FULFILLED":"JOURNEY ENDED");
            Color c=won?brass:silver;
            Halo(new Rect(450,135,700,520),Alpha(c,.20f));
            Label(new Rect(600,202,400,110),run.Floor.ToString("00"),85,c,TextAnchor.MiddleCenter,true);
            Label(new Rect(600,318,400,32),"/ "+run.MaxFloor+"층",22,muted,TextAnchor.MiddleCenter);
            float x=240;
            StatCell(new Rect(x,420,240,75),"처치",kills.ToString());StatCell(new Rect(x+290,420,240,75),"등반 시간",CombatTime(elapsed));
            StatCell(new Rect(x+580,420,240,75),run.IsOathTrial?"서약 연계":"획득 포인트",run.IsOathTrial?run.Oath.Activations+"회":"+"+run.ClearedFloors+" P");
            StatCell(new Rect(x+870,420,240,75),"무피격 클리어",FlawlessFloors+"층");
            Fill(new Rect(240,534,1120,1),Alpha(c,.18f));
            Label(new Rect(240,565,1120,64),"순간 회피  "+PerfectDodges+"     ·     반격 적중  "+CounterHits+"     ·     돌진 베기  "+DashSlashes+"     ·     마무리 일격  "+Finishers+"     ·     받은 피해  "+Mathf.CeilToInt(DamageTaken),18,muted,TextAnchor.MiddleCenter);
            if(Button(new Rect(555,689,490,60),run.IsOathTrial?"다른 서약으로 도전":"다시 등반하기",brass,true,true))
            {bool trial=run.IsOathTrial;NewRun();intro=true;oathLobby=trial;inspectedOwner="player";}
            if(Button(new Rect(660,771,280,44),"데모 선택",muted))BackToHub();
        }
        private void DrawHelp()
        {
            MenuBackdrop("조작과 규칙","FIELD GUIDE");
            if(Button(new Rect(1320,76,216,48),"닫기",brass))help=false;
            string[] keys={"A / D","Space","J","Shift","Shift → J","Ctrl","S / ↓","E","Esc","Tab"};
            string[] actions={"이동","점프 · 공중에서 한 번 더 · 짧게 누르면 낮게","검 공격 · 3타째 마무리 일격","무적 회피 · 공중에서도 높이 유지","돌진 베기 · 회피 중이나 직후 공격","정밀 이동","발판 아래로","가까운 문으로 이동 · 상인과 거래","메뉴 · 상점 나가기","동료와 능력"};
            for(int i=0;i<keys.Length;i++){float y=196+i*58;Keycap(new Rect(96,y,120,36),keys[i]);Label(new Rect(234,y,520,36),actions[i],18,paper,TextAnchor.MiddleLeft);}
            bool trial=run.IsOathTrial||oathLobby;
            Panel(new Rect(772,198,712,574),brass);
            Label(new Rect(808,229,640,37),trial?"서약 연계":"회피와 반격",27,brass,TextAnchor.MiddleLeft,true);
            string rules=trial?"검격 적중 +1 · 순간 회피 +2\n기본 4충전 후 다음 검격 적중에 연계가 발동합니다. 헛스윙은 충전을 소모하지 않습니다.\n\n회피 시작 0.12초 안에 탄환을 통과하면 순간 회피입니다. 다음 검격의 반격 효과는 3초간 유지됩니다.\n\n여러 서약 변형은 함께 적용되며 충전은 다음 층에도 유지됩니다.\n\n5·10층 보스 · 10층 클리어로 종료\n신화 포인트는 적립하지 않습니다.":
                "회피 시작 0.12초 안에 탄환을 통과하면 반격을 준비합니다. 3초 안에 맞힌 다음 검격은 피해가 50% 증가합니다.\n\n출발과 30·60층에서 모든 신화의 동료를 영입합니다. 같은 신화 2·3명이면 시너지가 발동합니다.\n\n5층마다 보스와 증강이 있고, 출구에서 엘리트·휴식·상점 층을 고를 수 있습니다. 층마다 1 P를 얻어 상점에서 씁니다.";
            Label(new Rect(808,298,640,360),rules,19,muted);
            Label(new Rect(808,675,640,65),"적을 모두 처치하면 생명력 18 회복\n보상은 하나만 선택 · 각 칸 다시 뽑기 1회",18,paper);
        }
    }
}
