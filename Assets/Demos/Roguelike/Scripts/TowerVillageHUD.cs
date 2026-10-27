using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Screens of the story campaign: the village HUD and its world labels, the tower gate that previews an
    // expedition's companion draft, the shrine of restored myths, story pages, and the outcome of each expedition.
    public sealed partial class TowerGame
    {
        private const int MythChapters=8;
        private bool confirmReset;
        private string RestoredLine()
        {return campaign.IsComplete?"모든 신화를 되찾고 악신을 쓰러뜨렸습니다":"되찾은 신화  "+Mathf.Min(campaign.ClearedChapters,MythChapters)+" / "+MythChapters;}

        private void DrawVillageHUD()
        {
            Vignette(new Color(0,0,0,.45f));
            Light(new Rect(0,0,1600,160),new Color(.02f,.004f,.008f,.55f));
            Panel(new Rect(36,27,440,92),brass);
            if(Button(new Rect(45,35,74,74),"",brass))OpenParty("player");
            Portrait(new Rect(45,35,74,74),0,brass);
            Label(new Rect(135,34,320,27),"루멘의 유일신",19,paper,TextAnchor.MiddleLeft,true);
            Label(new Rect(135,62,330,22),"Lv. "+run.Player.Level+"  ·  생명력 "+Mathf.CeilToInt(PlayerMaxHealth)+"  ·  공격력 "+run.Player.Damage.ToString("0"),14,muted);
            Label(new Rect(135,86,330,22),RestoredLine(),14,brass);
            Label(new Rect(500,24,600,44),TowerCampaign.Village,30,paper,TextAnchor.MiddleCenter,true);
            var next=campaign.NextChapter;
            Label(new Rect(400,70,800,24),next==null?"악신이 쓰러진 행성 "+TowerCampaign.Planet:"다음 원정  ·  "+next.Numeral+" "+next.Title+"  ·  "+next.Floors,15,next==null?brass:muted,TextAnchor.MiddleCenter);
            DrawGold(new Rect(1150,84,262,24),15,brass);
            if(Button(new Rect(1424,28,112,42),"메뉴",muted))menu=!menu;Keycap(new Rect(1499,39,28,20),"Esc");
            if(noticeTime>0&&!menu&&!party&&!help&&!VillageBlocking)
            {
                float a=Mathf.Clamp01(noticeTime*2);
                Round(new Rect(420,132,760,45),new Color(.03f,.012f,.016f,a*.88f),5);
                Fill(new Rect(440,176,720,1),Alpha(brass,.5f*a));
                Label(new Rect(438,135,724,39),notice,17,Alpha(paper,a),TextAnchor.MiddleCenter);
            }
            string prompt=SpotPrompt();
            if(prompt!=null&&!VillageBlocking)
            {
                Rect r=new Rect(520,790,560,62);Panel(r,brass);
                Keycap(new Rect(r.x+22,r.y+17,34,28),"E");
                Label(new Rect(r.x+70,r.y+10,r.width-90,42),prompt,18,paper,TextAnchor.MiddleLeft,true);
            }
        }
        private bool VillageHUDHit(Vector2 p){return new Rect(36,27,440,92).Contains(p)||new Rect(1424,28,112,42).Contains(p);}

        private void WorldTag(Vector2 world,string title,string subtitle,Color accent,bool active)
        {
            var p=WorldGui(world);if(p.x<-200||p.x>1800)return;
            float w=Mathf.Max(132,Mathf.Max(title.Length*17,(subtitle??"").Length*12)+36),h=string.IsNullOrEmpty(subtitle)?30:48;
            Rect r=new Rect(p.x-w*.5f,p.y-h*.5f,w,h);
            Round(r,new Color(.03f,.015f,.02f,active?.92f:.76f),5);
            if(active)Border(r,Alpha(accent,.8f));
            Label(new Rect(r.x,r.y+3,w,24),title+(active?"  [E]":""),15,accent,TextAnchor.MiddleCenter,true);
            if(!string.IsNullOrEmpty(subtitle))Label(new Rect(r.x,r.y+24,w,20),subtitle,12,muted,TextAnchor.MiddleCenter);
        }
        private void DrawVillageLabels()
        {
            foreach(var sign in TutorialSigns)
            {
                var p=WorldGui(new Vector2(sign.x,sign.y+sign.lift));if(p.x<-200||p.x>1800)continue;
                float keys=sign.keys.Length*9+22,w=keys+sign.text.Length*15+44;
                Rect r=new Rect(p.x-w*.5f,p.y-22,w,44);
                Round(r,new Color(.03f,.015f,.02f,.84f),5);Border(r,Alpha(TowerWorld.Teal,.45f));
                Keycap(new Rect(r.x+10,r.y+8,keys,28),sign.keys);
                Label(new Rect(r.x+keys+20,r.y,w-keys-26,44),sign.text,15,paper,TextAnchor.MiddleLeft);
            }
            Fighter npc;var spot=NearestSpot(out npc);
            WorldTag(new Vector2(SteleX,TowerWorld.Ground+2.25f),"이야기 비석",null,silver,spot==VillageSpot.Stele);
            WorldTag(new Vector2(ShrineX,TowerWorld.Ground+3.75f),"신화의 제단",RestoredLine(),gold,spot==VillageSpot.Shrine);
            var next=campaign.NextChapter;
            WorldTag(new Vector2(TowerWorld.ExitX,TowerWorld.Ground+4.1f),"서약의 탑",next==null?"고요해진 탑":next.Numeral+" "+next.Title+"  ·  "+next.Floors,brass,spot==VillageSpot.Gate);
            foreach(var v in villagers)
            {
                // The crowd of restored gods only names the one in reach; the next chapter's two gods are always named.
                var d=TowerRun.GetDefinition(v.id);bool candidate=IsCandidate(v.id);if(!candidate&&npc!=v)continue;
                WorldTag(v.pos+Vector2.up*2.15f,d.Name,candidate?"새 동료 후보":"되찾은 신",candidate?TowerWorld.Teal:TierColor(d.Tier),npc==v);
            }
        }

        private void DrawGatePanel()
        {
            var next=campaign.NextChapter;
            if(next==null)
            {
                MenuBackdrop("서약의 탑","TOWER GATE");
                Halo(new Rect(450,180,700,480),Alpha(brass,.18f));
                Label(new Rect(0,330,1600,50),"악신 니힐로스는 쓰러졌습니다",34,paper,TextAnchor.MiddleCenter,true);
                Label(new Rect(0,400,1600,30),"탑은 이제 모든 신이 함께 지키는 등대입니다. 되찾은 신화는 "+MythChapters+"개입니다.",18,muted,TextAnchor.MiddleCenter);
                if(Button(new Rect(64,791,236,53),"돌아가기  [Esc]",muted))villagePanel=VillagePanel.None;
                return;
            }
            var theme=TowerThemeCatalog.Get(next.Theme);
            MenuBackdrop(next.Numeral+"  ·  "+next.Title,"TOWER GATE  /  "+next.Floors);
            DrawGold(new Rect(1130,83,400,36),22,gold);
            Panel(new Rect(64,176,470,580),brass);
            Label(new Rect(96,198,410,36),next.Subtitle,27,paper,TextAnchor.MiddleLeft,true);
            Label(new Rect(96,238,410,24),next.Floors+"  ·  "+theme.Name,15,brass);
            Fill(new Rect(96,272,406,1),Alpha(brass,.25f));
            FitLabel(new Rect(96,286,410,170),next.Intro,18,14,muted);
            Label(new Rect(96,470,410,24),"보스",15,brass,TextAnchor.MiddleLeft,true);
            Label(new Rect(96,498,410,24),(next.FirstFloor+4)+"층  ·  "+theme.Boss.Name,16,paper);
            Label(new Rect(96,526,410,24),next.LastFloor+"층  ·  "+(theme.FinalBoss!=null?theme.FinalBoss.Name:theme.Boss.Name+" · 각성"),16,theme.FinalBoss!=null?brass:paper,TextAnchor.MiddleLeft,theme.FinalBoss!=null);
            Fill(new Rect(96,566,406,1),Alpha(brass,.18f));
            Label(new Rect(96,580,410,24),RestoredLine(),15,muted);
            FitLabel(new Rect(96,610,410,130),"층마다 능력치 증강 · 5층마다 보스와 스킬 증강 · 5층을 넘으면 휴식·상점·엘리트 문을 고릅니다.\n원정에 실패하면 그 원정에서 얻은 성장은 사라지고, 마지막 층을 넘기면 성장을 간직한 채 마을로 돌아옵니다.",14,12,muted);

            // Companions are picked on departure from the whole pool, one per open slot.
            int slots=campaign.CompanionSlots;var pool=campaign.CompanionPool;
            Label(new Rect(566,176,970,30),"동료 선택  ·  출발하면 후보 "+pool.Count+"명 가운데 함께 오를 동료 "+slots+"명을 직접 고릅니다",20,paper,TextAnchor.MiddleLeft,true);
            for(int i=0;i<TowerRun.MaxCompanions;i++)
            {
                bool open=i<slots;Rect r=new Rect(566+i*328,218,314,92);
                Round(r,rim,5);Round(Inset(r,1),ink,4);if(open){Light(new Rect(r.x+2,r.y+2,r.width-4,44),Alpha(brass,.12f));Border(r,Alpha(brass,.55f));}
                Diamond(new Vector2(r.x+30,r.y+46),open?9:6,open?brass:Alpha(muted,.6f));
                Label(new Rect(r.x+52,r.y+14,250,28),(i+1)+"번째 동료",19,open?paper:muted,TextAnchor.MiddleLeft,true);
                Label(new Rect(r.x+52,r.y+48,250,24),open?"출발할 때 후보 가운데 고릅니다":TowerRun.RecruitFloors[i-1]+"층을 클리어하면 열립니다",14,open?brass:Alpha(muted,.8f));
            }
            Label(new Rect(566,334,500,28),"동료 후보  "+pool.Count+"명",19,paper,TextAnchor.MiddleLeft,true);
            Label(new Rect(1036,334,500,28),"되찾은 신화와 이번 장 신화의 신들  ·  마우스를 올리면 정보",14,muted,TextAnchor.MiddleRight);
            string hoverId=null;Rect hoverRect=default(Rect);
            for(int i=0;i<pool.Count;i++)
            {
                // The next chapter's own gods are new to the pool; the others show the growth they bring back.
                string id=pool[i];var d=TowerRun.GetDefinition(id);bool fresh=IsCandidate(id);int saved=campaign.SavedUpgrades(id);
                float x=566+i%8*121,y=372+i/8*190;Rect card=new Rect(x,y,113,180);
                Round(card,rim,5);Round(Inset(card,1),ink,4);if(fresh)Border(card,Alpha(TowerWorld.Teal,.7f));
                PortraitArt(new Rect(x+16,y+10,80,80),d.ArtIndex);
                Label(new Rect(x+4,y+96,105,24),d.Name,15,paper,TextAnchor.MiddleCenter,true);
                Label(new Rect(x+4,y+121,105,20),TowerMythCatalog.Get(d.MythId).Name,12,muted,TextAnchor.MiddleCenter);
                Label(new Rect(x+4,y+146,105,22),fresh?"새 동료 후보":saved>0?"성장 Lv. "+(1+saved):RoleName(d.Role),12,fresh?TowerWorld.Teal:saved>0?brass:muted,TextAnchor.MiddleCenter);
                if(GUI.enabled&&card.Contains(Pointer)){hoverId=id;hoverRect=card;}
            }
            Fill(new Rect(64,772,1472,1),new Color(.5f,.56f,.63f,.15f));
            if(Button(new Rect(64,791,236,53),"돌아가기  [Esc]",muted))villagePanel=VillagePanel.None;
            Label(new Rect(326,791,730,53),"동료 "+slots+"명을 고른 뒤 1층부터 "+next.LastFloor+"층까지 오릅니다",16,muted,TextAnchor.MiddleLeft);
            if(Button(new Rect(1080,786,456,64),next.IsFinal?"최후의 원정 출발":"원정 출발",brass,true,true))StartExpedition();
            ShowGodInfo(hoverId,hoverRect,1);
        }

        private void DrawShrinePanel()
        {
            MenuBackdrop("신화의 제단","SHRINE OF MYTHS");
            Label(new Rect(1030,83,500,36),RestoredLine(),20,brass,TextAnchor.MiddleRight,true);
            for(int i=0;i<TowerCampaign.Chapters.Count;i++)
            {
                var ch=TowerCampaign.Chapters[i];bool restored=campaign.IsRestored(ch),next=ch==campaign.NextChapter;
                Color c=restored?brass:next?TowerWorld.Teal:muted;Rect r=new Rect(64+(i%3)*496,176+(i/3)*196,476,180);
                Round(r,rim,5);Round(Inset(r,1),ink,4);Light(new Rect(r.x+2,r.y+2,r.width-4,80),Alpha(c,restored?.12f:.05f));
                Label(new Rect(r.x+22,r.y+14,320,28),ch.Numeral+"  ·  "+ch.Title,20,restored||next?paper:muted,TextAnchor.MiddleLeft,true);
                Label(new Rect(r.x+22,r.y+44,320,22),ch.Subtitle+"  ·  "+ch.Floors,13,muted);
                Label(new Rect(r.xMax-150,r.y+14,128,28),restored?"되찾음":next?"다음 원정":"봉인됨",15,c,TextAnchor.MiddleRight,true);
                Fill(new Rect(r.x+22,r.y+72,r.width-44,1),Alpha(c,.2f));
                if(restored&&ch.Candidates.Count>0)
                {
                    // Both gods of a restored myth stay in the companion draft for every later expedition.
                    for(int k=0;k<ch.Candidates.Count;k++)
                    {
                        var d=TowerRun.GetDefinition(ch.Candidates[k]);float x=r.x+22+k*220;
                        Portrait(new Rect(x,r.y+86,70,70),d.ArtIndex,brass);
                        Label(new Rect(x+80,r.y+94,136,26),d.Name,18,paper,TextAnchor.MiddleLeft,true);
                        Label(new Rect(x+80,r.y+122,136,22),"동료 후보",13,brass);
                    }
                }
                else if(restored)FitLabel(new Rect(r.x+22,r.y+84,r.width-44,86),ch.RestoredBody,15,12,paper);
                else if(next)FitLabel(new Rect(r.x+22,r.y+84,r.width-44,86),ch.Intro,14,11,muted);
                else Label(new Rect(r.x+22,r.y+84,r.width-44,40),"앞선 장을 마치면 열립니다",14,Alpha(muted,.7f));
            }
            if(Button(new Rect(64,791,236,53),"돌아가기  [Esc]",muted))villagePanel=VillagePanel.None;
            if(Button(new Rect(1240,791,296,53),"이야기 다시 보기",silver)){villagePanel=VillagePanel.None;ShowStory(TowerCampaign.Prologue,"PROLOGUE",null);}
        }

        private void DrawStory()
        {
            var page=storyPages[storyPage];bool last=storyPage==storyPages.Count-1;
            Fill(Screenful,new Color(.008f,.004f,.006f,.95f));
            Halo(new Rect(300,40,1000,760),new Color(.42f,.04f,.06f,.2f));
            Grain(Screenful,.09f,true);Vignette(new Color(0,0,0,.9f));
            Label(new Rect(0,176,1600,24),storyEyebrow+"   "+(storyPage+1)+" / "+storyPages.Count,14,brass,TextAnchor.MiddleCenter,true);
            Label(new Rect(3,231,1600,70),page.Title,44,new Color(.55f,.04f,.06f,.55f),TextAnchor.MiddleCenter,true);
            Label(new Rect(0,228,1600,70),page.Title,44,paper,TextAnchor.MiddleCenter,true);
            Fill(new Rect(560,318,480,1),Alpha(brass,.45f));
            FitLabel(new Rect(330,352,940,260),page.Body,25,18,paper,TextAnchor.UpperCenter);
            for(int i=0;i<storyPages.Count;i++)Diamond(new Vector2(800+(i-(storyPages.Count-1)*.5f)*24,652),i==storyPage?8:5,i==storyPage?brass:Alpha(muted,.5f));
            if(Button(new Rect(660,690,280,58),last?"계속":"다음",brass,true,true))AdvanceStory();
            Label(new Rect(0,770,1600,24),"Enter · Space · E 로 넘기기   ·   Esc 건너뛰기",13,muted,TextAnchor.MiddleCenter);
        }

        private void DrawExpeditionStats(float y)
        {
            float x=240;
            StatCell(new Rect(x,y,240,75),"처치",kills.ToString());StatCell(new Rect(x+290,y,240,75),"원정 시간",CombatTime(elapsed));
            StatCell(new Rect(x+580,y,240,75),"모은 골드","+"+GoldCollected+" G");StatCell(new Rect(x+870,y,240,75),"무피격 클리어",FlawlessFloors+"층");
        }
        private void DrawChapterComplete()
        {
            var ch=run.Chapter;
            MenuBackdrop(ch.RestoredTitle,ch.Numeral+"  /  "+(ch.IsFinal?"THE EVIL GOD FALLS":"MYTH RESTORED"));
            Halo(new Rect(450,135,700,520),Alpha(brass,.2f));
            FitLabel(new Rect(260,176,1080,64),ch.RestoredBody,22,17,paper,TextAnchor.MiddleCenter);
            float y=260;var next=campaign.NextChapter;
            if(next!=null&&next.Candidates.Count>0)
            {
                // The next chapter's two gods join the companion draft.
                var names=new List<string>();float left=800-(next.Candidates.Count*150-20)*.5f;
                for(int i=0;i<next.Candidates.Count;i++)
                {
                    var d=TowerRun.GetDefinition(next.Candidates[i]);names.Add(d.Name);
                    Portrait(new Rect(left+i*150,y,130,130),d.ArtIndex,TowerWorld.Teal);
                }
                Label(new Rect(0,y+140,1600,30),"새 동료 후보  ·  "+string.Join("  ·  ",names.ToArray()),20,TowerWorld.Teal,TextAnchor.MiddleCenter,true);y+=186;
            }
            else y+=40;
            // Clearing floors 30 and 60 opens another companion slot for the next expedition.
            bool wider=next!=null&&TowerCampaign.CompanionSlots(next)>TowerCampaign.CompanionSlots(ch);
            Label(new Rect(0,y,1600,28),next!=null?"다음 원정이 열렸습니다  ·  "+next.Numeral+" "+next.Title+"  "+next.Floors:"모든 신화를 되찾고 악신을 쓰러뜨렸습니다",18,paper,TextAnchor.MiddleCenter);
            Label(new Rect(0,y+30,1600,24),wider?"이제 동료 "+TowerCampaign.CompanionSlots(next)+"명을 골라 함께 오릅니다  ·  이번 원정의 성장은 마을에 간직됩니다":"이번 원정의 성장은 마을에 간직됩니다",14,wider?brass:muted,TextAnchor.MiddleCenter,wider);
            Fill(new Rect(240,y+70,1120,1),Alpha(brass,.18f));
            DrawExpeditionStats(y+90);
            if(ch.IsFinal)
            {
                // The finale gathers every god whose myth was restored on the way up.
                var roster=TowerCampaign.CompanionPool(ch);float width=roster.Count*84-14,x=800-width*.5f;
                Label(new Rect(0,y+196,1600,26),"함께 탑을 오른 신들",16,brass,TextAnchor.MiddleCenter,true);
                for(int i=0;i<roster.Count;i++)
                {
                    var d=TowerRun.GetDefinition(roster[i]);Portrait(new Rect(x+i*84,y+232,70,70),d.ArtIndex,TierColor(d.Tier));
                    Label(new Rect(x+i*84-10,y+306,90,22),d.Name,13,paper,TextAnchor.MiddleCenter);
                }
            }
            bool epilogue=ch.IsFinal&&campaign.IsComplete&&!campaign.EndingSeen;
            if(Button(new Rect(555,760,490,60),epilogue?"에필로그 보기":"마을로 귀환",brass,true,true))ReturnFromChapter();
        }
        private void DrawExpeditionLost()
        {
            var ch=run.Chapter;
            MenuBackdrop("원정이 끝났습니다","EXPEDITION FAILED  /  "+ch.Numeral+"  "+ch.Title);
            Halo(new Rect(450,135,700,520),Alpha(silver,.16f));
            Label(new Rect(600,202,400,110),run.Floor.ToString("00"),85,silver,TextAnchor.MiddleCenter,true);
            Label(new Rect(600,318,400,32),"/ "+ch.LastFloor+"층",22,muted,TextAnchor.MiddleCenter);
            DrawExpeditionStats(420);
            Fill(new Rect(240,534,1120,1),Alpha(silver,.18f));
            Label(new Rect(240,560,1120,60),"이번 원정에서 얻은 증강은 사라졌습니다. 모은 골드는 남아 있습니다.\n같은 동료와 곧바로 다시 오르거나, 마을로 돌아가 다음 출발 때 동료를 새로 고를 수 있습니다.",17,muted,TextAnchor.MiddleCenter);
            if(Button(new Rect(555,689,490,60),"같은 동료와 다시 도전",brass,true,true))RetryExpedition();
            if(Button(new Rect(660,771,280,44),"마을로 돌아가기",muted))EnterVillage();
        }
    }
}
