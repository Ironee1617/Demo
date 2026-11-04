using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Pacing telemetry: every floor of a real climb appends one JSON line to persistentDataPath/tower-pace.jsonl when it is
    // left (or the hero falls), and F9 shows the current floor's figures. Fixtures (SmokeControlled) and runs that suppress
    // progress persistence neither sample nor write. Times are real (unscaled) seconds; observation only, no game rule reads it.
    public sealed partial class TowerGame
    {
        /// <summary>Where a hit on the hero came from; callers name it through <see cref="HurtHeroBy"/>.</summary>
        private enum PaceHit { Other, Melee, Bullet, Landing, Trap }
        private enum PaceStage { Idle, Running, Cleared }
        private sealed class PaceRecord
        {
            public int seed,chapter,floor,hits,dashes,kills,perfectDodges,counterHits,poiseBreaks,telegraphs,maxTelegraphs,maxEnemies,maxShots;
            public bool campaign,boss,branch,oath;
            public string type,weapon,outcome,deathCause;
            // Floor entry to clear: wall clock, paused share, simulated time and the unpaused fight.
            public float seconds,paused,gameSeconds,combat,empty,backtrack,invulnerable,hurtGuard;
            public float damagePlayer,damagePlayerSkill,damageCompanion,damageDot,damageOther;
            // After the clear: cards pending (reveal included), cards on screen, walking out, and all of it.
            public float rewardDwell,cards,exit,afterClear;
            public readonly int[] hitsBy=new int[5];
        }
        // Draws the overlay above the HUD from its own component, so TowerGame's OnGUI stays untouched.
        private sealed class PaceOverlay : MonoBehaviour
        {
            internal TowerGame game;
            private void Awake(){useGUILayout=false;}
            private void OnGUI(){GUI.depth=-20;if(game!=null&&Event.current.type==EventType.Repaint)game.DrawPaceOverlay();}
        }
        internal const string PaceFileName="tower-pace.jsonl";
        // A frame longer than PaceFrameCap (a hitch, a lost focus) counts as that long. PaceHurtGuard matches HurtHero's invulnerability.
        private const float PaceFrameCap=.25f,PaceHurtGuard=.65f;
        private static readonly string[] PaceHitNames={"other","melee","bullet","landing","trap"};
        private PaceRecord paceRecord=new PaceRecord();
        private PaceStage paceStage;
        private PaceHit paceHitSource,paceLastHit;
        private float paceFrame,paceLastX=float.NaN,paceHurtAt=-10;
        private int paceKillsAtStart,paceDodgesAtStart,paceCountersAtStart,paceBreaksAtStart;
        private readonly HashSet<Fighter> paceWinding=new HashSet<Fighter>();
        private bool paceOverlayShown;
        private PaceOverlay paceOverlay;
        private GUIStyle paceStyle;

        private bool PaceLogging{get{return !SmokeControlled&&!SuppressProgressPersistence;}}
        private bool PaceSampling{get{return paceStage==PaceStage.Running&&PaceLogging&&!village&&!practice;}}

        /// <summary>Hurts the hero and records where the blow came from.</summary>
        private void HurtHeroBy(PaceHit source,float damage)
        {paceHitSource=source;HurtHero(damage);paceHitSource=PaceHit.Other;}

        /// <summary>Starts the next floor's record (BeginFloorRecord); a cleared floor still waiting is written first.</summary>
        private void BeginPaceFloor()
        {
            FlushPace();
            paceRecord=new PaceRecord();paceStage=PaceStage.Running;
            paceLastX=float.NaN;paceHurtAt=-10;paceLastHit=paceHitSource=PaceHit.Other;paceWinding.Clear();
            paceKillsAtStart=kills;paceDodgesAtStart=PerfectDodges;paceCountersAtStart=CounterHits;paceBreaksAtStart=PoiseBreaks;
        }

        /// <summary>First thing every frame, paused or not: the F9 toggle, the floor's clock and what follows its clear.</summary>
        private void TickPace()
        {
            if(!SmokeControlled&&Input.GetKeyDown(KeyCode.F9))TogglePaceOverlay();
            paceFrame=Mathf.Min(Time.unscaledDeltaTime,PaceFrameCap);
            if(paceStage==PaceStage.Idle||!PaceLogging)return;
            if(village||practice){if(paceStage==PaceStage.Cleared)FlushPace();return;}
            var p=paceRecord;bool paused=Paused;
            if(paceStage==PaceStage.Running)
            {
                p.seconds+=paceFrame;if(paused)p.paused+=paceFrame;
                if(hero.hp<=0)ClosePaceFloor("death");
                return;
            }
            p.afterClear+=paceFrame;
            if(run.IsRewardPending){p.rewardDwell+=paceFrame;if(!ClearRevealing)p.cards+=paceFrame;}
            else if(!paused)p.exit+=paceFrame;
            if(hero.hp<=0){p.deathCause=PaceHitNames[(int)paceLastHit];FlushPace();}
        }

        /// <summary>Once per unpaused frame, right after the enemies move: the screen, the telegraphs and the hero's ground.</summary>
        private void SamplePace()
        {
            if(!PaceSampling||!view)return;
            var p=paceRecord;float real=paceFrame;
            p.combat+=real;
            if(float.IsNaN(paceLastX))paceLastX=hero.pos.x;
            if(hero.pos.x<paceLastX)p.backtrack+=paceLastX-hero.pos.x;
            paceLastX=hero.pos.x;
            if(invulnerable>0){p.invulnerable+=real;if(elapsed-paceHurtAt<PaceHurtGuard)p.hurtGuard+=real;}
            // The same chest point InView tests, against the camera's world rectangle.
            Vector3 low=view.ViewportToWorldPoint(Vector3.zero),high=view.ViewportToWorldPoint(Vector3.one);
            int seen=0,winding=0,bolts=0;
            foreach(var e in enemies)
            {
                if(e.hp<=0)continue;
                Vector2 chest=e.pos+Vector2.up*.8f;
                if(chest.x>low.x&&chest.x<high.x&&chest.y>low.y&&chest.y<high.y)seen++;
                if(e.windup>0){winding++;if(paceWinding.Add(e))p.telegraphs++;}
                else paceWinding.Remove(e);
            }
            foreach(var s in shots)if(s.hostile&&s.pos.x>low.x&&s.pos.x<high.x&&s.pos.y>low.y&&s.pos.y<high.y)bolts++;
            if(seen==0&&RemainingEnemies>0)p.empty+=real;
            p.maxEnemies=Mathf.Max(p.maxEnemies,seen);p.maxTelegraphs=Mathf.Max(p.maxTelegraphs,winding);p.maxShots=Mathf.Max(p.maxShots,bolts);
        }

        /// <summary>RecordFloorResult: the fight is over; the record keeps counting its cards and the walk out.</summary>
        private void PaceFloorCleared(){if(paceStage==PaceStage.Running&&PaceLogging)ClosePaceFloor("clear");}

        private void ClosePaceFloor(string outcome)
        {
            var p=paceRecord;
            p.outcome=outcome;p.seed=seed;p.campaign=run.IsCampaign;p.chapter=run.Chapter!=null?run.Chapter.Index+1:0;
            p.floor=run.Floor;p.type=run.FloorType.ToString();p.boss=run.FloorType==FloorType.Normal&&TowerDifficulty.IsBossFloor(run.Floor);
            p.branch=run.IsBranchFloor;p.oath=run.IsOathTrial;p.weapon=AttackMode.ToString();
            p.gameSeconds=Mathf.Max(0,elapsed-floorStartedAt);p.hits=FloorHits;p.kills=kills-paceKillsAtStart;
            p.perfectDodges=PerfectDodges-paceDodgesAtStart;p.counterHits=CounterHits-paceCountersAtStart;
            // Monsters the player's blows staggered out of a windup on this floor (TowerPoise.cs).
            p.poiseBreaks=PoiseBreaks-paceBreaksAtStart;
            if(hero.hp<=0)p.deathCause=PaceHitNames[(int)paceLastHit];
            paceStage=PaceStage.Cleared;
            if(hero.hp<=0)FlushPace();
        }

        /// <summary>Writes a closed record once; a floor left before its clear or fall is dropped. File errors are ignored.</summary>
        private void FlushPace()
        {
            if(paceStage!=PaceStage.Cleared)return;
            paceStage=PaceStage.Idle;
            if(!PaceLogging)return;
            try{File.AppendAllText(Path.Combine(Application.persistentDataPath,PaceFileName),PaceLine(paceRecord)+"\n");}
            catch(Exception){}
        }

        // DamageEnemy: who dealt it, read from the strike being resolved (TowerAction.cs).
        private void PaceDamage(Fighter e,float damage)
        {
            if(!PaceSampling)return;
            float dealt=Mathf.Min(Mathf.Max(0,e.hp),damage);if(dealt<=0)return;
            var p=paceRecord;
            if(lastStrikeDot)p.damageDot+=dealt;
            else if(lastStrikeOwner!=null&&lastStrikeOwner==run.Player){p.damagePlayer+=dealt;if(striking)p.damagePlayerSkill+=dealt;}
            else if(lastStrikeOwner is CompanionState)p.damageCompanion+=dealt;
            else p.damageOther+=dealt;
        }
        // HurtHero, as the hit is counted (FloorHits).
        private void PaceHurt()
        {
            var source=paceHitSource;paceHitSource=PaceHit.Other;
            if(!PaceLogging)return;
            paceLastHit=source;
            if(!PaceSampling)return;
            paceRecord.hitsBy[(int)source]++;paceHurtAt=elapsed;
        }
        // BeginDodgeCounter, which every dash start runs.
        private void PaceDash(){if(PaceSampling)paceRecord.dashes++;}

        private static string PaceLine(PaceRecord p)
        {
            float dealt=p.damagePlayer+p.damageCompanion+p.damageDot+p.damageOther;
            var b=new StringBuilder(900);
            b.Append("{\"v\":1");
            PaceText(b,"utc",DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture));
            PaceInt(b,"seed",p.seed);PaceFlag(b,"campaign",p.campaign);PaceInt(b,"chapter",p.chapter);PaceInt(b,"floor",p.floor);
            PaceText(b,"type",p.type);PaceFlag(b,"boss",p.boss);PaceFlag(b,"branch",p.branch);PaceFlag(b,"oath",p.oath);PaceText(b,"weapon",p.weapon);
            PaceText(b,"outcome",p.outcome);PaceText(b,"deathCause",p.deathCause);
            PaceFloat(b,"seconds",p.seconds);PaceFloat(b,"pausedSeconds",p.paused);PaceFloat(b,"gameSeconds",p.gameSeconds);PaceFloat(b,"combatSeconds",p.combat);
            PaceFloat(b,"emptyScreenSeconds",p.empty);PaceFloat(b,"emptyScreenRatio",PaceRatio(p.empty,p.combat));PaceFloat(b,"backtrackUnits",p.backtrack);
            PaceInt(b,"hits",p.hits);
            b.Append(",\"hitsBy\":{");
            for(int i=1;i<=PaceHitNames.Length;i++){int k=i%PaceHitNames.Length;if(i>1)b.Append(',');b.Append('"').Append(PaceHitNames[k]).Append("\":").Append(p.hitsBy[k].ToString(CultureInfo.InvariantCulture));}
            b.Append('}');
            PaceInt(b,"dashes",p.dashes);PaceInt(b,"kills",p.kills);PaceInt(b,"perfectDodges",p.perfectDodges);PaceInt(b,"counterHits",p.counterHits);PaceInt(b,"poiseBreaks",p.poiseBreaks);
            b.Append(",\"damage\":{\"player\":").Append(PaceNumber(p.damagePlayer));
            PaceFloat(b,"playerSkill",p.damagePlayerSkill);PaceFloat(b,"companion",p.damageCompanion);PaceFloat(b,"dot",p.damageDot);PaceFloat(b,"other",p.damageOther);
            b.Append('}');
            PaceFloat(b,"companionShare",PaceRatio(p.damageCompanion,dealt));
            PaceFloat(b,"invulnerableSeconds",p.invulnerable);PaceFloat(b,"invulnerableRatio",PaceRatio(p.invulnerable,p.combat));PaceFloat(b,"hurtGuardSeconds",p.hurtGuard);
            PaceInt(b,"telegraphs",p.telegraphs);PaceInt(b,"maxTelegraphs",p.maxTelegraphs);PaceInt(b,"maxEnemiesOnScreen",p.maxEnemies);PaceInt(b,"maxEnemyShotsOnScreen",p.maxShots);
            PaceFloat(b,"rewardDwell",p.rewardDwell);PaceFloat(b,"cardSeconds",p.cards);PaceFloat(b,"exitSeconds",p.exit);PaceFloat(b,"afterClearSeconds",p.afterClear);
            return b.Append('}').ToString();
        }
        private static float PaceRatio(float part,float whole){return whole>.0001f?part/whole:0;}
        private static string PaceNumber(float value)
        {return (float.IsNaN(value)||float.IsInfinity(value)?0:value).ToString("0.###",CultureInfo.InvariantCulture);}
        private static void PaceFloat(StringBuilder b,string key,float value){b.Append(",\"").Append(key).Append("\":").Append(PaceNumber(value));}
        private static void PaceInt(StringBuilder b,string key,int value){b.Append(",\"").Append(key).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));}
        private static void PaceFlag(StringBuilder b,string key,bool value){b.Append(",\"").Append(key).Append("\":").Append(value?"true":"false");}
        // Values are enum names and ISO dates, which never need escaping.
        private static void PaceText(StringBuilder b,string key,string value)
        {b.Append(",\"").Append(key).Append("\":");if(value==null)b.Append("null");else b.Append('"').Append(value).Append('"');}

        private void TogglePaceOverlay()
        {
            paceOverlayShown=!paceOverlayShown;
            if(paceOverlay==null){paceOverlay=gameObject.AddComponent<PaceOverlay>();paceOverlay.game=this;}
            paceOverlay.enabled=paceOverlayShown;
        }
        // Small text over the left edge of the play area, in the HUD's 1600x900 layout scaled to the screen.
        private void DrawPaceOverlay()
        {
            if(hero==null||run==null)return;
            float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);
            Vector2 offset=new Vector2((Screen.width-1600*scale)*.5f,(Screen.height-900*scale)*.5f);
            if(paceStyle==null)paceStyle=new GUIStyle{font=uiFont!=null?uiFont:Resources.Load<Font>("Tower/Font"),wordWrap=false,richText=false,alignment=TextAnchor.UpperLeft};
            paceStyle.fontSize=Mathf.Max(10,Mathf.RoundToInt(13*scale));paceStyle.normal.textColor=new Color(.86f,.93f,.9f);
            var saved=GUI.matrix;GUI.matrix=Matrix4x4.identity;
            var box=new Rect(offset.x+20*scale,offset.y+300*scale,330*scale,132*scale);
            GUI.color=new Color(0,0,0,.6f);GUI.DrawTexture(box,Texture2D.whiteTexture);GUI.color=Color.white;
            GUI.Label(new Rect(box.x+10*scale,box.y+8*scale,box.width-20*scale,box.height-16*scale),PaceOverlayText(),paceStyle);
            GUI.matrix=saved;
        }
        private string PaceOverlayText()
        {
            const string head="페이스 계측 · F9";
            if(!PaceLogging)return head+"\n기록 꺼짐 (픽스처)";
            if(village||practice)return head+"\n마을과 연습장은 기록하지 않습니다";
            var p=paceRecord;bool live=paceStage==PaceStage.Running;
            string state=live?"경과 "+CombatTime(Mathf.Max(0,p.seconds-p.paused))+"  ·  정지 "+PaceTenths(p.paused)+"초"
                :paceStage==PaceStage.Cleared?"클리어 "+CombatTime(Mathf.Max(0,p.seconds-p.paused))+"  ·  보상 "+PaceTenths(p.rewardDwell)+"초  ·  출구 "+PaceTenths(p.exit)+"초"
                :p.outcome=="death"?"쓰러짐  ·  원인 "+p.deathCause:"기록 완료";
            int hits=live?FloorHits:p.hits,slain=live?kills-paceKillsAtStart:p.kills;
            return head+"  ·  "+run.Floor+"층 "+FloorTypeName(run.FloorType)+"\n"+state
                +"\n빈 화면 "+PacePercent(p.empty,p.combat)+"  ·  되돌아감 "+PaceTenths(p.backtrack)
                +"\n무적 커버리지 "+PacePercent(p.invulnerable,p.combat)+"  (피격 무적 "+PacePercent(p.hurtGuard,p.combat)+")"
                +"\n동시 예고 최대 "+p.maxTelegraphs+"  ·  화면 탄 최대 "+p.maxShots
                +"\n피격 "+hits+"  ·  대시 "+p.dashes+"  ·  처치 "+slain;
        }
        private static string PaceTenths(float value){return value.ToString("0.0",CultureInfo.InvariantCulture);}
        private static string PacePercent(float part,float whole){return Mathf.RoundToInt(PaceRatio(part,whole)*100)+"%";}
    }
}
