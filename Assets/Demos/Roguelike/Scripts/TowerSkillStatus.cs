using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Statuses the party's skills leave on enemies and the reactions between them, so one companion's skill sets up another's:
    // lightning on the slowed (superconduct), fire on the slowed or frost on the burning (steam), poison on the burning (toxic),
    // melee skills on the stunned (defenseless); marks raise every blow and curses, marks and seeds pass on when a host falls.
    public sealed partial class TowerGame
    {
        private const float ReactionGap=.35f,BurnShare=.12f,BurnSeconds=2.5f;
        private readonly List<Fighter> statusScratch=new List<Fighter>();
        /// <summary>Reactions set off so far (each struck enemy counts), for the practice range and its checks.</summary>
        internal int ReactionCount;
        private static readonly string[] ReactionNames={"초전도","증기 폭발","무방비","독연","전이"};
        private static readonly Color[] ReactionColors={new Color(.62f,.78f,1),new Color(.92f,.95f,1),new Color(1,.82f,.4f),new Color(.7f,1,.38f),new Color(.8f,.55f,1)};

        private void TickSkillStatuses(float dt)
        {
            statusScratch.Clear();statusScratch.AddRange(enemies);
            foreach(var e in statusScratch)
            {
                if(e.hp<=0)continue;
                if(e.markTime>0){e.markTime-=dt;if(e.markTime<=0)e.markSpread=false;}
                if(e.burnTime>0){e.burnTime-=dt;e.burnClock-=dt;if(e.burnClock<=0){e.burnClock+=.5f;DotStrike(e,e.burnDps*.5f,e.burnOwner,e.burnCast);}}
                if(e.hp>0&&e.curseTime>0){e.curseTime-=dt;e.curseClock-=dt;if(e.curseClock<=0){e.curseClock+=.5f;DotStrike(e,e.curseDps*.5f,e.curseOwner,e.curseCast);}}
                if(e.hp>0&&e.seedTime>0){e.seedTime-=dt;if(e.seedTime<=0)Blossom(e,e.pos);}
            }
            statusScratch.Clear();
        }
        // Damage over time counts toward the cast that applied it but never sets off reactions or new statuses.
        private void DotStrike(Fighter enemy,float damage,HeroState owner,int cast)
        {
            if(damage<=0||(owner!=null&&!IsCurrentHero(owner)))return;
            var saved=SaveStrike();striking=false;practiceStrike=cast;
            StrikeEnemy(enemy,damage,owner,0);
            RestoreStrike(saved);
        }

        /// <summary>Damage factor for a blow from <paramref name="owner"/>: marks and banners for every blow, reactions for skill hits.</summary>
        private float SkillReactionScale(Fighter enemy,HeroState owner)
        {
            float scale=1;
            if(enemy.markTime>0)scale*=1.2f;
            if(owner!=null&&BannerEmpowers(owner))scale*=1.15f;
            if(!striking)return scale;
            var element=TowerSkillBuilds.Element(strikeEffect);float bonus;TowerVfx.ReactionKind kind;
            if(element==SkillElement.Lightning&&(enemy.slowTime>0||enemy.stunTime>0)){bonus=1.5f;kind=TowerVfx.ReactionKind.Superconduct;}
            else if(element==SkillElement.Fire&&enemy.slowTime>0){bonus=1.4f;kind=TowerVfx.ReactionKind.Steam;enemy.slowTime=0;}
            else if(element==SkillElement.Frost&&enemy.burnTime>0){bonus=1.4f;kind=TowerVfx.ReactionKind.Steam;Extinguish(enemy);}
            else if(element==SkillElement.Poison&&enemy.burnTime>0){bonus=1.5f;kind=TowerVfx.ReactionKind.Toxic;}
            else if(TowerSkillBuilds.Style(strikeEffect)==SkillStyle.Melee&&enemy.stunTime>0){bonus=1.35f;kind=TowerVfx.ReactionKind.Defenseless;}
            else return scale;
            ReactionCount++;ShowReaction(enemy,kind);
            return scale*bonus;
        }
        private void ShowReaction(Fighter enemy,TowerVfx.ReactionKind kind)
        {
            if(elapsed-enemy.reactionAt<ReactionGap)return;enemy.reactionAt=elapsed;
            int k=(int)kind;FloatText(enemy.pos+Vector2.up*(enemy.isBoss?3.3f:2.6f),ReactionNames[k]+"!",ReactionColors[k]);
            if(vfx!=null)vfx.Reaction(enemy.pos+Vector2.up*(enemy.isBoss?1.4f:.9f),kind);
        }
        // Statuses a skill hit leaves behind, and what passes on when the enemy falls.
        private void AfterSkillStrike(Fighter enemy,HeroState owner,float damage)
        {
            if(enemy.hp<=0)return;
            if(striking&&TowerSkillBuilds.Element(strikeEffect)==SkillElement.Fire)ApplyBurn(enemy,owner,damage*BurnShare,BurnSeconds);
        }
        // Called once as an enemy falls (DamageEnemy), however the last blow landed.
        private void StatusesOnFall(Fighter fallen)
        {
            Vector2 at=fallen.pos;
            if(vfx!=null&&fallen.root)for(int k=0;k<=(int)TowerVfx.StatusKind.Seed;k++)vfx.ClearStatus(fallen.root.transform,(TowerVfx.StatusKind)k);
            if(fallen.seedTime>0)Blossom(fallen,at);
            if(fallen.curseTime>0&&fallen.curseSpread>0&&fallen.curseOwner!=null&&IsCurrentHero(fallen.curseOwner))
            {
                float left=Mathf.Max(1.2f,fallen.curseTime*.75f);fallen.curseTime=0;
                foreach(var next in FallTargets(at,6,fallen.curseSpread))
                {
                    if(vfx!=null)vfx.Trail(at+Vector2.up*.9f,next.pos+Vector2.up*.9f,SkillEffect.Curse,SkillColor(SkillEffect.Curse));
                    if(vfx!=null)vfx.Grade=fallen.curseSpread>1?3:1;
                    ApplyCurse(next,fallen.curseOwner,fallen.curseDps,left,fallen.curseSpread);ShowReaction(next,TowerVfx.ReactionKind.Spread);
                    if(vfx!=null)vfx.Grade=striking?strikeLevel:1;
                }
            }
            if(fallen.markTime>0&&fallen.markSpread)
            {
                float left=fallen.markTime;fallen.markTime=0;
                foreach(var next in FallTargets(at,8,1))
                {
                    if(vfx!=null)vfx.Trail(at+Vector2.up*.9f,next.pos+Vector2.up*.9f,SkillEffect.HuntMark,SkillColor(SkillEffect.HuntMark));
                    if(vfx!=null)vfx.Grade=3;
                    ApplyMark(next,left,true);ShowReaction(next,TowerVfx.ReactionKind.Spread);
                    if(vfx!=null)vfx.Grade=striking?strikeLevel:1;
                }
            }
        }
        private List<Fighter> FallTargets(Vector2 at,float range,int count)
        {
            var found=enemies.FindAll(e=>CanTarget(e,null)&&Vector2.Distance(e.pos,at)<=range);
            found.Sort((a,b)=>Vector2.Distance(a.pos,at).CompareTo(Vector2.Distance(b.pos,at)));
            if(found.Count>count)found.RemoveRange(count,found.Count-count);
            return found;
        }

        // ---------------------------------------------------------------- applying statuses

        private int StatusCast{get{return practiceStrike>0?practiceStrike:PracticeCasts;}}
        private void ShowStatus(Fighter enemy,TowerVfx.StatusKind kind,float seconds,SkillEffect look)
        {if(vfx!=null&&enemy.root)vfx.Status(enemy.root.transform,kind,seconds,SkillColor(look));}
        private void Stun(Fighter enemy,float seconds,bool freeze)
        {
            if(enemy==null||enemy.hp<=0||seconds<=0)return;
            // Bosses shrug holds off entirely: any stun cancels a boss pattern, so even a short one would lock them down.
            if(enemy.isBoss)return;
            enemy.stunTime=Mathf.Max(enemy.stunTime,seconds);
            ShowStatus(enemy,freeze?TowerVfx.StatusKind.Freeze:TowerVfx.StatusKind.Stun,enemy.stunTime,freeze?SkillEffect.IceNova:SkillEffect.Quake);
        }
        private void ApplyBurn(Fighter enemy,HeroState owner,float dps,float seconds)
        {
            if(dps<=0)return;
            enemy.burnDps=enemy.burnTime>0?Mathf.Max(enemy.burnDps,dps):dps;enemy.burnTime=Mathf.Max(enemy.burnTime,seconds);enemy.burnOwner=owner;
            if(enemy.burnClock<=0)enemy.burnClock=.5f;enemy.burnCast=StatusCast;
            ShowStatus(enemy,TowerVfx.StatusKind.Burn,enemy.burnTime,SkillEffect.Fireball);
        }
        private void Extinguish(Fighter enemy)
        {enemy.burnTime=0;if(vfx!=null&&enemy.root)vfx.ClearStatus(enemy.root.transform,TowerVfx.StatusKind.Burn);}
        private void ApplyMark(Fighter enemy,float seconds,bool spread)
        {
            enemy.markTime=Mathf.Max(enemy.markTime,seconds);enemy.markSpread|=spread;
            ShowStatus(enemy,TowerVfx.StatusKind.Mark,enemy.markTime,SkillEffect.HuntMark);
        }
        private void ApplyCurse(Fighter enemy,HeroState owner,float dps,float seconds,int spread)
        {
            enemy.curseDps=enemy.curseTime>0?Mathf.Max(enemy.curseDps,dps):dps;enemy.curseTime=Mathf.Max(enemy.curseTime,seconds);enemy.curseOwner=owner;
            enemy.curseSpread=Mathf.Max(enemy.curseTime>seconds?enemy.curseSpread:0,spread);if(enemy.curseClock<=0)enemy.curseClock=.5f;enemy.curseCast=StatusCast;
            ShowStatus(enemy,TowerVfx.StatusKind.Curse,enemy.curseTime,SkillEffect.Curse);
        }
        private void ApplySeed(Fighter enemy,HeroState owner,float power,float radius,float delay,int level)
        {
            bool doubled=TowerSkillBuilds.Awakened(level);if(enemy.seedTime<=0||level>enemy.seedLevel)enemy.seedLevel=level;
            bool growing=enemy.seedTime>0;
            enemy.seedTime=growing?enemy.seedTime:Mathf.Max(.2f,delay);enemy.seedPower=Mathf.Max(growing?enemy.seedPower:0,power);enemy.seedRadius=radius;enemy.seedOwner=owner;
            enemy.seedDouble=growing&&enemy.seedDouble||doubled;if(!growing)enemy.seedCast=StatusCast;
            ShowStatus(enemy,TowerVfx.StatusKind.Seed,enemy.seedTime,SkillEffect.Bloom);
        }
        // A seed bursts where its host stands (or fell): it strikes nearby enemies and heals the player.
        private void Blossom(Fighter host,Vector2 at)
        {
            var owner=host.seedOwner;float power=host.seedPower,radius=host.seedRadius;bool doubled=host.seedDouble;
            host.seedTime=0;host.seedPower=0;host.seedDouble=false;host.seedOwner=null;
            if(vfx!=null&&host.root)vfx.ClearStatus(host.root.transform,TowerVfx.StatusKind.Seed);
            if(owner==null||!IsCurrentHero(owner)||power<=0)return;
            var saved=SaveStrike();BeginStrike(SkillEffect.Bloom,Mathf.Max(1,host.seedLevel),host.seedCast);
            foreach(var e in enemies.ToArray())if(CanTarget(e,owner)&&Vector2.Distance(e.pos,at)<radius)StrikeEnemy(e,power,owner,0);
            HealPlayer(power*.5f*(doubled?2:1)*owner.HealingMultiplier,true);
            if(vfx!=null)vfx.Impact(SkillEffect.Bloom,at+Vector2.up*.8f,radius,SkillColor(SkillEffect.Bloom),0);
            RestoreStrike(saved);
        }
    }
}
