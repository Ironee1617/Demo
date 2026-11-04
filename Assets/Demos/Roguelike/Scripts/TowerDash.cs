using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Dash charges. The hero holds two (three at most from every source) and they come back one at a time; dashCooldown is the
    // time left until the next one, zero while every charge is ready. Two dashes stay at least DashMinGap apart. Real input
    // waits for a charge (DashReady); StartDash itself always runs and spends what is there, so fixtures can dash on demand.
    public sealed partial class TowerGame
    {
        internal const float DashMinGap=.2f;
        private int dashCharges;
        private float dashGap;
        /// <summary>Charges ready to spend now.</summary>
        internal int DashCharges{get{return Mathf.Min(dashCharges,MaxDashCharges);}}
        /// <summary>The most charges the hero holds: two, three with the diamond card, never more than <see cref="HeroState.DashChargeCap"/>.</summary>
        internal int MaxDashCharges{get{return run.Player.MaxDashCharges;}}
        /// <summary>Seconds one charge takes to come back: .9 times the dodge cooldown, never under <see cref="HeroState.MinDashRecharge"/> (.75).</summary>
        internal float DashRechargeTime{get{return run.Player.DashRechargeTime;}}
        private bool DashReady{get{return dashCharges>0&&dashGap<=0;}}

        /// <summary>Gives back up to <paramref name="n"/> charges, never past the maximum; returns how many came back.</summary>
        internal int RefundDashCharge(int n)
        {
            int max=MaxDashCharges,before=Mathf.Min(dashCharges,max);
            dashCharges=Mathf.Min(max,before+Mathf.Max(0,n));
            if(dashCharges>=max)dashCooldown=0;
            return dashCharges-before;
        }
        /// <summary>Fills every charge. The gap after the last dash still holds.</summary>
        internal void RefillDashCharges(){dashCharges=MaxDashCharges;dashCooldown=0;}
        /// <summary>A hero that has not dashed for a while: every charge and no gap. New heroes start so; fixtures call it before a dash.</summary>
        internal void ReadyDash(){RefillDashCharges();dashGap=0;}

        private void SpendDashCharge()
        {
            int max=MaxDashCharges;
            // Spending from full starts the recharge; one already running keeps its progress.
            if(dashCharges>=max||dashCooldown<=0)dashCooldown=DashRechargeTime;
            dashCharges=Mathf.Clamp(dashCharges-1,0,max-1);dashGap=DashMinGap;
        }
        private void TickDashCharges(float dt)
        {
            dashGap=Mathf.Max(0,dashGap-dt);
            int max=MaxDashCharges;
            if(dashCharges>=max){dashCharges=max;dashCooldown=0;return;}
            // A long step carries its leftover into the next charge.
            dashCooldown-=dt;
            while(dashCooldown<=0&&dashCharges<max){dashCharges++;dashCooldown=dashCharges<max?dashCooldown+DashRechargeTime:0;}
        }
    }
}
