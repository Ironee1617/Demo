using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerGame
    {
        internal const float DodgeCounterWindow=.12f, DodgeCounterDuration=3f, DodgeCounterMultiplier=1.5f;
        internal float CounterRemaining { get; private set; }
        internal int PerfectDodges { get; private set; }
        internal int CounterHits { get; private set; }
        internal int FlawlessFloors { get; private set; }
        internal int FlawlessStreak { get; private set; }
        internal int BestFlawlessStreak { get; private set; }
        internal int FloorHits { get; private set; }
        internal float DamageTaken { get; private set; }
        internal float LastFloorSeconds { get; private set; }
        internal bool LastFloorFlawless { get; private set; }
        private float dodgeStartedAt=-10, floorStartedAt;
        private bool dodgeCounterGranted;

        private void ResetCombatRewards()
        {
            PerfectDodges=CounterHits=FlawlessFloors=FlawlessStreak=BestFlawlessStreak=0;
            DamageTaken=LastFloorSeconds=0;LastFloorFlawless=false;
            BeginFloorRecord();
        }
        private void BeginFloorRecord()
        {
            FloorHits=0;floorStartedAt=elapsed;CounterRemaining=0;
            dodgeStartedAt=-10;dodgeCounterGranted=false;
        }
        private void BeginDodgeCounter()
        {dodgeStartedAt=elapsed;dodgeCounterGranted=false;}
        private bool TryPrimeDodgeCounter()
        {
            if(hero.hp<=0||run.IsFloorCleared||dashTime<=0||invulnerable<=0||dodgeCounterGranted||elapsed-dodgeStartedAt>DodgeCounterWindow)return false;
            dodgeCounterGranted=true;CounterRemaining=DodgeCounterDuration;PerfectDodges++;
            if(run.Oath!=null)run.Oath.OnPerfectDodge();
            FloatText(hero.pos+Vector2.up*2.35f,"회피 반격 준비",TowerWorld.Teal);
            if(vfx!=null)vfx.Cast(SkillEffect.ShieldPulse,hero.pos+Vector2.up,.65f,TowerWorld.Teal);
            Sound(chimeSound,.35f);return true;
        }
        private void ConsumeDodgeCounter()
        {
            CounterRemaining=0;CounterHits++;
            FloatText(hero.pos+Vector2.up*2.35f,"반격!",TowerWorld.Teal);
        }
        private void RecordFloorResult()
        {
            LastFloorSeconds=Mathf.Max(0,elapsed-floorStartedAt);LastFloorFlawless=FloorHits==0;
            if(LastFloorFlawless){FlawlessFloors++;FlawlessStreak++;BestFlawlessStreak=Mathf.Max(BestFlawlessStreak,FlawlessStreak);}
            else FlawlessStreak=0;
            CounterRemaining=0;
        }
        private static string CombatTime(float seconds)
        {int total=Mathf.FloorToInt(seconds);return (total/60).ToString()+":"+(total%60).ToString("00");}
    }
}
