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
        private void CreateHitCore()
        {
            playerHitCore=new GameObject("Precision movement hit core");playerHitCore.transform.SetParent(hero.root.transform,false);
            playerHitCore.transform.localPosition=Vector2.up*.9f;
            TowerWorld.SpriteObject(playerHitCore.transform,"Collision radius",Vector2.zero,Vector2.one*PlayerBulletHitRadius*2,new Color(.3f,1,1,.19f),150,TowerWorld.Disc);
            TowerWorld.SpriteObject(playerHitCore.transform,"Core",Vector2.zero,Vector2.one*.1f,Color.white,151,TowerWorld.Disc);
            playerHitCore.SetActive(false);
        }
        private void StyleHostileLastShot(float radius=.16f)
        {
            if(shots.Count==0)return;var shot=shots[shots.Count-1];if(!shot.hostile)return;
            shot.hitRadius=Mathf.Clamp(radius,.12f,.24f);shot.effect=SkillEffect.ArcaneVolley;shot.life=7;
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
                else StrikeEnemy(enemy,shot.damage,shot.owner,Mathf.Sign(shot.velocity.x)*.1f);
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
        // Skill shots burst at full size and kick the camera; basic and hostile shots keep a small hit.
        private void ShotImpact(Shot shot)
        {
            bool signature=shot.signature&&!shot.hostile;
            if(vfx!=null){vfx.Grade=signature?shot.level:1;vfx.ProjectileHit(shot.effect,shot.pos,shot.hostile?new Color(1,.3f,.45f):shot.color,signature,shot.velocity);vfx.Grade=1;}
            if(!signature)return;
            if(shot.effect==SkillEffect.Fireball&&shot.level>=3)FireField(shot);
            bool player=shot.owner==run.Player;float weight=player?1:.5f;
            if(shot.effect==SkillEffect.Fireball){Shake(.14f*weight,.2f);if(player)HitStop(.03f);}
            else if(shot.effect==SkillEffect.PierceShot||shot.effect==SkillEffect.Shockwave)Shake(.06f*weight,.1f);
            else if(shot.effect!=SkillEffect.Sentry)Shake(.03f*weight,.08f);
        }
        private void TickShots(float dt)
        {
            for(int i=shots.Count-1;i>=0;i--)
            {
                var shot=shots[i];Vector2 previous=shot.pos;
                if(shot.returning)SteerHome(shot);
                bool alive=shot.life>0;
                float step=Mathf.Min(dt,Mathf.Max(0,shot.life));shot.life-=dt;shot.pos+=shot.velocity*step;bool hit=false;
                if(alive&&shot.hostile)
                {
                    // Companions have no hostile contact shape and cannot consume
                    // or block a projectile on its way to the player.
                    Vector2 body=Vector2.up*.9f;
                    Vector2 heroEnd=dt>0?Vector2.Lerp(previousHeroPosition,hero.pos,step/dt):hero.pos;
                    if(CrossesCircle(previous-(previousHeroPosition+body),shot.pos-(heroEnd+body),Vector2.zero,PlayerBulletHitRadius+shot.hitRadius))
                    {TryPrimeDodgeCounter();HurtHero(shot.damage);hit=true;}
                }
                else if(alive)
                {
                    practiceStrike=shot.practiceCast;striking=shot.skill;strikeEffect=shot.effect;strikeLevel=shot.level;
                    hit=HitFriendlyShot(shot,previous);
                    striking=false;strikeLevel=1;practiceStrike=0;
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
