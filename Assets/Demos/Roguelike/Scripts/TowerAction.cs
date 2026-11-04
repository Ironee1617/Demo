using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Action feel: dash slashes, the combo's beat and finisher, attack lunges, down-cut bounces, the fast fall's landing
    // strike, the camera's lead, hit stop and screen shake.
    // Hit stop and shake are presentation only and stay off while fixtures drive the game.
    public sealed partial class TowerGame
    {
        internal const float DashSlashWindow=.16f,DashSlashMultiplier=1.35f,FinisherMultiplier=1.4f;
        internal const float DashSlashReach=1.25f,FinisherReach=1.15f;
        // The sword's beat scales a swing and its cooldown: two quick cuts, then a finisher slower to swing and recover from.
        internal const float QuickBeat=.8f,FinisherBeat=1.3f;
        // A cut aimed down (attackAim.y below PogoAimY) that lands on an enemy in the air bounces the hero up, less each time
        // within one airtime and at most PogoSameTargetLimit times running off the same enemy; a bounce gives a spent air jump
        // back once per airtime and the airtime's rising air dash back every time. Standing on anything starts afresh.
        internal const float PogoAimY=-.6f;internal const int PogoSameTargetLimit=2;
        internal static readonly float[] PogoVelocities={9,6,3};
        // S pressed fresh in the air drops at FastFallSpeed; landing from it at FastFallSlamSpeed or faster strikes every enemy
        // within FastFallSlamRadius of the feet for FastFallSlamScale of the player's damage. Only a fall of FastFallSlamHeight
        // or more below where S was pressed slams, and no sooner than FastFallSlamInterval after the last slam, so short hops
        // cannot chain it.
        internal const float FastFallSpeed=-14,FastFallSlamSpeed=-12,FastFallSlamRadius=1.2f,FastFallSlamScale=.5f;
        internal const float FastFallSlamHeight=1.5f,FastFallSlamInterval=.8f;
        // Real play leads the view toward the reticle as well as the keys (the gun leans on the aim more), past a dead zone
        // around the hero, and runs ahead by the hero's actual speed to make up for the camera's lag.
        internal const float CameraKeyLead=1.6f,CameraAimScale=.3f,CameraAimLimit=2.5f,CameraAimDeadZone=1.5f,CameraAimWeight=.5f,CameraGunAimWeight=.7f;
        internal const float CameraLeadRate=7,CameraSpeedScale=.2f,CameraSpeedLimit=2;
        /// <summary>A fixture's fresh press of S in the air (a fast fall); a held S stays DropOverride.</summary>
        internal bool FastFallOverride;
        internal int FastFallSlams{get;private set;}
        /// <summary>Down-cut bounces spent in the current airtime.</summary>
        internal int PogoBounces{get{return pogoBounces;}}
        internal bool FastFalling{get{return fastFalling;}}
        private int pogoBounces,pogoStreak;private Fighter pogoTarget;private bool pogoRestored,fastFalling,fastFallHold;
        // Where the current fast fall began and when the last slam landed; whether this airtime's rising air dash is spent (TowerAim.cs).
        private float fastFallFrom,lastSlamAt=-10;private bool airDashLiftSpent;
        // Time stops only for the player's own big moments; a light sword hit holds just the enemy it struck (LocalHitStop).
        internal const float HeavyHitStop=.085f,CounterHitStop=.1f,PlayerKillHitStop=.06f,BossKillHitStop=.12f,PerfectDodgeHitStop=.05f,LocalHitStop=.06f;
        // A heavy blow first jolts the view along the swing for this long, then the ordinary shake takes over.
        internal const float ShakeKickTime=.05f;
        // Bosses give this share of the ground an ordinary blow pushes other enemies.
        internal const float BossPushScale=.15f;
        internal bool ScreenShakeEnabled=true;
        internal int DashSlashes{get;private set;}
        internal int Finishers{get;private set;}
        private float hitStop,shakeTime,shakeDuration,shakeStrength,kickTime,kickStrength,dashEndedAt=-10,lungeTime,lungeSpeed;
        private Vector3 appliedShake;private Vector2 kickDirection;
        private bool jumpCut,slashIsDash;
        private int slashCombo;
        // Who landed the blow DamageEnemy is resolving, and whether it is a damage-over-time tick (DotStrike sets dotStrike
        // for the next StrikeEnemy). StrikeEnemy restores both as it returns, so nested strikes keep their own.
        private HeroState lastStrikeOwner;
        private bool dotStrike,lastStrikeDot;
        private bool PlayerStrike{get{return lastStrikeOwner!=null&&lastStrikeOwner==run.Player&&!lastStrikeDot;}}

        private void ResetAction()
        {
            hitStop=shakeTime=shakeDuration=shakeStrength=kickTime=kickStrength=lungeTime=lungeSpeed=0;dashEndedAt=-10;
            appliedShake=Vector3.zero;kickDirection=Vector2.zero;jumpCut=slashIsDash=false;slashCombo=0;DashSlashes=Finishers=0;
            lastStrikeOwner=null;dotStrike=lastStrikeDot=false;
            ResetAirStrikes();fastFallHold=false;FastFallSlams=0;fastFallFrom=0;lastSlamAt=-10;
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
        private void KickShake(Vector2 direction,float strength)
        {
            if(SmokeControlled||!ScreenShakeEnabled||direction.sqrMagnitude<.0001f)return;
            kickDirection=direction.normalized;kickStrength=strength;kickTime=ShakeKickTime;
        }
        // A light sword hit holds only the enemy it struck: its pose waits and its body flashes white (TowerActorArt.HitFlash
        // never touches Tint, so elite colours, veils and death fades keep theirs). Presentation only, off for fixtures.
        private void HoldStruck(Fighter enemy)
        {if(!SmokeControlled&&enemy!=null&&enemy.art!=null)enemy.art.HitFlash(LocalHitStop);}
        private Vector3 NextShake(float frame)
        {
            if(kickTime>0)
            {
                // The kick spends the shake's own time, so the whole jolt lasts as long as before; it eases back to rest.
                kickTime=Mathf.Max(0,kickTime-frame);shakeTime=Mathf.Max(0,shakeTime-frame);
                if(shakeTime<=0){shakeDuration=shakeStrength=0;}
                return kickDirection*(kickStrength*kickTime/ShakeKickTime);
            }
            if(shakeTime<=0)return Vector3.zero;
            shakeTime=Mathf.Max(0,shakeTime-frame);
            float fade=shakeDuration>0?shakeTime/shakeDuration:0,amount=shakeStrength*fade*fade;
            if(shakeTime<=0){shakeDuration=shakeStrength=0;}
            float t=Time.unscaledTime*38;
            return new Vector3((Mathf.PerlinNoise(t,.3f)-.5f)*2*amount,(Mathf.PerlinNoise(.7f,t)-.5f)*2*amount,0);
        }
        // An attack started during a dash, or right after it, becomes a lunging dash slash.
        private bool DashSlashReady{get{return dashTime>0||elapsed-dashEndedAt<DashSlashWindow;}}
        // The beat of the swing about to start: the finisher (the third cut, unless it is a dash slash) is the slow one.
        private float SwingBeat(int combo){return combo==2&&!DashSlashReady?FinisherBeat:QuickBeat;}
        private void BeginAttackMotion(int combo,float duration)
        {
            slashIsDash=DashSlashReady;slashCombo=combo;
            if(dashTime>0){dashTime=0;SettleDash();}
            dashEndedAt=-10;
            lungeTime=Mathf.Min(duration*.45f,.12f);
            // The lunge follows the swing's horizontal heading: full ahead, none for a cut straight up or down.
            lungeSpeed=(slashIsDash?9f:combo==2?5.5f:3f)*Mathf.Abs(attackAim.x);
            if(slashIsDash&&vfx!=null)vfx.SpeedLines(hero.pos+Vector2.up*.9f,hero.facing,TowerWorld.Teal);
        }
        private void TickLunge(float dt)
        {
            if(lungeTime<=0)return;
            float step=Mathf.Min(dt,lungeTime);lungeTime-=step;
            hero.pos.x=Mathf.Clamp(hero.pos.x+hero.facing*lungeSpeed*step,TowerWorld.Left+.6f,TowerWorld.Right-.6f);
        }
        // A landed cut aimed down from the air bounces off the enemy it struck (see PogoAimY).
        private void Pogo(Fighter from)
        {
            if(from==null||hero.grounded||attackAim.y>=PogoAimY||pogoBounces>=PogoVelocities.Length)return;
            int streak=from==pogoTarget?pogoStreak+1:1;if(streak>PogoSameTargetLimit)return;
            pogoTarget=from;pogoStreak=streak;
            hero.velocityY=Mathf.Max(hero.velocityY,PogoVelocities[pogoBounces++]);
            // A bounce is not a jump, so letting go of the key does not cut it; it ends a fast fall and gives back the
            // airtime's rising air dash.
            jumpCut=true;fastFalling=false;airDashLiftSpent=false;
            // The air jump comes back once per airtime, spent only when there is one to give back.
            if(!pogoRestored&&airJumps<MaxAirJumps){pogoRestored=true;airJumps=MaxAirJumps;}
            if(vfx!=null)vfx.ImpactRing(hero.pos+Vector2.up*.1f,.45f,TowerWorld.Gold);Sound(jumpCue,.24f);
        }
        private void ResetAirStrikes(){pogoBounces=pogoStreak=0;pogoTarget=null;pogoRestored=fastFalling=airDashLiftSpent=false;}
        // S pressed fresh in the air (not mid-dash) drops at FastFallSpeed. The S held on from that press never drops the
        // hero through what it lands on: the returned drop input is what the platforms see.
        private bool TickFastFall(bool pressed,bool drop)
        {
            if(!drop)fastFallHold=false;
            if(pressed&&!hero.grounded&&dashTime<=0)
            {
                // The slam's height counts from the press that began this fall; pressing again mid-fall keeps it.
                if(!fastFalling)fastFallFrom=hero.pos.y;
                hero.velocityY=Mathf.Min(hero.velocityY,FastFallSpeed);fastFalling=true;fastFallHold=drop;
                if(vfx!=null)vfx.SpeedLines(hero.pos+Vector2.up*.9f,Vector2.down,TowerWorld.Teal);
            }
            return drop&&!fastFallHold;
        }
        // Called after every hero gravity step. A fast fall ends when anything lifts the hero out of it (a jump, a bounce, an
        // air dash) and lands as a slam once it fell FastFallSlamHeight and the last slam is FastFallSlamInterval past; any
        // other landing, a short or a too-early one included, only raises dust. Standing on anything ends the airtime's bounces.
        private void LandingFeedback(bool wasAirborne,float fallSpeed)
        {
            bool landed=wasAirborne&&hero.grounded,slam=landed&&fastFalling&&fallSpeed<=FastFallSlamSpeed
                &&fastFallFrom-hero.pos.y>=FastFallSlamHeight&&elapsed-lastSlamAt>=FastFallSlamInterval;
            if(hero.grounded)ResetAirStrikes();else if(hero.velocityY>FastFallSlamSpeed)fastFalling=false;
            if(slam){FastFallSlam();return;}
            if(!landed||fallSpeed>-7||vfx==null)return;
            vfx.Dust(hero.pos,0,Mathf.Clamp01(-fallSpeed/16f)+.3f);
        }
        // The player's own blow on every enemy near the feet, taking SlamPoise; the dust and the jolt are bigger whether or
        // not one is there.
        private void FastFallSlam()
        {
            FastFallSlams++;lastSlamAt=elapsed;bool struck=false;float damage=PlayerDamage*FastFallSlamScale,reach=FastFallSlamRadius*FastFallSlamRadius;
            practiceStrike=-1;
            foreach(var e in enemies.ToArray())
                if(CanTarget(e,run.Player)&&(e.pos-hero.pos).sqrMagnitude<=reach)
                {float push=e.pos.x>=hero.pos.x?.35f:-.35f;StrikeEnemy(e,damage,run.Player,Staggerable(e)?0:push);PlayerBlow(e,push,SlamPoise,false);HoldStruck(e);struck=true;}
            practiceStrike=0;
            if(vfx!=null){vfx.Dust(hero.pos,0,2.2f);vfx.ImpactRing(hero.pos+Vector2.up*.15f,FastFallSlamRadius*.7f,struck?TowerWorld.Gold:TowerWorld.Teal);}
            Shake(struck?.16f:.1f,.2f);KickShake(Vector2.down,.12f);
            if(struck){HitStop(.05f);Sound(heavyCue,.6f);}else Sound(heavyCue,.3f);
        }
        // Real play only; fixtures keep the key-only lead so their camera paths stay as they were.
        private bool AimLeadsCamera{get{return MouseAims&&!SmokeControlled;}}
        private float CameraLeadGoal(float move)
        {
            float weight=AttackMode==TowerAttackMode.Gun?CameraGunAimWeight:CameraAimWeight,dx=AimPoint.x-hero.pos.x;
            float aim=Mathf.Abs(dx)<CameraAimDeadZone?0:Mathf.Clamp(dx*CameraAimScale,-CameraAimLimit,CameraAimLimit);
            return (1-weight)*move*CameraKeyLead+weight*aim;
        }
        private float CameraSpeedLead(float speed){return Mathf.Clamp(speed*CameraSpeedScale,-CameraSpeedLimit,CameraSpeedLimit);}
    }
}
