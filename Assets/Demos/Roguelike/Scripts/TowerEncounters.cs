using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Monsters of a long floor: ground packs spread from the first hills to the exit lane, some ranged members posted
    // on a platform above their pack, flyers perched on the high shelves and the boss holding the last stretch.
    // Monsters further ahead than the next screen are created as the hero approaches, one per frame, so entering a
    // floor never stalls on building every actor at once.
    public sealed partial class TowerGame
    {
        private sealed class PendingSpawn { public TowerMonsterDefinition definition; public int kind,ordinal; public Vector2 pos; public bool elite; }
        private readonly List<PendingSpawn> pendingSpawns=new List<PendingSpawn>();
        internal const float SpawnAhead=24,PackSpacing=1.7f,BossStandoff=6;
        /// <summary>Monsters alive on the floor plus those still waiting further ahead.</summary>
        internal int RemainingEnemies{get{return enemies.Count+pendingSpawns.Count;}}
        internal int PendingEnemies{get{return pendingSpawns.Count;}}
        /// <summary>Fixtures receive the whole floor at once unless they measure play as it streams.</summary>
        internal bool StreamOverride;
        internal void DropPendingSpawns(){pendingSpawns.Clear();}

        private void SpawnEnemies(int count,bool elite,bool bossFloor,List<TowerMonsterDefinition> roster)
        {
            var random=new System.Random(unchecked(seed*7741^run.Floor*104729));
            float first=-1.5f,last=TowerWorld.ExitX-(bossFloor?BossStandoff+12:7);
            int packs=Mathf.Max(1,Mathf.CeilToInt(count/2.6f));
            var plan=new List<PendingSpawn>();
            for(int p=0,index=0;p<packs;p++)
            {
                int members=count*(p+1)/packs-count*p/packs;
                float centre=Mathf.Lerp(first,last,packs==1?.5f:p/(float)(packs-1))+((float)random.NextDouble()-.5f)*2.4f;
                for(int m=0;m<members;m++,index++)
                {
                    var definition=roster[index%roster.Count];
                    float x=Mathf.Clamp(centre+(m-(members-1)*.5f)*PackSpacing,first-1,last+2);
                    var spot=new Vector2(x,world.GroundAt(x));
                    if(definition.Kind==1&&random.NextDouble()<.55)spot=PlatformPost(x,spot);
                    plan.Add(new PendingSpawn{definition=definition,kind=definition.Kind,ordinal=index,pos=spot,elite=elite});
                }
            }
            // Flyers perch on the high shelves nearest their share of the floor, or hover once every shelf is taken.
            int flyers=TowerDifficulty.FlyerCount(DifficultyFloor(run.Floor));var used=new HashSet<int>();
            var flyer=run.Theme.Monsters[run.Theme.AirborneVariant];
            for(int f=0;f<flyers;f++)
            {
                float target=Mathf.Lerp(first+4,last,(f+.5f)/flyers);int best=-1;float nearest=float.PositiveInfinity;
                for(int i=0;i<world.Ledges.Length;i++)
                {
                    var ledge=world.Ledges[i];float distance=Mathf.Abs(ledge.x-target);
                    if(ledge.y<2||used.Contains(i)||distance>=nearest)continue;
                    best=i;nearest=distance;
                }
                Vector2 spot;
                if(best>=0){used.Add(best);spot=new Vector2(world.Ledges[best].x,world.Ledges[best].y+.8f);}
                else spot=new Vector2(target,world.GroundAt(target)+3.8f);
                plan.Add(new PendingSpawn{definition=flyer,kind=3,ordinal=count+f,pos=spot,elite=elite});
            }
            plan.Sort((a,b)=>a.pos.x.CompareTo(b.pos.x));
            // Fixtures expect the whole floor at once; play streams everything past the next screen.
            foreach(var spawn in plan)
                if(SmokeControlled&&!StreamOverride||spawn.pos.x<hero.pos.x+SpawnAhead)SpawnNow(spawn);else pendingSpawns.Add(spawn);
            if(bossFloor)
            {
                // The expedition's last floor holds its chapter's climax: the evil god in the abyss, an awakened guardian
                // elsewhere. Restored chapters passed on the way up keep their ordinary bosses.
                bool climax=run.IsCampaign&&run.Floor==run.MaxFloor,final=climax&&run.Theme.FinalBoss!=null;
                float x=TowerWorld.ExitX-BossStandoff;
                var definition=final?run.Theme.FinalBoss:run.Theme.Boss;var boss=Spawn(definition.Name,4,true,new Vector2(x,world.GroundAt(x)));
                boss.monster=definition;boss.id=definition.Id;boss.theme=run.Theme.Id;boss.art.SetThemeMonster(boss.theme,final?TowerThemeCatalog.FinalVariant:0,true);
                ConfigureBoss(boss,run.Floor);
                if(climax&&!final){boss.title=definition.Name+" · 각성";boss.hp=boss.maxHp*=AwakenedBossHealth;}
                enemies.Add(boss);
            }
        }

        /// <summary>A spot on the nearest low platform over <paramref name="x"/>, or <paramref name="fallback"/> when none is close.</summary>
        private Vector2 PlatformPost(float x,Vector2 fallback)
        {
            int best=-1;float nearest=2.5f;
            for(int i=0;i<world.Ledges.Length;i++)
            {
                var ledge=world.Ledges[i];
                if(world.CurrentLayout.Platforms[i].Tier>2||ledge.width<2.6f)continue;
                float distance=Mathf.Abs(Mathf.Clamp(x,ledge.Left+.5f,ledge.Right-.5f)-x);
                if(distance<nearest){nearest=distance;best=i;}
            }
            if(best<0)return fallback;
            var post=world.Ledges[best];float px=Mathf.Clamp(x,post.Left+.5f,post.Right-.5f);
            return new Vector2(px,post.HeightAt(px));
        }

        private Fighter SpawnNow(PendingSpawn spawn)
        {
            var e=Spawn(spawn.definition.Name,spawn.kind,true,spawn.pos);
            e.monster=spawn.definition;e.id=spawn.definition.Id;e.theme=run.Theme.Id;
            e.art.SetThemeMonster(e.theme,spawn.definition.Variant,false);
            ConfigureEnemy(e,spawn.kind,spawn.ordinal);if(spawn.elite)MakeElite(e);
            enemies.Add(e);return e;
        }

        /// <summary>A thin track of the floor from spawn to gate: monsters on it, those still ahead, the boss and the hero.</summary>
        private void DrawFloorTrack(Rect r)
        {
            float left=TowerWorld.Left+.6f,right=TowerWorld.ExitX,mid=r.y+r.height*.5f;
            System.Func<float,float> at=x=>r.x+r.width*Mathf.Clamp01((x-left)/(right-left));
            Color foe=new Color(.95f,.36f,.3f);
            Round(r,new Color(.02f,.025f,.04f,.85f),3);
            float reached=at(hero.pos.x)-r.x-2;if(reached>0)Round(new Rect(r.x+1,r.y+1,reached,r.height-2),Alpha(gold,.4f),2);
            foreach(var p in pendingSpawns)Round(new Rect(at(p.pos.x)-1.5f,mid-1.5f,3,3),Alpha(foe,.4f),1);
            foreach(var e in enemies)if(e.hp>0&&!e.isBoss)Round(new Rect(at(e.pos.x)-2,mid-2,4,4),foe,2);
            var boss=enemies.Find(e=>e.isBoss&&e.hp>0);if(boss!=null)Diamond(new Vector2(at(boss.pos.x),mid),7,foe);
            Diamond(new Vector2(r.xMax,mid),5,run.IsFloorCleared?gold:muted);
            Diamond(new Vector2(at(hero.pos.x),mid),5,paper);
        }

        /// <summary>Creates the next queued monster once the hero comes within <see cref="SpawnAhead"/> of it.</summary>
        private void TickSpawnQueue()
        {
            if(pendingSpawns.Count==0||pendingSpawns[0].pos.x>hero.pos.x+SpawnAhead)return;
            var next=pendingSpawns[0];pendingSpawns.RemoveAt(0);
            using(TowerProfiling.Actors.Auto())SpawnNow(next);
        }
    }
}
