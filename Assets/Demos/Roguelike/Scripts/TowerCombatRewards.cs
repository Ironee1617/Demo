using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerGame
    {
        internal const float DodgeCounterWindow=.12f, DodgeCounterDuration=3f, DodgeCounterMultiplier=1.5f;
        // A perfect dodge gives one dash charge back (TowerDash.cs), at most once every DodgeRefundInterval; the rat scout's
        // full refill keeps the same pace (OnPassivePerfectDodge).
        internal const float DodgeRefundInterval=3f;
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
        private float dodgeStartedAt=-10, floorStartedAt, dodgeRefundAt=-10;
        private bool dodgeCounterGranted;

        private void ResetCombatRewards()
        {
            PerfectDodges=CounterHits=FlawlessFloors=FlawlessStreak=BestFlawlessStreak=0;BoltsCut=BoltsReflected=0;
            DamageTaken=LastFloorSeconds=0;LastFloorFlawless=false;
            BeginFloorRecord();
        }
        private void BeginFloorRecord()
        {
            FloorHits=0;floorStartedAt=elapsed;CounterRemaining=0;
            dodgeStartedAt=dodgeRefundAt=-10;dodgeCounterGranted=false;
            ResetWeaponVolleys();ResetPassiveFloor();BeginPaceFloor();
        }
        private void BeginDodgeCounter()
        {dodgeStartedAt=elapsed;dodgeCounterGranted=false;OnPassiveDodge();PaceDash();}
        /// <summary>
        /// A dash just begun that a monster's bolt, melee blow or a boss's landing meets: the counter, the oath's charge, the
        /// passives' rewards and a dash charge back (once every <see cref="DodgeRefundInterval"/>).
        /// </summary>
        private bool TryPrimeDodgeCounter(){return PrimeDodgeCounter(true);}
        /// <summary>A trap's flame or boulder met the same way readies the counter alone, so dashing beside a trap gathers nothing.</summary>
        private bool TryPrimeTrapDodge(){return PrimeDodgeCounter(false);}
        private bool PrimeDodgeCounter(bool rewarded)
        {
            if(hero.hp<=0||run.IsFloorCleared||dashTime<=0||invulnerable<=0||dodgeCounterGranted||elapsed-dodgeStartedAt>DodgeCounterWindow+PassiveDodgeWindow)return false;
            dodgeCounterGranted=true;CounterRemaining=DodgeCounterDuration;PerfectDodges++;
            if(rewarded)
            {
                // The timer starts only when a charge actually came back; the scout's full refill keeps the same pace,
                // so chained dodges cannot stitch the dashes' invulnerability into one unbroken guard.
                bool refundReady=elapsed-dodgeRefundAt>=DodgeRefundInterval;
                if(refundReady&&RefundDashCharge(1)>0)dodgeRefundAt=elapsed;
                if(run.Oath!=null)run.Oath.OnPerfectDodge();
                OnPassivePerfectDodge(refundReady);
            }
            FloatText(hero.pos+Vector2.up*2.35f,"회피 반격 준비",TowerWorld.Teal);
            if(vfx!=null)vfx.Cast(SkillEffect.ShieldPulse,hero.pos+Vector2.up,.65f,TowerWorld.Teal);
            Sound(dodgeCue,.45f);HitStop(PerfectDodgeHitStop);return true;
        }
        // The counter blow (sword, bullet or bolt) holds time a little longer than any other hit.
        private void ConsumeDodgeCounter()
        {
            CounterRemaining=0;CounterHits++;HitStop(CounterHitStop);
            FloatText(hero.pos+Vector2.up*2.35f,"반격!",TowerWorld.Teal);
        }
        private void RecordFloorResult()
        {
            LastFloorSeconds=Mathf.Max(0,elapsed-floorStartedAt);LastFloorFlawless=FloorHits==0;
            if(LastFloorFlawless){FlawlessFloors++;FlawlessStreak++;BestFlawlessStreak=Mathf.Max(BestFlawlessStreak,FlawlessStreak);}
            else FlawlessStreak=0;
            CounterRemaining=0;
            OnPassiveFloorCleared();PaceFloorCleared();
        }
        private static string CombatTime(float seconds)
        {int total=Mathf.FloorToInt(seconds);return (total/60).ToString()+":"+(total%60).ToString("00");}
    }
}
