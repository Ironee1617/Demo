using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerGame : MonoBehaviour
    {
        internal sealed class Fighter
        {
            public GameObject root; public TowerActorArt art; public Vector2 pos; public float hp,maxHp,velocityY,cooldown,windup,hurt,attackPose;
            public float basicDelay,castRemaining; public bool basicPending; public Fighter basicTarget,combatTarget; public int combo;
            public float moveVelocity,followGroundY,followGroundX,dropTime,dropHeight;
            public int followSource=-2,followGoal=-2,followNext=-2; public bool followMoving,returningToFormation;
            public int facing=1,kind,difficultyFloor=1; public string id; public bool grounded,airborne,isBoss,isElite,isEnemy; public float slowTime,stunTime; public Vector2 strikePoint;
            // A monster's poise (TowerPoise.cs): what the player's blows have taken of it, the time until it refills, and a
            // break's stagger and guard. Its moveVelocity carries the player's sliding knockback.
            public float poiseDamage,poiseRest,staggerTime,poiseGuard;
            // Platform index the actor stands on (-1: the ground), the platform being dropped through, and the x at the previous step.
            public int support=-1,dropSupport=-1; public float lastX=float.NaN;
            public float damageScale=1,moveScale=1,bulletSpeedScale=1,attackRateScale=1;
            public TowerMonsterDefinition monster;
            public TowerThemeId theme;
            // Shown instead of the species name, e.g. an awakened chapter boss.
            public string title;
            // Statuses skills leave on an enemy (TowerSkillStatus.cs): burning and cursed damage over time, the hunter's mark,
            // a seed that blooms, who applied them, and when this enemy last showed a reaction.
            public float burnTime,burnDps,burnClock,curseTime,curseDps,curseClock,markTime,seedTime,seedPower,seedRadius,reactionAt=-9;
            public HeroState burnOwner,curseOwner,seedOwner; public int curseSpread,burnCast,curseCast,seedCast,seedLevel; public bool markSpread,seedDouble;
        }
        private sealed class Shot { public GameObject root; public Vector2 pos,velocity; public float damage,life,area,slowDuration,hitRadius=.16f; public bool hostile,oathChain,basic,empowered,forceCrit,signature; public Color color; public HeroState owner; public SkillEffect effect; public int pierce=1,practiceCast;
            // Skill level (drawn grade); a returning disc's thrower and turn; a bouncing shot's remaining bounces.
            public int level=1,bounces; public Fighter home; public bool returning,skill; public float returnScale=1,bounceRange,bounceGain=1;
            // The hero's gun bullets and orb bolts (Sword for every other shot), and the enemy an orb bolt homes on.
            public TowerAttackMode weapon; public Fighter seek;
            // The monster that fired a hostile bolt (its bolts fade away when it falls) and the fade's remaining time (-1: none).
            public Fighter shooter; public float fade=-1;
            // A trap's bolt (a totem's): dodging it readies the counter alone, like the traps' other dangers.
            public bool trap;
            // The poise the player's own shot (a gun bullet, an orb bolt, a reflected bolt) takes from a monster; 0 for any other.
            public float poise;
            // The dash volley a weapon shot belongs to (TowerWeapons.cs): a dash shot's bullets share one, the orb's dash bolt has its own; 0 for any other.
            public int volley;
            public readonly HashSet<Fighter> hit=new HashSet<Fighter>(); }
        // Floating text over the world; size scales its 24-point face. A damage-over-time number keeps its enemy (tally),
        // the damage gathered so far and when it began, so later ticks within a second add to it.
        internal sealed class Number { public Vector2 pos; public string text; public Color color; public float life,size=1; public Fighter tally; public float total,since; }
        internal TowerRun run;
        internal TowerProgress progress;
        internal bool SuppressProgressPersistence;
        private const string ProgressKey="Tower.MythProgress.v1";
        internal readonly List<Fighter> enemies=new List<Fighter>(), allies=new List<Fighter>();
        internal readonly List<Number> numbers=new List<Number>();
        private readonly List<Shot> shots=new List<Shot>();
        internal Fighter hero;
        internal Camera view;
        private TowerWorld world;
        internal bool intro=true,menu,party,help;
        internal string dismissId="",notice="";
        internal float noticeTime,elapsed,invulnerable,dashCooldown,slashCooldown,attackTimer,dashTime;
        private float jumpBuffer,coyote,healClock,cameraLead=1.6f,cameraCeiling=1.8f;
        // Real input keeps a tap made while a swing or the dash is still cooling down for a moment; fixtures hold their overrides.
        internal const float AttackBufferTime=.15f,DashBufferTime=.12f;
        private float attackBuffer,dashBuffer;private bool dashBufferAimed;
        // Real play lets a clear land before its cards or the ending cover it: the floor keeps running until rewardRevealAt
        // (unscaled time), then the cards ignore input for RewardInputLock so a held attack cannot pick one. A cleared floor
        // with a single way on is left by standing in its exit for AutoExitDwell; a hero already in it as the cards close
        // stands AutoExitGrace longer, so the floor does not end the moment they are taken.
        internal const float RewardRevealDelay=.7f,BossRewardRevealDelay=1.2f,RewardInputLock=.35f,AutoExitReach=.6f,AutoExitDwell=.15f,AutoExitGrace=.6f;
        internal float rewardRevealAt;
        private float exitDwell;
        internal bool ClearRevealing{get{return Time.unscaledTime<rewardRevealAt;}}
        internal bool RewardInputLocked{get{return rewardRevealAt>0&&Time.unscaledTime<rewardRevealAt+RewardInputLock;}}
        /// <summary>The floor's exit is walked through rather than chosen: not a branch floor's doors, and not the oath trial.</summary>
        internal bool AutoExitFloor{get{return !run.IsBranchFloor&&!run.IsOathTrial;}}
        // One extra jump in the air; touching any support restores it.
        internal const int MaxAirJumps=1;
        internal const float AirJumpVelocity=9.6f;
        internal int airJumps;
        private Vector2 previousHeroPosition;
        private GameObject playerHitCore;
        // True while a hostile bolt closes in on the hero's chest; the hit core then shows itself (TowerProjectiles.cs).
        internal bool FocusActive;
        [Obsolete("Ctrl precision movement was removed; the hit core shows itself as hostile bolts close in. Kept so fixtures still compile.")]
        internal bool FocusOverride;
        private bool pendingSlash;
        internal int kills,seed;
        private AudioSource audioSource;
        private TowerMusic music;
        private AudioClip slashSound,hitSound,chimeSound;
        internal const float MaxHealth=160;
        internal float PlayerMaxHealth { get { return run.Player.MaxHealth; } }
        internal float PlayerDamage { get { return run.Player.Damage; } }
        internal float PlayerArmor { get { return run.Player.Armor; } }
        internal float PlayerMoveSpeed { get { return run.Player.MoveSpeed; } }
        internal float PlayerAttackInterval { get { return run.Player.AttackInterval*BannerInterval(hero); } }
        internal int ActiveProjectiles { get { return shots.Count; } }
        internal bool SmokeControlled;
        internal float MoveOverride;
        internal bool AttackOverride,JumpOverride,DashOverride,DropOverride;
        internal bool Paused { get { return intro||menu||party||help||shopOpen||VillageBlocking||(run.IsRewardPending||run.IsComplete)&&!ClearRevealing||hero.hp<=0; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Enter()
        {
            if(SceneManager.GetActiveScene().name!="20_Roguelike"||FindFirstObjectByType<TowerGame>()!=null)return;
            new GameObject("Tower of Companions").AddComponent<TowerGame>();
        }
        private void Awake()
        {
            Application.targetFrameRate=60;
            // The HUD draws with absolute-rect GUI calls only (no GUILayout or GUI.Window), so the IMGUI layout pass is skipped.
            useGUILayout=false;
            view=Camera.main;
            if(view==null){var c=new GameObject("Tower camera");c.tag="MainCamera";view=c.AddComponent<Camera>();c.AddComponent<AudioListener>();}
            view.orthographic=true;view.orthographicSize=5.8f;view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.035f,.065f,.105f);
            view.transform.rotation=Quaternion.identity;view.transform.position=new Vector3(-8,0,-10);
            vfx=TowerVfx.Create(transform);world=new TowerWorld(transform);ConfigurePixelCamera();
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.volume=.22f;
            music=TowerMusic.Create(transform);
            slashSound=Tone(220,.11f);hitSound=Tone(95,.10f);chimeSound=Tone(660,.28f);BuildCombatCues();
            progress=TowerProgress.Load(PlayerPrefs.GetString(ProgressKey,""));
            campaign=TowerCampaignSave.Load(PlayerPrefs.GetString(CampaignKey,""));LoadAttackMode();
            var launchArgs=Environment.GetCommandLineArgs();
            bool visualFixture=Array.IndexOf(launchArgs,"-towerAnimationSmoke")>=0||Array.IndexOf(launchArgs,"-towerArtSmoke")>=0||Array.IndexOf(launchArgs,"-towerMonsterRigSmoke")>=0;
            if(visualFixture)
            {
                // Visual fixtures instantiate their own actors/camera. They do
                // not require a random floor or unrelated theme resources.
                SuppressProgressPersistence=true;enabled=false;seed=15;
                run=new TowerRun(seed,TowerRun.DefaultMaxFloor,false);
            }
            else NewRun();
            var driver=gameObject.AddComponent<TowerSmokeDriver>();driver.Initialize(this);
            // A real launch opens on the title over the village; fixtures keep driving their own runs.
            if(!visualFixture&&!SuppressProgressPersistence&&!SmokeControlled){EnterVillage();intro=true;}
        }
        internal void NewRun(int? fixedSeed=null,string oathCompanionId=null)
        {
            // Free climbs open with a recruit from every mythology; fixtures compose their own parties.
            seed=fixedSeed??(Environment.TickCount&0x7fffffff);
            StartRun(new TowerRun(seed,TowerRun.DefaultMaxFloor,!SmokeControlled,oathCompanionId));
        }
        /// <summary>Replaces every actor and starts <paramref name="next"/> on its first floor; seed must already be set.</summary>
        private void StartRun(TowerRun next)
        {
            ResetActors();
            run=next;
            SpawnHero(new Vector2(-12,TowerWorld.Ground));
            SpawnFloor();
        }
        private void ResetActors()
        {
            ResetEnemyCombat();ClearVillage();ClearPractice();ResetLoot();
            foreach(var e in enemies)if(e.root)Destroy(e.root);enemies.Clear();
            foreach(var a in allies)if(a.root)Destroy(a.root);allies.Clear();
            foreach(var s in shots)if(s.root)Destroy(s.root);shots.Clear();
            numbers.Clear();
            if(hero!=null&&hero.root)Destroy(hero.root);
        }
        private void SpawnHero(Vector2 at)
        {
            hero=Spawn("Player",0,false,at);hero.id="player";hero.hp=hero.maxHp=PlayerMaxHealth;ResetSkills();
            elapsed=0;kills=0;healClock=0;invulnerable=0;ReadyDash();slashCooldown=0;attackTimer=0;dashTime=0;pendingSlash=false;jumpBuffer=0;coyote=0;airJumps=MaxAirJumps;
            attackBuffer=dashBuffer=0;dashBufferAimed=false;rewardRevealAt=exitDwell=0;
            menu=party=help=oathLobby=shopOpen=false;dismissId="";notice="";toasts.Clear();FocusActive=false;cameraLead=1.6f;ResetPresentation();ResetAction();CreateHitCore();ResetCombatRewards();ClearOathEffects();SyncAttackMode();
        }
        private Fighter Spawn(string name,int index,bool monster,Vector2 pos)
        {
            var f=new Fighter{root=new GameObject(name),pos=pos,isEnemy=monster};f.root.transform.SetParent(transform);f.root.transform.position=pos;
            TowerWorld.SpriteObject(f.root.transform,"Shadow",new Vector2(0,.025f),new Vector2(1.2f,.16f),new Color(0,0,0,.36f),-1,TowerWorld.Disc);
            f.art=TowerActorArt.Create(f.root.transform,index,monster);return f;
        }
        private void SpawnFloor()
        {
            using(TowerProfiling.SpawnFloor.Auto())SpawnFloorContents();
        }
        private void SpawnFloorContents()
        {
            ClearOathEffects();
            ResetEnemyCombat();
            BeginFloorRecord();
            // Rest and shop floors hold no fight, so they are a short quiet room instead of the long generated climb.
            bool peaceful=run.FloorType==FloorType.Rest||run.FloorType==FloorType.Shop;
            using(TowerProfiling.SetTheme.Auto()){world.SetTheme(run.Theme.Id,run.Floor,seed,peaceful?TowerTerrainGenerator.PeacefulLayout(seed,run.Floor):null);world.SetFloorFeature(run.FloorType,run.IsBranchFloor);}
            using(TowerProfiling.Loot.Auto()){BankLooseCoins();FlushGold();SpawnCaches();}
            using(TowerProfiling.Music.Auto())music.PlayTheme(run.Theme.Id);
            cameraCeiling=Mathf.Max(1.8f,world.HighestPlatform+4);BuildTraps();
            hero.pos=new Vector2(-12,TowerWorld.Ground);hero.velocityY=0;hero.dropTime=0;hero.facing=1;hero.grounded=true;hero.support=-1;hero.lastX=-12;
            hero.root.transform.position=hero.pos;previousHeroPosition=hero.pos;view.transform.position=new Vector3(-7,0,-10);appliedShake=Vector3.zero;
            foreach(var s in shots)Destroy(s.root);shots.Clear();ClearFloorSkillEffects();shopOpen=false;lastFallen=null;
            // A campfire left without a choice gives its heal on the way out.
            SettleRestChoice();
            if(peaceful)
            {
                // Peaceful floors hold no monsters; the first update clears them.
                SyncAllies();ResetAllyFormation();Notice(FloorTypeName(run.FloorType)+" 층  ·  "+FloorTypeSummary(run.FloorType),3);return;
            }
            bool elite=run.FloorType==FloorType.Elite;int scaled=DifficultyFloor(run.Floor);
            int count=elite?TowerDifficulty.EliteCount(scaled):TowerDifficulty.RegularCount(scaled);bool bossFloor=!elite&&TowerDifficulty.IsBossFloor(run.Floor);
            var roster=new List<TowerMonsterDefinition>(run.Theme.Monsters);
            var floorRandom=new System.Random(unchecked(seed*397 ^ run.Floor*7919));
            for(int i=roster.Count-1;i>0;i--){int swap=floorRandom.Next(i+1);var saved=roster[i];roster[i]=roster[swap];roster[swap]=saved;}
            for(int kind=0;kind<3;kind++)
            {
                int at=roster.FindIndex(kind,d=>d.Kind==kind);var saved=roster[kind];roster[kind]=roster[at];roster[at]=saved;
            }
            using(TowerProfiling.Actors.Auto())SpawnEnemies(count,elite,bossFloor,roster);
            using(TowerProfiling.Party.Auto()){SyncAllies();ResetAllyFormation();}
            // A boss built on entry raises its banner now; one still queued raises it as it streams in (SpawnNow).
            var boss=bossFloor?enemies.Find(e=>e.isBoss):null;
            if(elite)Notice("엘리트 층  ·  클리어하면 금 등급 이상 스킬 증강과 현상금 "+TowerRun.EliteBounty+" G",3);
            else if(boss!=null)AnnounceBoss(boss);
            else Notice("",0);
        }
        internal void StartClimb()
        {
            if(oathLobby){StartOathTrial(oathSelection);return;}
            // Free climbs are fixture runs only; a real title always leads into the campaign.
            if(village||run.IsCampaign||!SmokeControlled){BeginCampaign();return;}
            intro=false;Notice("",0);
        }
        internal void SaveProgress(){if(progress==null||SuppressProgressPersistence||SmokeControlled)return;goldUnsaved=false;PlayerPrefs.SetString(ProgressKey,progress.Save());PlayerPrefs.Save();}
        private void OnApplicationPause(bool paused){if(paused)SaveProgress();}
        private void OnApplicationQuit(){SaveProgress();FlushPace();}
        private void OnDestroy()
        {if(slashSound)Destroy(slashSound);if(hitSound)Destroy(hitSound);if(chimeSound)Destroy(chimeSound);DestroyCombatCues();if(uiGradient)Destroy(uiGradient);if(uiHalo)Destroy(uiHalo);if(uiDiamond)Destroy(uiDiamond);if(uiGrain)Destroy(uiGrain);if(uiVignette)Destroy(uiVignette);if(uiScrollThumb)Destroy(uiScrollThumb);if(uiSkin)Destroy(uiSkin);DestroyWeapons();FlushPace();}
        private void Update()
        {
            float frame=Mathf.Min(Time.unscaledDeltaTime,.04f);
            TickHUD(frame);TickToasts(frame);
            if(noticeTime>0)noticeTime-=frame;
            TickPace();
            if(!SmokeControlled)
            {
                if(Input.GetKeyDown(KeyCode.Escape)){if(help)help=false;else if(StoryOpen)SkipStory();else if(villagePanel!=VillagePanel.None)villagePanel=VillagePanel.None;else if(intro){}else if(shopOpen)shopOpen=false;else if(party){party=false;dismissId="";}else if(!run.IsRewardPending)menu=!menu;}
                if(Input.GetKeyDown(KeyCode.Tab)&&!intro&&!VillageBlocking&&hero.hp>0&&!run.IsComplete){party=!party;dismissId="";shopOpen=false;}
                if(StoryOpen&&!help&&(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Space)||Input.GetKeyDown(KeyCode.E)))AdvanceStory();
                else if(Input.GetKeyDown(KeyCode.Return)&&intro&&!help)StartClimb();
            }
            if(run.IsCampaign&&run.IsComplete&&!chapterRecorded)CompleteChapter();
            TickOpeningAugments();
            ConfigurePixelCamera();TickHeroBlink();
            // A press made on a paused frame (a card, a menu) never carries into play as a buffered swing or dash.
            if(Paused){attackBuffer=dashBuffer=0;AnimateIdle(frame);TickDeaths(frame);return;}
            // A short hit stop slows the simulation; input, HUD and camera keep real time.
            float dt=TickHitStop(frame);
            previousHeroPosition=hero.pos;
            CounterRemaining=Mathf.Max(0,CounterRemaining-dt);
            hero.castRemaining=Mathf.Max(0,hero.castRemaining-dt);
            elapsed+=dt;TickDashCharges(dt);slashCooldown=Mathf.Max(0,slashCooldown-dt);invulnerable=Mathf.Max(0,invulnerable-dt);
            float move=SmokeControlled?MoveOverride:(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1:0);
            bool jump=SmokeControlled?JumpOverride:Input.GetKeyDown(KeyCode.Space);
            bool drop=SmokeControlled?DropOverride:Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow);
            // A fresh S in the air is a fast fall instead (TickFastFall); fixtures press it with FastFallOverride.
            bool fastFall=SmokeControlled?FastFallOverride:Input.GetKeyDown(KeyCode.S)||Input.GetKeyDown(KeyCode.DownArrow);
            bool dash=SmokeControlled?DashOverride:Input.GetKeyDown(KeyCode.LeftShift)||Input.GetKeyDown(KeyCode.RightShift);
            bool aimedDash=!SmokeControlled&&Input.GetMouseButtonDown(1)&&!MouseInUI();
            bool attack=SmokeControlled?AttackOverride:(Input.GetKey(KeyCode.J)||(Input.GetMouseButton(0)&&!MouseInUI()));
            // A fresh press during the dash cuts it into a dash slash; a held key waits for the dash to end.
            bool attackPressed=!SmokeControlled&&(Input.GetKeyDown(KeyCode.J)||(Input.GetMouseButtonDown(0)&&!MouseInUI()));
            bool jumpHeld=SmokeControlled||Input.GetKey(KeyCode.Space);
            // Nothing slows the hero any more: the hit core fades in by itself while a hostile bolt closes in.
            FocusActive=HostileBoltClosing();TickHitCore(dt);
            if(jump)jumpBuffer=.14f;else jumpBuffer-=dt;
            if(hero.grounded){coyote=.1f;airJumps=MaxAirJumps;}else coyote-=dt;
            var heldMode=AttackMode;
            TickAim();TickAttackModeInput();SyncAttackMode();
            // Real presses wait briefly for the cooldown to end (switching weapons drops a waiting swing); fixtures never fill these.
            if(AttackMode!=heldMode)attackBuffer=0;else if(attackPressed)attackBuffer=AttackBufferTime;else attackBuffer=Mathf.Max(0,attackBuffer-dt);
            if((dash||aimedDash)&&!SmokeControlled){dashBuffer=DashBufferTime;dashBufferAimed=aimedDash&&!dash;}else dashBuffer=Mathf.Max(0,dashBuffer-dt);
            // With the mouse the hero faces the reticle once a swing is over; fixtures turn with the keys as before.
            if(MouseAims){if(dashTime<=0&&attackTimer<=0&&hero.castRemaining<=0)FaceAim();}
            else if(move!=0&&dashTime<=0&&(attackTimer<=0||!pendingSlash)&&hero.castRemaining<=0)hero.facing=move>0?1:-1;
            drop=TickFastFall(fastFall,drop);
            if(drop&&hero.grounded&&hero.support>=0){jumpBuffer=0;coyote=0;}
            if(jumpBuffer>0&&coyote>0&&hero.dropTime<=0){hero.velocityY=10.7f;hero.grounded=false;jumpBuffer=0;coyote=0;jumpCut=false;Sound(jumpCue,.3f);if(vfx!=null)vfx.Dust(hero.pos,0,.5f);}
            // Past the coyote window a press spends the air jump, which also turns a fall back upward.
            else if(jumpBuffer>0&&!hero.grounded&&airJumps>0&&hero.dropTime<=0){airJumps--;hero.velocityY=AirJumpVelocity;jumpBuffer=0;jumpCut=false;Sound(airJumpCue,.34f);if(vfx!=null){vfx.ImpactRing(hero.pos+Vector2.up*.1f,.5f,TowerWorld.Teal);vfx.Dust(hero.pos,0,.3f);}}
            // Releasing jump early cuts the rise for short hops.
            if(!jumpHeld&&!jumpCut&&!hero.grounded&&hero.velocityY>4){hero.velocityY*=.55f;jumpCut=true;}
            // A dash waits for a charge and the gap after the last one. A buffered dash keeps how it was asked for (Shift or the
            // right button) and takes the aim and keys of the moment it starts.
            if((dash||aimedDash||dashBuffer>0)&&DashReady){StartDash(move,dash||aimedDash?aimedDash&&!dash:dashBufferAimed);dashBuffer=0;}
            if(dashTime>0)TickDash(dt);
            else hero.pos.x+=move*PlayerMoveSpeed*(attackTimer>0?.85f:1)*dt;
            TickLunge(dt);
            hero.pos.x=Mathf.Clamp(hero.pos.x,TowerWorld.Left+.6f,TowerWorld.Right-.6f);

            bool wasAirborne=!hero.grounded;float fallSpeed=hero.velocityY;
            StepGravity(hero,dt,drop);hero.root.transform.position=hero.pos;
            LandingFeedback(wasAirborne,fallSpeed);TickTraps(dt);
            // A buffered press counts as fresh, so one made early in a dash still cuts it into a dash slash.
            bool swing=attack||attackBuffer>0,fresh=attackPressed||attackBuffer>0;
            if(AttackMode==TowerAttackMode.Sword){if(swing&&slashCooldown<=0&&hero.castRemaining<=0&&(dashTime<=0||fresh&&dashTime<=.12f)){StartPlayerAttack();attackBuffer=0;}}
            else if(AttackMode==TowerAttackMode.Gun&&GunTriggerReady(swing)){FireGun();attackBuffer=0;}
            TickPlayerAttack(dt);TickOrb(dt);
            hero.hurt=Mathf.Max(0,hero.hurt-dt);
            float actualHeroSpeed=dt>0?(hero.pos.x-previousHeroPosition.x)/dt:0;
            hero.art.SetLocomotion(hero.grounded,hero.velocityY,dashTime>0,Mathf.Abs(actualHeroSpeed),actualHeroSpeed);
            hero.art.SetPose(Mathf.Abs(actualHeroSpeed)>.08f,attackTimer>0,hero.hurt>0,hero.facing,dt);
            using(TowerProfiling.Enemies.Auto()){TickSpawnQueue();foreach(var enemy in enemies)TickEnemy(enemy,dt);}
            SamplePace();
            if(hero.hp<=0){TickEffects(dt);return;}
            using(TowerProfiling.Allies.Auto())TickAllies(dt);
            using(TowerProfiling.Skills.Auto()){TickSkills(dt);TickOath(dt);}
            using(TowerProfiling.Shots.Auto())TickShots(dt);
            using(TowerProfiling.Effects.Auto())TickEffects(dt);
            using(TowerProfiling.Loot.Auto())TickLoot(dt);
            if(hero.hp<=0)return;
            if(village)TickVillage(dt);
            else if(practice)TickPractice(dt);
            // ClearFloor refuses while other cards (a draft, the tree's opening cards) are pending, so a clear is recorded,
            // healed and paid exactly once, after they are taken.
            else if(RemainingEnemies==0&&!run.IsFloorCleared&&run.ClearFloor())
            {
                ClearOathEffects();foreach(var shot in shots)if(shot.root)Destroy(shot.root);shots.Clear();RecordFloorResult();FlushGold();CancelPendingSkills();hero.hp=Mathf.Min(PlayerMaxHealth,hero.hp+18);Sound(chimeSound,.8f);
                if(run.FloorType==FloorType.Elite)DropEliteBounty();
                RewardTreeFloorClear();
                if(run.FloorType==FloorType.Rest||run.FloorType==FloorType.Shop)OnSpecialFloorCleared();
                else
                {
                    // The last blow lands in slow motion, and in real play the clear plays out before its cards or the ending
                    // cover it; the card screen names the floor itself, so only floors without cards announce the clear.
                    HitStop(.15f);
                    if(!SmokeControlled&&(run.IsRewardPending||run.IsComplete))
                        rewardRevealAt=Time.unscaledTime+(run.FloorType==FloorType.Normal&&TowerDifficulty.IsBossFloor(run.Floor)?BossRewardRevealDelay:RewardRevealDelay);
                    if(!run.IsRewardPending)Notice(run.IsBranchFloor?"층 클리어  ·  원하는 출구를 고르세요":"층 클리어",2.5f);
                }
            }
            if(!SmokeControlled&&village&&Input.GetKeyDown(KeyCode.E))InteractVillage();
            else if(!SmokeControlled&&run.IsFloorCleared&&!run.IsRewardPending&&Input.GetKeyDown(KeyCode.E))
            {if(NearMerchant)InteractMerchant();else{var gate=NearestGate();if(gate.HasValue)NextFloor(gate.Value);}}
            // In real play a lone exit is walked through: standing in it a moment climbs on. Branch doors and the oath trial wait for E.
            if(!SmokeControlled&&!village&&!practice&&AutoExitFloor&&run.IsFloorCleared&&!run.IsRewardPending&&!run.IsComplete&&!shopOpen&&hero.pos.x>=TowerWorld.ExitX-AutoExitReach)
            {exitDwell+=dt;if(exitDwell>=AutoExitDwell){exitDwell=0;NextFloor();}}
            // The clear plays out (unpaused) with its cards pending, so the dwell leaves the cards owing the grace.
            else exitDwell=run.IsRewardPending?-AutoExitGrace:0;
            using(TowerProfiling.World.Auto())world.Tick(dt,village||run.IsFloorCleared&&!run.IsRewardPending);
            // Real play leads toward the reticle and the keys and runs ahead of the hero's actual speed; fixtures keep the key lead.
            bool aimLead=AimLeadsCamera&&!practice;
            if(practice)cameraLead=PracticeCameraLead;
            else if(aimLead)cameraLead=Mathf.MoveTowards(cameraLead,CameraLeadGoal(move),frame*CameraLeadRate);
            else if(move!=0)cameraLead=Mathf.MoveTowards(cameraLead,move*1.6f,frame*4);
            float cameraX=Mathf.Clamp(hero.pos.x+cameraLead+(aimLead?CameraSpeedLead(actualHeroSpeed):0),-7.5f,TowerWorld.Right-7.5f);
            // The practice range keeps its ground at the same screen height on every resolution, above its skill bar.
            float cameraY=practice?PracticeCameraY:Mathf.Clamp(hero.pos.y+1.5f,0,cameraCeiling);
            Vector3 destination=new Vector3(cameraX,cameraY,-10);
            Vector3 settled=Vector3.Lerp(view.transform.position-appliedShake,destination,1-Mathf.Exp(-frame*5));
            appliedShake=NextShake(frame);
            view.transform.position=SnapCamera(settled+appliedShake);
        }
        private void AnimateIdle(float dt)
        {
            float poseDt=intro||hero.hp<=0?dt:0;
            hero.art.SetPose(false,false,false,hero.facing,poseDt);TickOrb(poseDt,false);
            // A frozen frame would hold a hit flash lit, so a pause ends it.
            foreach(var e in enemies){if(!intro)e.art.ClearHitFlash();e.art.SetPose(false,false,false,e.facing,intro?dt:0);}
            foreach(var a in allies)a.art.SetPose(false,false,false,a.facing,intro?dt:0);
        }
        private void StepGravity(Fighter f,float dt,bool drop=false)
        {
            // Standing on a platform (support >= 0) lets a drop input fall through it; the rolling ground never drops.
            if(drop&&f.grounded&&f.support>=0)
            {f.dropTime=.22f;f.dropHeight=f.pos.y;f.dropSupport=f.support;f.support=-1;f.grounded=false;f.velocityY=Mathf.Min(f.velocityY,-2.5f);}
            f.dropTime=Mathf.Max(0,f.dropTime-dt);
            float xOld=float.IsNaN(f.lastX)?f.pos.x:f.lastX,y=f.pos.y;bool grounded=f.grounded;int support=f.support;
            TowerPhysics.Step(world.CurrentLayout,xOld,f.pos.x,ref y,ref f.velocityY,ref grounded,ref support,dt,f.dropTime>0?f.dropSupport:-1);
            f.pos.y=y;f.grounded=grounded;f.support=support;f.lastX=f.pos.x;
        }

        private void PlayerSlash()
        {
            // The third combo hit and dash slashes reach further, hit harder and knock back more.
            bool dashSlash=slashIsDash,finisher=!dashSlash&&slashCombo==2;slashIsDash=false;slashCombo=0;
            bool heavy=dashSlash||finisher;
            // Fixtures cut along the facing they hold when the blow lands; the mouse fixed the aim when the swing began.
            if(!MouseAims)attackAim=new Vector2(hero.facing,0);
            Vector2 origin=hero.pos+Vector2.up+attackAim*1.1f;
            bool counter=CounterRemaining>0;Color color=counter?TowerWorld.Teal:TowerWorld.Gold;
            if(vfx!=null){if(heavy)vfx.AimedHeavySlash(origin,attackAim,hero.facing,swingRising,color);else vfx.AimedSlash(origin,attackAim,hero.facing,swingRising,color);}
            bool connected=false;
            float reach=run.Player.Range*(dashSlash?DashSlashReach:finisher?FinisherReach:1);
            float damage=PlayerDamage*(counter?DodgeCounterMultiplier:1)*(dashSlash?DashSlashMultiplier:finisher?FinisherMultiplier:1);
            float push=hero.facing*(heavy?.9f:.35f);
            // The cut takes poise, twice a plain cut's for a heavy blow or a counter; only the counter also breaks a monster
            // within its guard. Its knockback slides out over time (PlayerBlow, TowerPoise.cs).
            float poise=heavy||counter?HeavyBlowPoise:SwordPoise;bool breaker=counter;
            Vector2 oathTarget=origin;float nearest=float.PositiveInfinity;
            // A down-cut in the air bounces off a struck enemy, preferring one other than the last bounce's (Pogo).
            Fighter bounce=null;
            practiceStrike=-1;
            var struck=enemies.ToArray();for(int i=struck.Length-1;i>=0;i--){var e=struck[i];if(CanTarget(e,run.Player)&&SwingReaches(e.pos+Vector2.up*.85f,reach,.4f,1.9f)){float distance=(e.pos-hero.pos).sqrMagnitude;if(distance<nearest){nearest=distance;oathTarget=e.pos;}StrikeEnemy(e,damage,run.Player,Staggerable(e)?0:push);PlayerBlow(e,push,poise,breaker);HoldStruck(e);connected=true;if(bounce==null||bounce==pogoTarget)bounce=e;}}
            practiceStrike=0;
            if(StrikeTraps(reach,heavy))connected=true;
            if(village&&StrikeDummies(reach,damage,heavy))connected=true;
            bool smashed=StrikeCaches(reach,heavy);
            // The counter and the dash slash cut the bolts they reach; a counter that connects reflects two (TowerProjectiles.cs).
            if(counter||dashSlash)SlashBolts(reach,counter,connected);
            if(connected)
            {
                if(counter)ConsumeDodgeCounter();OnOathSwordHit(oathTarget);Sound(heavy?heavyCue:strikeCue,.65f);
                if(dashSlash)DashSlashes++;else if(finisher)Finishers++;
                // Only heavy blows stop time (the counter its own, in ConsumeDodgeCounter); a light hit holds just the struck enemies.
                if(heavy){HitStop(HeavyHitStop);Shake(.11f,.16f);KickShake(attackAim,.11f);}else Shake(.04f,.08f);
                if(heavy)FloatText(hero.pos+Vector2.up*2.6f,dashSlash?"돌진 베기":"마무리 일격",TowerWorld.Gold);
            }
            // Breaking a cache gives its own small jolt (heavy blows only) but never spends the dodge counter or charges the oath.
            else if(smashed){Sound(heavy?heavyCue:strikeCue,.5f);if(heavy)HitStop(.05f);}
            Pogo(bounce);
        }

        private void Fire(Vector2 from,Vector2 to,float speed,float damage,bool hostile,Color color,float area=0,HeroState owner=null,int pierce=1,float slowDuration=0)
        {
            var go=TowerWorld.SpriteObject(transform,hostile?"Enemy bolt":"Companion bolt",from,area>.5f?Vector2.one*.65f:pierce>1?new Vector2(.8f,.35f):new Vector2(.45f,.16f),color,120,TowerWorld.Glow);
            go.transform.localScale=Vector3.one;
            var v=(to-from).normalized*speed;go.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(v.y,v.x)*Mathf.Rad2Deg);
            shots.Add(new Shot{root=go,pos=from,velocity=v,damage=damage,hostile=hostile,color=color,area=area,owner=owner,pierce=pierce,slowDuration=slowDuration,life=4,practiceCast=practiceStrike>0?practiceStrike:PracticeCasts,level=striking?strikeLevel:1,skill=striking});
            if(hostile)StyleHostileLastShot();else StyleLastShot(owner is CompanionState&&((CompanionState)owner).Definition.Role==CompanionRole.Archer?SkillEffect.MultiShot:SkillEffect.ArcaneVolley,false);
        }

        private bool criticalHit;
        private void DamageEnemy(Fighter e,float damage,float push)
        {
            bool critical=criticalHit;criticalHit=false;
            // Bosses barely give ground to ordinary blows. Skills and companions push at once; the player's own blows on other
            // monsters pass no push here and slide them back instead (PlayerBlow, TowerPoise.cs).
            if(!CanTarget(e,null))return;PaceDamage(e,damage);e.hp-=damage;e.hurt=.14f;e.pos.x=Mathf.Clamp(e.pos.x+push*(e.isBoss?BossPushScale:1),TowerWorld.Left,TowerWorld.Right);
            DamageNumber(e,damage,critical);Burst(e.pos+Vector2.up,TowerWorld.Gold,4,.3f);Sound(PlayerStrike?strikeCue:struckCue,.35f);
            if(practice)PracticeStruck(e,damage);
            // Only the player's own blows stop time on a kill; companions' kills and damage over time just nudge the view.
            if(e.hp<=0){kills++;lastFallen=e.pos;if(vfx!=null)vfx.KillBurst(e.pos+Vector2.up*(e.isBoss?1.6f:.9f),e.isBoss?TowerWorld.Gold:TowerWorld.Teal);if(PlayerStrike)HitStop(e.isBoss?BossKillHitStop:PlayerKillHitStop);else Shake(.03f,.08f);if(e.isBoss)Shake(.3f,.45f);if(IsFinalBoss(e)){Shake(.6f,1.1f);Toast(e.monster.Name+" 붕괴","지구를 삼킨 악신이 무너집니다",brass,4);}enemies.Remove(e);RetireEnemy(e);StatusesOnFall(e);}
        }
        private void HurtHero(float damage)
        {
            // The player is the only party member with a hostile hit receiver.
            // Companions remain offensive actors without damage, stagger or knockback.
            if(invulnerable>0||hero.hp<=0)return;
            if(PassiveWardsHit())return;
            FloorHits++;CounterRemaining=0;PaceHurt();
            damage=MitigatePlayerDamage(damage*PassiveDamageTakenScale);if(damage<=0)return;
            DamageTaken+=Mathf.Min(hero.hp,damage);
            hero.hp=Mathf.Max(0,hero.hp-damage);hero.hurt=.25f;invulnerable=.65f;HitStop(.05f);Shake(.16f,.22f);
            FloatText(hero.pos+Vector2.up*2,"−"+Mathf.RoundToInt(damage),new Color(1,.4f,.35f));Sound(hurtCue,.8f);
            if(hero.hp<=0)PassiveRebirth();
            if(hero.hp<=0){ClearOathEffects();hero.art.PlayDeath(.65f);}else OnPassiveHurt();
        }
        internal void SyncAllies()
        {
            for(int i=allies.Count-1;i>=0;i--)if(run.GetCompanion(allies[i].id)==null){Destroy(allies[i].root);allies.RemoveAt(i);}
            foreach(var c in run.Companions)
            {
                var a=allies.Find(f=>f.id==c.Definition.Id);
                if(a==null){a=Spawn(c.Definition.Name,c.Definition.ArtIndex,false,FormationAnchor(c));a.id=c.Definition.Id;allies.Add(a);}
                a.art.transform.localScale=Vector3.one*.82f;var shadow=a.root.transform.Find("Shadow");if(shadow)shadow.localScale=new Vector3(.9f,.12f,1);
            }
        }
        internal void SelectOffer(long id)
        {
            var offer=run.Offers.Count>0?FindOffer(id):null;bool recruit=offer!=null&&offer.Kind==RewardKind.Recruit;
            if(!run.ChooseOffer(id))return;
            ReconcileLoadout();Sound(chimeSound,.8f);
            if(recruit)
            {
                var c=run.GetCompanion(offer.CompanionId);if(c==null)return;
                var myth=TowerMythCatalog.Get(c.Definition.MythId);var synergy=TowerMythCatalog.GetSynergy(myth.Id);int members=run.MythCount(myth.Id);
                Toast(c.Name+" 합류",RoleName(c.Definition.Role)+"  ·  "+myth.Name+"  "+members+" / 3",TierColor(c.Definition.Tier),3.2f);
                if(members>=2)Toast("신화 시너지 「"+synergy.Name+"」 "+(members>=3?"2단계":"발동"),synergy.Describe(members),brass,4.2f);
            }
            else Toast(offer==null?"증강 획득":offer.Title.Replace(" 습득","").Replace(" 강화",""),offer==null?"":offer.Kind==RewardKind.Skill?(offer.IsUpgrade?"스킬 Lv."+offer.NextLevel+" 강화":"스킬 습득"):offer.Kind==RewardKind.Oath?"서약 변형":"능력치 강화",offer==null?brass:TierColor(offer.Tier));
        }
        private RewardOffer FindOffer(long id){foreach(var offer in run.Offers)if(offer.Id==id)return offer;return null;}
        internal void Dismiss(string id)
        { var c=run.GetCompanion(id);if(run.DismissCompanion(id)){ReconcileLoadout();dismissId="";Toast("동료와 작별",(c==null?"":c.Name+"  ·  ")+"능력도 함께 사라졌습니다",muted);} }
        internal void NextFloor(FloorType type=FloorType.Normal){if(run.AdvanceFloor(type))SpawnFloor();}
        internal void Notice(string text,float seconds){notice=text;noticeTime=seconds;}
        // At most MaxNumbers float at once; a new one past that drops the oldest.
        internal const int MaxNumbers=40;
        internal const float CompanionNumberSize=.7f,DotNumberWindow=.95f,CompanionCastLabelSize=14f/24,CompanionCastLabelLife=.5f;
        private Number companionCastLabel;
        private void FloatText(Vector2 pos,string text,Color color){AddNumber(pos,text,color,1,.8f);}
        private Number AddNumber(Vector2 pos,string text,Color color,float size,float life)
        {
            if(numbers.Count>=MaxNumbers)numbers.RemoveAt(0);
            var n=new Number{pos=pos,text=text,color=color,life=life,size=size};numbers.Add(n);return n;
        }
        // Companions' damage reads smaller and in silver; damage over time gathers into one number per enemy about each second.
        private void DamageNumber(Fighter e,float damage,bool critical)
        {
            bool companion=lastStrikeOwner is CompanionState;
            Color color=companion?silver:critical?new Color(1,.5f,.32f):TowerWorld.Gold;float size=companion?CompanionNumberSize:1;
            if(lastStrikeDot)
            {
                foreach(var n in numbers)
                    if(n.tally==e&&elapsed-n.since<DotNumberWindow){n.total+=damage;n.text=Mathf.CeilToInt(n.total).ToString();n.life=Mathf.Max(n.life,.5f);return;}
                var tally=AddNumber(e.pos+Vector2.up*1.8f,Mathf.CeilToInt(damage).ToString(),color,size,.8f);tally.tally=e;tally.total=damage;tally.since=elapsed;return;
            }
            AddNumber(e.pos+Vector2.up*1.8f,Mathf.CeilToInt(damage)+(critical?"!":""),color,size,.8f);
        }
        // A companion's cast is named on a small tag in its colour; only the latest one shows.
        private void CompanionCastLabel(Vector2 pos,string text,Color color)
        {
            if(companionCastLabel!=null)numbers.Remove(companionCastLabel);
            companionCastLabel=AddNumber(pos,text,color,CompanionCastLabelSize,CompanionCastLabelLife);
        }
        private void Burst(Vector2 pos,Color color,int count,float size)
        {
            if(vfx!=null)vfx.Hit(pos,color);
        }
        private void SlashArc(Vector2 center,int direction,Color color)
        {if(vfx!=null)vfx.BasicSlash(center,direction,color);}
        private void TickEffects(float dt)
        {
            if(vfx!=null)vfx.Tick(dt);TickDeaths(dt);
            for(int i=numbers.Count-1;i>=0;i--){numbers[i].life-=dt;numbers[i].pos+=Vector2.up*dt*1.4f;if(numbers[i].life<=0)numbers.RemoveAt(i);}
        }
        private AudioClip Tone(float hz,float length)
        {int n=(int)(22050*length);var data=new float[n];for(int i=0;i<n;i++){float t=i/22050f;data[i]=(Mathf.Sin(t*hz*6.283f)+Mathf.Sin(t*hz*12.566f)*.25f)*Mathf.Pow(1-i/(float)n,2)*.35f;}var c=AudioClip.Create("Tower sound",n,1,22050,false);c.SetData(data,0);return c;}
        private bool MouseInUI()
        {
            float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);
            Vector2 offset=new Vector2((Screen.width-1600*scale)*.5f,(Screen.height-900*scale)*.5f);
            return HUDHit((new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y)-offset)/scale);
        }
        internal void BackToHub(){if(Application.CanStreamedLevelBeLoaded("00_Hub"))SceneManager.LoadScene("00_Hub");else{EnterVillage();intro=true;}}
    }
}
