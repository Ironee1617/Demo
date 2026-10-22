using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerGame
    {
        private sealed class SkillClock
        {
            public HeroState owner; public SkillState skill; public float remaining=.25f,lastCast=-1,lastStarted=-1,castDuration; public int casts; public bool pending;
        }
        private sealed class PendingSkill { public SkillClock clock; public Fighter source,target; public float remaining; }
        private readonly List<PendingSkill> pendingSkills=new List<PendingSkill>();
        private sealed class SkillArea
        {
            public HeroState owner; public Fighter source; public SkillEffect effect; public GameObject root;
            public Vector2 center; public Color color; public float radius,remaining,interval,tick,damage; public int count;
        }
        private readonly Dictionary<SkillState,SkillClock> skillClocks=new Dictionary<SkillState,SkillClock>();
        private readonly List<SkillArea> skillAreas=new List<SkillArea>();
        private float playerShield,shieldTime,regenerationClock;
        internal float PlayerShield { get { return playerShield; } }
        internal float SkillRemaining(string ownerId,string skillId)
        {
            var owner=run.GetHero(ownerId);var skill=owner==null?null:owner.GetSkill(skillId);SkillClock clock;
            return skill!=null&&skillClocks.TryGetValue(skill,out clock)?clock.remaining:0;
        }
        internal int SkillCasts(string ownerId,string skillId)
        {
            var owner=run.GetHero(ownerId);var skill=owner==null?null:owner.GetSkill(skillId);SkillClock clock;
            return skill!=null&&skillClocks.TryGetValue(skill,out clock)?clock.casts:0;
        }
        internal float SkillLastCastAt(string ownerId,string skillId)
        {
            var owner=run.GetHero(ownerId);var skill=owner==null?null:owner.GetSkill(skillId);SkillClock clock;
            return skill!=null&&skillClocks.TryGetValue(skill,out clock)?clock.lastCast:-1;
        }
        internal float SkillLastStartedAt(string ownerId,string skillId)
        {var owner=run.GetHero(ownerId);var skill=owner==null?null:owner.GetSkill(skillId);SkillClock clock;return skill!=null&&skillClocks.TryGetValue(skill,out clock)?clock.lastStarted:-1;}
        internal float SkillCastDuration(string ownerId,string skillId)
        {var owner=run.GetHero(ownerId);var skill=owner==null?null:owner.GetSkill(skillId);SkillClock clock;return skill!=null&&skillClocks.TryGetValue(skill,out clock)?clock.castDuration:0;}
        internal int OwnerProjectiles(string id){return shots.FindAll(s=>s.owner!=null&&s.owner.Id==id).Count;}
        internal void ReconcileLoadout()
        {
            float gain=Mathf.Max(0,PlayerMaxHealth-hero.maxHp);hero.maxHp=PlayerMaxHealth;hero.hp=Mathf.Clamp(hero.hp+gain,0,PlayerMaxHealth);
            SyncAllies();
            foreach(var skill in new List<SkillState>(skillClocks.Keys))
                if(!IsCurrentHero(skillClocks[skill].owner))skillClocks.Remove(skill);
            for(int i=shots.Count-1;i>=0;i--)if(shots[i].owner!=null&&!IsCurrentHero(shots[i].owner)){Destroy(shots[i].root);shots.RemoveAt(i);}
            for(int i=skillAreas.Count-1;i>=0;i--)if(!IsCurrentHero(skillAreas[i].owner)){Destroy(skillAreas[i].root);skillAreas.RemoveAt(i);}
            for(int i=pendingSkills.Count-1;i>=0;i--)if(!IsCurrentHero(pendingSkills[i].clock.owner))pendingSkills.RemoveAt(i);
            EnsureSkillClocks(run.Player);
            foreach(var c in run.Companions)EnsureSkillClocks(c);
        }
        private bool IsCurrentHero(HeroState state){return state!=null&&ReferenceEquals(run.GetHero(state.Id),state);}
        private void EnsureSkillClocks(HeroState state)
        {foreach(var s in state.Skills)if(!skillClocks.ContainsKey(s))skillClocks.Add(s,new SkillClock{owner=state,skill=s});}
        private void ResetSkills()
        {skillClocks.Clear();ClearFloorSkillEffects();if(hero!=null)hero.castRemaining=0;foreach(var actor in allies)actor.castRemaining=0;playerShield=0;shieldTime=0;regenerationClock=0;}
        private void ClearFloorSkillEffects()
        {foreach(var area in skillAreas)if(area.root)Destroy(area.root);skillAreas.Clear();CancelPendingSkills();if(vfx!=null)vfx.Clear();}
        private void CancelPendingSkills()
        {foreach(var pending in pendingSkills){pending.clock.pending=false;if(pending.source!=null)pending.source.castRemaining=0;}pendingSkills.Clear();}
        private void CancelPlayerCast()
        {for(int i=pendingSkills.Count-1;i>=0;i--)if(pendingSkills[i].source==hero){pendingSkills[i].clock.pending=false;pendingSkills.RemoveAt(i);}hero.castRemaining=0;hero.attackPose=0;}
        private Fighter ActorFor(string id){return id=="player"?hero:allies.Find(a=>a.id==id);}
        private Fighter ClosestEnemy(Vector2 pos,float range=13,HeroState owner=null)
        {Fighter best=null;foreach(var e in enemies){float d=Vector2.Distance(pos,e.pos);if(CanTarget(e,owner)&&d<range){range=d;best=e;}}return best;}
        private void TickSkills(float dt)
        {
            shieldTime=Mathf.Max(0,shieldTime-dt);if(shieldTime<=0)playerShield=0;
            regenerationClock+=dt;
            if(regenerationClock>=1){regenerationClock-=1;if(run.Player.Regeneration>0)HealPlayer(run.Player.Regeneration,false);}
            for(int i=pendingSkills.Count-1;i>=0;i--)if(!IsCurrentHero(pendingSkills[i].clock.owner))pendingSkills.RemoveAt(i);
            EnsureSkillClocks(run.Player);foreach(var c in run.Companions)EnsureSkillClocks(c);
            TickPendingSkills(dt);
            foreach(var clock in skillClocks.Values)
            {
                clock.remaining=Mathf.Max(0,clock.remaining-dt);
                if(clock.remaining>0||clock.pending||!IsCurrentHero(clock.owner))continue;
                var source=ActorFor(clock.owner.Id);if(source==null||source.basicPending||source.castRemaining>0||(source==hero&&(attackTimer>0||dashTime>0)))continue;
                var effect=clock.skill.Definition.Effect;
                var target=source==hero?ClosestEnemy(source.pos,13,clock.owner):source.combatTarget;
                if(source!=hero&&!ValidAllyTarget(target,(CompanionState)clock.owner))target=null;
                if(target==null&&(effect==SkillEffect.HealPulse||effect==SkillEffect.ShieldPulse))target=enemies.Find(enemy=>enemy.hp>0);
                if(target==null)continue;float distance=Vector2.Distance(source.pos,target.pos);
                if((effect==SkillEffect.IceNova||effect==SkillEffect.BladeStorm)&&distance>clock.skill.Radius)continue;
                if((effect==SkillEffect.ChainLightning||effect==SkillEffect.LifeDrain)&&distance>clock.skill.Radius)continue;
                if((effect==SkillEffect.Sentry||effect==SkillEffect.MultiShot||effect==SkillEffect.PierceShot||effect==SkillEffect.ArcaneVolley||effect==SkillEffect.Shockwave)&&distance>clock.skill.Radius)continue;
                if(effect==SkillEffect.HealPulse&&hero.hp>=PlayerMaxHealth)continue;
                if((effect==SkillEffect.HealPulse||effect==SkillEffect.ShieldPulse)&&Vector2.Distance(source.pos,hero.pos)>clock.skill.Radius)continue;
                if(clock.skill.Definition.Effect==SkillEffect.ShieldPulse&&playerShield>=PlayerMaxHealth*.35f)continue;
                BeginSkillCast(clock,source,target);
            }
            TickSkillAreas(dt);
        }
        private void BeginSkillCast(SkillClock clock,Fighter source,Fighter target)
        {
            var effect=clock.skill.Definition.Effect;
            float duration=effect==SkillEffect.Meteor?.72f:effect==SkillEffect.DashStrike?.34f:effect==SkillEffect.ShieldPulse||effect==SkillEffect.HealPulse?.55f:.46f;
            source.facing=target.pos.x>=source.pos.x?1:-1;source.castRemaining=duration;source.attackPose=duration;
            source.art.PlayCast(effect,duration);clock.remaining=clock.skill.Cooldown;clock.pending=true;clock.lastStarted=elapsed;clock.castDuration=duration;
            pendingSkills.Add(new PendingSkill{clock=clock,source=source,target=target,remaining=source.art.CastReleaseTime(effect,duration)});
            if(vfx!=null)vfx.Cast(effect,source.pos+Vector2.up,Mathf.Min(1.3f,clock.skill.Radius*.3f),SkillColor(effect));
        }
        private void TickPendingSkills(float dt)
        {
            for(int i=pendingSkills.Count-1;i>=0;i--)
            {
                var pending=pendingSkills[i];pending.remaining-=dt;
                if(!IsCurrentHero(pending.clock.owner)||!pending.source.root){pending.clock.pending=false;pendingSkills.RemoveAt(i);continue;}
                if(pending.remaining>0)continue;
                bool support=pending.clock.skill.Definition.Effect==SkillEffect.HealPulse||pending.clock.skill.Definition.Effect==SkillEffect.ShieldPulse;
                var target=pending.target!=null&&pending.target.hp>0&&(support||CanTarget(pending.target,pending.clock.owner))?pending.target:pending.source==hero?ClosestEnemy(pending.source.pos,13,pending.clock.owner):pending.source.combatTarget;
                if(pending.source!=hero&&!support&&!ValidAllyTarget(target,(CompanionState)pending.clock.owner))target=null;
                if(target!=null&&hero.hp>0)
                {CastSkill(pending.clock.owner,pending.clock.skill,pending.source,target);pending.clock.casts++;pending.clock.lastCast=elapsed;}
                pending.clock.pending=false;pendingSkills.RemoveAt(i);
            }
        }
        private Color SkillColor(SkillEffect effect)
        {
            switch(effect)
            {
                case SkillEffect.Fireball:case SkillEffect.Meteor:return new Color(1,.4f,.15f);
                case SkillEffect.IceNova:return new Color(.45f,.88f,1);
                case SkillEffect.PoisonCloud:return new Color(.58f,.9f,.32f);
                case SkillEffect.HealPulse:case SkillEffect.LifeDrain:return new Color(.4f,1,.7f);
                case SkillEffect.ShieldPulse:return new Color(.9f,.83f,.45f);
                case SkillEffect.ChainLightning:case SkillEffect.ArcaneVolley:return new Color(.66f,.6f,1);
                default:return TowerWorld.Gold;
            }
        }
        private void CastSkill(HeroState owner,SkillState skill,Fighter source,Fighter target)
        {
            var d=skill.Definition;var color=SkillColor(d.Effect);float damage=owner.Damage*skill.Power;
            Vector2 origin=source.pos+Vector2.up*(source==hero?.95f:.78f),aim=target.pos+Vector2.up*.7f;
            source.attackPose=.4f;source.facing=target.pos.x>=source.pos.x?1:-1;
            if(source.castRemaining<=0&&vfx!=null)vfx.Cast(d.Effect,origin,Mathf.Min(1.3f,skill.Radius*.3f),color);
            FloatText(source.pos+Vector2.up*2.3f,d.Name,color);Sound(chimeSound,.2f);
            switch(d.Effect)
            {
                case SkillEffect.Fireball:
                    for(int i=0;i<skill.Count;i++){Fire(origin,aim+new Vector2(0,(i-(skill.Count-1)*.5f)*.2f),9,damage,false,color,Mathf.Max(.7f,skill.Radius),owner);StyleLastShot(d.Effect);}
                    break;
                case SkillEffect.ArcaneVolley:
                    var eligible=enemies.FindAll(e=>CanTarget(e,owner));
                    for(int i=0;i<skill.Count;i++){var e=eligible.Count>0?eligible[i%eligible.Count]:target;Fire(origin,e.pos+Vector2.up+new Vector2(0,(i%3-1)*.15f),13,damage,false,color,0,owner);StyleLastShot(d.Effect);LimitLastShotRange(skill.Radius,13);}break;
                case SkillEffect.MultiShot:
                    Vector2 v=(aim-origin).normalized;
                    for(int i=0;i<skill.Count;i++){Vector2 dir=Quaternion.Euler(0,0,(i-(skill.Count-1)*.5f)*9)*v;Fire(origin,origin+dir*10,13,damage,false,color,0,owner);StyleLastShot(d.Effect);LimitLastShotRange(skill.Radius,13);}break;
                case SkillEffect.PierceShot:
                    Fire(origin,aim,16,damage,false,color,0,owner,Mathf.Max(2,skill.Count));StyleLastShot(d.Effect);LimitLastShotRange(skill.Radius,16);break;
                case SkillEffect.Shockwave:
                    Fire(origin,aim,9,damage,false,color,0,owner,Mathf.Max(3,skill.Count),Mathf.Max(.6f,skill.Duration));StyleLastShot(d.Effect);LimitLastShotRange(skill.Radius,9);
                    if(vfx!=null)vfx.Impact(d.Effect,origin,skill.Radius,color);break;
                case SkillEffect.IceNova:
                    foreach(var e in enemies.ToArray())if(CanTarget(e,owner)&&Vector2.Distance(e.pos,source.pos)<skill.Radius)
                    {e.slowTime=Mathf.Max(e.slowTime,skill.Duration);StrikeEnemy(e,damage,owner,Mathf.Sign(e.pos.x-source.pos.x)*.2f);}
                    if(vfx!=null)vfx.Impact(d.Effect,origin,skill.Radius,color);break;
                case SkillEffect.ChainLightning:
                    var targets=enemies.FindAll(e=>CanTarget(e,owner));targets.Sort((a,b)=>Vector2.Distance(a.pos,source.pos).CompareTo(Vector2.Distance(b.pos,source.pos)));
                    Vector2 previous=origin;
                    for(int i=0;i<Mathf.Min(skill.Count,targets.Count);i++)
                    {var e=targets[i];if(Vector2.Distance(e.pos,source.pos)>skill.Radius)continue;if(vfx!=null)vfx.Trail(previous,e.pos+Vector2.up,d.Effect,color);StrikeEnemy(e,damage*Mathf.Pow(.88f,i),owner,0);previous=e.pos+Vector2.up;}
                    break;
                case SkillEffect.Meteor:
                    for(int i=0;i<skill.Count;i++)AddArea(owner,source,d.Effect,target.pos+new Vector2((i-(skill.Count-1)*.5f)*1.2f,0),skill.Radius,.7f,.7f,damage,color,1);break;
                case SkillEffect.RainArrows:
                    for(int i=0;i<skill.Count;i++)
                    {Vector2 landing=aim+new Vector2((i-(skill.Count-1)*.5f)*.55f,0);Fire(landing+new Vector2(-1,6),landing,12,damage,false,color,Mathf.Max(.2f,skill.Radius),owner);StyleLastShot(d.Effect);}break;
                case SkillEffect.BladeStorm:
                    AddArea(owner,source,d.Effect,source.pos,skill.Radius,skill.Duration,Mathf.Max(.1f,skill.Duration/Mathf.Max(1,skill.Count)),damage,color,skill.Count);break;
                case SkillEffect.PoisonCloud:
                    AddArea(owner,source,d.Effect,target.pos,skill.Radius,skill.Duration,.7f,damage,color,skill.Count);break;
                case SkillEffect.DashStrike:
                    if(vfx!=null){vfx.Afterimage(source.root.transform);vfx.Trail(origin,aim,d.Effect,color);}
                    source.pos=new Vector2(Mathf.Clamp(target.pos.x-source.facing*.9f,TowerWorld.Left+.5f,TowerWorld.Right-.5f),target.pos.y);
                    source.root.transform.position=source.pos;if(source==hero)invulnerable=Mathf.Max(invulnerable,.35f);
                    foreach(var e in enemies.ToArray())if(CanTarget(e,owner)&&Vector2.Distance(source.pos,e.pos)<skill.Radius)StrikeEnemy(e,damage,owner,source.facing*.4f);
                    SlashArc(source.pos+Vector2.up,source.facing,color);if(vfx!=null)vfx.Impact(d.Effect,source.pos+Vector2.up,skill.Radius,color);break;
                case SkillEffect.HealPulse:
                    HealPlayer(damage,true);if(vfx!=null)vfx.Impact(d.Effect,origin,skill.Radius,color);break;
                case SkillEffect.ShieldPulse:
                    playerShield=Mathf.Min(PlayerMaxHealth*.65f,playerShield+damage*run.Player.HealingMultiplier);shieldTime=Mathf.Max(shieldTime,skill.Duration);
                    if(vfx!=null)vfx.Impact(d.Effect,hero.pos+Vector2.up,1.2f,color);FloatText(hero.pos+Vector2.up*2,"보호막 +"+Mathf.RoundToInt(damage*run.Player.HealingMultiplier),color);break;
                case SkillEffect.LifeDrain:
                    float drained=0;var victims=enemies.FindAll(e=>CanTarget(e,owner));victims.Sort((a,b)=>Vector2.Distance(a.pos,source.pos).CompareTo(Vector2.Distance(b.pos,source.pos)));
                    for(int i=0;i<Mathf.Min(skill.Count,victims.Count);i++)if(Vector2.Distance(source.pos,victims[i].pos)<skill.Radius)
                    {if(vfx!=null)vfx.Trail(victims[i].pos+Vector2.up,origin,d.Effect,color);drained+=StrikeEnemy(victims[i],damage,owner,0);}
                    HealPlayer(drained*.35f*owner.HealingMultiplier,true);break;
                case SkillEffect.Sentry:
                    AddArea(owner,source,d.Effect,source.pos+new Vector2(-source.facing*.8f,.15f),skill.Radius,skill.Duration,.85f,damage,color,skill.Count);break;
            }
        }
        private void LimitLastShotRange(float distance,float speed)
        {if(shots.Count>0)shots[shots.Count-1].life=distance/speed;}
        private void AddArea(HeroState owner,Fighter source,SkillEffect effect,Vector2 center,float radius,float duration,float interval,float damage,Color color,int count)
        {
            var go=new GameObject(effect.ToString());go.transform.SetParent(transform,false);go.transform.localPosition=center+Vector2.up*.6f;
            var area=new SkillArea{owner=owner,source=source,effect=effect,center=center,radius=radius,remaining=duration,interval=interval,tick=effect==SkillEffect.Meteor?interval:.05f,damage=damage,color=color,count=count,root=go};skillAreas.Add(area);
            if(vfx!=null){if(effect==SkillEffect.Meteor)vfx.DescendMeteor(center+Vector2.up*.6f,interval);else vfx.AttachZone(go,effect,radius,duration);}
        }
        private void TickSkillAreas(float dt)
        {
            for(int i=skillAreas.Count-1;i>=0;i--)
            {
                var a=skillAreas[i];a.remaining-=dt;a.tick-=dt;
                if(!IsCurrentHero(a.owner)){Destroy(a.root);skillAreas.RemoveAt(i);continue;}
                if(a.effect==SkillEffect.BladeStorm&&a.source.root){a.center=a.source.pos;a.root.transform.position=a.center+Vector2.up*.6f;}
                if(a.tick<=0)
                {
                    a.tick+=a.interval;
                    if(a.effect==SkillEffect.Sentry)
                    {var target=ClosestEnemy(a.center,Mathf.Max(a.radius,6),a.owner);if(target!=null)for(int j=0;j<a.count;j++){Fire(a.center+Vector2.up,target.pos+Vector2.up,11,a.damage,false,a.color,0,a.owner);StyleLastShot(SkillEffect.Sentry);}}
                    else if(a.effect==SkillEffect.HealPulse)
                    {if(Vector2.Distance(hero.pos,a.center)<=a.radius)HealPlayer(a.damage,true);}
                    else
                    {
                        foreach(var e in enemies.ToArray())if(CanTarget(e,a.owner)&&Vector2.Distance(e.pos,a.center)<a.radius)
                        {if(a.effect==SkillEffect.PoisonCloud)e.slowTime=Mathf.Max(e.slowTime,.8f);StrikeEnemy(e,a.damage,a.owner,0);}
                        if(a.effect==SkillEffect.BladeStorm)SlashArc(a.center+Vector2.up,1,a.color);
                        if(a.effect==SkillEffect.Meteor){if(vfx!=null){vfx.Trail(a.center+Vector2.up*7,a.center+Vector2.up,a.effect,a.color);vfx.Impact(a.effect,a.center+Vector2.up,a.radius,a.color);}}
                        if(vfx!=null&&a.effect!=SkillEffect.PoisonCloud&&a.effect!=SkillEffect.Meteor)vfx.Impact(a.effect,a.center+Vector2.up*.5f,a.radius,a.color);
                    }
                }
                if(a.remaining<=0){Destroy(a.root);skillAreas.RemoveAt(i);}
            }
        }
        private float StrikeEnemy(Fighter enemy,float damage,HeroState owner,float push)
        {
            if(!CanTarget(enemy,owner))return 0;
            bool critical=owner!=null&&UnityEngine.Random.value<owner.CritChance;if(critical)damage*=1.75f;
            float dealt=Mathf.Min(enemy.hp,damage);criticalHit=critical;DamageEnemy(enemy,damage,push);
            if(owner!=null&&owner.LifeSteal>0)HealPlayer(dealt*owner.LifeSteal,false);
            return dealt;
        }
        private void HealPlayer(float amount,bool show)
        {
            if(amount<=0||hero.hp<=0||hero.hp>=PlayerMaxHealth)return;
            float healed=Mathf.Min(PlayerMaxHealth-hero.hp,amount*run.Player.HealingMultiplier);hero.hp+=healed;
            if(show){FloatText(hero.pos+Vector2.up*2,"+"+Mathf.CeilToInt(healed),TowerWorld.Teal);Burst(hero.pos+Vector2.up,TowerWorld.Teal,7,.5f);}
        }
        private float MitigatePlayerDamage(float raw)
        {
            float reduced=raw*100/(100+Mathf.Max(0,PlayerArmor));float absorbed=Mathf.Min(playerShield,reduced);playerShield-=absorbed;
            if(absorbed>0){if(vfx!=null)vfx.Impact(SkillEffect.ShieldPulse,hero.pos+Vector2.up,1,TowerWorld.Gold);if(reduced<=absorbed+.001f)FloatText(hero.pos+Vector2.up*2,"막음",TowerWorld.Gold);}
            return reduced-absorbed;
        }
    }
}
