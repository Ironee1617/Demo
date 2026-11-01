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
        // The grid is taller than the frame, so it scrolls in a view under the frame's header of points and branches.
        private static readonly Rect TreeView=new Rect(72,260,1006,510),TreeTrack=new Rect(1086,272,6,486);
        private const float TreeSpacing=46,TreeNode=34,TreeKeystone=40,TreeTop=32;
        private static readonly float TreeMiddle=TreeMiddleColumn(),TreeHeight=TreeTop+(TowerSkillTree.Rows-1)*TreeSpacing+44;
        private static float TreeScrollMax{get{return Mathf.Max(0,TreeHeight-TreeView.height);}}
        // The view eases toward its goal; wheel, keys and the bar move the goal, a drag moves both.
        private float treeScroll,treeScrollGoal,treeDragFrom,treeDragScroll;
        private enum TreeDrag{None,Pan,Bar}
        private TreeDrag treeDrag;

        internal void OpenSkillTree()
        {
            villagePanel=VillagePanel.SkillTree;confirmTreeReset=false;treeOpenedFrame=Time.frameCount;treeDrag=TreeDrag.None;
            if(TowerSkillTree.Get(treeSelection)==null)treeSelection=TowerSkillTree.Root.Id;
            RevealTreeNode(TowerSkillTree.Get(treeSelection),true);
            Sound(chimeSound,.3f);
        }
        /// <summary>Scrolls the least distance that brings the node into the view, at once or eased.</summary>
        internal void RevealTreeNode(SkillNode node,bool instant)
        {
            if(node==null)return;float y=TreeTop+node.Row*TreeSpacing;
            treeScrollGoal=Mathf.Clamp(Mathf.Clamp(treeScrollGoal,y+72-TreeView.height,y-72),0,TreeScrollMax);
            if(instant)treeScroll=treeScrollGoal;
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
            if(village||practice||StoryOpen||hero==null||hero.hp<=0||run.OpeningAugments<=0)return;
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
                case SkillBranch.Heaven: return new Color(1f,.58f,.8f);
                case SkillBranch.Underworld: return new Color(1f,.52f,.2f);
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
        private Vector2 TreeCenter(SkillNode node)
        {return new Vector2(TreeFrame.center.x+(node.Column-TreeMiddle)*TreeSpacing,TreeView.y+TreeTop+node.Row*TreeSpacing-treeScroll);}
        private Rect TreeNodeRect(SkillNode node)
        {float size=node.IsKeystone?TreeKeystone:TreeNode;var c=TreeCenter(node);return new Rect(c.x-size*.5f,c.y-size*.5f,size,size);}
        /// <summary>Cuts a link to the rows between <paramref name="top"/> and <paramref name="bottom"/>; false when none of it is left.</summary>
        private static bool ClipLink(ref Vector2 a,ref Vector2 b,float top,float bottom)
        {
            Vector2 p=a.y<=b.y?a:b,q=a.y<=b.y?b:a;
            if(q.y<top||p.y>bottom)return false;
            float dy=q.y-p.y;
            if(dy>.01f){Vector2 from=p;if(p.y<top)p=Vector2.Lerp(from,q,(top-from.y)/dy);if(q.y>bottom)q=Vector2.Lerp(from,q,(bottom-from.y)/dy);}
            a=p;b=q;return true;
        }
        /// <summary>Wheel, keys and drags move the view; a drag starts on the bar or on empty ground between the nodes.</summary>
        private void ScrollTree(Event e,bool overNode)
        {
            float max=TreeScrollMax,thumb=TreeThumb.height,travel=TreeTrack.height-thumb;
            if(GUI.enabled&&e.type==EventType.ScrollWheel&&TreeFrame.Contains(e.mousePosition)){ScrollTreeBy(e.delta.y*20);e.Use();}
            else if(GUI.enabled&&e.type==EventType.KeyDown)
            {
                float step=e.keyCode==KeyCode.UpArrow?-TreeSpacing*2:e.keyCode==KeyCode.DownArrow?TreeSpacing*2:e.keyCode==KeyCode.PageUp?-TreeView.height*.8f:e.keyCode==KeyCode.PageDown?TreeView.height*.8f:0;
                if(e.keyCode==KeyCode.Home)step=-max;else if(e.keyCode==KeyCode.End)step=max;
                if(step!=0){ScrollTreeBy(step);e.Use();}
            }
            else if(e.type==EventType.MouseDown&&GUI.enabled)
            {
                var m=e.mousePosition;var bar=new Rect(TreeTrack.x-10,TreeTrack.y-6,TreeTrack.width+20,TreeTrack.height+12);
                if(e.button==0&&bar.Contains(m)&&max>0)
                {
                    // A press beside the thumb jumps it under the pointer, then drags from there.
                    if(!TreeThumb.Contains(m))treeScroll=treeScrollGoal=Mathf.Clamp01((m.y-thumb*.5f-TreeTrack.y)/travel)*max;
                    treeDrag=TreeDrag.Bar;
                }
                else if(TreeView.Contains(m)&&(e.button!=0||!overNode))treeDrag=TreeDrag.Pan;
                if(treeDrag!=TreeDrag.None){treeDragFrom=m.y;treeDragScroll=treeScroll;e.Use();}
            }
            else if(e.type==EventType.MouseDrag&&treeDrag!=TreeDrag.None)
            {
                float moved=e.mousePosition.y-treeDragFrom;
                treeScroll=treeScrollGoal=Mathf.Clamp(treeDrag==TreeDrag.Bar?treeDragScroll+moved*max/Mathf.Max(1,travel):treeDragScroll-moved,0,max);
                e.Use();
            }
            if(e.type==EventType.Repaint)
                treeScroll=Mathf.Abs(treeScrollGoal-treeScroll)<.5f?treeScrollGoal:Mathf.Lerp(treeScroll,treeScrollGoal,1-Mathf.Exp(-16*Time.unscaledDeltaTime));
            treeScroll=Mathf.Clamp(treeScroll,0,max);
        }
        /// <summary>Moves the view's goal down by <paramref name="pixels"/>, or up when negative; the view eases after it.</summary>
        internal void ScrollTreeBy(float pixels){treeScrollGoal=Mathf.Clamp(treeScrollGoal+pixels,0,TreeScrollMax);}
        /// <summary>Whether the node lies wholly inside the tree's view at its current scroll.</summary>
        internal bool TreeNodeShown(SkillNode node){var r=TreeNodeRect(node);return r.y>=TreeView.y&&r.yMax<=TreeView.yMax;}
        private Rect TreeThumb
        {
            get
            {
                float max=TreeScrollMax,h=Mathf.Max(48,TreeTrack.height*TreeView.height/TreeHeight);
                return new Rect(TreeTrack.x,TreeTrack.y+(max>0?(TreeTrack.height-h)*treeScroll/max:0),TreeTrack.width,h);
            }
        }
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
            // A faint star chart behind the tree.
            Halo(new Rect(TreeFrame.center.x-420,TreeFrame.y-60,840,640),new Color(.35f,.45f,.8f,.08f));
            Slot(new Rect(TreeFrame.x+22,TreeFrame.y+20,200,60),SkillPointColor,true);
            Icon(new Rect(TreeFrame.x+32,TreeFrame.y+30,40,40),UIIcon.Crystal);
            Label(new Rect(TreeFrame.x+78,TreeFrame.y+24,140,20),"스킬 포인트",13,muted);
            Title(new Rect(TreeFrame.x+78,TreeFrame.y+40,140,34),points.ToString("N0"),26,points>0?paper:muted,TextAnchor.MiddleLeft);
            float x=TreeFrame.x+250;
            for(var branch=SkillBranch.Arcane;branch<=SkillBranch.Underworld;branch++)
            {
                Color c=BranchColor(branch);Diamond(new Vector2(x+6,TreeFrame.y+50),12,c);
                Label(new Rect(x+18,TreeFrame.y+38,48,24),TowerSkillTree.BranchName(branch),13,Alpha(c,.9f),TextAnchor.MiddleLeft,true);x+=64;
            }
            Label(new Rect(x,TreeFrame.y+38,TreeFrame.xMax-24-x,24),"휠 · 드래그로 위아래 이동",13,muted,TextAnchor.MiddleRight);
            Divider(new Rect(TreeFrame.x+18,TreeView.y-9,TreeFrame.width-36,10),.55f);

            // A drag ends on release, or on the next press if the release fell outside the window.
            if(e.rawType==EventType.MouseUp||e.type==EventType.MouseDown)treeDrag=TreeDrag.None;
            SkillNode hovered=null;
            if(treeDrag==TreeDrag.None&&TreeView.Contains(e.mousePosition))
                foreach(var node in TowerSkillTree.Nodes)if(TreeNodeRect(node).Contains(e.mousePosition))hovered=node;
            ScrollTree(e,hovered!=null);
            if(GUI.enabled&&hovered!=null&&e.type==EventType.MouseDown&&e.button==0)
            {
                // A click selects a node; a double click also learns its next rank.
                treeSelection=hovered.Id;confirmTreeReset=false;
                if(e.clickCount>=2)LearnTreeNode(hovered);else Sound(chimeSound,.12f);
                e.Use();
            }
            // Links first, brighter once the lower node has a rank, cut by hand to the view since they are drawn turned.
            float top=TreeView.y+4,bottom=TreeView.yMax-4;
            foreach(var node in TowerSkillTree.Nodes)
            {
                if(node.IsRoot)continue;var parent=TowerSkillTree.Get(node.ParentId);Color c=BranchColor(node.Branch);
                Vector2 a=TreeCenter(parent),b=TreeCenter(node);if(!ClipLink(ref a,ref b,top,bottom))continue;
                bool learned=tree.Rank(node.Id)>0,open=tree.IsOpen(node);
                if(learned)TreeLine(a,b,7,Alpha(c,.18f));
                TreeLine(a,b,learned?3:2,learned?Alpha(Color.Lerp(c,Color.white,.2f),.95f):open?Alpha(c,.4f):new Color(.4f,.44f,.56f,.28f));
            }
            // Then the nodes over them, clipped to the view. Runes of nodes wholly inside are drawn after the clip
            // through the crisp text path; only those cut by the edge are drawn through the clip.
            var selected=TowerSkillTree.Get(treeSelection)??TowerSkillTree.Root;
            GUI.BeginClip(TreeView,-TreeView.position,Vector2.zero,false);scrollDepth++;
            foreach(var node in TowerSkillTree.Nodes)
            {
                var r=TreeNodeRect(node);if(r.yMax+44<TreeView.y||r.y-44>TreeView.yMax)continue;
                DrawTreeNode(node,r,tree.Rank(node.Id),tree.IsOpen(node),campaign.CanLearn(node),node==selected,node==hovered,r.y<TreeView.y||r.yMax>TreeView.yMax);
            }
            scrollDepth--;GUI.EndClip();
            foreach(var node in TowerSkillTree.Nodes)
                if(TreeNodeShown(node))TreeRune(node,TreeNodeRect(node),tree.Rank(node.Id)>0,tree.IsOpen(node));
            // Shadows where the view cuts the tree, and the bar on the right.
            float max=TreeScrollMax;
            if(treeScroll>.5f)Light(new Rect(TreeView.x,TreeView.y,TreeView.width,30),new Color(0,0,0,.5f*Mathf.Clamp01(treeScroll/40)));
            if(treeScroll<max-.5f)
            {
                EnsureUITextures();GUI.color=new Color(0,0,0,.5f*Mathf.Clamp01((max-treeScroll)/40));
                GUI.DrawTextureWithTexCoords(new Rect(TreeView.x,TreeView.yMax-30,TreeView.width,30),uiGradient,new Rect(0,1,1,-1));GUI.color=Color.white;
            }
            if(max>0)
            {
                bool active=treeDrag==TreeDrag.Bar||new Rect(TreeTrack.x-10,TreeTrack.y,TreeTrack.width+20,TreeTrack.height).Contains(e.mousePosition);
                Round(TreeTrack,new Color(0,0,0,.4f),3);Round(TreeThumb,Alpha(brass,active?.95f:.6f),3);
            }

            DrawTreeCard(hovered??selected,hovered==null);
            if(Button(new Rect(64,791,236,53),"돌아가기  [Esc]",muted)){villagePanel=VillagePanel.None;return;}
            Label(new Rect(326,791,800,53),"층을 넘길 때마다 "+TowerSkillTree.FloorPoints+" SP · 보스·엘리트 층 "+TowerSkillTree.BossFloorPoints+" SP · 원정에 실패해도 포인트는 남습니다",15,muted,TextAnchor.MiddleLeft);
            if(Button(new Rect(1240,791,296,53),confirmTreeReset?"정말 모두 되돌릴까요?":"모두 되돌리기",confirmTreeReset?crimson:muted,tree.Spent>0))
            {if(confirmTreeReset)ResetTree();else confirmTreeReset=true;}
        }

        private void DrawTreeNode(SkillNode node,Rect r,int rank,bool open,bool affordable,bool selected,bool hovered,bool rune=true)
        {
            Color c=BranchColor(node.Branch);bool learned=rank>0,maxed=rank>=node.MaxRank;
            float flash=node.Id==treeFlashId?Mathf.Clamp01(1-(Time.unscaledTime-treeFlashTime)/.6f):0;
            if(selected)Halo(new Rect(r.x-20,r.y-20,r.width+40,r.height+40),Alpha(c,.45f));
            else if(learned)Halo(new Rect(r.x-12,r.y-12,r.width+24,r.height+24),Alpha(c,.2f));
            if(flash>0)Halo(new Rect(r.x-26,r.y-26,r.width+52,r.height+52),Alpha(Color.Lerp(c,Color.white,.4f),.7f*flash));
            // Learnable now: a slow pulse of light behind the node.
            if(open&&!maxed&&affordable){float pulse=.18f+.18f*Mathf.Sin(Time.unscaledTime*4+node.Column);Halo(new Rect(r.x-16,r.y-16,r.width+32,r.height+32),Alpha(c,pulse));}
            // Round sockets for ordinary nodes, a larger diamond for keystones; both carved from the kit and tinted by branch.
            Color body=learned?Color.Lerp(c,Color.black,maxed?.25f:.42f):open?new Color(.13f,.15f,.22f):new Color(.07f,.08f,.11f);
            Color edge=learned?Color.Lerp(c,Color.white,.25f):open?Alpha(Color.Lerp(c,Color.white,.2f),affordable?.9f:.55f):new Color(.42f,.45f,.55f,.55f);
            if(selected||hovered)edge=Color.Lerp(edge,Color.white,selected?.6f:.35f);
            Rect shape=node.IsKeystone?Inset(r,-r.width*.2f):Inset(r,-r.width*.08f);
            GUI.color=body;var fill=Kit(node.IsKeystone?"key-fill":"node-disc");if(fill!=null)GUI.DrawTexture(shape,fill);
            GUI.color=edge;var ring=Kit(node.IsKeystone?"key-rim":"node-rim");if(ring!=null)GUI.DrawTexture(shape,ring);GUI.color=Color.white;
            if(rune)TreeRune(node,r,learned,open);
            if(node.MaxRank>1)
                for(int i=0;i<node.MaxRank;i++)
                {float px=r.center.x+(i-(node.MaxRank-1)*.5f)*8;Diamond(new Vector2(px,shape.yMax+3),6,i<rank?Color.Lerp(c,Color.white,.2f):new Color(.3f,.33f,.42f,.85f));}
        }
        private void TreeRune(SkillNode node,Rect r,bool learned,bool open)
        {
            Color c=BranchColor(node.Branch);
            Title(new Rect(r.x,r.y+1,r.width,r.height),NodeRune(node),node.IsKeystone?19:16,learned?new Color(1,.97f,.9f):open?Alpha(Color.Lerp(c,paper,.35f),.95f):Alpha(muted,.5f),TextAnchor.MiddleCenter);
        }

        private void DrawTreeCard(SkillNode node,bool interactive)
        {
            var tree=campaign.SkillTree;Color c=BranchColor(node.Branch);int rank=tree.Rank(node.Id);
            bool open=tree.IsOpen(node),maxed=rank>=node.MaxRank,affordable=campaign.SkillPoints>=node.Cost;
            Card(TreeCard,c,false);
            float x=TreeCard.x+26,w=TreeCard.width-52,y=TreeCard.y+24;
            Rect icon=new Rect(x+6,y+6,54,54);Halo(new Rect(icon.x-22,icon.y-22,98,98),Alpha(c,.3f));
            DrawTreeNode(node,icon,rank,open,false,false,false);
            FitTitle(new Rect(x+84,y+2,w-84,34),node.Name,25,18,paper,TextAnchor.MiddleLeft);
            Label(new Rect(x+84,y+38,w-84,24),TowerSkillTree.BranchName(node.Branch)+(node.IsKeystone?"  ·  핵심":"")+"  ·  "+TargetName(node),14,c);
            y+=86;Divider(new Rect(x,y,w,10),.7f,false,c);y+=18;
            Title(new Rect(x,y,w,26),"단계  "+rank+" / "+node.MaxRank,18,paper,TextAnchor.MiddleLeft);
            Label(new Rect(x,y,w,26),node.Cost+" SP / 단계",16,SkillPointColor,TextAnchor.MiddleRight,true);y+=36;
            Label(new Rect(x,y,w,24),rank>0?"현재   "+node.Effect(rank):"현재   배우지 않음",16,rank>0?paper:muted);y+=28;
            Label(new Rect(x,y,w,24),maxed?"최대 단계에 도달했습니다":(rank>0?"다음   ":"배우면   ")+node.Effect(rank+1),16,maxed?brass:TowerWorld.Teal);y+=34;
            var parent=TowerSkillTree.Get(node.ParentId);
            string state=maxed?"모든 단계를 배웠습니다":!open?"먼저 「"+parent.Name+"」을(를) 배워야 합니다":!affordable?"스킬 포인트가 "+(node.Cost-campaign.SkillPoints)+" 부족합니다":"노드를 더블클릭하거나 아래 버튼으로 배웁니다";
            FitLabel(new Rect(x,y,w,40),state,15,12,!maxed&&(!open||!affordable)?new Color(.95f,.45f,.42f):muted);y+=46;
            bool can=interactive&&campaign.CanLearn(node);
            if(Button(new Rect(x,y,w,50),maxed?"최대 단계":"배우기  ·  "+node.Cost+" SP",c,can,true))LearnTreeNode(node);
            y+=64;Divider(new Rect(x,y,w,10),.5f);y+=16;
            Title(new Rect(x,y,w,26),"적용 중인 효과",18,paper,TextAnchor.MiddleLeft);y+=32;
            var summary=tree.Summary();
            if(summary.Count==0){Label(new Rect(x,y,w,48),"아직 배운 노드가 없습니다.\n층을 넘기며 모은 포인트로 위쪽 불씨부터 배우세요.",14,muted);return;}
            Rect view=new Rect(x,y,w+12,TreeCard.yMax-24-y);
            treeSummaryScroll=BeginScroll(view,treeSummaryScroll,new Rect(0,0,w-6,Mathf.Max(view.height-2,summary.Count*25)));
            for(int i=0;i<summary.Count;i++){Diamond(new Vector2(6,i*25+12),7,Alpha(c,.85f));Label(new Rect(18,i*25,w-30,24),summary[i],15,paper,TextAnchor.MiddleLeft);}
            EndScroll();
        }
    }
}
