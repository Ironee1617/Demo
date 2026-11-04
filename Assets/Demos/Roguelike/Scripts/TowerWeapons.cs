using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>How the hero attacks: the aimed sword, a pistol fired toward the reticle, or an orb that strikes by itself.</summary>
    public enum TowerAttackMode { Sword, Gun, Orb }

    // Attack modes. X or the mouse wheel cycles sword, gun and orb, and the choice is remembered between launches. The gun
    // fires toward the reticle while the attack button is held; the orb floats behind the hero's shoulder and shoots on its
    // own rhythm whatever comes within its reach: the enemy nearest the reticle first, otherwise the nearest vessel, bolt
    // totem or training dummy. With either the sword arm points at the aim (TowerActorArt's ranged stances). Their hits
    // spend a ready dodge counter and charge the oath like sword hits. Fixtures keep the sword unless they set
    // AttackModeOverride.
    // Both lean on the dash as the sword does: the gun keeps firing through a dash, its first shot in the dash-slash window
    // is a dash shot (GunDashPellets bullets GunDashSpread degrees apart at GunDashScale each, each striking up to
    // GunDashPierce enemies, the volley charging the oath once), and a bullet landing within GunCloseReach of the chest
    // strikes for GunCloseScale; a dash lets the orb loose at once (WeaponsOnDash), and that bolt bursts on every other
    // enemy within OrbDashBurst of the one it strikes.
    public sealed partial class TowerGame
    {
        internal const float GunIntervalScale=.75f,GunDamageScale=.6f,GunBulletSpeed=26,GunReach=12;
        internal const float GunDashSpread=7,GunDashScale=.75f,GunCloseReach=3,GunCloseScale=1.1f;
        internal const int GunDashPellets=3,GunDashPierce=2;
        internal const float OrbIntervalScale=1.3f,OrbDamageScale=.8f,OrbBoltSpeed=15,OrbReach=6.5f,OrbTurnRate=540,OrbDashBurst=1;
        private const float BaseRange=2.6f;
        private const string AttackModeKey="Tower.AttackMode.v1";
        internal static readonly Color OrbTint=new Color(.56f,.84f,1);
        /// <summary>The mode fixtures fight with; without it they keep the sword whatever the player chose.</summary>
        internal TowerAttackMode? AttackModeOverride;
        internal TowerAttackMode AttackMode{get{return SmokeControlled?AttackModeOverride??TowerAttackMode.Sword:chosenMode;}}
        internal int GunShots{get;private set;}
        internal int GunDashShots{get;private set;}
        internal int OrbShots{get;private set;}
        internal Vector2 OrbPosition{get{return orbPos;}}
        internal bool OrbShown{get{return orb;}}
        // Range cards stretch the gun's carry and the orb's reach as they stretch the sword's.
        internal float GunRange{get{return GunReach*Mathf.Max(1,run.Player.Range/BaseRange);}}
        internal float OrbRange{get{return OrbReach*Mathf.Max(1,run.Player.Range/BaseRange);}}
        internal float GunInterval{get{return PlayerAttackInterval*GunIntervalScale;}}
        internal float OrbInterval{get{return PlayerAttackInterval*OrbIntervalScale;}}
        private TowerAttackMode chosenMode;
        private GameObject orb;private Vector2 orbPos;private float orbClock,orbPulse,modeSwitchAt=-1;
        private AudioClip gunSound,orbSound;
        // Whether this dash's dash shot is gone, and whether the orb's next bolt is the dash's. Each dash volley carries its own
        // id (Shot.volley): the first of its shots to land charges the oath and the rest never do, whatever flies meanwhile.
        // The ids already charged are forgotten with each floor (BeginFloorRecord).
        private bool gunDashSpent,orbDashBolt;private int lastVolley;
        private readonly System.Collections.Generic.HashSet<int> chargedVolleys=new System.Collections.Generic.HashSet<int>();

        private void LoadAttackMode(){chosenMode=(TowerAttackMode)Mathf.Clamp(PlayerPrefs.GetInt(AttackModeKey,0),0,2);}
        // X steps to the next mode; the wheel steps either way, at most once every 0.15 s so one flick changes it once.
        private void TickAttackModeInput()
        {
            if(SmokeControlled)return;
            int step=Input.GetKeyDown(KeyCode.X)?1:0;float wheel=Input.mouseScrollDelta.y;
            if(step==0&&wheel!=0&&Time.unscaledTime-modeSwitchAt>.15f&&!MouseInUI())step=wheel<0?1:-1;
            if(step!=0)ChooseAttackMode((TowerAttackMode)(((int)chosenMode+step+3)%3));
        }
        internal void ChooseAttackMode(TowerAttackMode mode)
        {
            if(mode==chosenMode)return;
            chosenMode=mode;modeSwitchAt=Time.unscaledTime;SyncAttackMode(true);
            if(!SuppressProgressPersistence&&!SmokeControlled){PlayerPrefs.SetInt(AttackModeKey,(int)mode);PlayerPrefs.Save();}
        }
        /// <summary>Hands the hero the current mode's weapon. A switch cuts a swing in progress and readies the new weapon at once.</summary>
        internal void SyncAttackMode(bool announce=false)
        {
            var mode=AttackMode;if(hero==null||hero.art.Weapon==mode)return;
            if(attackTimer>0||pendingSlash){attackTimer=0;pendingSlash=false;lungeTime=0;hero.art.InterruptAction();}
            slashCooldown=Mathf.Min(slashCooldown,.1f);orbClock=Mathf.Max(orbClock,.3f);
            hero.art.SetWeapon(mode);orbPos=OrbHome;
            if(announce){FloatText(hero.pos+Vector2.up*2.5f,AttackModeName(mode),AttackModeColor(mode));Sound(chimeSound,.22f);}
        }
        internal static string AttackModeName(TowerAttackMode mode){return mode==TowerAttackMode.Gun?"총":mode==TowerAttackMode.Orb?"오브":"검";}
        private static string AttackModeHint(TowerAttackMode mode){return mode==TowerAttackMode.Gun?"원거리 · 조준 사격":mode==TowerAttackMode.Orb?"자동 · 범위 안 적":"근접 · 조준 베기";}
        private static Color AttackModeColor(TowerAttackMode mode){return mode==TowerAttackMode.Gun?TowerWorld.Gold:mode==TowerAttackMode.Orb?OrbTint:new Color(.95f,.92f,.85f);}

        // ---------------------------------------------------------------- gun

        // The trigger: held (or a buffered press) once the last shot has cooled, through a dash as well, never mid-cast.
        private bool GunTriggerReady(bool held){return held&&slashCooldown<=0&&hero.castRemaining<=0;}
        // One shot from the muzzle toward the reticle itself; a reticle right at the hero fires along the aim instead. The
        // first shot in the dash-slash window fans out as a dash shot of piercing bullets under one volley id and spends the
        // window as a dash slash does (a dash still running would hold it open, hence gunDashSpent), so the next is an
        // ordinary shot.
        private void FireGun()
        {
            slashCooldown=GunInterval;
            // Update leaves the facing alone mid-dash; a shot turns to the reticle as a dash slash turns to its cut.
            if(dashTime>0&&MouseAims)FaceAim();
            Vector2 muzzle=hero.art.MuzzleToward(AimPoint,hero.facing),heading=AimPoint-muzzle;
            if((AimPoint-Chest).sqrMagnitude<1.6f||Vector2.Dot(heading,AimDirection)<=0)heading=AimDirection;
            heading.Normalize();
            bool counter=CounterRemaining>0,dashShot=DashSlashReady&&!gunDashSpent;Color color=counter?TowerWorld.Teal:TowerWorld.Gold;
            float damage=PlayerDamage*GunDamageScale*(counter?DodgeCounterMultiplier:1)*(dashShot?GunDashScale:1);
            int bullets=dashShot?GunDashPellets:1,volley=dashShot?++lastVolley:0;
            if(dashShot){gunDashSpent=true;dashEndedAt=-10;GunDashShots++;}
            for(int i=0;i<bullets;i++)
            {
                Vector2 direction=bullets>1?(Vector2)(Quaternion.Euler(0,0,(i-(bullets-1)*.5f)*GunDashSpread)*heading):heading;
                var shot=WeaponShot(TowerAttackMode.Gun,muzzle,direction*GunBulletSpeed,damage,color,GunRange/GunBulletSpeed);
                shot.hitRadius=.1f;shot.empowered=counter;shot.volley=volley;if(dashShot)shot.pierce=GunDashPierce;
            }
            hero.art.Recoil();reticleKick=Mathf.Max(reticleKick,dashShot?1:.6f);GunShots++;
            if(vfx!=null)vfx.MuzzleFlash(muzzle,heading,color);
            if(dashShot)FloatText(hero.pos+Vector2.up*2.6f,"돌진 사격",TowerWorld.Gold);
            Sound(GunSound,dashShot?.42f:.3f);
        }
        // StartDash: each dash readies one dash shot and lets the orb loose at once with a bolt that bursts (OrbBurst); the
        // orb's rhythm carries on from that bolt.
        private void WeaponsOnDash(){gunDashSpent=false;if(AttackMode==TowerAttackMode.Orb){orbClock=0;orbDashBolt=true;}}
        // A gun bullet landing within GunCloseReach of the chest strikes for GunCloseScale.
        private float GunCloseBonus(Shot shot,Vector2 impact)
        {return shot.weapon==TowerAttackMode.Gun&&(impact-Chest).sqrMagnitude<=GunCloseReach*GunCloseReach?GunCloseScale:1;}
        private Shot WeaponShot(TowerAttackMode weapon,Vector2 from,Vector2 velocity,float damage,Color color,float life)
        {
            var go=new GameObject(weapon==TowerAttackMode.Gun?"Hero bullet":"Orb bolt");go.transform.SetParent(transform,false);
            go.transform.position=from;go.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg);
            var shot=new Shot{root=go,pos=from,velocity=velocity,damage=damage,color=color,owner=run.Player,life=life,practiceCast=-1,weapon=weapon,effect=SkillEffect.ArcaneVolley,poise=ShotPoise};
            shots.Add(shot);if(vfx!=null)vfx.WeaponProjectile(go.transform,weapon,color);
            return shot;
        }

        // ---------------------------------------------------------------- orb

        // The orb hovers behind the shoulder in a slow drifting figure and swings round when the hero turns.
        private Vector2 OrbHome{get{return hero.pos+new Vector2(-hero.facing*.78f+Mathf.Sin(elapsed*1.6f)*.14f,1.9f+Mathf.Sin(elapsed*3.1f)*.11f);}}
        /// <summary>Moves the orb along (showing or hiding it with the mode) and, when <paramref name="fight"/> is set, lets it fire.</summary>
        private void TickOrb(float dt,bool fight=true)
        {
            // Put away, the orb goes with its light (an inactive root would keep shedding motes where it was left).
            if(AttackMode!=TowerAttackMode.Orb||hero.hp<=0){orbDashBolt=false;if(orb){Destroy(orb);orb=null;}return;}
            if(!orb){orb=new GameObject("Hero orb");orb.transform.SetParent(transform,false);orbPos=OrbHome;}
            // Clearing the effects between floors takes the orb's light with it; it is lit again here.
            if(vfx!=null&&!vfx.Carries(orb.transform))vfx.HeroOrb(orb.transform,OrbTint);
            Vector2 home=OrbHome;
            orbPos=(orbPos-home).sqrMagnitude>36?home:Vector2.Lerp(orbPos,home,1-Mathf.Exp(-dt*9));
            orbPulse=Mathf.Max(0,orbPulse-dt*5);
            if(fight)
            {
                orbClock=Mathf.Max(0,orbClock-dt);
                Fighter enemy;Vector2 point;
                if(orbClock<=0&&OrbTarget(out enemy,out point))FireOrb(enemy,point);
                // Only the bolt the dash lets loose bursts; with nothing to shoot just then, the next is an ordinary one.
                orbDashBolt=false;
            }
            orb.transform.position=orbPos;orb.transform.localScale=Vector3.one*(1+.4f*orbPulse*orbPulse);
        }
        /// <summary>
        /// What the orb shoots next: the enemy within reach of the hero's chest nearest the reticle, or with none in reach
        /// the nearest vessel, standing bolt totem or (in the village) training dummy.
        /// </summary>
        private bool OrbTarget(out Fighter enemy,out Vector2 point)
        {
            enemy=null;point=Vector2.zero;float limit=OrbRange*OrbRange,best=float.PositiveInfinity;
            foreach(var e in enemies)
            {
                if(!CanTarget(e,run.Player))continue;
                Vector2 at=e.pos+Vector2.up*(e.isBoss?1.2f:.7f);if((at-Chest).sqrMagnitude>limit)continue;
                float score=(at-AimPoint).sqrMagnitude;if(score<best){best=score;enemy=e;point=at;}
            }
            if(enemy!=null)return true;
            foreach(var cache in caches)ConsiderOrbProp(cache.pos+Vector2.up*(cache.height*.5f+.05f),limit,ref best,ref point);
            foreach(var trap in traps)if(trap.spec.Kind==TowerHazardKind.BoltTotem&&!trap.broken)ConsiderOrbProp(new Vector2(trap.spec.X,trap.baseY+.8f),limit,ref best,ref point);
            if(village)foreach(var dummy in dummies)ConsiderOrbProp(dummy.pos+Vector2.up*ChestHeight,limit,ref best,ref point);
            return best<float.PositiveInfinity;
        }
        private void ConsiderOrbProp(Vector2 at,float limit,ref float best,ref Vector2 point)
        {float distance=(at-Chest).sqrMagnitude;if(distance<=limit&&distance<best){best=distance;point=at;}}
        // A bolt that homes on its enemy (props hold still and are shot straight at); the orb jolts back as it lets go.
        private void FireOrb(Fighter enemy,Vector2 point)
        {
            orbClock=OrbInterval;
            Vector2 heading=point-orbPos;heading=heading.sqrMagnitude>.0001f?heading.normalized:new Vector2(hero.facing,0);
            bool counter=CounterRemaining>0&&enemy!=null;Color color=counter?TowerWorld.Teal:OrbTint;
            var shot=WeaponShot(TowerAttackMode.Orb,orbPos+heading*.25f,heading*OrbBoltSpeed,PlayerDamage*OrbDamageScale*(counter?DodgeCounterMultiplier:1),color,2.2f);
            shot.hitRadius=.14f;shot.seek=enemy;shot.empowered=counter;if(orbDashBolt)shot.volley=++lastVolley;
            if(vfx!=null)vfx.OrbRelease(orbPos,heading,color);
            orbPos-=heading*.22f;orbPulse=1;hero.art.Beckon();OrbShots++;
            Sound(OrbSound,.2f);
        }
        private void SteerSeek(Shot shot,float dt)
        {
            var target=shot.seek;
            if(!CanTarget(target,shot.owner)||!target.root){shot.seek=null;return;}
            Vector2 to=target.pos+Vector2.up*(target.isBoss?1.2f:.7f)-shot.pos;if(to.sqrMagnitude<.0001f)return;
            shot.velocity=(Vector2)Vector3.RotateTowards(shot.velocity,to.normalized*shot.velocity.magnitude,OrbTurnRate*Mathf.Deg2Rad*dt,0);
            shot.root.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(shot.velocity.y,shot.velocity.x)*Mathf.Rad2Deg);
        }

        // ---------------------------------------------------------------- hits

        // A weapon shot that struck an enemy spends a ready dodge counter once (its damage was raised when it was fired) and
        // charges the oath like a sword hit; a dash volley charges it once, as one swing does whatever it cuts, and the orb's
        // dash bolt bursts. A piercing bullet reports again as it flies on (HitProps), so repeat calls change nothing.
        private void WeaponShotLanded(Shot shot)
        {
            Vector2 at=shot.pos;Fighter struck=null;foreach(var e in shot.hit){struck=e;at=e.pos;break;}
            if(shot.empowered&&CounterRemaining>0){shot.empowered=false;ConsumeDodgeCounter();}
            if(shot.volley!=0&&!chargedVolleys.Add(shot.volley))return;
            if(shot.volley!=0&&shot.weapon==TowerAttackMode.Orb&&struck!=null)OrbBurst(shot,struck);
            OnOathSwordHit(at);
        }
        // Every other enemy within OrbDashBurst of the one the dash bolt struck takes the same blow, the player's own (outside
        // the practice tally, as the bolt itself), pushed a little like an area shot's.
        private void OrbBurst(Shot shot,Fighter struck)
        {
            float reach=OrbDashBurst*OrbDashBurst,push=Mathf.Sign(shot.velocity.x)*.1f;Vector2 center=struck.pos;
            practiceStrike=-1;
            foreach(var e in enemies.ToArray())
                if(e!=struck&&!shot.hit.Contains(e)&&CanTarget(e,shot.owner)&&(e.pos-center).sqrMagnitude<=reach)StrikeEnemy(e,shot.damage,shot.owner,push);
            practiceStrike=0;
            Burst(shot.pos,shot.color,12,OrbDashBurst);
        }
        /// <summary>Clears the volleys that have charged the oath; a new floor starts with none (BeginFloorRecord).</summary>
        private void ResetWeaponVolleys(){chargedVolleys.Clear();}
        // Gun bullets and orb bolts chip what a light sword blow would: vessels, bolt totems and the village's dummies. A step
        // that left a weapon shot flying on calls this; a dash-shot bullet that pierced a body on the way has landed too.
        private bool HitProps(Shot shot,Vector2 previous)
        {
            if(shot.volley!=0&&shot.hit.Count>0)WeaponShotLanded(shot);
            for(int i=caches.Count-1;i>=0;i--)
            {
                var cache=caches[i];
                if(CrossesCircle(previous,shot.pos,cache.pos+Vector2.up*(cache.height*.5f+.05f),cache.height*.5f+.12f+shot.hitRadius)){ChipCache(cache,1);return true;}
            }
            foreach(var trap in traps)
                if(trap.spec.Kind==TowerHazardKind.BoltTotem&&!trap.broken&&CrossesCircle(previous,shot.pos,new Vector2(trap.spec.X,trap.baseY+.8f),.5f+shot.hitRadius)){ChipTotem(trap,1);return true;}
            if(village)foreach(var dummy in dummies)
                if(CrossesCircle(previous,shot.pos,dummy.pos+Vector2.up*ChestHeight,.55f+shot.hitRadius)){ChipDummy(dummy,shot.damage*GunCloseBonus(shot,previous),false);return true;}
            return false;
        }

        // ---------------------------------------------------------------- sound and HUD

        private AudioClip GunSound{get{if(!gunSound)gunSound=Gunshot();return gunSound;}}
        private AudioClip OrbSound{get{if(!orbSound)orbSound=Tone(990,.14f);return orbSound;}}
        // A short crack of noise over a falling thump.
        private static AudioClip Gunshot()
        {
            int n=(int)(22050*.16f);var data=new float[n];var random=new System.Random(11);
            for(int i=0;i<n;i++)
            {
                float t=i/22050f,crack=((float)random.NextDouble()*2-1)*Mathf.Exp(-t*42),thump=Mathf.Sin(2*Mathf.PI*(150*t-420*t*t))*Mathf.Exp(-t*18);
                data[i]=(crack*.6f+thump*.5f)*.5f;
            }
            var clip=AudioClip.Create("Tower gunshot",n,1,22050,false);clip.SetData(data,0);return clip;
        }
        private void DestroyWeapons(){if(gunSound)Destroy(gunSound);if(orbSound)Destroy(orbSound);if(orb)Destroy(orb);}
        // Beside the dodge panel: the mode, its key and how soon the weapon is ready again.
        private void DrawAttackModePanel()
        {
            var mode=AttackMode;Color c=AttackModeColor(mode);
            Panel(new Rect(1082,786,192,80),c);Keycap(new Rect(1098,804,40,34),"X");
            Title(new Rect(1150,796,116,30),AttackModeName(mode),20,paper,TextAnchor.MiddleLeft);
            Label(new Rect(1150,828,120,20),AttackModeHint(mode),13,muted);
            float interval=mode==TowerAttackMode.Gun?GunInterval:mode==TowerAttackMode.Orb?OrbInterval:PlayerAttackInterval;
            float ready=1-Mathf.Clamp01((mode==TowerAttackMode.Orb?orbClock:slashCooldown)/Mathf.Max(.01f,interval));
            Round(new Rect(1098,852,160,5),new Color(0,0,0,.6f),2);Round(new Rect(1098,852,160*ready,5),c,2);
        }
    }
}
