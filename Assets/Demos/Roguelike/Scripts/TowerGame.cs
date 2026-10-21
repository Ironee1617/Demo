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
            public int facing=1,kind,difficultyFloor=1; public string id; public bool grounded,airborne,isBoss,isEnemy; public float slowTime,stunTime; public Vector2 strikePoint;
            public float damageScale=1,moveScale=1,bulletSpeedScale=1,attackRateScale=1;
            public TowerMonsterDefinition monster;
            public TowerThemeId theme;
        }
        private sealed class Shot { public GameObject root; public Vector2 pos,velocity; public float damage,life,area,slowDuration,hitRadius=.16f; public bool hostile,oathChain; public Color color; public HeroState owner; public SkillEffect effect; public int pierce=1; public readonly HashSet<Fighter> hit=new HashSet<Fighter>(); }
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
        internal bool Paused { get { return intro||menu||party||help||run.IsRewardPending||hero.hp<=0||run.IsComplete; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Enter()
        {
            if(SceneManager.GetActiveScene().name!="20_Roguelike"||FindFirstObjectByType<TowerGame>()!=null)return;
            new GameObject("Tower of Companions").AddComponent<TowerGame>();
        }
        private void Awake()
        {
            Application.targetFrameRate=60;
            view=Camera.main;
            if(view==null){var c=new GameObject("Tower camera");c.tag="MainCamera";view=c.AddComponent<Camera>();c.AddComponent<AudioListener>();}
            view.orthographic=true;view.orthographicSize=5.8f;view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.035f,.065f,.105f);
            view.transform.rotation=Quaternion.identity;view.transform.position=new Vector3(-8,0,-10);
            vfx=TowerVfx.Create(transform);world=new TowerWorld(transform);ConfigurePixelCamera();
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.volume=.22f;
            music=TowerMusic.Create(transform);
            slashSound=Tone(220,.11f);hitSound=Tone(95,.10f);chimeSound=Tone(660,.28f);
            progress=TowerProgress.Load(PlayerPrefs.GetString(ProgressKey,""));
            if(progress.HasSelectedMyth)previewMyth=progress.SelectedMythId;
            var launchArgs=Environment.GetCommandLineArgs();
            bool visualFixture=Array.IndexOf(launchArgs,"-towerAnimationSmoke")>=0||Array.IndexOf(launchArgs,"-towerArtSmoke")>=0||Array.IndexOf(launchArgs,"-towerMonsterRigSmoke")>=0;
            if(visualFixture)
            {
                // Visual fixtures instantiate their own actors/camera. They do
                // not require a random floor or unrelated theme resources.
                SuppressProgressPersistence=true;enabled=false;seed=15;
                run=new TowerRun(seed,25,TowerMythCatalog.StarterMythId);
            }
            else NewRun();
            var driver=gameObject.AddComponent<TowerSmokeDriver>();driver.Initialize(this);
        }
        internal void NewRun(int? fixedSeed=null,string oathCompanionId=null)
        {
            ResetEnemyCombat();
            foreach(var e in enemies)if(e.root)Destroy(e.root);enemies.Clear();
            foreach(var a in allies)if(a.root)Destroy(a.root);allies.Clear();
            foreach(var s in shots)if(s.root)Destroy(s.root);shots.Clear();
            numbers.Clear();
            if(hero!=null&&hero.root)Destroy(hero.root);
            seed=fixedSeed??(Environment.TickCount&0x7fffffff);run=new TowerRun(seed,25,!SmokeControlled&&progress!=null&&!string.IsNullOrEmpty(progress.SelectedMythId)?progress.SelectedMythId:TowerMythCatalog.StarterMythId,oathCompanionId);
            hero=Spawn("Player",0,false,new Vector2(-12,TowerWorld.Ground));hero.id="player";hero.hp=hero.maxHp=PlayerMaxHealth;ResetSkills();
            elapsed=0;kills=0;healClock=0;invulnerable=0;dashCooldown=0;slashCooldown=0;attackTimer=0;dashTime=0;pendingSlash=false;jumpBuffer=0;coyote=0;
            menu=party=help=oathLobby=false;dismissId="";notice="";FocusActive=false;cameraLead=1.6f;ResetPresentation();CreateHitCore();ResetCombatRewards();ClearOathEffects();
            SpawnFloor();
        }
        private Fighter Spawn(string name,int index,bool monster,Vector2 pos)
        {
            var f=new Fighter{root=new GameObject(name),pos=pos,isEnemy=monster};f.root.transform.SetParent(transform);f.root.transform.position=pos;
            TowerWorld.SpriteObject(f.root.transform,"Shadow",new Vector2(0,.025f),new Vector2(1.2f,.16f),new Color(0,0,0,.36f),-1,TowerWorld.Disc);
            f.art=TowerActorArt.Create(f.root.transform,index,monster);return f;
        }
        private void SpawnFloor()
        {
            ClearOathEffects();
            ResetEnemyCombat();
            BeginFloorRecord();
            world.SetTheme(run.Theme.Id,run.Floor,seed);
            music.PlayTheme(run.Theme.Id);
            cameraCeiling=Mathf.Max(1.8f,world.HighestPlatform+4);
            hero.pos=new Vector2(-12,TowerWorld.Ground);hero.velocityY=0;hero.dropTime=0;hero.facing=1;hero.grounded=true;
            hero.root.transform.position=hero.pos;previousHeroPosition=hero.pos;view.transform.position=new Vector3(-7,0,-10);
            foreach(var s in shots)Destroy(s.root);shots.Clear();ClearFloorSkillEffects();
            int count=TowerDifficulty.RegularCount(run.Floor);bool bossFloor=TowerDifficulty.IsBossFloor(run.Floor);
            var roster=new List<TowerMonsterDefinition>(run.Theme.Monsters);
            var floorRandom=new System.Random(unchecked(seed*397 ^ run.Floor*7919));
            for(int i=roster.Count-1;i>0;i--){int swap=floorRandom.Next(i+1);var saved=roster[i];roster[i]=roster[swap];roster[swap]=saved;}
            for(int kind=0;kind<3;kind++)
            {
                int at=roster.FindIndex(kind,d=>d.Kind==kind);var saved=roster[kind];roster[kind]=roster[at];roster[at]=saved;
            }
            float spacing=Mathf.Min(4.2f,(bossFloor?18f:25f)/Mathf.Max(1,count-1));
            for(int i=0;i<count;i++)
            {
                var definition=roster[i%roster.Count];float x=-1+i*spacing;
                var e=Spawn(definition.Name,definition.Kind,true,new Vector2(x,TowerWorld.Ground));
                e.monster=definition;e.id=definition.Id;e.theme=run.Theme.Id;
                e.art.SetThemeMonster(e.theme,definition.Variant,false);
                ConfigureEnemy(e,definition.Kind,i);enemies.Add(e);
            }
            if(run.Floor>=2)
            {
                float tier=2.4f+(run.Floor-2)%3*1.4f;
                var perch=world.Ledges[0];float nearest=float.PositiveInfinity;
                foreach(var ledge in world.Ledges)
                {
                    if(Mathf.Abs(ledge.y-tier)>.05f)continue;
                    float distance=Mathf.Abs(ledge.x-12);
                    if(distance<nearest){perch=ledge;nearest=distance;}
                }
                var definition=run.Theme.Monsters[run.Theme.AirborneVariant];var flying=Spawn(definition.Name,3,true,new Vector2(perch.x,perch.y+.8f));
                flying.monster=definition;flying.id=definition.Id;flying.theme=run.Theme.Id;flying.art.SetThemeMonster(flying.theme,definition.Variant,false);
                ConfigureEnemy(flying,3,count);enemies.Add(flying);
            }
            if(bossFloor)
            {
                var definition=run.Theme.Boss;var boss=Spawn(definition.Name,4,true,new Vector2(22,TowerWorld.Ground));
                boss.monster=definition;boss.id=definition.Id;boss.theme=run.Theme.Id;boss.art.SetThemeMonster(boss.theme,0,true);
                ConfigureBoss(boss,run.Floor);enemies.Add(boss);
            }
            SyncAllies();ResetAllyFormation();Notice(bossFloor?run.Theme.Boss.Name+" 출현":"",bossFloor?3:0);
        }
        internal void StartClimb()
        {
            if(oathLobby){StartOathTrial(oathSelection);return;}
            if(!SmokeControlled&&(progress==null||string.IsNullOrEmpty(progress.SelectedMythId)))
            {mythMessage="등반할 신화를 먼저 선택하세요.";return;}
            if(!SmokeControlled&&previewMyth!=progress.SelectedMythId)
            {mythMessage="화면에서 보고 있는 신화를 선택한 뒤 시작하세요.";return;}
            if(!SmokeControlled&&progress!=null&&!string.IsNullOrEmpty(progress.SelectedMythId)&&run.SelectedMythId!=progress.SelectedMythId)NewRun();
            intro=false;Notice("",0);
        }
        internal void SaveProgress(){if(progress==null||SuppressProgressPersistence||SmokeControlled)return;PlayerPrefs.SetString(ProgressKey,progress.Save());PlayerPrefs.Save();}
        private void OnApplicationPause(bool paused){if(paused)SaveProgress();}
        private void OnApplicationQuit(){SaveProgress();}
        private void OnDestroy()
        {if(slashSound)Destroy(slashSound);if(hitSound)Destroy(hitSound);if(chimeSound)Destroy(chimeSound);if(uiGradient)Destroy(uiGradient);if(uiHalo)Destroy(uiHalo);if(uiDiamond)Destroy(uiDiamond);if(uiScrollThumb)Destroy(uiScrollThumb);if(uiSkin)Destroy(uiSkin);}
        private void Update()
        {
            float dt=Mathf.Min(Time.unscaledDeltaTime,.04f);
            TickHUD(dt);
            if(noticeTime>0)noticeTime-=dt;
            if(!SmokeControlled)
            {
                if(Input.GetKeyDown(KeyCode.Escape)){if(help)help=false;else if(intro){}else if(party){party=false;dismissId="";}else if(!run.IsRewardPending)menu=!menu;}
                if(Input.GetKeyDown(KeyCode.Tab)&&!intro&&hero.hp>0&&!run.IsComplete){party=!party;dismissId="";}
                if(Input.GetKeyDown(KeyCode.Return)&&intro&&!help)StartClimb();
            }
            ConfigurePixelCamera();
            if(Paused){AnimateIdle(dt);TickDeaths(dt);return;}
            previousHeroPosition=hero.pos;
            CounterRemaining=Mathf.Max(0,CounterRemaining-dt);
            hero.castRemaining=Mathf.Max(0,hero.castRemaining-dt);
            elapsed+=dt;dashCooldown=Mathf.Max(0,dashCooldown-dt);slashCooldown=Mathf.Max(0,slashCooldown-dt);invulnerable=Mathf.Max(0,invulnerable-dt);
            float move=SmokeControlled?MoveOverride:(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1:0);
            bool jump=SmokeControlled?JumpOverride:Input.GetKeyDown(KeyCode.Space);
            bool drop=SmokeControlled?DropOverride:Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow);
            bool dash=SmokeControlled?DashOverride:Input.GetKeyDown(KeyCode.LeftShift)||Input.GetKeyDown(KeyCode.RightShift);
            bool attack=SmokeControlled?AttackOverride:(Input.GetKey(KeyCode.J)||(Input.GetMouseButton(0)&&!MouseInUI()));
            FocusActive=SmokeControlled?FocusOverride:Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl);
            if(playerHitCore)playerHitCore.SetActive(FocusActive);
            if(jump)jumpBuffer=.14f;else jumpBuffer-=dt;
            if(hero.grounded)coyote=.1f;else coyote-=dt;
            if(move!=0&&dashTime<=0&&(attackTimer<=0||!pendingSlash)&&hero.castRemaining<=0)hero.facing=move>0?1:-1;
            if(drop&&hero.grounded&&hero.pos.y>TowerWorld.Ground+.1f){jumpBuffer=0;coyote=0;}
            if(jumpBuffer>0&&coyote>0&&hero.dropTime<=0){hero.velocityY=10.7f;hero.grounded=false;jumpBuffer=0;coyote=0;Sound(slashSound,.28f);}
            if(dash&&dashCooldown<=0){if(move!=0)hero.facing=move>0?1:-1;attackTimer=0;pendingSlash=false;CancelPlayerCast();hero.art.InterruptAction();dashTime=.18f;BeginDodgeCounter();dashCooldown=1.15f*run.Player.DodgeCooldownMultiplier;invulnerable=Mathf.Max(invulnerable,.25f);if(vfx!=null){vfx.Afterimage(hero.root.transform);vfx.Cast(SkillEffect.DashStrike,hero.pos+Vector2.up,1,TowerWorld.Teal);}Sound(slashSound,.5f);}
            if(dashTime>0){dashTime-=dt;hero.pos.x+=hero.facing*19*dt;if(vfx!=null&&elapsed>=nextAfterimage){nextAfterimage=elapsed+.05f;vfx.Afterimage(hero.root.transform);}}
            else hero.pos.x+=move*PlayerMoveSpeed*(FocusActive?.45f:1)*(attackTimer>0?.85f:1)*dt;
            hero.pos.x=Mathf.Clamp(hero.pos.x,TowerWorld.Left+.6f,TowerWorld.Right-.6f);

            StepGravity(hero,dt,drop);hero.root.transform.position=hero.pos;
            if(attack&&slashCooldown<=0&&hero.castRemaining<=0&&dashTime<=0)StartPlayerAttack();
            TickPlayerAttack(dt);
            hero.hurt=Mathf.Max(0,hero.hurt-dt);
            float actualHeroSpeed=dt>0?(hero.pos.x-previousHeroPosition.x)/dt:0;
            hero.art.SetLocomotion(hero.grounded,hero.velocityY,dashTime>0,Mathf.Abs(actualHeroSpeed),actualHeroSpeed);
            hero.art.SetPose(Mathf.Abs(actualHeroSpeed)>.08f,attackTimer>0,hero.hurt>0,hero.facing,dt);
            foreach(var enemy in enemies)TickEnemy(enemy,dt);
            if(hero.hp<=0){TickEffects(dt);return;}
            TickAllies(dt);TickSkills(dt);TickOath(dt);TickShots(dt);TickEffects(dt);
            if(hero.hp<=0)return;
            if(enemies.Count==0&&!run.IsFloorCleared)
            {
                run.ClearFloor();ClearOathEffects();foreach(var shot in shots)if(shot.root)Destroy(shot.root);shots.Clear();RecordFloorResult();int earned=SmokeControlled?0:progress.RecordFloorClear(run);if(earned>0)SaveProgress();CancelPendingSkills();hero.hp=Mathf.Min(PlayerMaxHealth,hero.hp+18);Sound(chimeSound,.8f);
                Notice("층 클리어",2);
            }
            if(!SmokeControlled&&run.IsFloorCleared&&!run.IsRewardPending&&Mathf.Abs(hero.pos.x-TowerWorld.ExitX)<2&&Input.GetKeyDown(KeyCode.E))NextFloor();
            world.Tick(dt,run.IsFloorCleared&&!run.IsRewardPending);
            if(move!=0)cameraLead=Mathf.MoveTowards(cameraLead,move*1.6f,dt*4);
            float cameraX=Mathf.Clamp(hero.pos.x+cameraLead,-7.5f,21.5f);
            Vector3 destination=new Vector3(cameraX,Mathf.Clamp(hero.pos.y+1.5f,0,cameraCeiling),-10);
            view.transform.position=SnapCamera(Vector3.Lerp(view.transform.position,destination,1-Mathf.Exp(-dt*5)));
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
            if(drop&&f.grounded&&f.pos.y>TowerWorld.Ground+.1f)
            {f.dropTime=.22f;f.dropHeight=f.pos.y;f.grounded=false;f.velocityY=Mathf.Min(f.velocityY,-2.5f);}
            f.dropTime=Mathf.Max(0,f.dropTime-dt);
            float old=f.pos.y;f.velocityY-=25*dt;float next=old+f.velocityY*dt;float ground=TowerWorld.Ground;
            if(f.velocityY<=0)foreach(var p in world.Ledges)
                if(!(f.dropTime>0&&Mathf.Abs(p.y-f.dropHeight)<.08f)&&Mathf.Abs(f.pos.x-p.x)<p.width*.5f+.18f&&old>=p.y-.02f&&next<=p.y)
                    ground=Mathf.Max(ground,p.y);
            f.grounded=next<=ground;if(f.grounded){next=ground;f.velocityY=0;}f.pos.y=next;
        }

        private void PlayerSlash()
        {
            Vector2 origin=hero.pos+new Vector2(hero.facing*1.1f,1);
            bool counter=CounterRemaining>0;SlashArc(origin,hero.facing,counter?TowerWorld.Teal:TowerWorld.Gold);bool connected=false;
            Vector2 oathTarget=origin;float nearest=float.PositiveInfinity;
            for(int i=enemies.Count-1;i>=0;i--){var e=enemies[i];if(CanTarget(e,run.Player)&&(e.pos.x-hero.pos.x)*hero.facing>-.4f&&Mathf.Abs(e.pos.x-hero.pos.x)<run.Player.Range&&Mathf.Abs((e.pos.y+.85f)-(hero.pos.y+.9f))<1.9f){float distance=(e.pos-hero.pos).sqrMagnitude;if(distance<nearest){nearest=distance;oathTarget=e.pos;}StrikeEnemy(e,PlayerDamage*(counter?DodgeCounterMultiplier:1),run.Player,hero.facing*.35f);connected=true;}}
            if(connected){if(counter)ConsumeDodgeCounter();OnOathSwordHit(oathTarget);Sound(slashSound,.65f);}
        }

        private void Fire(Vector2 from,Vector2 to,float speed,float damage,bool hostile,Color color,float area=0,HeroState owner=null,int pierce=1,float slowDuration=0)
        {
            var go=TowerWorld.SpriteObject(transform,hostile?"Enemy bolt":"Companion bolt",from,area>.5f?Vector2.one*.65f:pierce>1?new Vector2(.8f,.35f):new Vector2(.45f,.16f),color,120,TowerWorld.Glow);
            go.transform.localScale=Vector3.one;
            var v=(to-from).normalized*speed;go.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(v.y,v.x)*Mathf.Rad2Deg);
            shots.Add(new Shot{root=go,pos=from,velocity=v,damage=damage,hostile=hostile,color=color,area=area,owner=owner,pierce=pierce,slowDuration=slowDuration,life=4});
            if(hostile)StyleHostileLastShot();else StyleLastShot(owner is CompanionState&&((CompanionState)owner).Definition.Role==CompanionRole.Archer?SkillEffect.MultiShot:SkillEffect.ArcaneVolley);
        }

        private void DamageEnemy(Fighter e,float damage,float push)
        {
            if(!CanTarget(e,null))return;e.hp-=damage;e.hurt=.14f;e.pos.x=Mathf.Clamp(e.pos.x+push,TowerWorld.Left,TowerWorld.Right);
            FloatText(e.pos+Vector2.up*1.8f,Mathf.CeilToInt(damage).ToString(),TowerWorld.Gold);Burst(e.pos+Vector2.up,TowerWorld.Gold,4,.3f);Sound(hitSound,.35f);
            if(e.hp<=0){kills++;Burst(e.pos+Vector2.up,TowerWorld.Teal,12,.7f);enemies.Remove(e);RetireEnemy(e);}
        }
        private void HurtHero(float damage)
        {
            // The player is the only party member with a hostile hit receiver.
            // Companions remain offensive actors without damage, stagger or knockback.
            if(invulnerable>0||hero.hp<=0)return;
            FloorHits++;CounterRemaining=0;
            damage=MitigatePlayerDamage(damage);if(damage<=0)return;
            DamageTaken+=Mathf.Min(hero.hp,damage);
            hero.hp=Mathf.Max(0,hero.hp-damage);hero.hurt=.25f;invulnerable=.65f;
            FloatText(hero.pos+Vector2.up*2,"−"+Mathf.RoundToInt(damage),new Color(1,.4f,.35f));Sound(hitSound,.8f);if(hero.hp<=0){ClearOathEffects();hero.art.PlayDeath(.65f);}
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
        { if(run.ChooseOffer(id)){ReconcileLoadout();Sound(chimeSound,.8f);Notice("증강 획득",2);} }
        internal void Dismiss(string id)
        { if(run.DismissCompanion(id)){ReconcileLoadout();dismissId="";Notice("동료와 작별했습니다",2);} }
        internal void NextFloor(){if(run.AdvanceFloor())SpawnFloor();}
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
        internal void BackToHub(){if(Application.CanStreamedLevelBeLoaded("00_Hub"))SceneManager.LoadScene("00_Hub");else{intro=true;NewRun();}}
    }
}
