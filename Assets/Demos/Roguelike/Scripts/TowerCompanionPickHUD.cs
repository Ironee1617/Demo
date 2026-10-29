using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // The campaign's opening companion pick: every free god of the chapter's pool laid out at once, the party
    // slots above them, and an info card for whichever god the cursor rests on (the tower gate's preview uses it too).
    public sealed partial class TowerGame
    {
        private readonly List<string> companionPicks=new List<string>();
        private string pickRunId;
        private string infoGodId;private CompanionState infoGod;
        // Smoke captures point at a card without a real cursor.
        internal Vector2? PointerOverride;
        private Vector2 Pointer{get{return PointerOverride??Event.current.mousePosition;}}

        /// <summary>Gods still to choose before the expedition's first fight.</summary>
        private int PicksNeeded{get{return Mathf.Min(run.CompanionLimit-run.Companions.Count,run.Offers.Count);}}
        private RewardOffer RecruitOfferFor(string id){foreach(var offer in run.Offers)if(offer.CompanionId==id)return offer;return null;}
        private int PartyMythCount(string mythId)
        {int count=run.MythCount(mythId);foreach(string id in companionPicks)if(TowerRun.GetDefinition(id).MythId==mythId)count++;return count;}

        private void DrawCompanionPick()
        {
            // Choices belong to one expedition and only to gods still on offer.
            if(pickRunId!=run.RunId){pickRunId=run.RunId;companionPicks.Clear();}
            companionPicks.RemoveAll(id=>RecruitOfferFor(id)==null);
            int needed=PicksNeeded;bool ready=companionPicks.Count==needed;
            var e=Event.current;
            if(ready&&GUI.enabled&&e.type==EventType.KeyDown&&(e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter)){e.Use();ConfirmCompanionPick();return;}
            var chapter=run.Chapter;
            MenuBackdrop("함께 오를 동료를 고르세요","EXPEDITION START  /  "+chapter.Numeral+"  "+chapter.Title+"  /  COMPANIONS");
            Label(new Rect(1030,83,500,36),"후보 "+run.Offers.Count+"명  ·  동료 "+needed+"명 선택",18,brass,TextAnchor.MiddleRight,true);
            string hoverId=null;Rect hoverRect=default(Rect);int hoverMembers=0;
            bool live=GUI.enabled;Vector2 mouse=Pointer;

            // Slots: companions already along, then this pick's choices in order; a click on a choice takes it back.
            for(int i=0;i<TowerRun.MaxCompanions;i++)
            {
                Rect r=new Rect(64+i*328,176,314,92);bool open=i<run.CompanionLimit,kept=i<run.Companions.Count;
                int pick=i-run.Companions.Count;string id=kept?run.Companions[i].Id:pick>=0&&pick<companionPicks.Count?companionPicks[pick]:null;
                if(id==null)
                {
                    bool nextSlot=open&&pick==companionPicks.Count;Slot(r,brass,nextSlot);
                    Diamond(new Vector2(r.x+32,r.y+46),open?13:8,open?brass:Alpha(muted,.5f));if(open)Diamond(new Vector2(r.x+32,r.y+46),6,night);
                    Title(new Rect(r.x+56,r.y+14,250,28),(i+1)+"번째 동료",19,open?paper:muted,TextAnchor.MiddleLeft);
                    Label(new Rect(r.x+56,r.y+48,250,24),open?"아래 후보에서 고르세요":TowerRun.RecruitFloors[i-1]+"층을 클리어하면 열립니다",14,open?brass:Alpha(muted,.8f));
                    continue;
                }
                var d=TowerRun.GetDefinition(id);Color c=TierColor(d.Tier);
                if(kept)GodCard(r,c,true,.12f);
                else if(Tile(r,c,true)){companionPicks.Remove(id);Sound(chimeSound,.2f);return;}
                Portrait(new Rect(r.x+12,r.y+11,70,70),d.ArtIndex,c);
                Title(new Rect(r.x+96,r.y+12,190,28),d.Name,19,paper,TextAnchor.MiddleLeft);
                Label(new Rect(r.x+96,r.y+42,200,20),RoleName(d.Role)+"  ·  "+TowerMythCatalog.Get(d.MythId).Name,13,muted);
                Label(new Rect(r.x+96,r.y+64,200,18),kept?"함께하는 동료":"누르면 뺍니다",12,kept?brass:Alpha(muted,.85f));
                if(live&&r.Contains(mouse)){hoverId=id;hoverRect=r;hoverMembers=PartyMythCount(d.MythId);}
            }
            DrawPickSynergies(new Rect(1048,176,488,92));

            // Every free god at once, in chapter order: two myths' gods side by side, eight to a row.
            int count=run.Offers.Count,columns=Mathf.Min(8,count),rows=(count+7)/8;
            float left=800-(columns*172+(columns-1)*12)*.5f,top=rows>1?296:340;
            bool full=companionPicks.Count>=needed&&needed>1;
            for(int i=0;i<count;i++)
            {
                string id=run.Offers[i].CompanionId;var d=TowerRun.GetDefinition(id);var myth=TowerMythCatalog.Get(d.MythId);Color c=TierColor(d.Tier);
                Rect card=new Rect(left+i%8*184,top+i/8*226,172,212);int order=companionPicks.IndexOf(id);bool picked=order>=0,fresh=IsCandidate(id);
                if(Tile(card,c,picked)){TogglePick(id,needed);return;}
                Halo(new Rect(card.x+16,card.y+6,140,126),Alpha(c,picked?.34f:.2f));
                PortraitArt(new Rect(card.x+31,card.y+14,110,110),d.ArtIndex);
                Title(new Rect(card.x+6,card.y+128,160,28),d.Name,18,paper,TextAnchor.MiddleCenter);
                Label(new Rect(card.x+6,card.y+158,160,20),myth.Name+"  ·  "+RoleName(d.Role),13,muted,TextAnchor.MiddleCenter);
                Label(new Rect(card.x+6,card.y+182,160,20),fresh?"새 동료":TierName(d.Tier),12,fresh?TowerWorld.Teal:c,TextAnchor.MiddleCenter,true);
                if(picked){Diamond(new Vector2(card.x+26,card.y+26),30,c);Diamond(new Vector2(card.x+26,card.y+26),24,Color.Lerp(c,Color.black,.55f));Title(new Rect(card.x+10,card.y+10,32,32),(order+1).ToString(),17,paper,TextAnchor.MiddleCenter);}
                else if(full)Round(card,new Color(0,0,0,.45f),6);
                if(live&&card.Contains(mouse)){hoverId=id;hoverRect=card;hoverMembers=PartyMythCount(d.MythId)+(picked?0:1);}
            }

            Divider(new Rect(64,767,1472,10),.5f);
            Label(new Rect(64,786,990,28),needed>1?"후보를 눌러 동료 "+needed+"명을 고르세요  ·  고른 동료는 다시 누르거나 위의 칸을 눌러 뺍니다":"후보를 눌러 함께 오를 동료를 고르세요  ·  다른 신을 누르면 바뀝니다",17,paper,TextAnchor.MiddleLeft);
            Label(new Rect(64,816,990,24),run.CompanionLimit>1?"신 위에 마우스를 올리면 정보가 보입니다  ·  같은 신화 2명이면 시너지가 발동합니다":"신 위에 마우스를 올리면 고유 패시브와 능력치가 보입니다",14,muted,TextAnchor.MiddleLeft);
            if(Button(new Rect(1080,786,456,64),ready?"이 동료들과 출발  [Enter]":"동료 "+(needed-companionPicks.Count)+"명을 더 고르세요",brass,ready,true)){ConfirmCompanionPick();return;}
            ShowGodInfo(hoverId,hoverRect,hoverMembers);
        }
        private void TogglePick(string id,int needed)
        {
            if(companionPicks.Remove(id)){Sound(chimeSound,.2f);return;}
            if(companionPicks.Count>=needed)
            {
                if(needed>1){Toast("동료 칸이 가득 찼습니다","고른 동료를 다시 누르거나 위의 칸을 눌러 먼저 빼세요",muted,2.5f);return;}
                companionPicks.Clear();
            }
            companionPicks.Add(id);Sound(chimeSound,.35f);
        }
        private void ConfirmCompanionPick()
        {
            // Each choice joins in order; the run opens the next slot's offer until the party is full.
            var picks=new List<string>(companionPicks);companionPicks.Clear();
            foreach(string id in picks){var offer=RecruitOfferFor(id);if(offer!=null)SelectOffer(offer.Id);}
        }
        private void DrawPickSynergies(Rect r)
        {
            Slot(r);
            Label(new Rect(r.x+20,r.y+10,r.width-40,24),"신화 시너지",15,brass,TextAnchor.MiddleLeft,true);
            if(run.CompanionLimit<2){FitLabel(new Rect(r.x+20,r.y+38,r.width-40,46),"이번 원정은 동료 1명과 오릅니다.\n30층을 넘으면 둘, 60층을 넘으면 셋이 함께합니다.",14,11,muted);return;}
            var active=new List<string>();var seen=new List<string>();
            var members=new List<string>();foreach(var c in run.Companions)members.Add(c.Id);members.AddRange(companionPicks);
            foreach(string id in members)
            {
                string mythId=TowerRun.GetDefinition(id).MythId;if(seen.Contains(mythId))continue;seen.Add(mythId);
                int n=PartyMythCount(mythId);if(n>=2){var synergy=TowerMythCatalog.GetSynergy(mythId);active.Add("「"+synergy.Name+"」 "+n+"명  ·  "+synergy.Describe(n));}
            }
            if(active.Count==0){FitLabel(new Rect(r.x+20,r.y+38,r.width-40,46),"같은 신화의 신 둘을 고르면 시너지가 발동합니다.",14,11,muted);return;}
            for(int i=0;i<active.Count&&i<2;i++)FitLabel(new Rect(r.x+20,r.y+36+i*24,r.width-40,22),active[i],14,10,paper);
        }

        /// <summary>Draws the info card for <paramref name="id"/> beside <paramref name="anchor"/>; null hides it.</summary>
        private void ShowGodInfo(string id,Rect anchor,int members)
        {
            if(id==null){infoGodId=null;return;}
            var d=TowerRun.GetDefinition(id);if(d==null)return;
            // The campaign's own preview builds the god as they would join, with whatever the village grants them.
            if(infoGodId!=id){infoGodId=id;var preview=campaign.Preview(0,new[]{id});infoGod=preview.Companions.Count>0?preview.Companions[0]:null;}
            var god=infoGod;if(god==null)return;
            var myth=TowerMythCatalog.Get(d.MythId);var synergy=TowerMythCatalog.GetSynergy(d.MythId);var passive=d.Passive;Color c=TierColor(d.Tier);
            // Below the fixed header and numbers, the passive, its legend, skills and synergy follow their text's length.
            const float w=440;
            float said=Mathf.Min(60,TextHeight(passive.Description,15,w-44)),lore=Mathf.Min(40,TextHeight(passive.Lore,13,w-44));
            float skills=288+said+lore+14,h=skills+198;
            float x=anchor.xMax+12+w<=1592?anchor.xMax+12:anchor.x-12-w;x=Mathf.Clamp(x,8,1592-w);
            float y=Mathf.Clamp(anchor.y-30,12,888-h);
            Panel(new Rect(x,y,w,h),c);
            Halo(new Rect(x+6,y+8,136,124),Alpha(c,.3f));PortraitArt(new Rect(x+22,y+20,104,104),d.ArtIndex);
            Title(new Rect(x+142,y+22,280,32),d.Name,26,paper,TextAnchor.MiddleLeft);
            Label(new Rect(x+142,y+56,280,22),d.Title,15,c);
            Label(new Rect(x+142,y+82,280,20),RoleName(d.Role)+"  ·  "+myth.Name+"  ·  "+TierName(d.Tier)+(god.Level>1?"  ·  Lv. "+god.Level:""),13,muted);
            Label(new Rect(x+142,y+104,280,20),d.Description.Split('\n')[0],12,muted);
            Divider(new Rect(x+22,y+133,w-44,10),.6f,false,c);
            bool healer=d.Role==CompanionRole.Healer;
            string[] names={"공격력","공격 간격","사거리","이동 속도","스킬 위력",healer?"5초마다 회복":"치유 배율"};
            string[] values={god.Damage.ToString("0.#"),god.AttackInterval.ToString("0.00")+"초",god.Range.ToString("0.#"),god.MoveSpeed.ToString("0.#"),
                god.SkillPowerMultiplier.ToString("0.##")+"배",healer?god.HealAmount.ToString("0.#"):god.HealingMultiplier.ToString("0.##")+"배"};
            for(int i=0;i<names.Length;i++)
            {
                float cx=x+22+i%3*132,cy=y+148+i/3*48;int icon=IconFor(names[i]);
                if(icon>=0)Icon(new Rect(cx-2,cy+10,26,26),(UIIcon)icon);
                Label(new Rect(cx+28,cy,100,18),names[i],12,muted);Title(new Rect(cx+28,cy+17,100,26),values[i],19,paper);
            }
            Divider(new Rect(x+22,y+241,w-44,10),.6f,false,c);
            Title(new Rect(x+22,y+256,w-44,24),"고유 패시브  ·  "+passive.Name,16,c,TextAnchor.MiddleLeft);
            FitLabel(new Rect(x+22,y+282,w-44,60),passive.Description,15,12,paper);
            FitLabel(new Rect(x+22,y+288+said,w-44,40),passive.Lore,13,11,muted);
            Label(new Rect(x+22,y+skills,w-44,22),"스킬  ·  증강으로 배우고 강화 · 계열은 빌드 공명",14,paper,TextAnchor.MiddleLeft,true);
            int slot=0;
            foreach(var augment in TowerRun.Augments)
            {
                if(augment.OwnerId!=id||augment.Kind!=RewardKind.Skill)continue;
                var skill=god.GetSkill(augment.Id);Rect cell=new Rect(x+22+slot%2*198,y+skills+26+slot/2*24,194,22);
                FitLabel(new Rect(cell.x,cell.y,cell.width-40,cell.height),augment.Name+(skill!=null?"  Lv."+skill.Level:""),14,11,skill!=null?brass:muted);
                Label(new Rect(cell.xMax-42,cell.y,40,cell.height),TowerSkillBuilds.StyleName(augment.Style),12,Alpha(StyleColor(augment.Style),.9f),TextAnchor.MiddleRight);
                if(++slot==6)break;
            }
            float box=y+skills+106;
            Slot(new Rect(x+16,box,w-32,76),brass,members>=2);
            Label(new Rect(x+28,box+6,w-56,22),"「"+synergy.Name+"」  "+myth.Name+"  "+Mathf.Min(members,3)+" / 3"+(members>=2?"  ·  발동":""),14,members>=2?brass:paper,TextAnchor.MiddleLeft,true);
            FitLabel(new Rect(x+28,box+30,w-56,40),members>=2?synergy.Describe(members):"같은 신화 2명부터  "+synergy.Describe(2),13,10,members>=2?paper:muted);
        }
        private float TextHeight(string text,int size,float width)
        {textStyle.wordWrap=true;textStyle.fontStyle=FontStyle.Normal;textStyle.fontSize=size;return textStyle.CalcHeight(new GUIContent(text),width);}
    }
}
