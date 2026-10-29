using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // The skill practice range: a level yard where the hero casts any of the skill effects on demand, at any level and without
    // cooldowns, at standing monsters of a chosen mythology that never strike back and never fall. A status can be kept on the
    // targets to try the reactions. It opens from the title and from the village training yard, and leaving it returns to the village.
    public sealed partial class TowerGame
    {
        // Skills grouped by build style (melee, ranged, automatic, spell, support: TowerSkillBuilds.Style), in bar order.
        internal static readonly SkillEffect[] PracticeEffects={
            SkillEffect.Shockwave,SkillEffect.BladeStorm,SkillEffect.DashStrike,SkillEffect.Hook,SkillEffect.Quake,
            SkillEffect.MultiShot,SkillEffect.PierceShot,SkillEffect.RainArrows,SkillEffect.ArcaneVolley,SkillEffect.Boomerang,SkillEffect.Ricochet,SkillEffect.HuntMark,
            SkillEffect.Sentry,SkillEffect.Orbit,SkillEffect.Decoy,SkillEffect.Mine,
            SkillEffect.Fireball,SkillEffect.Meteor,SkillEffect.IceNova,SkillEffect.ChainLightning,SkillEffect.PoisonCloud,SkillEffect.Vortex,SkillEffect.Curse,
            SkillEffect.HealPulse,SkillEffect.ShieldPulse,SkillEffect.LifeDrain,SkillEffect.WarBanner,SkillEffect.Bloom};
        private static int PracticeStyleOf(int index){return (int)TowerSkillBuilds.Style(PracticeEffects[index]);}
        // Names by SkillEffect: the hero's own cards keep theirs, the companions' effects get a plain one.
        private static readonly string[] PracticeNames={"화염구","빙결 고리","연쇄 번개","낙성","비전 연사","검의 파동","검의 폭풍","섬광 돌진",
            "산개 사격","관통 사격","화살비","독안개","치유 파동","보호막","생명 흡수","자동 포탑",
            "귀환 원반","도탄 사격","사냥 표식","사슬 갈고리","지맥 균열","수호 구체","환영 분신","봉인 지뢰","소용돌이","저주 낙인","전투 깃발","생명꽃 씨앗"};
        // A status the range keeps on its targets, so reactions can be tried: none, slowed, burning, stunned, marked.
        internal static readonly string[] PracticeStatusNames={"없음","둔화","화상","기절","표식"};
        internal int PracticeStatus;
        private float practiceStatusShown;
        internal static readonly int[] PracticeTargetCounts={1,3,5};
        internal const float PracticeHomeX=0,PracticeTargetHealth=900;
        // The camera looks ahead to the targets; an automatic repeat waits this long after the last cast has settled.
        private const float PracticeCameraLead=3.2f,PracticeRepeatPause=.9f;
        // The ground sits 0.4 of the view half-height below centre, whatever the pixel-perfect zoom.
        private float PracticeCameraY{get{return TowerWorld.Ground+.4f*view.orthographicSize;}}
        private sealed class PracticeTarget { public Fighter actor;public float post,quiet; }
        private readonly List<PracticeTarget> practiceTargets=new List<PracticeTarget>();
        private static readonly Dictionary<SkillEffect,AugmentDefinition> practiceDefinitions=new Dictionary<SkillEffect,AugmentDefinition>();
        internal bool practice,PracticeAuto;
        internal int PracticeIndex,PracticeLevel=1,PracticeTargetCount=3,PracticeCasts,PracticeHits;
        internal TowerThemeId PracticeTheme=TowerThemeId.Olympus;
        // Damage the latest cast has dealt. Shots and zones remember the cast that made them (practiceCast), and
        // practiceStrike names the cast striking right now: 0 for a skill's own release, -1 for the sword, otherwise the
        // cast number of the striking shot or zone. Hits of earlier casts' sentries and clouds, and sword hits, are not counted.
        internal float PracticeDamage;
        private int practiceStrike;
        private float practiceQuiet,practiceCastFrom;
        // practiceDashed: the latest cast was a dash strike that began at practiceCastFrom.
        private bool practiceQueued,practiceDashed;
        private SkillState practiceSkill;private HeroState practiceSkillOwner;
        internal SkillEffect PracticeEffect{get{return PracticeEffects[PracticeIndex];}}
        internal int PracticeTargetsStanding{get{int n=0;foreach(var t in practiceTargets)if(t.actor.hp>0&&enemies.Contains(t.actor))n++;return n;}}
        internal static string PracticeName(SkillEffect effect){return PracticeNames[(int)effect];}

        /// <summary>Opens the practice range from the title or the village; its settings carry over between visits.</summary>
        internal void EnterPractice()
        {
            seed=Environment.TickCount&0x7fffffff;
            ResetActors();
            practice=true;intro=false;oathLobby=false;
            run=campaign.Preview(seed,new string[0]);
            SpawnHero(new Vector2(PracticeHomeX,TowerWorld.Ground));
            BeginFloorRecord();
            BuildPracticeYard(true);
            Notice("스킬 연습장  ·  아래 스킬을 누르거나 F로 시전  ·  Esc 메뉴에서 마을로",5);
        }
        private void ClearPractice(){practice=false;practiceQueued=false;practiceStrike=0;practiceTargets.Clear();PracticeDamage=0;PracticeHits=0;}
        /// <summary>Back to the village the way the title's adventure button goes, so a new save still opens on its prologue.</summary>
        internal void LeavePractice(){BeginCampaign();inspectedOwner="player";}

        /// <summary>Flat ground without ledges, so every target stands at the same height.</summary>
        private static TowerFloorLayout PracticeLayout()
        {
            var ground=new TowerSurface(new[]{TowerTerrainGenerator.TerrainLeft,TowerTerrainGenerator.TerrainRight},new[]{TowerWorld.Ground,TowerWorld.Ground});
            return new TowerFloorLayout(9191,"스킬 연습장",1,ground,new List<TowerPlatform>());
        }
        /// <summary>
        /// Clears the yard's effects and stands fresh targets at their posts with the hero at home; with
        /// <paramref name="scenery"/> it first raises the chosen mythology's backdrop and music.
        /// </summary>
        private void BuildPracticeYard(bool scenery)
        {
            foreach(var t in practiceTargets){enemies.Remove(t.actor);RemoveEnemyCombat(t.actor);if(t.actor.root)Destroy(t.actor.root);}
            practiceTargets.Clear();practiceQueued=practiceDashed=false;
            foreach(var s in shots)if(s.root)Destroy(s.root);shots.Clear();ClearFloorSkillEffects();
            playerShield=0;shieldTime=0;attackTimer=0;pendingSlash=false;
            if(scenery)
            {
                int floor=1;for(int i=0;i<TowerThemeCatalog.All.Count;i++)if(TowerThemeCatalog.All[i].Id==PracticeTheme)floor=i*TowerThemeCatalog.FloorsPerTheme+1;
                using(TowerProfiling.SetTheme.Auto()){world.SetTheme(PracticeTheme,floor,seed,PracticeLayout());world.SetFloorFeature(FloorType.Normal,false);}
                music.PlayTheme(PracticeTheme);
                cameraCeiling=Mathf.Max(1.8f,world.HighestPlatform+4);
            }
            PlacePracticeHero(PracticeHomeX);hero.facing=1;hero.hp=PlayerMaxHealth;
            cameraLead=PracticeCameraLead;view.transform.position=new Vector3(PracticeHomeX+PracticeCameraLead,PracticeCameraY,-10);appliedShake=Vector3.zero;
            // Melee, ranged and heavy species stand in turn; five targets stand closer together to stay in view.
            // The nearest one stands inside the smallest level-one radius (Blade Storm's 3), so every skill reaches it.
            var theme=TowerThemeCatalog.Get(PracticeTheme);int count=PracticeTargetCount;
            float spacing=count>=5?1.7f:2.2f,first=2.2f;
            for(int i=0;i<count;i++)
            {
                var definition=theme.Monsters[i%theme.Monsters.Count];float x=first+i*spacing;
                var e=Spawn(definition.Name,definition.Kind,true,new Vector2(x,TowerWorld.Ground));
                e.monster=definition;e.id=definition.Id;e.theme=PracticeTheme;e.art.SetThemeMonster(PracticeTheme,definition.Variant,false);
                ConfigureEnemy(e,definition.Kind,i);
                e.hp=e.maxHp=PracticeTargetHealth;e.facing=-1;
                enemies.Add(e);practiceTargets.Add(new PracticeTarget{actor=e,post=x});
            }
            PracticeDamage=0;PracticeHits=0;practiceQuiet=0;
        }
        private void PlacePracticeHero(float x)
        {
            hero.pos=new Vector2(x,world.GroundAt(x));hero.velocityY=0;hero.dropTime=0;hero.grounded=true;hero.support=-1;hero.lastX=x;
            hero.root.transform.position=hero.pos;previousHeroPosition=hero.pos;dashTime=0;
        }

        internal void SelectPractice(int index){int n=PracticeEffects.Length;PracticeIndex=(index%n+n)%n;practiceQuiet=0;}
        internal void SetPracticeLevel(int level){PracticeLevel=Mathf.Clamp(level,1,3);}
        internal void SetPracticeTargets(int count){if(count==PracticeTargetCount)return;PracticeTargetCount=count;BuildPracticeYard(false);}
        internal void ShiftPracticeTheme(int step)
        {
            int n=TowerThemeCatalog.All.Count,at=0;
            for(int i=0;i<n;i++)if(TowerThemeCatalog.All[i].Id==PracticeTheme)at=i;
            PracticeTheme=TowerThemeCatalog.All[((at+step)%n+n)%n].Id;BuildPracticeYard(true);
        }
        internal void ResetPracticeYard(){BuildPracticeYard(false);}
        internal void SetPracticeStatus(int status)
        {
            PracticeStatus=Mathf.Clamp(status,0,PracticeStatusNames.Length-1);practiceStatusShown=0;
            foreach(var t in practiceTargets)
            {
                var e=t.actor;e.slowTime=e.stunTime=e.markTime=0;Extinguish(e);
                if(vfx!=null&&e.root){vfx.ClearStatus(e.root.transform,TowerVfx.StatusKind.Stun);vfx.ClearStatus(e.root.transform,TowerVfx.StatusKind.Mark);}
            }
        }
        // Keeps the chosen status on every target (a harmless burn, a long slow, a held stun or a mark), shown every half second.
        private void KeepPracticeStatus(float dt)
        {
            if(PracticeStatus==0)return;
            practiceStatusShown-=dt;bool show=practiceStatusShown<=0;if(show)practiceStatusShown=.5f;
            foreach(var t in practiceTargets)
            {
                var e=t.actor;if(e.hp<=0)continue;
                switch(PracticeStatus)
                {
                    case 1:e.slowTime=Mathf.Max(e.slowTime,1);break;
                    case 2:e.burnTime=Mathf.Max(e.burnTime,1);e.burnDps=0;if(show)ShowStatus(e,TowerVfx.StatusKind.Burn,1,SkillEffect.Fireball);break;
                    case 3:e.stunTime=Mathf.Max(e.stunTime,1);if(show)ShowStatus(e,TowerVfx.StatusKind.Stun,1,SkillEffect.Quake);break;
                    case 4:e.markTime=Mathf.Max(e.markTime,1);if(show)ShowStatus(e,TowerVfx.StatusKind.Mark,1,SkillEffect.HuntMark);break;
                }
            }
        }

        /// <summary>The hero's own card for an effect, or else the first companion card that casts it, carried by the hero.</summary>
        private static AugmentDefinition PracticeDefinition(SkillEffect effect)
        {
            AugmentDefinition definition;if(practiceDefinitions.TryGetValue(effect,out definition))return definition;
            AugmentDefinition source=null;
            foreach(var a in TowerRun.Augments)
                if(a.Kind==RewardKind.Skill&&a.Effect==effect&&(source==null||a.OwnerId=="player"&&source.OwnerId!="player"))source=a;
            definition=new AugmentDefinition("practice."+effect,"player",PracticeName(effect),source.Description,source.Tier,effect,source.Cooldown,source.Power,source.Radius,source.Count,source.Duration);
            practiceDefinitions[effect]=definition;return definition;
        }
        /// <summary>The selected skill at the selected level, scaled by the hero's own stats and skill tree.</summary>
        internal SkillState PracticeSkill()
        {
            var effect=PracticeEffect;
            if(practiceSkill==null||practiceSkill.Definition.Effect!=effect||practiceSkill.Level!=PracticeLevel||!ReferenceEquals(practiceSkillOwner,run.Player))
            {practiceSkill=new SkillState(PracticeDefinition(effect),run.Player,PracticeLevel);practiceSkillOwner=run.Player;}
            return practiceSkill;
        }

        /// <summary>Casts at once when the hero is free, or right after the current swing or cast.</summary>
        internal void QueuePractice(){if(!CastPractice())practiceQueued=true;}
        /// <summary>Winds up and releases the selected skill at the nearest target, with no cooldown; false while the hero is busy.</summary>
        internal bool CastPractice()
        {
            if(!practice||Paused||PracticeBusy)return false;
            var target=PracticeAim();if(target==null)return false;
            var effect=PracticeEffect;
            // Heals only read on a wounded hero.
            if((effect==SkillEffect.HealPulse||effect==SkillEffect.LifeDrain||effect==SkillEffect.Bloom)&&hero.hp>PlayerMaxHealth*.6f)hero.hp=PlayerMaxHealth*.5f;
            // Orbs circle the hero closely: step up to the target first, the way a fighter would.
            if(effect==SkillEffect.Orbit&&Mathf.Abs(target.pos.x-hero.pos.x)>1.6f)
            {PlacePracticeHero(target.pos.x-Mathf.Sign(target.pos.x-hero.pos.x)*1.3f);if(vfx!=null)vfx.Dust(hero.pos,0,.5f);}
            PracticeDamage=0;PracticeHits=0;PracticeCasts++;practiceCastFrom=hero.pos.x;practiceDashed=effect==SkillEffect.DashStrike;practiceQuiet=0;practiceQueued=false;
            BeginSkillCast(new SkillClock{owner=run.Player,skill=PracticeSkill()},hero,target);
            return true;
        }
        internal bool PracticeBusy
        {
            get
            {
                if(hero.castRemaining>0||dashTime>0||attackTimer>0)return true;
                foreach(var pending in pendingSkills)if(pending.source==hero)return true;
                return false;
            }
        }
        /// <summary>The nearest standing target ahead of the hero, or behind when none stands ahead.</summary>
        private Fighter PracticeAim()
        {
            Fighter best=null;float score=float.PositiveInfinity;
            foreach(var t in practiceTargets)
            {
                var e=t.actor;if(!CanTarget(e,run.Player))continue;
                float dx=e.pos.x-hero.pos.x,s=Mathf.Abs(dx)+(dx*hero.facing<0?100:0);
                if(s<score){score=s;best=e;}
            }
            return best;
        }

        private void TickPractice(float dt)
        {
            if(!SmokeControlled)
            {
                if(Input.GetKeyDown(KeyCode.R)){ResetPracticeYard();return;}
                if(Input.GetKeyDown(KeyCode.Q))SelectPractice(PracticeIndex-1);
                if(Input.GetKeyDown(KeyCode.E))SelectPractice(PracticeIndex+1);
                if(Input.GetKeyDown(KeyCode.Alpha1))SetPracticeLevel(1);
                if(Input.GetKeyDown(KeyCode.Alpha2))SetPracticeLevel(2);
                if(Input.GetKeyDown(KeyCode.Alpha3))SetPracticeLevel(3);
                if(Input.GetKeyDown(KeyCode.T))PracticeAuto=!PracticeAuto;
                if(Input.GetKeyDown(KeyCode.F))QueuePractice();
                if(Input.GetKeyDown(KeyCode.G))SetPracticeStatus((PracticeStatus+1)%PracticeStatusNames.Length);
            }
            KeepPracticeStatus(dt);
            bool busy=PracticeBusy;
            if(practiceQueued){if(!busy){practiceQueued=false;CastPractice();}return;}
            // The automatic repeat waits for the previous cast's shots and zones to finish, then a short beat.
            bool quiet=!busy&&skillAreas.Count==0;
            if(quiet)foreach(var s in shots)if(s.owner!=null){quiet=false;break;}
            if(!PracticeAuto||!quiet){practiceQuiet=0;return;}
            practiceQuiet+=dt;if(practiceQuiet<PracticeRepeatPause)return;
            // A repeated dash strike starts again from where the last one began.
            if(practiceDashed&&PracticeEffect==SkillEffect.DashStrike&&hero.grounded&&Mathf.Abs(hero.pos.x-practiceCastFrom)>.3f)
            {PlacePracticeHero(practiceCastFrom);if(vfx!=null)vfx.Dust(hero.pos,0,.5f);}
            CastPractice();
        }

        /// <summary>Practice targets stand at their posts: knocked back they slide home, and left alone they heal to full.</summary>
        private void TickPracticeTarget(Fighter actor,float dt)
        {
            actor.hurt=Mathf.Max(0,actor.hurt-dt);actor.slowTime=Mathf.Max(0,actor.slowTime-dt);actor.stunTime=Mathf.Max(0,actor.stunTime-dt);
            actor.windup=actor.attackPose=0;actor.facing=hero.pos.x>=actor.pos.x?1:-1;
            foreach(var t in practiceTargets)
            {
                if(t.actor!=actor)continue;
                t.quiet+=dt;if(t.quiet>1.6f)actor.hp=Mathf.MoveTowards(actor.hp,actor.maxHp,actor.maxHp*1.5f*dt);
                actor.pos.x=Mathf.MoveTowards(actor.pos.x,t.post,dt*2.5f);break;
            }
            StepGravity(actor,dt);actor.root.transform.position=actor.pos;
            actor.art.SetLocomotion(actor.grounded,actor.velocityY,false,0,0);
            actor.art.SetPose(false,false,actor.hurt>0,actor.facing,dt);
        }
        /// <summary>Counts a hit on a practice target; one brought to zero bursts like a kill and stands up again at full health.</summary>
        private void PracticeStruck(Fighter target,float damage)
        {
            if(practiceStrike==0||practiceStrike==PracticeCasts){PracticeDamage+=damage;PracticeHits++;}
            foreach(var t in practiceTargets)if(t.actor==target)t.quiet=0;
            if(target.hp>0)return;
            target.hp=target.maxHp;
            if(vfx!=null)vfx.KillBurst(target.pos+Vector2.up*.9f,TowerWorld.Teal);
            HitStop(.05f);
        }
    }
}
