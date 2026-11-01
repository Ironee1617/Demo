using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerGame
    {
        private int formationFacing=1;
        private float formationTurnTime;
        internal bool CanTarget(Fighter enemy,HeroState owner)
        {
            // Shared by direct hits, projectiles and status areas: party members
            // are never eligible, even if a caller passes an ally with positive HP.
            return enemy!=null&&enemy.isEnemy&&enemy.hp>0;
        }
        internal static string FormationName(CompanionRole role)
        {return role==CompanionRole.Vanguard?"전위":role==CompanionRole.Archer?"중위":"후위";}
        internal const float FollowSpacing=1.05f;
        internal Vector2 FormationAnchor(CompanionState state)
        {return FollowSlot(hero.grounded?hero.pos:new Vector2(hero.pos.x,world.GroundAt(hero.pos.x)),CompanionIndex(state));}
        private int CompanionIndex(CompanionState state)
        {if(state==null)return -1;for(int i=0;i<run.Companions.Count;i++)if(ReferenceEquals(run.Companions[i],state))return i;return -1;}
        internal static bool LeadsFormation(CompanionState state)
        {return state!=null&&state.Definition.Role==CompanionRole.Vanguard;}
        // Companions stand one spacing apart on the support the hero stands on or lands on:
        // vanguards ahead of the hero's facing, everyone else behind, each side in party order.
        // A narrow ledge shrinks the spacing, and a side without room spills past the other side's members;
        // a vanguard spills only into free room behind and otherwise holds the front edge.
        // Every slot rests on the support's actual surface height at its own x, so sloped ledges keep followers on the slab.
        private Vector2 FollowSlot(Vector2 anchor,int index)
        {
            if(index<0)return anchor;
            float min=TowerWorld.Left+.6f,max=TowerWorld.Right-.6f;
            int support=AllySupport(anchor.y,anchor.x);
            if(support>=0){var ledge=world.Ledges[support];min=ledge.Left+.3f;max=ledge.Right-.3f;}
            float x0=Mathf.Clamp(anchor.x,min,max);int count=Mathf.Max(1,run.Companions.Count);
            float spacing=Mathf.Min(FollowSpacing,(max-min)/count);
            int ahead=spacing>0?Mathf.FloorToInt(((formationFacing>0?max-x0:x0-min)+.001f)/spacing):0;
            int behind=spacing>0?Mathf.FloorToInt(((formationFacing>0?x0-min:max-x0)+.001f)/spacing):0;
            bool leads=LeadsFormation(run.Companions[index]);int rank=0,leaders=0;
            for(int i=0;i<run.Companions.Count;i++)
            {bool other=LeadsFormation(run.Companions[i]);if(other)leaders++;if(i<index&&other==leads)rank++;}
            int rear=Mathf.Min(run.Companions.Count-leaders,behind);
            float offset=leads?rank>=ahead&&rank-ahead<behind-rear?-(rear+rank-ahead+1)*spacing:(rank+1)*spacing
                :rank<behind?-(rank+1)*spacing:(Mathf.Min(leaders,ahead)+rank-behind+1)*spacing;
            float slotX=Mathf.Clamp(x0+formationFacing*offset,min,max);
            return new Vector2(slotX,world.SurfaceHeight(support,slotX));
        }
        private Vector2 AllyLeaderDestination(float leaderSpeed)
        {
            if(hero.grounded)return hero.pos;
            // Predict the first support the leader will cross while descending.
            // Waiting for the leader to land loses a full jump on every step.
            Vector2 position=hero.pos;float velocity=hero.velocityY;bool grounded=false;int support=-1;
            int ignore=hero.dropTime>0?hero.dropSupport:-1;
            for(int i=0;i<100;i++)
            {
                float xOld=position.x,y=position.y;
                position.x=Mathf.Clamp(position.x+leaderSpeed*.02f,TowerWorld.Left+.6f,TowerWorld.Right-.6f);
                TowerPhysics.Step(world.CurrentLayout,xOld,position.x,ref y,ref velocity,ref grounded,ref support,.02f,ignore);
                position.y=y;
                if(!grounded)continue;
                if(support>=0)
                {var ledge=world.Ledges[support];position.x=Mathf.Clamp(position.x,ledge.Left+.3f,ledge.Right-.3f);position.y=ledge.HeightAt(position.x);}
                return position;
            }
            return new Vector2(position.x,world.GroundAt(position.x));
        }
        private void ResetAllyFormation()
        {
            formationFacing=1;formationTurnTime=0;
            foreach(var actor in allies)
            {
                actor.combatTarget=actor.basicTarget=null;actor.basicPending=false;actor.returningToFormation=actor.followMoving=false;
                actor.moveVelocity=actor.velocityY=actor.attackPose=actor.castRemaining=0;actor.pos=FormationAnchor(run.GetCompanion(actor.id));
                actor.followGroundY=actor.pos.y;actor.followGroundX=actor.pos.x;actor.followSource=actor.followGoal=actor.followNext=-2;actor.dropTime=0;actor.facing=formationFacing;actor.grounded=true;
                actor.support=world.SupportAt(actor.pos.x,actor.pos.y);actor.lastX=actor.pos.x;actor.root.transform.position=actor.pos;actor.art.InterruptAction();
            }
        }
        private bool ValidAllyTarget(Fighter target,CompanionState state)
        {return CanTarget(target,state)&&target.root&&enemies.Contains(target);}
        /// <summary>Platform whose surface passes through (x, height); -1 for the ground or no support.</summary>
        private int AllySupport(float height,float x){return world.SupportAt(x,height,.12f);}
        private bool ReachableAllySupport(int source,int destination,float maxSpeed)
        {
            if(destination<0)return true;
            var landing=world.Ledges[destination];
            float launchY,gap=0;
            if(source<0)launchY=world.GroundAt(Mathf.Clamp(landing.x,landing.Left-2,landing.Right+2));
            else
            {
                var launch=world.Ledges[source];
                launchY=launch.HeightAt(Mathf.Clamp(landing.x,launch.Left,launch.Right));
                gap=Mathf.Max(0,Mathf.Abs(landing.x-launch.x)-(landing.width+launch.width)*.5f+.5f);
            }
            // The landing edge nearest the launch decides the rise a follower has to clear.
            float landingY=landing.HeightAt(Mathf.Clamp(source<0?landing.x:world.Ledges[source].x,landing.Left,landing.Right));
            float rise=landingY-launchY;
            if(rise>2.1f)return false;
            float flight=(10.7f+Mathf.Sqrt(Mathf.Max(0,10.7f*10.7f-50*rise)))/25;
            return gap<=maxSpeed*flight-maxSpeed*maxSpeed/60-.3f;
        }
        private int NextAllySupport(int source,int goal,float speed)
        {
            int ground=world.Ledges.Length,start=source<0?ground:source,finish=goal<0?ground:goal;
            var previous=new int[ground+1];var queue=new int[ground+1];
            for(int i=0;i<previous.Length;i++)previous[i]=-2;
            previous[start]=-1;int head=0,tail=0;queue[tail++]=start;
            while(head<tail&&previous[finish]==-2)
            {
                int from=queue[head++];
                for(int to=0;to<=ground;to++)
                {
                    if(previous[to]!=-2||!ReachableAllySupport(from==ground?-1:from,to==ground?-1:to,speed))continue;
                    previous[to]=from;queue[tail++]=to;
                }
            }
            if(previous[finish]==-2)return -1;
            int next=finish;while(previous[next]>=0&&previous[next]!=start)next=previous[next];
            return next==ground?-1:next;
        }
        private Vector2 AllyFollowDestination(Fighter actor,Vector2 anchor,float maxSpeed,out bool jump,bool followLeader=true)
        {
            jump=false;
            int source=AllySupport(actor.followGroundY,actor.followGroundX),goal=AllySupport(anchor.y,anchor.x);
            if(actor.grounded)
            {
                if(source==goal)
                {
                    jump=followLeader&&!hero.grounded&&hero.velocityY>0&&hero.pos.y>=actor.pos.y&&Mathf.Abs(hero.pos.x-actor.pos.x)<1+FollowSpacing*TowerRun.MaxCompanions;
                    actor.followSource=source;actor.followGoal=goal;actor.followNext=-2;return anchor;
                }
                if(actor.followSource!=source||actor.followGoal!=goal)
                {actor.followSource=source;actor.followGoal=goal;actor.followNext=NextAllySupport(source,goal,maxSpeed);}
            }
            else if(actor.followNext==-2)return new Vector2(anchor.x,actor.followGroundY);
            int next=actor.followNext;
            if(next<0)return new Vector2(anchor.x,world.GroundAt(anchor.x));
            var landing=world.Ledges[next];
            float landingX=Mathf.Clamp(actor.pos.x,landing.Left+.25f,landing.Right-.25f),launchX=landingX;
            if(source>=0){var launch=world.Ledges[source];launchX=Mathf.Clamp(landingX,launch.Left+.25f,launch.Right-.25f);}
            if(actor.grounded&&Mathf.Abs(actor.pos.x-launchX)>.2f)return new Vector2(launchX,world.SurfaceHeight(source,launchX));
            jump=actor.grounded;
            return new Vector2(landingX,landing.HeightAt(landingX));
        }
        private Vector2 AllyCombatAnchor(Fighter actor,Fighter target)
        {
            Vector2 aim=target.pos;EnemyState flight;
            if(target.airborne&&enemyCombat.TryGetValue(target,out flight))aim.y=flight.hoverHeight;
            Vector2 best=new Vector2(aim.x,world.GroundAt(aim.x));
            float score=(best-aim).sqrMagnitude;
            foreach(var ledge in world.Ledges)
            {
                if(ledge.y>aim.y+.35f)continue;
                float x=Mathf.Clamp(aim.x,ledge.Left+.3f,ledge.Right-.3f);
                var candidate=new Vector2(x,ledge.HeightAt(x));
                float next=(candidate-aim).sqrMagnitude+Mathf.Abs(x-actor.pos.x)*.02f;
                if(next>=score)continue;
                score=next;best=candidate;
            }
            return best;
        }
        // Followers plant their feet harder than they push off.
        private const float AllyAcceleration=30,AllyBraking=45;
        private void MoveAlly(Fighter actor,float destination,float maxSpeed,float leaderSpeed,float dt)
        {
            float dx=destination-actor.pos.x;
            float desired=Mathf.Clamp(dx*6+leaderSpeed,-maxSpeed,maxSpeed);
            // Never close in faster than the distance left after this step can brake to a stop.
            // A running follower trails its slot by that braking distance, so it eases in when the hero halts.
            if(desired*dx>0)
            {float reach=AllyBraking*dt;desired=Mathf.Sign(dx)*Mathf.Min(Mathf.Abs(desired),Mathf.Sqrt(reach*reach+2*AllyBraking*Mathf.Abs(dx))-reach);}
            bool braking=actor.moveVelocity*desired<0||Mathf.Abs(desired)<Mathf.Abs(actor.moveVelocity);
            actor.moveVelocity=Mathf.MoveTowards(actor.moveVelocity,desired,(braking?AllyBraking:AllyAcceleration)*dt);
            float step=actor.moveVelocity*dt;
            if(step*dx>0&&Mathf.Abs(step)>Mathf.Abs(dx))
            {step=dx;actor.moveVelocity=leaderSpeed;}
            actor.pos.x=Mathf.Clamp(actor.pos.x+step,TowerWorld.Left+.5f,TowerWorld.Right-.5f);
        }
        private void TickAllies(float dt)
        {
            healClock+=dt;
            float leaderSpeed=dt>0?(hero.pos.x-previousHeroPosition.x)/dt:0;
            if(Mathf.Abs(leaderSpeed)>.1f&&(leaderSpeed>0?1:-1)!=formationFacing)
            {formationTurnTime+=dt;if(formationTurnTime>=.14f){formationFacing=-formationFacing;formationTurnTime=0;}}
            else formationTurnTime=0;
            Vector2 leaderAnchor=AllyLeaderDestination(leaderSpeed);
            foreach(var actor in allies)
            {
                var state=run.GetCompanion(actor.id);if(state==null)continue;
                if(actor.grounded){actor.followGroundY=actor.pos.y;actor.followGroundX=actor.pos.x;}
                actor.cooldown-=dt;actor.attackPose=Mathf.Max(0,actor.attackPose-dt);actor.castRemaining=Mathf.Max(0,actor.castRemaining-dt);
                ResolveAllyAttack(actor,state,dt);
                if(actor.combatTarget!=null&&!ValidAllyTarget(actor.combatTarget,state))
                {
                    actor.combatTarget=actor.basicTarget=null;actor.basicPending=false;actor.returningToFormation=true;
                    if(actor.grounded){actor.moveVelocity=0;actor.followMoving=false;actor.followSource=actor.followGoal=actor.followNext=-2;}
                }
                Vector2 anchor=FollowSlot(leaderAnchor,CompanionIndex(state));
                if(actor.returningToFormation&&actor.grounded&&hero.grounded&&Vector2.Distance(anchor,actor.pos)<1.2f)actor.returningToFormation=false;
                if(actor.combatTarget==null)
                {
                    actor.combatTarget=ClosestEnemy(actor.pos,state.Range-.05f,state);
                    if(actor.combatTarget==null&&!actor.returningToFormation)actor.combatTarget=ClosestEnemy(actor.pos,13,state);
                    if(actor.combatTarget!=null)actor.returningToFormation=false;
                }
                var target=actor.combatTarget;
                bool followJump=false;
                bool followingRoute=target==null&&((!actor.grounded&&actor.followNext>=0)||AllySupport(actor.followGroundY,actor.followGroundX)!=AllySupport(anchor.y,anchor.x));
                Vector2 destination=target==null?AllyFollowDestination(actor,anchor,state.MoveSpeed+PlayerMoveSpeed,out followJump):anchor;
                bool inRange=target!=null&&Vector2.Distance(actor.pos,target.pos)<state.Range-.05f;
                if(target!=null)
                {
                    destination=actor.pos;
                    if(!inRange)
                    {
                        float height=target.pos.y-actor.pos.y,reach=Mathf.Max(.4f,state.Range-.2f);
                        float standOff=Mathf.Sqrt(Mathf.Max(.16f,reach*reach-height*height));
                        destination=new Vector2(target.pos.x-(target.pos.x>=actor.pos.x?1:-1)*standOff,target.pos.y);
                        if(state.Definition.Role==CompanionRole.Vanguard||Mathf.Abs(height)>reach)
                        {
                            Vector2 perch=AllyCombatAnchor(actor,target);
                            int source=AllySupport(actor.followGroundY,actor.followGroundX),goal=AllySupport(perch.y,perch.x);
                            if(source!=goal||!actor.grounded&&actor.followNext>=0)
                            {
                                destination=AllyFollowDestination(actor,perch,state.MoveSpeed,out followJump,false);
                                followingRoute=true;
                            }
                        }
                    }
                }
                // Finish the current jump even if an attack starts in flight.
                // Cancelling horizontal steering here strands followers below ledges.
                if(!actor.grounded&&actor.followNext>=0)
                {
                    var landing=world.Ledges[actor.followNext];
                    float landingX=Mathf.Clamp(actor.pos.x,landing.Left+.3f,landing.Right-.3f);
                    destination=new Vector2(landingX,landing.HeightAt(landingX));
                    followingRoute=true;
                }
                float beforeX=actor.pos.x,dx=destination.x-actor.pos.x;
                bool actionLocked=actor.basicPending||actor.castRemaining>0||actor.attackPose>0;
                if((actionLocked||inRange)&&actor.grounded){actor.moveVelocity=0;actor.followMoving=false;actor.followSource=actor.followGoal=actor.followNext=-2;}
                else
                {
                    // A standing leader lets followers close small gaps so they settle exactly in their slots;
                    // they stop only once slowed to a shuffle, so an arrival never freezes mid-stride.
                    if(Mathf.Abs(dx)>.45f||Mathf.Abs(dx)>.2f&&Mathf.Abs(leaderSpeed)<.1f)actor.followMoving=true;
                    else if(Mathf.Abs(dx)<.12f&&Mathf.Abs(leaderSpeed)<.1f&&Mathf.Abs(actor.moveVelocity)<1)actor.followMoving=false;
                    if(target!=null||followingRoute)actor.followMoving=Mathf.Abs(dx)>.12f;
                    if(actor.followMoving)
                    {
                        float speed=state.MoveSpeed+(target==null||followingRoute?Mathf.Min(Mathf.Abs(dx)*2,PlayerMoveSpeed):0);
                        // A vanguard's slot runs ahead of the hero, so it keeps at least the hero's walking pace.
                        if(target==null&&!followingRoute&&LeadsFormation(state))speed=Mathf.Max(speed,Mathf.Min(Mathf.Abs(leaderSpeed),PlayerMoveSpeed)+Mathf.Min(Mathf.Abs(dx)*2,1));
                        MoveAlly(actor,destination.x,speed,target==null&&!followingRoute?leaderSpeed:0,dt);
                    }
                    else actor.moveVelocity=0;
                }
                if(actor.grounded&&!actionLocked&&!inRange)
                {
                    bool jump=followingRoute||target==null?followJump:!inRange&&target.pos.y-actor.pos.y>.65f&&Mathf.Abs(dx)<5;
                    if(jump){actor.velocityY=10.7f;actor.grounded=false;}
                }
                float actualSpeed=dt>0?(actor.pos.x-beforeX)/dt:0;bool moving=Mathf.Abs(actualSpeed)>.08f;
                if(!actionLocked)actor.facing=target!=null?target.pos.x>=actor.pos.x?1:-1:moving?actualSpeed>0?1:-1:formationFacing;
                bool drop=!actionLocked&&!inRange&&destination.y<actor.pos.y-.65f&&Mathf.Abs(dx)<4;
                StepGravity(actor,dt,drop);actor.root.transform.position=actor.pos;
                if(target!=null&&Vector2.Distance(actor.pos,target.pos)<state.Range&&
                    (state.Definition.Role!=CompanionRole.Vanguard||Mathf.Abs(target.pos.y-actor.pos.y)<3.5f)&&
                    actor.cooldown<=0&&!actor.basicPending&&actor.castRemaining<=0&&actor.attackPose<=0)
                    BeginAllyAttack(actor,state,target);
                actor.art.SetLocomotion(actor.grounded,actor.velocityY,false,Mathf.Abs(actualSpeed),actualSpeed);
                actor.art.SetPose(moving,actor.attackPose>0,false,actor.facing,dt);
                if(healClock>=5&&state.HealAmount>0)CompanionHeal(state);
            }
            if(healClock>=5)healClock=0;
        }
    }
}
