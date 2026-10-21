using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerGame
    {
        private sealed class DeathVisual { public Fighter actor; public float remaining=.62f; }
        private readonly List<DeathVisual> deathVisuals=new List<DeathVisual>();
        private TowerVfx vfx;
        private int comboIndex,pixelScale;
        private float playerHitRemaining,lastAttackAt=-10,nextAfterimage;
        internal float LastPlayerAttackStartedAt=-1,LastPlayerAttackContactAt=-1,LastPlayerAttackDuration;
        private void ConfigurePixelCamera()
        {
            pixelScale=Mathf.Max(1,Mathf.RoundToInt(Screen.height/(11.6f*32)));
            view.orthographicSize=Screen.height/(64f*pixelScale);
        }
        private Vector3 SnapCamera(Vector3 position)
        {float pixels=32*Mathf.Max(1,pixelScale);return new Vector3(Mathf.Round(position.x*pixels)/pixels,Mathf.Round(position.y*pixels)/pixels,position.z);}
        private void ResetPresentation()
        {
            foreach(var d in deathVisuals)if(d.actor.root)Destroy(d.actor.root);deathVisuals.Clear();
            if(vfx!=null)vfx.Clear();comboIndex=0;lastAttackAt=-10;nextAfterimage=0;
            LastPlayerAttackStartedAt=LastPlayerAttackContactAt=-1;LastPlayerAttackDuration=0;
        }
        private void StartPlayerAttack()
        {
            if(elapsed-lastAttackAt>1)comboIndex=0;
            float duration=Mathf.Clamp(PlayerAttackInterval*.85f,.18f,.42f);
            slashCooldown=PlayerAttackInterval;attackTimer=duration;pendingSlash=true;
            playerHitRemaining=duration-hero.art.AttackHitTime(duration);
            hero.art.PlayAttack(duration,comboIndex);comboIndex=(comboIndex+1)%3;lastAttackAt=elapsed;
            LastPlayerAttackStartedAt=elapsed;LastPlayerAttackDuration=duration;Sound(slashSound,.35f);
        }
        private void TickPlayerAttack(float dt)
        {
            if(attackTimer<=0)return;attackTimer=Mathf.Max(0,attackTimer-dt);
            if(pendingSlash&&attackTimer<=playerHitRemaining){pendingSlash=false;LastPlayerAttackContactAt=elapsed;PlayerSlash();}
        }
        private void BeginAllyAttack(Fighter actor,CompanionState state,Fighter target)
        {
            float duration=Mathf.Clamp(state.AttackInterval*.65f,.24f,.62f);
            actor.facing=target.pos.x>actor.pos.x?1:-1;actor.cooldown=state.AttackInterval;actor.attackPose=duration;
            actor.basicTarget=target;actor.basicPending=true;if(actor.grounded)actor.moveVelocity=0;
            actor.basicDelay=state.Definition.Role==CompanionRole.Vanguard?actor.art.AttackHitTime(duration):actor.art.AttackReleaseTime(duration);
            actor.art.PlayAttack(duration,actor.combo++%3);
        }
        private void ResolveAllyAttack(Fighter actor,CompanionState state,float dt)
        {
            if(!actor.basicPending)return;actor.basicDelay-=dt;if(actor.basicDelay>0)return;
            actor.basicPending=false;var target=actor.basicTarget;actor.basicTarget=null;
            if(!ValidAllyTarget(target,state)||Vector2.Distance(actor.pos,target.pos)>state.Range+.55f||(state.Definition.Role==CompanionRole.Vanguard&&Mathf.Abs(target.pos.y-actor.pos.y)>3.5f))return;
            Color color;ColorUtility.TryParseHtmlString("#"+state.Definition.TintHex,out color);
            if(state.Definition.Role==CompanionRole.Vanguard)
            {SlashArc(actor.pos+new Vector2(actor.facing,.8f),actor.facing,color);if((target.pos.x-actor.pos.x)*actor.facing>-.4f)StrikeEnemy(target,state.Damage,state,actor.facing*.22f);}
            else Fire(actor.pos+Vector2.up*.78f,target.pos+Vector2.up*.7f,11,state.Damage,false,color,0,state);
        }
        private void StartEnemyMotion(Fighter actor,float windup)
        {
            float ratio=actor.kind==1||actor.kind==3?actor.art.AttackReleaseTime(1):actor.art.AttackHitTime(1);
            float duration=windup/Mathf.Max(.1f,ratio);actor.attackPose=duration;actor.art.PlayAttack(duration,actor.combo++%2);
        }
        private void RetireEnemy(Fighter actor)
        {RemoveEnemyCombat(actor);actor.art.PlayDeath(.58f);deathVisuals.Add(new DeathVisual{actor=actor});}
        private void TickDeaths(float dt)
        {
            for(int i=deathVisuals.Count-1;i>=0;i--)
            {
                var d=deathVisuals[i];d.remaining-=dt;
                if(d.actor.root){d.actor.art.SetPose(false,false,false,d.actor.facing,dt);if(d.remaining<.2f)d.actor.art.Tint=new Color(1,1,1,Mathf.Clamp01(d.remaining/.2f));}
                if(d.remaining<=0||!d.actor.root){if(d.actor.root)Destroy(d.actor.root);deathVisuals.RemoveAt(i);}
            }
        }
        private void StyleLastShot(SkillEffect effect)
        {if(shots.Count==0)return;var shot=shots[shots.Count-1];shot.effect=effect;if(vfx!=null)vfx.Projectile(shot.root.transform,effect,shot.color);}
    }
}
