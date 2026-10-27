using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // The village skill tree: skill points banked on every floor a campaign expedition clears, the tree screen
    // where the village spends them, and the perks of its fortune branch during an expedition.
    public sealed partial class TowerGame
    {
        internal static readonly Color SkillPointColor=new Color(.62f,.84f,1f);
        // Skill points banked during the current expedition, shown on its result screen.
        internal int ExpeditionPoints;
        internal string treeSelection;
        private bool confirmTreeReset;
        private string treeFlashId;private float treeFlashTime=-9;private int treeOpenedFrame=-1;
        private Vector2 treeSummaryScroll;
        private float goldCarry;
        // The tree's grid sits centred in the frame on the left; the selected node's card fills the right.
        private static readonly Rect TreeFrame=new Rect(64,170,1048,608),TreeCard=new Rect(1130,170,406,608);
        private const float TreeSpacing=46,TreeNode=34,TreeKeystone=40;
        private static readonly float TreeMiddle=TreeMiddleColumn();

        internal void OpenSkillTree()
        {
            villagePanel=VillagePanel.SkillTree;confirmTreeReset=false;treeOpenedFrame=Time.frameCount;
            if(TowerSkillTree.Get(treeSelection)==null)treeSelection=TowerSkillTree.Root.Id;
            Sound(chimeSound,.3f);
        }

        /// <summary>Every cleared floor of a campaign expedition banks skill points; the tree's feast perk heals on top of the usual 18.</summary>
        private void RewardTreeFloorClear()
        {
            var tree=run.SkillTree;if(tree==null)return;
            float heal=tree.Perk(SkillPerk.ClearHeal);if(heal>0&&hero.hp>0)hero.hp=Mathf.Min(PlayerMaxHealth,hero.hp+heal);
            if(!run.IsCampaign||campaign==null)return;
            int added=campaign.AddSkillPoints(TowerSkillTree.PointsFor(run.Floor,run.FloorType,tree));if(added<=0)return;
            ExpeditionPoints+=added;SaveCampaign();
            FloatText(hero.pos+Vector2.up*2.7f,"+"+added+" SP",SkillPointColor);
        }
        /// <summary>The tree's foresight deals its stat cards before the first fight, once the companions have joined.</summary>
        private void TickOpeningAugments()
        {
            if(village||StoryOpen||hero==null||hero.hp<=0||run.OpeningAugments<=0)return;
            if(run.DealOpeningAugment())Toast("선견지명","첫 전투 전에 능력치 증강을 고릅니다",SkillPointColor,3);
        }
        /// <summary>Picked-up gold raised by the tree's gold find; fractions carry over to the next coin.</summary>
        private int TreeGold(int amount)
        {
            float bonus=run.SkillTree==null?0:run.SkillTree.Perk(SkillPerk.GoldFind);if(bonus<=0||amount<=0)return amount;
            goldCarry+=amount*bonus;int extra=(int)goldCarry;goldCarry-=extra;return amount+extra;
        }
        private float TreeHeartHeal(float heal){return run.SkillTree==null?heal:heal*(1+run.SkillTree.Perk(SkillPerk.HeartHeal));}

        internal bool LearnTreeNode(SkillNode node)
        {
            if(node==null||!campaign.LearnSkill(node.Id)){Sound(hitSound,.15f);return false;}
            SaveCampaign();treeFlashId=node.Id;treeFlashTime=Time.unscaledTime;Sound(chimeSound,.4f);
            return true;
        }
        private void ResetTree()
        {
            int refund=campaign.ResetSkillTree();confirmTreeReset=false;SaveCampaign();
            Toast("스킬트리 초기화",refund+" SP를 돌려받았습니다",SkillPointColor,3);
        }

        internal static Color BranchColor(SkillBranch branch)
        {
            switch(branch)
            {
                case SkillBranch.Arcane: return new Color(.68f,.5f,.98f);
                case SkillBranch.Swift: return new Color(.48f,.85f,.42f);
                case SkillBranch.Bond: return new Color(.36f,.82f,.86f);
                case SkillBranch.Fortune: return new Color(.95f,.77f,.32f);
                case SkillBranch.Guard: return new Color(.42f,.62f,1f);
                case SkillBranch.Might: return new Color(.96f,.36f,.3f);
                default: return new Color(.94f,.88f,.77f);
            }
        }
        // One rune per kind of effect, carved into the node.
        private static string NodeRune(SkillNode node)
        {
            switch(node.Perk)
            {
                case SkillPerk.GoldFind: return "금";
                case SkillPerk.ClearHeal: return "식";
                case SkillPerk.HeartHeal: return "심";
                case SkillPerk.OpeningAugment: return "선";
                case SkillPerk.BossPoints: return "현";
            }
            string[] runes={"공","생","방","질","속","사","치","흡","쿨","마","탄","재","회","범","유"};
            return runes[(int)node.Stat];
        }
        private static string TargetName(SkillNode node)
        {return node.Perk!=SkillPerk.None?"원정 효과":node.Target==SkillTarget.Companions?"동료 전원":node.Target==SkillTarget.Party?"유일신과 동료":"유일신";}
        private static float TreeMiddleColumn()
        {
            int low=int.MaxValue,high=int.MinValue;
            foreach(var node in TowerSkillTree.Nodes){low=Mathf.Min(low,node.Column);high=Mathf.Max(high,node.Column);}
            return (low+high)*.5f;
        }
        private static Vector2 TreeCenter(SkillNode node)
        {return new Vector2(TreeFrame.center.x+(node.Column-TreeMiddle)*TreeSpacing,TreeFrame.y+28+node.Row*TreeSpacing);}
        private static Rect TreeNodeRect(SkillNode node)
        {float size=node.IsKeystone?TreeKeystone:TreeNode;var c=TreeCenter(node);return new Rect(c.x-size*.5f,c.y-size*.5f,size,size);}
        /// <summary>A straight bar between two points, drawn by rotating the GUI matrix.</summary>
        private void TreeLine(Vector2 a,Vector2 b,float width,Color color)
        {
            Vector2 d=b-a;float length=d.magnitude;if(length<.5f)return;
            var saved=GUI.matrix;
            GUI.matrix=saved*Matrix4x4.TRS(new Vector3(a.x,a.y,0),Quaternion.Euler(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg),Vector3.one);
            Fill(new Rect(0,-width*.5f,length,width),color);
            GUI.matrix=saved;
        }

        private void DrawSkillTreePanel()
        {
            var tree=campaign.SkillTree;int points=campaign.SkillPoints;
            MenuBackdrop("스킬트리","SKILL TREE  /  루멘의 신격");
            Label(new Rect(930,83,600,36),"배운 단계 "+tree.LearnedRanks+"  ·  사용 "+tree.Spent+" SP  ·  누적 "+campaign.EarnedPoints+" SP",17,muted,TextAnchor.MiddleRight);
            var e=Event.current;
            // K closes the screen it opened, but not on the very press that opened it.
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.K&&Time.frameCount!=treeOpenedFrame){villagePanel=VillagePanel.None;e.Use();return;}

            Panel(TreeFrame,brass);
            Round(new Rect(TreeFrame.x+22,TreeFrame.y+20,196,58),new Color(.02f,.012f,.016f,.92f),5);Border(new Rect(TreeFrame.x+22,TreeFrame.y+20,196,58),Alpha(SkillPointColor,.55f));
            Diamond(new Vector2(TreeFrame.x+48,TreeFrame.y+49),15,SkillPointColor);Diamond(new Vector2(TreeFrame.x+48,TreeFrame.y+49),7,new Color(.05f,.03f,.04f));
            Label(new Rect(TreeFrame.x+70,TreeFrame.y+22,140,22),"스킬 포인트",13,muted);
            Label(new Rect(TreeFrame.x+70,TreeFrame.y+40,140,34),points.ToString("N0"),26,points>0?paper:muted,TextAnchor.MiddleLeft,true);

            SkillNode hovered=null;
            foreach(var node in TowerSkillTree.Nodes)if(TreeNodeRect(node).Contains(e.mousePosition))hovered=node;
            if(GUI.enabled&&hovered!=null&&e.type==EventType.MouseDown&&e.button==0)
            {
                // A click selects a node; a double click also learns its next rank.
                treeSelection=hovered.Id;confirmTreeReset=false;
                if(e.clickCount>=2)LearnTreeNode(hovered);else Sound(chimeSound,.12f);
                e.Use();
            }
            // Links first, brighter once the lower node has a rank, then the nodes over them.
            foreach(var node in TowerSkillTree.Nodes)
            {
                if(node.IsRoot)continue;var parent=TowerSkillTree.Get(node.ParentId);Color c=BranchColor(node.Branch);
                bool learned=tree.Rank(node.Id)>0,open=tree.IsOpen(node);
                TreeLine(TreeCenter(parent),TreeCenter(node),learned?3:2,learned?Alpha(c,.9f):open?Alpha(c,.38f):new Color(.42f,.36f,.36f,.28f));
            }
            var selected=TowerSkillTree.Get(treeSelection)??TowerSkillTree.Root;
            foreach(var node in TowerSkillTree.Nodes)DrawTreeNode(node,TreeNodeRect(node),tree.Rank(node.Id),tree.IsOpen(node),campaign.CanLearn(node),node==selected,node==hovered);
            float x=TreeFrame.x+24;
            for(var branch=SkillBranch.Arcane;branch<=SkillBranch.Might;branch++)
            {
                Color c=BranchColor(branch);Round(new Rect(x,TreeFrame.yMax-34,12,12),c,2);
                Label(new Rect(x+18,TreeFrame.yMax-40,48,24),TowerSkillTree.BranchName(branch),13,Alpha(c,.9f),TextAnchor.MiddleLeft,true);x+=66;
            }

            DrawTreeCard(hovered??selected,hovered==null);
            if(Button(new Rect(64,791,236,53),"돌아가기  [Esc]",muted)){villagePanel=VillagePanel.None;return;}
            Label(new Rect(326,791,800,53),"층을 넘길 때마다 "+TowerSkillTree.FloorPoints+" SP · 보스·엘리트 층 "+TowerSkillTree.BossFloorPoints+" SP · 원정에 실패해도 포인트는 남습니다",15,muted,TextAnchor.MiddleLeft);
            if(Button(new Rect(1240,791,296,53),confirmTreeReset?"정말 모두 되돌릴까요?":"모두 되돌리기",confirmTreeReset?crimson:muted,tree.Spent>0))
            {if(confirmTreeReset)ResetTree();else confirmTreeReset=true;}
        }

        private void DrawTreeNode(SkillNode node,Rect r,int rank,bool open,bool affordable,bool selected,bool hovered)
        {
            Color c=BranchColor(node.Branch);bool learned=rank>0,maxed=rank>=node.MaxRank;
            float flash=node.Id==treeFlashId?Mathf.Clamp01(1-(Time.unscaledTime-treeFlashTime)/.6f):0;
            if(selected)Halo(new Rect(r.x-18,r.y-18,r.width+36,r.height+36),Alpha(c,.4f));
            else if(learned)Halo(new Rect(r.x-10,r.y-10,r.width+20,r.height+20),Alpha(c,.16f));
            if(flash>0)Halo(new Rect(r.x-26,r.y-26,r.width+52,r.height+52),Alpha(Color.Lerp(c,Color.white,.4f),.7f*flash));
            Color rim=learned?c:open?Alpha(c,affordable?.8f:.42f):new Color(.32f,.25f,.26f,.75f);
            Round(r,rim,5);
            Round(Inset(r,2),learned?Color.Lerp(c,Color.black,.6f):new Color(.05f,.03f,.035f,.97f),4);
            if(learned)Light(Inset(r,3),Alpha(Color.Lerp(c,Color.white,.3f),maxed?.32f:.18f));
            if(node.IsKeystone){Border(Inset(r,4),Alpha(learned?paper:c,learned?.5f:.3f));Diamond(new Vector2(r.center.x,r.y),7,rim);}
            // Learnable now: a slow pulse around the node.
            if(open&&!maxed&&affordable){float pulse=.3f+.3f*Mathf.Sin(Time.unscaledTime*4+node.Column);Border(Inset(r,-3),Alpha(c,pulse),2);}
            if(selected||hovered)Border(Inset(r,-5),Alpha(paper,selected?.85f:.45f));
            Label(r,NodeRune(node),node.IsKeystone?19:16,learned?paper:open?Alpha(Color.Lerp(c,paper,.25f),.95f):Alpha(muted,.5f),TextAnchor.MiddleCenter,true);
            if(node.MaxRank>1)
                for(int i=0;i<node.MaxRank;i++)
                {float px=r.center.x+(i-(node.MaxRank-1)*.5f)*7;Round(new Rect(px-2.5f,r.yMax+3,5,4),i<rank?c:new Color(.3f,.24f,.24f,.85f),1);}
        }

        private void DrawTreeCard(SkillNode node,bool interactive)
        {
            var tree=campaign.SkillTree;Color c=BranchColor(node.Branch);int rank=tree.Rank(node.Id);
            bool open=tree.IsOpen(node),maxed=rank>=node.MaxRank,affordable=campaign.SkillPoints>=node.Cost;
            Panel(TreeCard,c);Light(new Rect(TreeCard.x+2,TreeCard.y+2,TreeCard.width-4,120),Alpha(c,.12f));
            float x=TreeCard.x+24,w=TreeCard.width-48,y=TreeCard.y+22;
            Rect icon=new Rect(x,y,66,66);Halo(new Rect(icon.x-16,icon.y-16,98,98),Alpha(c,.3f));
            DrawTreeNode(node,icon,rank,open,false,false,false);
            FitLabel(new Rect(x+84,y+2,w-84,34),node.Name,25,18,paper,TextAnchor.MiddleLeft,true);
            Label(new Rect(x+84,y+38,w-84,24),TowerSkillTree.BranchName(node.Branch)+(node.IsKeystone?"  ·  핵심":"")+"  ·  "+TargetName(node),14,c);
            y+=90;Fill(new Rect(x,y,w,1),Alpha(c,.25f));y+=14;
            Label(new Rect(x,y,w,26),"단계  "+rank+" / "+node.MaxRank,18,paper,TextAnchor.MiddleLeft,true);
            Label(new Rect(x,y,w,26),node.Cost+" SP / 단계",16,SkillPointColor,TextAnchor.MiddleRight,true);y+=36;
            Label(new Rect(x,y,w,24),rank>0?"현재   "+node.Effect(rank):"현재   배우지 않음",16,rank>0?paper:muted);y+=28;
            Label(new Rect(x,y,w,24),maxed?"최대 단계에 도달했습니다":(rank>0?"다음   ":"배우면   ")+node.Effect(rank+1),16,maxed?brass:TowerWorld.Teal);y+=34;
            var parent=TowerSkillTree.Get(node.ParentId);
            string state=maxed?"모든 단계를 배웠습니다":!open?"먼저 「"+parent.Name+"」을(를) 배워야 합니다":!affordable?"스킬 포인트가 "+(node.Cost-campaign.SkillPoints)+" 부족합니다":"노드를 더블클릭하거나 아래 버튼으로 배웁니다";
            FitLabel(new Rect(x,y,w,40),state,15,12,!maxed&&(!open||!affordable)?new Color(.95f,.45f,.42f):muted);y+=46;
            bool can=interactive&&campaign.CanLearn(node);
            if(Button(new Rect(x,y,w,50),maxed?"최대 단계":"배우기  ·  "+node.Cost+" SP",c,can,true))LearnTreeNode(node);
            y+=68;Fill(new Rect(x,y,w,1),Alpha(c,.2f));y+=12;
            Label(new Rect(x,y,w,26),"적용 중인 효과",18,paper,TextAnchor.MiddleLeft,true);y+=32;
            var summary=tree.Summary();
            if(summary.Count==0){Label(new Rect(x,y,w,48),"아직 배운 노드가 없습니다.\n층을 넘기며 모은 포인트로 위쪽 불씨부터 배우세요.",14,muted);return;}
            PrepareScrollStyle();var previousSkin=GUI.skin;GUI.skin=uiSkin;
            Rect view=new Rect(x,y,w+12,TreeCard.yMax-18-y);
            treeSummaryScroll=GUI.BeginScrollView(view,treeSummaryScroll,new Rect(0,0,w-6,Mathf.Max(view.height-2,summary.Count*25)),false,true);
            for(int i=0;i<summary.Count;i++){Diamond(new Vector2(6,i*25+12),5,Alpha(c,.8f));Label(new Rect(18,i*25,w-30,24),summary[i],15,paper,TextAnchor.MiddleLeft);}
            GUI.EndScrollView();GUI.skin=previousSkin;
        }
    }
}
