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
            // Platform index the actor stands on (-1: the ground), the platform being dropped through, and the x at the previous step.
            public int support=-1,dropSupport=-1; public float lastX=float.NaN;
            public float damageScale=1,moveScale=1,bulletSpeedScale=1,attackRateScale=1;
            public TowerMonsterDefinition monster;
            public TowerThemeId theme;
            // Shown instead of the species name, e.g. an awakened chapter boss.
            public string title;
        }
        private sealed class Shot { public GameObject root; public Vector2 pos,velocity; public float damage,life,area,slowDuration,hitRadius=.16f; public bool hostile,oathChain,basic,empowered,forceCrit; public Color color; public HeroState owner; public SkillEffect effect; public int pierce=1; public readonly HashSet<Fighter> hit=new HashSet<Fighter>(); }
        internal sealed class Number { public Vector2 pos; public string text; public Color color; public float life; }
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
        // One extra jump in the air; touching any support restores it.
        internal const int MaxAirJumps=1;
        internal const float AirJumpVelocity=9.6f;
        internal int airJumps;
        private Vector2 previousHeroPosition;
        private GameObject playerHitCore;
        internal bool FocusActive,FocusOverride;
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
        internal float PlayerAttackInterval { get { return run.Player.AttackInterval; } }
        internal int ActiveProjectiles { get { return shots.Count; } }
        internal bool SmokeControlled;
        internal float MoveOverride;
        internal bool AttackOverride,JumpOverride,DashOverride,DropOverride;
        internal bool Paused { get { return intro||menu||party||help||shopOpen||VillageBlocking||run.IsRewardPending||hero.hp<=0||run.IsComplete; } }

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
            slashSound=Tone(220,.11f);hitSound=Tone(95,.10f);chimeSound=Tone(660,.28f);
            progress=TowerProgress.Load(PlayerPrefs.GetString(ProgressKey,""));
            campaign=TowerCampaignSave.Load(PlayerPrefs.GetString(CampaignKey,""));
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
            ResetEnemyCombat();ClearVillage();ResetLoot();
            foreach(var e in enemies)if(e.root)Destroy(e.root);enemies.Clear();
            foreach(var a in allies)if(a.root)Destroy(a.root);allies.Clear();
            foreach(var s in shots)if(s.root)Destroy(s.root);shots.Clear();
            numbers.Clear();
            if(hero!=null&&hero.root)Destroy(hero.root);
        }
        private void SpawnHero(Vector2 at)
        {
            hero=Spawn("Player",0,false,at);hero.id="player";hero.hp=hero.maxHp=PlayerMaxHealth;ResetSkills();
            elapsed=0;kills=0;healClock=0;invulnerable=0;dashCooldown=0;slashCooldown=0;attackTimer=0;dashTime=0;pendingSlash=false;jumpBuffer=0;coyote=0;airJumps=MaxAirJumps;
            menu=party=help=oathLobby=shopOpen=false;dismissId="";notice="";toasts.Clear();FocusActive=false;cameraLead=1.6f;ResetPresentation();ResetAction();CreateHitCore();ResetCombatRewards();ClearOathEffects();
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
            using(TowerProfiling.SetTheme.Auto()){world.SetTheme(run.Theme.Id,run.Floor,seed);world.SetFloorFeature(run.FloorType,run.IsBranchFloor);}
            using(TowerProfiling.Loot.Auto()){BankLooseCoins();FlushGold();SpawnCaches();}
            using(TowerProfiling.Music.Auto())music.PlayTheme(run.Theme.Id);
            cameraCeiling=Mathf.Max(1.8f,world.HighestPlatform+4);BuildTraps();
            hero.pos=new Vector2(-12,TowerWorld.Ground);hero.velocityY=0;hero.dropTime=0;hero.facing=1;hero.grounded=true;hero.support=-1;hero.lastX=-12;
            hero.root.transform.position=hero.pos;previousHeroPosition=hero.pos;view.transform.position=new Vector3(-7,0,-10);appliedShake=Vector3.zero;
            foreach(var s in shots)Destroy(s.root);shots.Clear();ClearFloorSkillEffects();shopOpen=false;lastFallen=null;
            if(run.FloorType==FloorType.Rest||run.FloorType==FloorType.Shop)
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
            var boss=bossFloor?enemies.Find(e=>e.isBoss):null;
            if(elite)Notice("엘리트 층  ·  클리어하면 금 등급 이상 스킬 증강과 현상금 "+TowerRun.EliteBounty+" G",3);
            else if(boss!=null&&IsFinalBoss(boss))Notice(boss.monster.Name+" 강림  ·  지구를 삼킨 자",4);
            else Notice(boss!=null?(boss.title??boss.monster.Name)+" 출현  ·  "+SignatureName(run.Theme.Id):"",boss!=null?3:0);
        }
        internal void StartClimb()
        {
            if(oathLobby){StartOathTrial(oathSelection);return;}
            if(village||run.IsCampaign){BeginCampaign();return;}
            intro=false;Notice("",0);
        }
        internal void SaveProgress(){if(progress==null||SuppressProgressPersistence||SmokeControlled)return;goldUnsaved=false;PlayerPrefs.SetString(ProgressKey,progress.Save());PlayerPrefs.Save();}
        private void OnApplicationPause(bool paused){if(paused)SaveProgress();}
        private void OnApplicationQuit(){SaveProgress();}
        private void OnDestroy()
        {if(slashSound)Destroy(slashSound);if(hitSound)Destroy(hitSound);if(chimeSound)Destroy(chimeSound);if(uiGradient)Destroy(uiGradient);if(uiHalo)Destroy(uiHalo);if(uiDiamond)Destroy(uiDiamond);if(uiGrain)Destroy(uiGrain);if(uiVignette)Destroy(uiVignette);if(uiScrollThumb)Destroy(uiScrollThumb);if(uiSkin)Destroy(uiSkin);}
        private void Update()
        {
            float frame=Mathf.Min(Time.unscaledDeltaTime,.04f);
            TickHUD(frame);TickToasts(frame);
            if(noticeTime>0)noticeTime-=frame;
            if(!SmokeControlled)
            {
                if(Input.GetKeyDown(KeyCode.Escape)){if(help)help=false;else if(StoryOpen)SkipStory();else if(villagePanel!=VillagePanel.None)villagePanel=VillagePanel.None;else if(intro){}else if(shopOpen)shopOpen=false;else if(party){party=false;dismissId="";}else if(!run.IsRewardPending)menu=!menu;}
                if(Input.GetKeyDown(KeyCode.Tab)&&!intro&&!VillageBlocking&&hero.hp>0&&!run.IsComplete){party=!party;dismissId="";shopOpen=false;}
                if(StoryOpen&&!help&&(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Space)||Input.GetKeyDown(KeyCode.E)))AdvanceStory();
                else if(Input.GetKeyDown(KeyCode.Return)&&intro&&!help)StartClimb();
            }
            if(run.IsCampaign&&run.IsComplete&&!chapterRecorded)CompleteChapter();
            TickOpeningAugments();
            ConfigurePixelCamera();
            if(Paused){AnimateIdle(frame);TickDeaths(frame);return;}
            // A short hit stop slows the simulation; input, HUD and camera keep real time.
            float dt=TickHitStop(frame);
            previousHeroPosition=hero.pos;
            CounterRemaining=Mathf.Max(0,CounterRemaining-dt);
            hero.castRemaining=Mathf.Max(0,hero.castRemaining-dt);
            elapsed+=dt;dashCooldown=Mathf.Max(0,dashCooldown-dt);slashCooldown=Mathf.Max(0,slashCooldown-dt);invulnerable=Mathf.Max(0,invulnerable-dt);
            float move=SmokeControlled?MoveOverride:(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1:0);
            bool jump=SmokeControlled?JumpOverride:Input.GetKeyDown(KeyCode.Space);
            bool drop=SmokeControlled?DropOverride:Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow);
            bool dash=SmokeControlled?DashOverride:Input.GetKeyDown(KeyCode.LeftShift)||Input.GetKeyDown(KeyCode.RightShift);
            bool attack=SmokeControlled?AttackOverride:(Input.GetKey(KeyCode.J)||(Input.GetMouseButton(0)&&!MouseInUI()));
            // A fresh press during the dash cuts it into a dash slash; a held key waits for the dash to end.
            bool attackPressed=!SmokeControlled&&(Input.GetKeyDown(KeyCode.J)||(Input.GetMouseButtonDown(0)&&!MouseInUI()));
            bool jumpHeld=SmokeControlled||Input.GetKey(KeyCode.Space);
            FocusActive=SmokeControlled?FocusOverride:Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl);
            if(playerHitCore)playerHitCore.SetActive(FocusActive);
            if(jump)jumpBuffer=.14f;else jumpBuffer-=dt;
            if(hero.grounded){coyote=.1f;airJumps=MaxAirJumps;}else coyote-=dt;
            if(move!=0&&dashTime<=0&&(attackTimer<=0||!pendingSlash)&&hero.castRemaining<=0)hero.facing=move>0?1:-1;
            if(drop&&hero.grounded&&hero.support>=0){jumpBuffer=0;coyote=0;}
            if(jumpBuffer>0&&coyote>0&&hero.dropTime<=0){hero.velocityY=10.7f;hero.grounded=false;jumpBuffer=0;coyote=0;jumpCut=false;Sound(slashSound,.28f);if(vfx!=null)vfx.Dust(hero.pos,0,.5f);}
            // Past the coyote window a press spends the air jump, which also turns a fall back upward.
            else if(jumpBuffer>0&&!hero.grounded&&airJumps>0&&hero.dropTime<=0){airJumps--;hero.velocityY=AirJumpVelocity;jumpBuffer=0;jumpCut=false;Sound(slashSound,.34f);if(vfx!=null){vfx.ImpactRing(hero.pos+Vector2.up*.1f,.5f,TowerWorld.Teal);vfx.Dust(hero.pos,0,.3f);}}
            // Releasing jump early cuts the rise for short hops.
            if(!jumpHeld&&!jumpCut&&!hero.grounded&&hero.velocityY>4){hero.velocityY*=.55f;jumpCut=true;}
            if(dash&&dashCooldown<=0){if(move!=0)hero.facing=move>0?1:-1;attackTimer=0;pendingSlash=false;lungeTime=0;CancelPlayerCast();hero.art.InterruptAction();dashTime=.18f;BeginDodgeCounter();dashCooldown=1.15f*run.Player.DodgeCooldownMultiplier;invulnerable=Mathf.Max(invulnerable,.25f);if(vfx!=null){vfx.Afterimage(hero.root.transform);vfx.Cast(SkillEffect.DashStrike,hero.pos+Vector2.up,1,TowerWorld.Teal);if(hero.grounded)vfx.Dust(hero.pos,hero.facing,.9f);vfx.SpeedLines(hero.pos+Vector2.up*.9f,hero.facing,TowerWorld.Teal);}Sound(slashSound,.5f);}
            if(dashTime>0)
            {
                dashTime-=dt;hero.pos.x+=hero.facing*19*dt;
                // Air dashes hold their height instead of dropping mid-dodge.
                if(!hero.grounded)hero.velocityY=Mathf.Max(0,hero.velocityY);
                if(vfx!=null&&elapsed>=nextAfterimage){nextAfterimage=elapsed+.05f;vfx.Afterimage(hero.root.transform);}
                if(dashTime<=0)dashEndedAt=elapsed;
            }
            else hero.pos.x+=move*PlayerMoveSpeed*(FocusActive?.45f:1)*(attackTimer>0?.85f:1)*dt;
            TickLunge(dt);
            hero.pos.x=Mathf.Clamp(hero.pos.x,TowerWorld.Left+.6f,TowerWorld.Right-.6f);

            bool wasAirborne=!hero.grounded;float fallSpeed=hero.velocityY;
            StepGravity(hero,dt,drop);hero.root.transform.position=hero.pos;
            LandingFeedback(wasAirborne,fallSpeed);TickTraps(dt);
            if(attack&&slashCooldown<=0&&hero.castRemaining<=0&&(dashTime<=0||attackPressed&&dashTime<=.12f))StartPlayerAttack();
            TickPlayerAttack(dt);
            hero.hurt=Mathf.Max(0,hero.hurt-dt);
            float actualHeroSpeed=dt>0?(hero.pos.x-previousHeroPosition.x)/dt:0;
            hero.art.SetLocomotion(hero.grounded,hero.velocityY,dashTime>0,Mathf.Abs(actualHeroSpeed),actualHeroSpeed);
            hero.art.SetPose(Mathf.Abs(actualHeroSpeed)>.08f,attackTimer>0,hero.hurt>0,hero.facing,dt);
            using(TowerProfiling.Enemies.Auto()){TickSpawnQueue();foreach(var enemy in enemies)TickEnemy(enemy,dt);}
            if(hero.hp<=0){TickEffects(dt);return;}
            using(TowerProfiling.Allies.Auto())TickAllies(dt);
            using(TowerProfiling.Skills.Auto()){TickSkills(dt);TickOath(dt);}
            using(TowerProfiling.Shots.Auto())TickShots(dt);
            using(TowerProfiling.Effects.Auto())TickEffects(dt);
            using(TowerProfiling.Loot.Auto())TickLoot(dt);
            if(hero.hp<=0)return;
            if(village)TickVillage(dt);
            else if(RemainingEnemies==0&&!run.IsFloorCleared)
            {
                run.ClearFloor();ClearOathEffects();foreach(var shot in shots)if(shot.root)Destroy(shot.root);shots.Clear();RecordFloorResult();FlushGold();CancelPendingSkills();hero.hp=Mathf.Min(PlayerMaxHealth,hero.hp+18);Sound(chimeSound,.8f);
                if(run.FloorType==FloorType.Elite)DropEliteBounty();
                RewardTreeFloorClear();
                if(run.FloorType==FloorType.Rest||run.FloorType==FloorType.Shop)OnSpecialFloorCleared();
                else Notice(run.IsBranchFloor?"층 클리어  ·  원하는 출구를 고르세요":"층 클리어",2.5f);
            }
            if(!SmokeControlled&&village&&Input.GetKeyDown(KeyCode.E))InteractVillage();
            else if(!SmokeControlled&&run.IsFloorCleared&&!run.IsRewardPending&&Input.GetKeyDown(KeyCode.E))
            {if(NearMerchant)InteractMerchant();else{var gate=NearestGate();if(gate.HasValue)NextFloor(gate.Value);}}
            using(TowerProfiling.World.Auto())world.Tick(dt,village||run.IsFloorCleared&&!run.IsRewardPending);
            if(move!=0)cameraLead=Mathf.MoveTowards(cameraLead,move*1.6f,frame*4);
            float cameraX=Mathf.Clamp(hero.pos.x+cameraLead,-7.5f,TowerWorld.Right-7.5f);
            Vector3 destination=new Vector3(cameraX,Mathf.Clamp(hero.pos.y+1.5f,0,cameraCeiling),-10);
            Vector3 settled=Vector3.Lerp(view.transform.position-appliedShake,destination,1-Mathf.Exp(-frame*5));
            appliedShake=NextShake(frame);
            view.transform.position=SnapCamera(settled+appliedShake);
        }
        private void AnimateIdle(float dt)
        {
            float poseDt=intro||hero.hp<=0?dt:0;
            hero.art.SetPose(false,false,false,hero.facing,poseDt);
            foreach(var e in enemies)e.art.SetPose(false,false,false,e.facing,intro?dt:0);
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
            Vector2 origin=hero.pos+new Vector2(hero.facing*1.1f,1);
            bool counter=CounterRemaining>0;Color color=counter?TowerWorld.Teal:TowerWorld.Gold;
            if(heavy&&vfx!=null)vfx.HeavySlash(origin,hero.facing,color);else SlashArc(origin,hero.facing,color);
            bool connected=false;
            float reach=run.Player.Range*(dashSlash?DashSlashReach:finisher?FinisherReach:1);
            float damage=PlayerDamage*(counter?DodgeCounterMultiplier:1)*(dashSlash?DashSlashMultiplier:finisher?FinisherMultiplier:1);
            float push=hero.facing*(heavy?.9f:.35f);
            Vector2 oathTarget=origin;float nearest=float.PositiveInfinity;
            for(int i=enemies.Count-1;i>=0;i--){var e=enemies[i];if(CanTarget(e,run.Player)&&(e.pos.x-hero.pos.x)*hero.facing>-.4f&&Mathf.Abs(e.pos.x-hero.pos.x)<reach&&Mathf.Abs((e.pos.y+.85f)-(hero.pos.y+.9f))<1.9f){float distance=(e.pos-hero.pos).sqrMagnitude;if(distance<nearest){nearest=distance;oathTarget=e.pos;}StrikeEnemy(e,damage,run.Player,push);connected=true;}}
            if(StrikeTraps(reach,heavy))connected=true;
            if(village&&StrikeDummies(reach,damage,heavy))connected=true;
            bool smashed=StrikeCaches(reach,heavy);
            if(connected)
            {
                if(counter)ConsumeDodgeCounter();OnOathSwordHit(oathTarget);Sound(slashSound,.65f);
                if(dashSlash)DashSlashes++;else if(finisher)Finishers++;
                HitStop(heavy?.075f:.035f);Shake(heavy?.11f:.04f,heavy?.16f:.08f);
                if(heavy)FloatText(hero.pos+Vector2.up*2.6f,dashSlash?"돌진 베기":"마무리 일격",TowerWorld.Gold);
            }
            // Breaking a cache gives its own small jolt but never spends the dodge counter or charges the oath.
            else if(smashed){Sound(slashSound,.5f);HitStop(heavy?.05f:.03f);}
        }

        private void Fire(Vector2 from,Vector2 to,float speed,float damage,bool hostile,Color color,float area=0,HeroState owner=null,int pierce=1,float slowDuration=0)
        {
            var go=TowerWorld.SpriteObject(transform,hostile?"Enemy bolt":"Companion bolt",from,area>.5f?Vector2.one*.65f:pierce>1?new Vector2(.8f,.35f):new Vector2(.45f,.16f),color,120,TowerWorld.Glow);
            go.transform.localScale=Vector3.one;
            var v=(to-from).normalized*speed;go.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(v.y,v.x)*Mathf.Rad2Deg);
            shots.Add(new Shot{root=go,pos=from,velocity=v,damage=damage,hostile=hostile,color=color,area=area,owner=owner,pierce=pierce,slowDuration=slowDuration,life=4});
            if(hostile)StyleHostileLastShot();else StyleLastShot(owner is CompanionState&&((CompanionState)owner).Definition.Role==CompanionRole.Archer?SkillEffect.MultiShot:SkillEffect.ArcaneVolley);
        }

        private bool criticalHit;
        private void DamageEnemy(Fighter e,float damage,float push)
        {
            bool critical=criticalHit;criticalHit=false;
            if(!CanTarget(e,null))return;e.hp-=damage;e.hurt=.14f;e.pos.x=Mathf.Clamp(e.pos.x+push,TowerWorld.Left,TowerWorld.Right);
            FloatText(e.pos+Vector2.up*1.8f,Mathf.CeilToInt(damage)+(critical?"!":""),critical?new Color(1,.5f,.32f):TowerWorld.Gold);Burst(e.pos+Vector2.up,TowerWorld.Gold,4,.3f);Sound(hitSound,.35f);
            if(e.hp<=0){kills++;lastFallen=e.pos;if(vfx!=null)vfx.KillBurst(e.pos+Vector2.up*(e.isBoss?1.6f:.9f),e.isBoss?TowerWorld.Gold:TowerWorld.Teal);HitStop(e.isBoss?.12f:.05f);if(e.isBoss)Shake(.3f,.45f);if(IsFinalBoss(e)){Shake(.6f,1.1f);Toast(e.monster.Name+" 붕괴","지구를 삼킨 악신이 무너집니다",brass,4);}enemies.Remove(e);RetireEnemy(e);}
        }
        private void HurtHero(float damage)
        {
            // The player is the only party member with a hostile hit receiver.
            // Companions remain offensive actors without damage, stagger or knockback.
            if(invulnerable>0||hero.hp<=0)return;
            if(PassiveWardsHit())return;
            FloorHits++;CounterRemaining=0;
            damage=MitigatePlayerDamage(damage*PassiveDamageTakenScale);if(damage<=0)return;
            DamageTaken+=Mathf.Min(hero.hp,damage);
            hero.hp=Mathf.Max(0,hero.hp-damage);hero.hurt=.25f;invulnerable=.65f;HitStop(.05f);Shake(.16f,.22f);
            FloatText(hero.pos+Vector2.up*2,"−"+Mathf.RoundToInt(damage),new Color(1,.4f,.35f));Sound(hitSound,.8f);
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
        private void FloatText(Vector2 pos,string text,Color color){numbers.Add(new Number{pos=pos,text=text,color=color,life=.8f});}
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
        private void Sound(AudioClip clip,float volume){if(audioSource&&clip)audioSource.PlayOneShot(clip,volume);}
        private bool MouseInUI()
        {
            float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);
            Vector2 offset=new Vector2((Screen.width-1600*scale)*.5f,(Screen.height-900*scale)*.5f);
            return HUDHit((new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y)-offset)/scale);
        }
        internal void BackToHub(){if(Application.CanStreamedLevelBeLoaded("00_Hub"))SceneManager.LoadScene("00_Hub");else{EnterVillage();intro=true;}}
    }
}
