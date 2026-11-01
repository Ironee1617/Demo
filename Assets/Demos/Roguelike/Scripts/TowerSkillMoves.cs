using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // The companions' narrower skills (returning discs, bouncing shots, marks, hooks, quakes, orbs, phantoms, mines, vortices,
    // curses, banners and seeds) and the level-three awakenings of the original skills. Statuses and reactions: TowerSkillStatus.cs.
    public sealed partial class TowerGame
    {
        // The skill whose hits are being resolved: a cast, a skill area's tick, a skill shot's flight or a delayed follow-up sets it.
        private bool striking; private SkillEffect strikeEffect; private int strikeLevel=1;
        private struct StrikeContext { public bool striking; public SkillEffect effect; public int level,practice; }
        private StrikeContext SaveStrike(){return new StrikeContext{striking=striking,effect=strikeEffect,level=strikeLevel,practice=practiceStrike};}
        private void BeginStrike(SkillEffect effect,int level,int practiceCast)
        {striking=true;strikeEffect=effect;strikeLevel=level;practiceStrike=practiceCast;if(vfx!=null)vfx.Grade=level;}
        private void RestoreStrike(StrikeContext saved)
        {striking=saved.striking;strikeEffect=saved.effect;strikeLevel=saved.level;practiceStrike=saved.practice;if(vfx!=null)vfx.Grade=saved.striking?saved.level:1;}

        // A level-three shield's burst when it breaks, and who raised it.
        private float shieldNova; private HeroState shieldNovaOwner;
        // Follow-ups a skill schedules (a second wave, a crossing cut, a hook's landing burst).
        private sealed class SkillMove { public float delay; public HeroState owner; public Action act; public SkillEffect effect; public int level,practiceCast; }
        private readonly List<SkillMove> skillMoves=new List<SkillMove>();
        // Enemies a hook is reeling in.
        private sealed class Reel { public Fighter enemy; public float from,to,time; }
        private readonly List<Reel> reels=new List<Reel>();
        private const float ReelSeconds=.16f,DiscSpeed=12,BounceSpeed=15;

        private void QueueSkillMove(float delay,HeroState owner,SkillState skill,Action act)
        {skillMoves.Add(new SkillMove{delay=delay,owner=owner,act=act,effect=skill.Definition.Effect,level=skill.Level,practiceCast=practiceStrike>0?practiceStrike:PracticeCasts});}
        // A level-three shield keeps its burst across floors with the shield itself; only a fresh hero (ResetSkills) drops it.
        private void ClearSkillMoves(){skillMoves.Clear();reels.Clear();shieldBreakPending=false;}
        private bool shieldBreakPending;
        private void TickSkillMoves(float dt)
        {
            if(shieldBreakPending){shieldBreakPending=false;ShieldBroken();}
            if(shieldNova>0&&playerShield<=0)shieldNova=0;
            for(int i=skillMoves.Count-1;i>=0;i--)
            {
                var move=skillMoves[i];move.delay-=dt;if(move.delay>0)continue;
                skillMoves.RemoveAt(i);if(!IsCurrentHero(move.owner)||hero.hp<=0)continue;
                var saved=SaveStrike();BeginStrike(move.effect,move.level,move.practiceCast);move.act();RestoreStrike(saved);
            }
            for(int i=reels.Count-1;i>=0;i--)
            {
                var reel=reels[i];reel.time+=dt;
                if(!reel.enemy.root||reel.enemy.hp<=0){reels.RemoveAt(i);continue;}
                float t=Mathf.Clamp01(reel.time/ReelSeconds);reel.enemy.pos.x=Mathf.Lerp(reel.from,reel.to,1-(1-t)*(1-t));
                if(t>=1)reels.RemoveAt(i);
            }
        }
        private void ReelTo(Fighter enemy,float x)
        {reels.RemoveAll(r=>r.enemy==enemy);reels.Add(new Reel{enemy=enemy,from=enemy.pos.x,to=Mathf.Clamp(x,TowerWorld.Left+.5f,TowerWorld.Right-.5f)});}

        /// <summary>How close the target must be before an automatic cast of one of the narrower skills.</summary>
        private static float OddCastRange(SkillState skill)
        {
            switch(skill.Definition.Effect)
            {
                case SkillEffect.Ricochet:return 9;
                case SkillEffect.Orbit:return 2.4f;
                case SkillEffect.Decoy:return 4;
                case SkillEffect.Mine:return 4.5f;
                case SkillEffect.Vortex:case SkillEffect.Bloom:return 8;
                case SkillEffect.WarBanner:return 5;
                default:return Mathf.Max(2,skill.Radius);
            }
        }
        // Orbs, phantoms, mines and banners work around the hero: a companion's version guards or fights at the front line.
        private static bool AnchoredOnHero(SkillEffect effect)
        {return effect==SkillEffect.Orbit||effect==SkillEffect.Decoy||effect==SkillEffect.Mine||effect==SkillEffect.WarBanner;}
        // Up to count enemies within range of the caster, nearest to the chosen target first.
        private List<Fighter> SkillTargets(Fighter first,Vector2 from,float range,HeroState owner,int count)
        {
            var found=enemies.FindAll(e=>CanTarget(e,owner)&&Vector2.Distance(e.pos,from)<=range);
            Vector2 near=first!=null?first.pos:from;found.Sort((a,b)=>Vector2.Distance(a.pos,near).CompareTo(Vector2.Distance(b.pos,near)));
            if(found.Count>count)found.RemoveRange(count,found.Count-count);
            return found;
        }

        private void CastOddSkill(HeroState owner,SkillState skill,Fighter source,Fighter target,float damage,Color color,Vector2 origin,Vector2 aim)
        {
            var effect=skill.Definition.Effect;bool awake=skill.Awakened;int count=Mathf.Max(1,skill.Count);
            var anchor=AnchoredOnHero(effect)?hero:source;
            switch(effect)
            {
                case SkillEffect.Boomerang:
                {
                    Vector2 v=aim-origin;if(v.sqrMagnitude<.01f)v=Vector2.right*source.facing;v.Normalize();
                    for(int i=0;i<count;i++)
                    {
                        Vector2 direction=Quaternion.Euler(0,0,i*12*(v.x>=0?1:-1))*v;
                        Fire(origin,origin+direction*10,DiscSpeed,damage,false,color,0,owner,99);StyleLastShot(effect);
                        var disc=shots[shots.Count-1];disc.home=source;disc.life=Mathf.Max(1.5f,skill.Radius)/DiscSpeed;disc.returnScale=awake?1.5f:1;disc.hitRadius=.3f;
                    }
                    break;
                }
                case SkillEffect.Ricochet:
                {
                    Fire(origin,aim,BounceSpeed,damage,false,color,0,owner,1);StyleLastShot(effect);
                    var shot=shots[shots.Count-1];shot.bounces=count;shot.bounceRange=Mathf.Max(2,skill.Radius);shot.bounceGain=awake?1.12f:.88f;shot.life=10f/BounceSpeed;
                    break;
                }
                case SkillEffect.HuntMark:
                    foreach(var e in SkillTargets(target,source.pos,skill.Radius,owner,count))
                    {
                        Vector2 at=e.pos+Vector2.up*(e.isBoss?1.4f:.9f);
                        if(vfx!=null){vfx.Trail(origin,at,effect,color);vfx.Impact(effect,at,.9f,color,source.facing);}
                        StrikeEnemy(e,damage,owner,0);if(e.hp>0)ApplyMark(e,skill.Duration,awake);
                    }
                    break;
                case SkillEffect.Hook:
                {
                    // Farthest first: the chain reaches past the front line. Bosses are struck and dazed but never moved.
                    var reach=enemies.FindAll(e=>CanTarget(e,owner)&&Vector2.Distance(e.pos,source.pos)<=skill.Radius);
                    reach.Sort((a,b)=>Vector2.Distance(b.pos,source.pos).CompareTo(Vector2.Distance(a.pos,source.pos)));
                    int hooked=0;
                    for(int i=0;i<Mathf.Min(count,reach.Count);i++)
                    {
                        var e=reach[i];Vector2 at=e.pos+Vector2.up*(e.isBoss?1.2f:.7f);
                        if(vfx!=null){vfx.Trail(origin,at,effect,color);vfx.Impact(effect,at,.8f,color,source.facing);}
                        StrikeEnemy(e,damage,owner,0);hooked++;if(e.hp<=0)continue;
                        Stun(e,skill.Duration,false);
                        // Flyers keep to the air where they hover; only foes on their feet are reeled in.
                        if(!e.isBoss&&!e.airborne){float side=e.pos.x>=source.pos.x?1:-1;ReelTo(e,source.pos.x+side*(.95f+i*.55f));}
                    }
                    if(awake&&hooked>0)
                    {var caster=source;QueueSkillMove(ReelSeconds+.03f,owner,skill,()=>{if(caster.root)SkillBurst(owner,SkillEffect.Hook,caster.pos,1.8f,damage*.6f,color,true);});}
                    break;
                }
                case SkillEffect.Quake:
                {
                    var quake=AddArea(owner,source,effect,source.pos+Vector2.right*source.facing*.3f,skill.Radius,10,.06f,damage,color,1);
                    quake.facing=source.facing;quake.struck=new HashSet<Fighter>();quake.hold=skill.Duration;quake.tick=0;
                    break;
                }
                case SkillEffect.Orbit:
                {var orbs=AddArea(owner,anchor,effect,anchor.pos,skill.Radius,skill.Duration,.05f,damage,color,count);orbs.touched=new Dictionary<Fighter,float>();break;}
                case SkillEffect.Decoy:
                    // A companion's phantom stands at the front line beside the hero; its look copies the caster.
                    for(int i=0;i<Mathf.Min(2,count);i++)
                    {
                        int side=i==0?anchor.facing:-anchor.facing;
                        var phantom=AddArea(owner,source,effect,new Vector2(Mathf.Clamp(anchor.pos.x+side*1.2f,TowerWorld.Left+.5f,TowerWorld.Right-.5f),anchor.pos.y),
                            skill.Radius,skill.Duration,skill.Level>=2?.5f:.7f,damage,color,1);
                        phantom.facing=side;phantom.tick=.25f;
                    }
                    break;
                case SkillEffect.Mine:
                    for(int i=0;i<count;i++)
                    {
                        float x=Mathf.Clamp(anchor.pos.x+anchor.facing*(1.1f+i*1),TowerWorld.Left+.5f,TowerWorld.Right-.5f);
                        var mine=AddArea(owner,source,effect,new Vector2(x,anchor.pos.y),skill.Radius,skill.Duration,.05f,damage,color,1);mine.hold=awake?1.5f:0;
                    }
                    break;
                case SkillEffect.Vortex:
                {var vortex=AddArea(owner,source,effect,target.pos,skill.Radius,skill.Duration,.1f,damage,color,1);vortex.hold=.6f;vortex.clock=.2f;break;}
                case SkillEffect.Curse:
                    foreach(var e in SkillTargets(target,source.pos,skill.Radius,owner,count))
                    {
                        Vector2 at=e.pos+Vector2.up*(e.isBoss?1.4f:.9f);
                        if(vfx!=null){vfx.Trail(origin,at,effect,color);vfx.Impact(effect,at,.9f,color,source.facing);}
                        ApplyCurse(e,owner,damage,skill.Duration,awake?2:1);
                    }
                    break;
                case SkillEffect.WarBanner:
                {var banner=AddArea(owner,source,effect,anchor.pos,skill.Radius,skill.Duration,1,damage,color,1);banner.tick=.2f;break;}
                case SkillEffect.Bloom:
                    foreach(var e in SkillTargets(target,source.pos,8,owner,count))
                    {
                        if(vfx!=null)vfx.Trail(origin,e.pos+Vector2.up*.9f,effect,color);
                        ApplySeed(e,owner,damage,skill.Radius,skill.Definition.Duration,skill.Level);
                    }
                    break;
            }
        }

        // Areas of the narrower skills; true when the area was handled here.
        private bool TickOddArea(SkillArea a,float dt)
        {
            switch(a.effect)
            {
                case SkillEffect.Quake:
                {
                    if(a.tick>0)return true;a.tick+=a.interval;
                    const float Stride=.6f;
                    a.travelled+=Stride;a.center.x=Mathf.Clamp(a.center.x+a.facing*Stride,TowerWorld.Left+.3f,TowerWorld.Right-.3f);a.root.transform.position=a.center+Vector2.up*.6f;
                    if(vfx!=null)vfx.Impact(a.effect,a.center,.8f,a.color,a.facing);
                    foreach(var e in enemies.ToArray())
                        if(CanTarget(e,a.owner)&&!a.struck.Contains(e)&&Mathf.Abs(e.pos.x-a.center.x)<.75f&&Mathf.Abs(e.pos.y-a.center.y)<1.6f)
                        {a.struck.Add(e);StrikeEnemy(e,a.damage,a.owner,a.facing*.15f);if(e.hp>0)Stun(e,a.hold,false);}
                    Shake(.05f*(a.owner==run.Player?1:.55f),.06f);
                    if(a.travelled>=a.radius){if(a.level>=3)SkillBurst(a.owner,a.effect,a.center,1.7f,a.damage*1.5f,a.color,true);a.remaining=0;}
                    return true;
                }
                case SkillEffect.Orbit:
                {
                    if(a.tick>0)return true;a.tick+=a.interval;
                    Vector2 hub=a.center+Vector2.up*.6f;
                    for(int k=0;k<a.count;k++)
                    {
                        Vector2 orb=hub+TowerVfx.OrbitOffset(k,a.count,a.age,a.radius);
                        foreach(var e in enemies.ToArray())
                        {
                            if(!CanTarget(e,a.owner))continue;
                            if(Vector2.Distance(orb,e.pos+Vector2.up*(e.isBoss?1.2f:.7f))>(e.isBoss?1.25f:.75f))continue;
                            float last;if(a.touched.TryGetValue(e,out last)&&a.age-last<.45f)continue;a.touched[e]=a.age;
                            StrikeEnemy(e,a.damage,a.owner,Mathf.Sign(e.pos.x-a.center.x)*.08f);if(vfx!=null)vfx.Impact(a.effect,orb,.5f,a.color,0);
                        }
                    }
                    return true;
                }
                case SkillEffect.Decoy:
                {
                    if(a.tick>0)return true;a.tick+=a.interval;
                    Fighter near=null;float best=a.radius;
                    foreach(var e in enemies)if(CanTarget(e,a.owner)){float d=Vector2.Distance(e.pos,a.center);if(d<best){best=d;near=e;}}
                    if(near==null)return true;
                    a.facing=near.pos.x>=a.center.x?1:-1;if(vfx!=null)vfx.Impact(a.effect,a.center+Vector2.up*.8f,a.radius,a.color,a.facing);
                    foreach(var e in enemies.ToArray())if(CanTarget(e,a.owner)&&Vector2.Distance(e.pos,a.center)<a.radius)StrikeEnemy(e,a.damage,a.owner,a.facing*.15f);
                    return true;
                }
                case SkillEffect.Mine:
                {
                    if(a.age<.35f||a.tick>0)return true;a.tick+=a.interval;
                    foreach(var e in enemies)if(CanTarget(e,a.owner)&&Mathf.Abs(e.pos.x-a.center.x)<.8f&&Mathf.Abs(e.pos.y-a.center.y)<1.4f){MineBlast(a);a.remaining=0;break;}
                    return true;
                }
                case SkillEffect.Vortex:
                {
                    float pull=(a.level>=2?4.5f:3)*dt;
                    foreach(var e in enemies)
                        if(CanTarget(e,a.owner)&&!e.isBoss&&Mathf.Abs(e.pos.x-a.center.x)<a.radius*1.7f&&Mathf.Abs(e.pos.y-a.center.y)<2.2f)
                            e.pos.x=Mathf.MoveTowards(e.pos.x,a.center.x,pull);
                    a.clock-=dt;
                    if(a.clock<=0){a.clock+=.4f;foreach(var e in enemies.ToArray())if(CanTarget(e,a.owner)&&Vector2.Distance(e.pos,a.center)<a.radius)StrikeEnemy(e,a.damage,a.owner,0);}
                    return true;
                }
                case SkillEffect.WarBanner:
                {
                    if(a.tick>0)return true;a.tick+=a.interval;
                    foreach(var e in enemies.ToArray())if(CanTarget(e,a.owner)&&Mathf.Abs(e.pos.x-a.center.x)<a.radius&&Mathf.Abs(e.pos.y-a.center.y)<2.5f)StrikeEnemy(e,a.damage,a.owner,0);
                    if(vfx!=null)vfx.Impact(a.effect,a.center+Vector2.up*.4f,a.radius,a.color,0);
                    return true;
                }
                default:return false;
            }
        }
        // What an area does as it ends: a phantom bursts, an untouched mine goes off, a vortex dazes what it gathered.
        private void ExpireArea(SkillArea a)
        {
            switch(a.effect)
            {
                case SkillEffect.Decoy:SkillBurst(a.owner,a.effect,a.center,a.radius*1.4f,a.damage*2,a.color,false);break;
                case SkillEffect.Mine:if(a.struck==null)MineBlast(a);break;
                case SkillEffect.Vortex:
                    foreach(var e in enemies.ToArray())if(CanTarget(e,a.owner)&&Vector2.Distance(e.pos,a.center)<a.radius)Stun(e,a.hold,false);
                    if(a.level>=3)SkillBurst(a.owner,a.effect,a.center,a.radius*1.2f,a.damage*3,a.color,true);
                    else if(vfx!=null)vfx.Impact(a.effect,a.center+Vector2.up*.6f,a.radius,a.color,0);
                    break;
            }
        }
        private void MineBlast(SkillArea a)
        {
            // The struck set doubles as the mine's spent flag so it never bursts twice.
            a.struck=new HashSet<Fighter>();
            foreach(var e in enemies.ToArray())if(CanTarget(e,a.owner)&&Vector2.Distance(e.pos,a.center)<a.radius)
            {StrikeEnemy(e,a.damage,a.owner,Mathf.Sign(e.pos.x-a.center.x)*.25f);if(a.hold>0)e.slowTime=Mathf.Max(e.slowTime,a.hold);}
            if(vfx!=null){vfx.Impact(a.effect,a.center+Vector2.up*.3f,a.radius,a.color,0);if(a.level>=3)vfx.Awaken(a.effect,a.center+Vector2.up*.3f,a.radius,a.color,0);}
            Shake(.1f*(a.owner==run.Player?1:.55f),.12f);
        }
        // Level three of the original areas: a blade storm drags enemies in, a poison cloud creeps after the nearest one.
        private void AwakenedArea(SkillArea a,float dt)
        {
            if(a.effect==SkillEffect.BladeStorm)
            {
                foreach(var e in enemies)
                    if(CanTarget(e,a.owner)&&!e.isBoss&&Mathf.Abs(e.pos.x-a.center.x)>.5f&&Mathf.Abs(e.pos.x-a.center.x)<a.radius*1.6f&&Mathf.Abs(e.pos.y-a.center.y)<2)
                        e.pos.x=Mathf.MoveTowards(e.pos.x,a.center.x,2.6f*dt);
            }
            else if(a.effect==SkillEffect.PoisonCloud)
            {
                var prey=ClosestEnemy(a.center,6,a.owner);
                if(prey!=null){a.center.x=Mathf.MoveTowards(a.center.x,prey.pos.x,1.2f*dt);a.root.transform.position=a.center+Vector2.up*.6f;}
            }
        }

        // ---------------------------------------------------------------- awakenings and bursts

        private void SkillBurst(HeroState owner,SkillEffect effect,Vector2 ground,float radius,float damage,Color color,bool awaken)
        {
            if(effect==SkillEffect.ChainLightning&&vfx!=null)vfx.Trail(ground+Vector2.up*7,ground+Vector2.up,effect,color);
            foreach(var e in enemies.ToArray())if(CanTarget(e,owner)&&Vector2.Distance(e.pos,ground)<radius)StrikeEnemy(e,damage,owner,Mathf.Sign(e.pos.x-ground.x)*.2f);
            if(vfx!=null){vfx.Impact(effect,ground+Vector2.up*.8f,radius,color,0);if(awaken)vfx.Awaken(effect,ground+Vector2.up*.8f,radius,color,0);}
            Shake(.12f*(owner==run.Player?1:.55f),.16f);
        }
        private void SecondShockwave(HeroState owner,SkillState skill,Fighter source,Vector2 direction,float damage,Color color)
        {
            if(source==null||!source.root)return;
            Vector2 origin=source.pos+Vector2.up*(source==hero?.95f:.78f);if(direction.sqrMagnitude<.01f)direction=Vector2.right*source.facing;
            Fire(origin,origin+direction.normalized*10,9,damage,false,color,0,owner,Mathf.Max(3,skill.Count),Mathf.Max(.6f,skill.Duration));
            StyleLastShot(SkillEffect.Shockwave);LimitLastShotRange(skill.Radius,9);
            if(vfx!=null){vfx.Impact(SkillEffect.Shockwave,origin,skill.Radius,color);vfx.Awaken(SkillEffect.Shockwave,origin,skill.Radius,color,source.facing);}
        }
        private void GrantShield(float amount,float seconds,Color color)
        {
            if(amount<=0)return;
            playerShield=Mathf.Min(PlayerMaxHealth*.65f,playerShield+amount);shieldTime=Mathf.Max(shieldTime,seconds);
            FloatText(hero.pos+Vector2.up*2.4f,"보호막 +"+Mathf.RoundToInt(amount),color);
        }
        private void ShieldBroken()
        {
            float damage=shieldNova;var owner=shieldNovaOwner;shieldNova=0;shieldNovaOwner=null;
            if(damage<=0||owner==null||!IsCurrentHero(owner))return;
            var saved=SaveStrike();BeginStrike(SkillEffect.ShieldPulse,3,PracticeCasts);
            SkillBurst(owner,SkillEffect.ShieldPulse,hero.pos,2.6f,damage,SkillColor(SkillEffect.ShieldPulse),true);
            RestoreStrike(saved);
        }
        // A level-three fireball sets the ground it lands on burning for a few seconds.
        private void FireField(Shot shot)
        {
            if(shot.owner==null||!IsCurrentHero(shot.owner))return;
            var saved=SaveStrike();BeginStrike(SkillEffect.Fireball,shot.level,shot.practiceCast);
            float floor=world.GroundAt(shot.pos.x),feet=shot.pos.y-.7f;
            bool standing=world.SupportAt(shot.pos.x,feet,.35f)>=0||feet-floor<=1.2f;
            var field=AddArea(shot.owner,null,SkillEffect.Fireball,new Vector2(shot.pos.x,standing?feet:floor),1.3f,3,.5f,shot.damage*.25f,shot.color,1);
            field.quiet=true;field.practiceCast=shot.practiceCast;
            if(vfx!=null)vfx.Awaken(SkillEffect.Fireball,shot.pos,1.3f,shot.color,0);
            RestoreStrike(saved);
        }

        // ---------------------------------------------------------------- discs and bouncing shots

        private void TurnHome(Shot shot)
        {
            shot.returning=true;shot.life=3;shot.hit.Clear();shot.damage*=shot.returnScale;
            if(shot.returnScale>1&&vfx!=null){vfx.Grade=shot.level;vfx.Awaken(SkillEffect.Boomerang,shot.pos,.8f,shot.color,0);vfx.Grade=1;}
        }
        private void SteerHome(Shot shot)
        {
            var home=shot.home;if(home==null||!home.root){shot.life=0;return;}
            Vector2 to=home.pos+Vector2.up*.9f-shot.pos;
            if(to.magnitude<.55f){shot.life=0;return;}
            shot.velocity=to.normalized*DiscSpeed*1.15f;
        }
        private bool Bounce(Shot shot)
        {
            Fighter next=null;float best=shot.bounceRange;
            foreach(var e in enemies)if(CanTarget(e,shot.owner)&&!shot.hit.Contains(e)){float d=Vector2.Distance(e.pos+Vector2.up*.7f,shot.pos);if(d<best){best=d;next=e;}}
            if(next==null)return false;
            Vector2 to=next.pos+Vector2.up*(next.isBoss?1.2f:.7f)-shot.pos;
            shot.velocity=to.normalized*BounceSpeed;shot.bounces--;shot.pierce=1;shot.damage*=shot.bounceGain;shot.life=best/BounceSpeed+.25f;
            shot.root.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(to.y,to.x)*Mathf.Rad2Deg);
            if(vfx!=null){vfx.Grade=shot.level;vfx.Trail(shot.pos,next.pos+Vector2.up*.7f,SkillEffect.Ricochet,shot.color);vfx.Grade=1;}
            return true;
        }

        // ---------------------------------------------------------------- banners

        /// <summary>Attack interval factor for a hero standing by a war banner (1 when none is near).</summary>
        private float BannerInterval(Fighter actor)
        {
            float best=1;if(actor==null)return best;
            foreach(var a in skillAreas)
                if(a.effect==SkillEffect.WarBanner&&Mathf.Abs(actor.pos.x-a.center.x)<=a.radius&&Mathf.Abs(actor.pos.y-a.center.y)<3)
                    best=Mathf.Min(best,1/(1+(a.level>=2?.5f:.35f)));
            return best;
        }
        // A level-three banner also strengthens every blow of the heroes standing by it.
        private bool BannerEmpowers(HeroState owner)
        {
            if(owner==null)return false;Fighter actor=null;
            foreach(var a in skillAreas)
            {
                if(a.effect!=SkillEffect.WarBanner||a.level<3)continue;
                if(actor==null)actor=ActorFor(owner.Id);if(actor==null)return false;
                if(Mathf.Abs(actor.pos.x-a.center.x)<=a.radius&&Mathf.Abs(actor.pos.y-a.center.y)<3)return true;
            }
            return false;
        }
    }
}
