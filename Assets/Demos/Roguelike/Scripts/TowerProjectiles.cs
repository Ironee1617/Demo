using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerGame
    {
        internal int HostileProjectiles {get{return shots.FindAll(shot=>shot.hostile).Count;}}
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
            Vector2 delta=to-from;float fraction=delta.sqrMagnitude>0?Mathf.Clamp01(Vector2.Dot(center-from,delta)/delta.sqrMagnitude):0;
            return (from+delta*fraction-center).sqrMagnitude<=radius*radius;
        }
        private void TickShots(float dt)
        {
            for(int i=shots.Count-1;i>=0;i--)
            {
                var shot=shots[i];Vector2 previous=shot.pos;shot.life-=dt;shot.pos+=shot.velocity*dt;shot.root.transform.position=shot.pos;bool hit=false;
                if(shot.hostile)
                {
                    Vector2 body=Vector2.up*.9f;
                    if(CrossesCircle(previous-(previousHeroPosition+body),shot.pos-(hero.pos+body),Vector2.zero,PlayerBulletHitRadius+shot.hitRadius))
                    {HurtHero(shot.damage);hit=true;}
                }
                else foreach(var enemy in enemies.ToArray())
                {
                    if(shot.hit.Contains(enemy)||!CanTarget(enemy,shot.owner)||!CrossesCircle(previous,shot.pos,enemy.pos+Vector2.up*(enemy.isBoss?1.2f:.7f),(enemy.isBoss?1.05f:.5f)+shot.hitRadius))continue;
                    if(shot.area>0)
                    {foreach(var nearby in enemies.ToArray())if(CanTarget(nearby,shot.owner)&&Vector2.Distance(nearby.pos,enemy.pos)<shot.area)StrikeEnemy(nearby,shot.damage,shot.owner,Mathf.Sign(shot.velocity.x)*.1f);Burst(shot.pos,shot.color,14,shot.area);}
                    else StrikeEnemy(enemy,shot.damage,shot.owner,Mathf.Sign(shot.velocity.x)*.1f);
                    enemy.slowTime=Mathf.Max(enemy.slowTime,shot.slowDuration);shot.hit.Add(enemy);shot.pierce--;hit=shot.pierce<=0;break;
                }
                if(hit||shot.life<=0||shot.pos.x<TowerWorld.Left-5||shot.pos.x>TowerWorld.Right+5||shot.pos.y<-7||shot.pos.y>12)
                {if(hit&&vfx!=null)vfx.Impact(shot.effect,shot.pos,.35f,shot.hostile?new Color(1,.3f,.45f):shot.color);Destroy(shot.root);shots.RemoveAt(i);}
            }
        }
    }
}
