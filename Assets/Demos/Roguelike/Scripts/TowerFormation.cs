using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerGame
    {
        internal bool HasRangedCompanion
        {get{foreach(var companion in run.Companions)if(CanHitAirborne(companion))return true;return false;}}
        internal static bool CanHitAirborne(HeroState owner)
        {var companion=owner as CompanionState;return companion!=null&&companion.Definition.Role!=CompanionRole.Vanguard;}
        internal bool CanTarget(Fighter enemy,HeroState owner)
        {return enemy!=null&&enemy.hp>0&&(!enemy.airborne||CanHitAirborne(owner));}
        internal static string FormationName(CompanionRole role)
        {return role==CompanionRole.Vanguard?"전위":role==CompanionRole.Archer?"중위":"후위";}
        internal float FormationOffset(CompanionState state)
        {
            int slot=0;string line=FormationName(state.Definition.Role);
            foreach(var companion in run.Companions)
            {if(ReferenceEquals(companion,state))break;if(FormationName(companion.Definition.Role)==line)slot++;}
            int middle=0;foreach(var companion in run.Companions)if(FormationName(companion.Definition.Role)=="중위")middle++;
            return line=="전위"?1.6f+slot*1.3f:line=="중위"?-1.4f-slot*1.3f:-2.9f-Mathf.Max(0,middle-1)*1.3f-slot*1.5f;
        }
        internal Vector2 FormationAnchor(CompanionState state)
        {return new Vector2(Mathf.Clamp(hero.pos.x+hero.facing*FormationOffset(state),TowerWorld.Left+.5f,TowerWorld.Right-.5f),hero.pos.y);}
        private void LandUnsupportedFlyers()
        {
            if(HasRangedCompanion)return;bool landed=false;
            foreach(var enemy in enemies)if(enemy.airborne){enemy.airborne=false;enemy.velocityY=0;landed=true;}
            if(landed)Notice("원거리 동료가 없어 공중 수호자가 착지합니다",4);
        }
        private void TickAllies(float dt)
        {
            healClock+=dt;
            foreach(var actor in allies)
            {
                var state=run.GetCompanion(actor.id);if(state==null)continue;
                actor.cooldown-=dt;actor.attackPose=Mathf.Max(0,actor.attackPose-dt);actor.castRemaining=Mathf.Max(0,actor.castRemaining-dt);
                ResolveAllyAttack(actor,state,dt);
                var target=ClosestEnemy(actor.pos,13,state);
                Vector2 anchor=FormationAnchor(state);float dx=anchor.x-actor.pos.x;
                bool moving=Mathf.Abs(dx)>.12f;
                float followSpeed=state.MoveSpeed+Mathf.Min(Mathf.Abs(dx)*2,PlayerMoveSpeed);
                if(moving)actor.pos.x=Mathf.MoveTowards(actor.pos.x,anchor.x,followSpeed*dt);
                if(Mathf.Abs(hero.pos.x-actor.pos.x)>15){actor.pos=anchor;actor.velocityY=0;}
                if(actor.grounded&&hero.pos.y-actor.pos.y>.65f&&Mathf.Abs(dx)<5)actor.velocityY=10.7f;
                if(!actor.basicPending&&actor.castRemaining<=0)actor.facing=target!=null?target.pos.x>=actor.pos.x?1:-1:hero.facing;
                StepGravity(actor,dt);actor.root.transform.position=actor.pos;
                if(target!=null&&Vector2.Distance(actor.pos,target.pos)<state.Range&&
                    (target.airborne&&CanHitAirborne(state)||Mathf.Abs(target.pos.y-actor.pos.y)<3.5f)&&
                    actor.cooldown<=0&&!actor.basicPending&&actor.castRemaining<=0)
                    BeginAllyAttack(actor,state,target);
                actor.art.SetLocomotion(actor.grounded,actor.velocityY,false,moving?state.MoveSpeed:0);
                actor.art.SetPose(moving,actor.attackPose>0,false,actor.facing,dt);
                if(healClock>=5&&state.HealAmount>0&&hero.hp<PlayerMaxHealth)HealPlayer(state.HealAmount,true);
            }
            if(healClock>=5)healClock=0;
        }
    }
}
