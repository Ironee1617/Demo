using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Action feel: dash slashes, combo finishers, attack lunges, hit stop and screen shake.
    // Hit stop and shake are presentation only and stay off while fixtures drive the game.
    public sealed partial class TowerGame
    {
        internal const float DashSlashWindow=.16f,DashSlashMultiplier=1.35f,FinisherMultiplier=1.4f;
        internal const float DashSlashReach=1.25f,FinisherReach=1.15f;
        internal bool ScreenShakeEnabled=true;
        internal int DashSlashes{get;private set;}
        internal int Finishers{get;private set;}
        private float hitStop,shakeTime,shakeDuration,shakeStrength,dashEndedAt=-10,lungeTime,lungeSpeed;
        private Vector3 appliedShake;
        private bool jumpCut,slashIsDash;
        private int slashCombo;

        private void ResetAction()
        {
            hitStop=shakeTime=shakeDuration=shakeStrength=lungeTime=lungeSpeed=0;dashEndedAt=-10;
            appliedShake=Vector3.zero;jumpCut=slashIsDash=false;slashCombo=0;DashSlashes=Finishers=0;
        }
        private float TickHitStop(float frame)
        {
            if(hitStop<=0)return frame;
            hitStop-=frame;return frame*.08f;
        }
        private void HitStop(float seconds){if(!SmokeControlled)hitStop=Mathf.Max(hitStop,seconds);}
        private void Shake(float strength,float seconds)
        {
            if(SmokeControlled||!ScreenShakeEnabled)return;
            shakeStrength=Mathf.Max(shakeTime>0?shakeStrength:0,strength);
            shakeTime=Mathf.Max(shakeTime,seconds);shakeDuration=Mathf.Max(shakeDuration,seconds);
        }
        private Vector3 NextShake(float frame)
        {
            if(shakeTime<=0)return Vector3.zero;
            shakeTime=Mathf.Max(0,shakeTime-frame);
            float fade=shakeDuration>0?shakeTime/shakeDuration:0,amount=shakeStrength*fade*fade;
            if(shakeTime<=0){shakeDuration=shakeStrength=0;}
            float t=Time.unscaledTime*38;
            return new Vector3((Mathf.PerlinNoise(t,.3f)-.5f)*2*amount,(Mathf.PerlinNoise(.7f,t)-.5f)*2*amount,0);
        }
        // An attack started during a dash, or right after it, becomes a lunging dash slash.
        private bool DashSlashReady{get{return dashTime>0||elapsed-dashEndedAt<DashSlashWindow;}}
        private void BeginAttackMotion(int combo,float duration)
        {
            slashIsDash=DashSlashReady;slashCombo=combo;
            if(dashTime>0)dashTime=0;
            dashEndedAt=-10;
            lungeTime=Mathf.Min(duration*.45f,.12f);
            lungeSpeed=slashIsDash?9f:combo==2?5.5f:3f;
            if(slashIsDash&&vfx!=null)vfx.SpeedLines(hero.pos+Vector2.up*.9f,hero.facing,TowerWorld.Teal);
        }
        private void TickLunge(float dt)
        {
            if(lungeTime<=0)return;
            float step=Mathf.Min(dt,lungeTime);lungeTime-=step;
            hero.pos.x=Mathf.Clamp(hero.pos.x+hero.facing*lungeSpeed*step,TowerWorld.Left+.6f,TowerWorld.Right-.6f);
        }
        private void LandingFeedback(bool wasAirborne,float fallSpeed)
        {
            if(!wasAirborne||!hero.grounded||fallSpeed>-7||vfx==null)return;
            vfx.Dust(hero.pos,0,Mathf.Clamp01(-fallSpeed/16f)+.3f);
        }
    }
}
