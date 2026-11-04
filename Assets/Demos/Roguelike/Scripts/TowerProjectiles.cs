using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerGame
    {
        private struct ProjectileContact : IComparable<ProjectileContact>
        {
            public Fighter enemy;
            public float fraction;
            public int CompareTo(ProjectileContact other) { return fraction.CompareTo(other.fraction); }
        }
        private readonly List<ProjectileContact> projectileContacts=new List<ProjectileContact>();
        internal int HostileProjectiles {get{int count=0;foreach(var shot in shots)if(shot.hostile)count++;return count;}}
        internal const float PlayerBulletHitRadius=.24f;
        // The hit core fades in over HitCoreFade while a hostile bolt within HitCoreReach of the chest heads for it, and stays
        // HitCoreHold past the last one so a crowded fight does not make it flicker.
        internal const float HitCoreReach=2.5f,HitCoreFade=.1f,HitCoreHold=.3f;
        // Monsters' bolts live EnemyBoltLife, about a screen's flight; bosses' and traps' keep HostileShotLife so their
        // patterns still reach across the arena. A fallen monster's bolts shrink away harmlessly over ShooterFallFade.
        internal const float EnemyBoltLife=3,HostileShotLife=7,ShooterFallFade=.2f;
        private SpriteRenderer[] hitCoreRenderers;private Color[] hitCoreColors;private float hitCoreAlpha,hitCoreHold;
        internal float HitCoreAlpha{get{return hitCoreAlpha;}}
        private void CreateHitCore()
        {
            playerHitCore=new GameObject("Precision movement hit core");playerHitCore.transform.SetParent(hero.root.transform,false);
            playerHitCore.transform.localPosition=Vector2.up*.9f;
            var radius=TowerWorld.SpriteObject(playerHitCore.transform,"Collision radius",Vector2.zero,Vector2.one*PlayerBulletHitRadius*2,new Color(.3f,1,1,.19f),150,TowerWorld.Disc);
            var core=TowerWorld.SpriteObject(playerHitCore.transform,"Core",Vector2.zero,Vector2.one*.1f,Color.white,151,TowerWorld.Disc);
            hitCoreRenderers=new[]{radius.GetComponent<SpriteRenderer>(),core.GetComponent<SpriteRenderer>()};
            hitCoreColors=new Color[hitCoreRenderers.Length];for(int i=0;i<hitCoreRenderers.Length;i++)hitCoreColors[i]=hitCoreRenderers[i]?hitCoreRenderers[i].color:Color.clear;
            hitCoreAlpha=hitCoreHold=0;
            playerHitCore.SetActive(false);
        }
        /// <summary>Whether a hostile bolt (not one fading away) within <see cref="HitCoreReach"/> of the hero's chest is still heading for it.</summary>
        private bool HostileBoltClosing()
        {
            if(hero==null||hero.hp<=0)return false;
            Vector2 chest=hero.pos+Vector2.up*.9f;
            foreach(var shot in shots)
            {
                if(!shot.hostile||shot.fade>=0||shot.life<=0)continue;
                Vector2 toward=chest-shot.pos;
                if(toward.sqrMagnitude<HitCoreReach*HitCoreReach&&Vector2.Dot(shot.velocity,toward)>0)return true;
            }
            return false;
        }
        private void TickHitCore(float dt)
        {
            if(!playerHitCore)return;
            hitCoreHold=FocusActive?HitCoreHold:Mathf.Max(0,hitCoreHold-dt);
            hitCoreAlpha=Mathf.MoveTowards(hitCoreAlpha,hitCoreHold>0?1:0,dt/HitCoreFade);
            playerHitCore.SetActive(hitCoreAlpha>0);
            for(int i=0;i<hitCoreRenderers.Length;i++)if(hitCoreRenderers[i]){var color=hitCoreColors[i];color.a*=hitCoreAlpha;hitCoreRenderers[i].color=color;}
        }
        private void StyleHostileLastShot(float radius=.16f,Fighter shooter=null,float life=HostileShotLife)
        {
            if(shots.Count==0)return;var shot=shots[shots.Count-1];if(!shot.hostile)return;
            shot.hitRadius=Mathf.Clamp(radius,.12f,.24f);shot.effect=SkillEffect.ArcaneVolley;shot.life=life;shot.shooter=shooter;
            if(vfx!=null)vfx.HostileProjectile(shot.root.transform,shot.hitRadius*2+.14f);
        }
        private static bool CrossesCircle(Vector2 from,Vector2 to,Vector2 center,float radius)
        {
            float fraction;return CircleContact(from,to,center,radius,out fraction);
        }
        private static bool CircleContact(Vector2 from,Vector2 to,Vector2 center,float radius,out float fraction)
        {
            Vector2 offset=from-center,delta=to-from;
            float outside=offset.sqrMagnitude-radius*radius;fraction=0;
            if(outside<=0)return true;
            float length=delta.sqrMagnitude;if(length<=Mathf.Epsilon)return false;
            float projection=Vector2.Dot(offset,delta),discriminant=projection*projection-length*outside;
            if(discriminant<0)return false;
            fraction=(-projection-Mathf.Sqrt(discriminant))/length;
            return fraction>=0&&fraction<=1;
        }
        private bool HitFriendlyShot(Shot shot,Vector2 previous)
        {
            // Keep a reusable ordered contact list: damage can remove enemies from the live list.
            projectileContacts.Clear();
            foreach(var enemy in enemies)
            {
                float fraction;
                if(shot.hit.Contains(enemy)||!CanTarget(enemy,shot.owner)||!CircleContact(previous,shot.pos,enemy.pos+Vector2.up*(enemy.isBoss?1.2f:.7f),(enemy.isBoss?1.05f:.5f)+shot.hitRadius,out fraction))continue;
                projectileContacts.Add(new ProjectileContact{enemy=enemy,fraction=fraction});
            }
            projectileContacts.Sort();bool consumed=false;
            foreach(var contact in projectileContacts)
            {
                var enemy=contact.enemy;if(!CanTarget(enemy,shot.owner))continue;
                Vector2 impact=Vector2.Lerp(previous,shot.pos,contact.fraction);
                forcedCritical=shot.forceCrit;
                if(shot.area>0)
                {
                    Vector2 center=enemy.pos;float radiusSquared=shot.area*shot.area;
                    // A snapshot: one fall can now take others with it (a seed blooming as its host dies).
                    var nearbyEnemies=enemies.ToArray();
                    for(int j=nearbyEnemies.Length-1;j>=0;j--)
                    {
                        var nearby=nearbyEnemies[j];
                        if(CanTarget(nearby,shot.owner)&&(nearby.pos-center).sqrMagnitude<radiusSquared)
                        {StrikeEnemy(nearby,shot.damage,shot.owner,Mathf.Sign(shot.velocity.x)*.1f);if(shot.slowDuration>0)nearby.slowTime=Mathf.Max(nearby.slowTime,shot.slowDuration);}
                    }
                    Burst(impact,shot.color,14,shot.area);
                }
                // A gun bullet landing close to the hero strikes harder (GunCloseBonus); every other shot keeps its damage. The
                // player's own shots (shot.poise) take poise and slide their knockback out (PlayerBlow, TowerPoise.cs).
                else
                {
                    float push=Mathf.Sign(shot.velocity.x)*.1f;bool blow=shot.poise>0&&Staggerable(enemy);
                    StrikeEnemy(enemy,shot.damage*GunCloseBonus(shot,impact),shot.owner,blow?0:push);
                    if(blow)PlayerBlow(enemy,push,shot.poise,false);
                }
                forcedCritical=false;
                // A returning disc sparks on every body it passes through; it never stops on one.
                if(shot.home!=null&&vfx!=null)vfx.ProjectileHit(shot.effect,impact,shot.color,true,shot.velocity);
                if(shot.basic)AfterShotHit(shot,enemy);
                ChainOathShot(shot,enemy);
                enemy.slowTime=Mathf.Max(enemy.slowTime,shot.slowDuration);shot.hit.Add(enemy);shot.pierce--;
                if(shot.pierce<=0){shot.pos=impact;consumed=true;break;}
            }
            projectileContacts.Clear();return consumed;
        }
        // The counter (a swing made while it is ready) and the dash slash cut the live hostile bolts in the band their swing
        // strikes enemies in, bosses' and traps' included. A counter that connects sends up to ReflectedBolts of them, the
        // nearest the hero first, straight at the nearest enemy as the player's own shots: ReflectSpeed, ReflectDamageScale of
        // the player's damage, ReflectLife seconds. Ordinary cuts, the gun and the orb cut nothing.
        internal const int ReflectedBolts=2;
        internal const float ReflectSpeed=14,ReflectDamageScale=.6f,ReflectLife=1.2f;
        internal int BoltsCut{get;private set;}
        internal int BoltsReflected{get;private set;}
        private struct CutBolt{public Vector2 pos,velocity;public float distance;}
        private readonly List<CutBolt> cutBolts=new List<CutBolt>();
        private static readonly Comparison<CutBolt> NearestCut=(a,b)=>a.distance.CompareTo(b.distance);
        /// <summary>
        /// Cuts the bolts a counter or dash slash reaches <paramref name="reach"/> along its aim. A counter that
        /// <paramref name="connected"/> reflects the nearest of them; one that struck only bolts is spent on them.
        /// </summary>
        private void SlashBolts(float reach,bool counter,bool connected)
        {
            cutBolts.Clear();Vector2 chest=Chest;
            for(int i=shots.Count-1;i>=0;i--)
            {
                var shot=shots[i];
                if(!shot.hostile||shot.fade>=0||shot.life<=0||!SwingReaches(shot.pos,reach,.4f,1.9f))continue;
                cutBolts.Add(new CutBolt{pos=shot.pos,velocity=shot.velocity,distance=(shot.pos-chest).sqrMagnitude});
                if(vfx!=null)vfx.Hit(shot.pos,TowerWorld.Teal);
                Destroy(shot.root);shots.RemoveAt(i);
            }
            if(cutBolts.Count==0)return;
            BoltsCut+=cutBolts.Count;Sound(strikeCue,.4f);
            if(counter&&!connected)CounterRemaining=0;
            else if(counter){cutBolts.Sort(NearestCut);for(int i=0;i<cutBolts.Count&&i<ReflectedBolts;i++)ReflectBolt(cutBolts[i]);}
            cutBolts.Clear();
        }
        private void ReflectBolt(CutBolt bolt)
        {
            Fighter target=null;float best=float.PositiveInfinity;
            foreach(var e in enemies){if(!CanTarget(e,run.Player))continue;float d=(e.pos-bolt.pos).sqrMagnitude;if(d<best){best=d;target=e;}}
            // With nothing left to aim at, it flies back the way it came.
            Vector2 to=target!=null?target.pos+Vector2.up*(target.isBoss?1.2f:.7f):bolt.pos-bolt.velocity;
            if((to-bolt.pos).sqrMagnitude<.0001f)to=bolt.pos+Vector2.right*hero.facing;
            Fire(bolt.pos,to,ReflectSpeed,PlayerDamage*ReflectDamageScale,false,TowerWorld.Teal,0,run.Player);
            // The player's own shot (owner, damage source, poise), never a practice cast's.
            var shot=shots[shots.Count-1];shot.life=ReflectLife;shot.practiceCast=-1;shot.poise=ShotPoise;
            BoltsReflected++;
        }
        // Skill shots burst at full size and kick the camera; basic and hostile shots keep a small hit.
        private void ShotImpact(Shot shot)
        {
            if(shot.weapon!=TowerAttackMode.Sword){if(vfx!=null)vfx.WeaponHit(shot.weapon,shot.pos,shot.color,shot.velocity);return;}
            bool signature=shot.signature&&!shot.hostile;
            if(vfx!=null){vfx.Grade=signature?shot.level:1;vfx.ProjectileHit(shot.effect,shot.pos,shot.hostile?new Color(1,.3f,.45f):shot.color,signature,shot.velocity);vfx.Grade=1;}
            if(!signature)return;
            if(shot.effect==SkillEffect.Fireball&&shot.level>=3)FireField(shot);
            bool player=shot.owner==run.Player;float weight=player?1:.5f;
            // The hero's automatic casts never stop time in a climb; the practice range's casts still do (SkillJuice).
            if(shot.effect==SkillEffect.Fireball){Shake(.14f*weight,.2f);if(player&&practice)HitStop(.03f);}
            else if(shot.effect==SkillEffect.PierceShot||shot.effect==SkillEffect.Shockwave)Shake(.06f*weight,.1f);
            else if(shot.effect!=SkillEffect.Sentry)Shake(.03f*weight,.08f);
        }
        private void TickShots(float dt)
        {
            for(int i=shots.Count-1;i>=0;i--)
            {
                var shot=shots[i];Vector2 previous=shot.pos;
                // A fallen monster's bolts drift on without harm and shrink away.
                if(shot.shooter!=null&&shot.fade<0&&shot.shooter.hp<=0)shot.fade=ShooterFallFade;
                if(shot.fade>=0)
                {
                    shot.fade-=dt;shot.pos+=shot.velocity*dt;shot.root.transform.position=shot.pos;
                    shot.root.transform.localScale=Vector3.one*Mathf.Clamp01(shot.fade/ShooterFallFade);
                    if(shot.fade<=0){Destroy(shot.root);shots.RemoveAt(i);}
                    continue;
                }
                if(shot.returning)SteerHome(shot);
                if(shot.seek!=null)SteerSeek(shot,dt);
                bool alive=shot.life>0;
                float step=Mathf.Min(dt,Mathf.Max(0,shot.life));shot.life-=dt;shot.pos+=shot.velocity*step;bool hit=false;
                if(alive&&shot.hostile)
                {
                    // Companions have no hostile contact shape and cannot consume
                    // or block a projectile on its way to the player.
                    Vector2 body=Vector2.up*.9f;
                    Vector2 heroEnd=dt>0?Vector2.Lerp(previousHeroPosition,hero.pos,step/dt):hero.pos;
                    if(CrossesCircle(previous-(previousHeroPosition+body),shot.pos-(heroEnd+body),Vector2.zero,PlayerBulletHitRadius+shot.hitRadius))
                    {if(shot.trap)TryPrimeTrapDodge();else TryPrimeDodgeCounter();HurtHeroBy(PaceHit.Bullet,shot.damage);hit=true;}
                }
                else if(alive)
                {
                    practiceStrike=shot.practiceCast;striking=shot.skill;strikeEffect=shot.effect;strikeLevel=shot.level;
                    hit=HitFriendlyShot(shot,previous);
                    striking=false;strikeLevel=1;practiceStrike=0;
                    if(shot.weapon!=TowerAttackMode.Sword){if(hit)WeaponShotLanded(shot);else hit=HitProps(shot,previous);}
                    // A bouncing shot turns to its next enemy instead of ending; a disc turns home at the end of its flight.
                    if(hit&&shot.bounces>0&&Bounce(shot)){ShotImpact(shot);hit=false;}
                    if(shot.home!=null&&!shot.returning&&shot.life<=0)TurnHome(shot);
                }
                shot.root.transform.position=shot.pos;
                if(hit||shot.life<=0||shot.pos.x<TowerWorld.Left-5||shot.pos.x>TowerWorld.Right+5||shot.pos.y<-7||shot.pos.y>Mathf.Max(12,world.HighestPlatform+10))
                {if(hit)ShotImpact(shot);Destroy(shot.root);shots.RemoveAt(i);}
            }
        }
    }
}
