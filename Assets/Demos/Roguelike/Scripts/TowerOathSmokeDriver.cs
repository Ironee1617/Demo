using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed class TowerOathSmokeDriver : MonoBehaviour
    {
        [Serializable] private sealed class Report
        {
            public bool success;
            public string mode="towerOathSmoke",finishedUtc;
            public List<string> checks=new List<string>(),screenshots=new List<string>(),errors=new List<string>();
        }
        private readonly Report report=new Report();
        private TowerGame game;private TowerSmokeDriver capture;
        private string prefix,reportPath;private bool finished,quitOnComplete;
        private float deadline;
        private const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
        private object Call(string name,params object[] args){return typeof(TowerGame).GetMethod(name,Hidden).Invoke(game,args);}
        private void Set(string name,object value){typeof(TowerGame).GetField(name,Hidden).SetValue(game,value);}
        private IList Shots {get{return (IList)typeof(TowerGame).GetField("shots",Hidden).GetValue(game);}}
        private IList Areas {get{return (IList)typeof(TowerGame).GetField("skillAreas",Hidden).GetValue(game);}}
        public void Initialize(TowerGame tower,TowerSmokeDriver captureDriver,string capturePrefix,string outputReportPath,bool quitOnComplete=false)
        {
            game=tower;capture=captureDriver;prefix=capturePrefix;reportPath=outputReportPath;this.quitOnComplete=quitOnComplete;
            capture.PrepareForSuite(game);game.SuppressProgressPersistence=true;game.SmokeControlled=true;
            Application.logMessageReceived+=Log;deadline=Time.realtimeSinceStartup+150;
            StartCoroutine(Guard(Suite()));
        }
        private void Log(string condition,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors.Add(condition+"\n"+trace);}
        private void OnDestroy(){Application.logMessageReceived-=Log;}
        private void Update(){if(!finished&&Time.realtimeSinceStartup>deadline){StopAllCoroutines();Finish(new TimeoutException("Oath suite timed out."));}}
        private IEnumerator Guard(IEnumerator body)
        {
            var stack=new Stack<IEnumerator>();stack.Push(body);
            while(stack.Count>0)
            {
                bool more=false;object value=null;Exception error=null;
                try{more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){Finish(error);yield break;}
                if(!more){stack.Pop();continue;}
                var nested=value as IEnumerator;if(nested!=null)stack.Push(nested);else yield return value;
            }
        }
        private void Check(bool condition,string message)
        {if(!condition)throw new InvalidOperationException(message);report.checks.Add(message);}
        private static bool Near(float a,float b){return Mathf.Abs(a-b)<.015f;}
        private void ClearEnemies()
        {foreach(var e in game.enemies)if(e.root){e.root.SetActive(false);Destroy(e.root);}game.enemies.Clear();Call("ResetEnemyCombat");}
        private void ClearShots()
        {foreach(object shot in Shots){var root=(GameObject)shot.GetType().GetField("root").GetValue(shot);if(root)Destroy(root);}Shots.Clear();}
        private static void Place(TowerGame.Fighter actor,float x,float y=TowerWorld.Ground)
        {actor.pos=new Vector2(x,y);actor.velocityY=0;actor.grounded=true;actor.root.transform.position=actor.pos;}
        private TowerGame.Fighter Enemy(float x,float y=TowerWorld.Ground)
        {
            var e=(TowerGame.Fighter)Call("Spawn","Oath target",0,true,new Vector2(x,y));
            game.enemies.Add(e);Call("ConfigureEnemy",e,0,0);e.hp=e.maxHp=10000;e.stunTime=e.cooldown=999;return e;
        }
        private void Setup(string id)
        {
            game.enabled=false;
            if(game.hero!=null&&game.hero.root)game.hero.root.SetActive(false);
            foreach(var a in game.allies)if(a.root)a.root.SetActive(false);
            ClearEnemies();game.StartOathTrial(id,74);ClearEnemies();ClearShots();Place(game.hero,0);
            game.MoveOverride=0;game.AttackOverride=game.JumpOverride=game.DashOverride=game.DropOverride=false;
            game.view.transform.position=new Vector3(0,0,-10);
            foreach(var a in game.allies){Place(a,-1);a.cooldown=999;a.basicPending=false;}
            Set("previousHeroPosition",game.hero.pos);
        }
        private void Ready(){while(!game.run.Oath.Ready)game.run.Oath.OnPerfectDodge();}
        private IEnumerator Capture(string name)
        {
            yield return null;yield return null;yield return capture.CaptureForSuite(prefix,name);
            report.screenshots.Add(prefix+"-"+name+".png");
        }
        private IEnumerator Suite()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(prefix));yield return null;
            Setup("michael");game.intro=true;game.oathLobby=true;
            yield return Capture("selection");game.intro=game.oathLobby=false;
            var first=Enemy(1);var second=Enemy(1.4f);
            for(int i=0;i<4;i++){Place(first,1);Place(second,1.4f);Call("PlayerSlash");}
            Check(game.run.Oath.Charge==4&&game.run.Oath.Activations==0,"Four multi-target sword swings arm exactly one link.");
            Place(first,10);Place(second,12);Call("PlayerSlash");
            Check(game.run.Oath.Ready,"A whiff preserves the prepared oath.");
            Call("StrikeEnemy",first,13f,game.run.GetCompanion("michael"),0f);
            Check(game.run.Oath.Ready&&game.run.Oath.Activations==0,"Companion damage does not consume player oath charge.");
            yield return Capture("ready");
            Place(first,1);Place(second,3);var behind=Enemy(-2);float frontHp=second.hp;
            Call("PlayerSlash");float michaelPower=game.run.GetCompanion("michael").Damage*2.8f;
            Check(Near(frontHp-second.hp,michaelPower)&&behind.hp==behind.maxHp&&game.run.Oath.Activations==1,"Michael's base link hits the wider forward arc and excludes the rear.");

            Setup("michael");first=Enemy(1);behind=Enemy(-3);var far=Enemy(6);
            foreach(string id in new[]{OathState.Echo,OathState.Overdrive,OathState.MichaelCircle,OathState.MichaelWave})game.run.Oath.Acquire(id);
            Ready();Call("PlayerSlash");
            Check(Near(behind.maxHp-behind.hp,michaelPower*2)&&Shots.Count==1&&game.OathEchoPending,"Michael's radial, wave, overdrive and delayed echo combine.");
            float hp=behind.hp;game.menu=true;Call("Update");game.menu=false;game.party=true;Call("Update");game.party=false;
            Check(game.OathEchoPending&&behind.hp==hp,"Menu and party pause freeze the delayed echo.");
            Call("TickShots",.38f);
            Check(far.hp<far.maxHp,"The added sword wave reaches an enemy beyond the radial strike.");
            Call("TickOath",.31f);
            Check(Near(hp-behind.hp,michaelPower*2*.6f)&&!game.OathEchoPending&&game.run.Oath.Activations==1,"Echo repeats at 60 percent without recursively charging or counting a second activation.");
            yield return Capture("michael");

            Setup("gabriel");first=Enemy(1);first.hp=1;far=Enemy(5);
            Ready();Call("PlayerSlash");
            Check(game.run.Oath.Activations==1&&Shots.Count==1&&far.hp==far.maxHp,"Gabriel launches a real ray even if the initiating sword kills its target.");
            Call("TickShots",.32f);
            float gabrielPower=game.run.GetCompanion("gabriel").Damage*4;
            Check(Near(far.maxHp-far.hp,gabrielPower),"Gabriel ray damages on actual swept collision.");
            Call("TickShots",.02f);
            Check(Near(far.maxHp-far.hp,gabrielPower),"A piercing ray cannot damage the same enemy twice.");

            Setup("gabriel");first=Enemy(1);far=Enemy(4);var offAxis=Enemy(3,TowerWorld.Ground+2.5f);
            game.run.Oath.Acquire(OathState.GabrielChain);Ready();Call("PlayerSlash");
            Check(offAxis.hp==offAxis.maxHp,"Chain does not damage off-axis targets before the ray arrives.");
            Call("TickShots",.1f);
            Check(Near(offAxis.maxHp-offAxis.hp,gabrielPower*.5f),"The first actual ray impact chains to nearby off-axis enemies.");
            float chainedHp=offAxis.hp;Call("TickShots",.3f);
            Check(Near(offAxis.hp,chainedHp),"Later piercing impacts cannot repeat the first-contact chain.");
            ClearShots();game.run.Oath.Acquire(OathState.GabrielFan);Place(first,1);Ready();Call("PlayerSlash");
            Check(Shots.Count==3,"Fan augmentation creates three distinct rays.");
            foreach(object shot in Shots)Check(Near((float)shot.GetType().GetField("damage").GetValue(shot),gabrielPower*.65f),"Each fan ray uses 65 percent base damage.");
            yield return Capture("gabriel");

            Setup("raphael");first=Enemy(1);game.hero.hp=80;Ready();Call("PlayerSlash");
            var healer=game.run.GetCompanion("raphael");
            float heal=healer.Damage*.8f,shield=healer.Damage*1.5f;
            Check(Near(game.hero.hp,80+heal)&&Near(game.PlayerShield,shield),"Raphael restores health and grants the base three-second shield.");
            Call("TickSkills",3.01f);Check(game.PlayerShield==0,"Raphael's shield expires.");
            game.run.Oath.Acquire(OathState.RaphaelOverflow);game.run.Oath.Acquire(OathState.RaphaelGarden);
            game.hero.hp=game.PlayerMaxHealth-1;Place(first,1);Ready();Call("PlayerSlash");
            Check(Near(game.hero.hp,game.PlayerMaxHealth)&&Near(game.PlayerShield,shield+(heal-1)*2)&&Areas.Count==1,"Overheal converts exactly the unused healing to shield and creates one healing area.");
            game.hero.hp=80;float targetHp=first.hp;Call("TickSkillAreas",.1f);
            Check(game.hero.hp>80&&first.hp==targetHp,"Sanctuary heals inside its area without damaging enemies.");
            float healed=game.hero.hp;Place(game.hero,5);Call("TickSkillAreas",.5f);
            Check(game.hero.hp==healed,"Leaving the sanctuary stops its healing.");
            Place(game.hero,0);game.run.Oath.Acquire(OathState.Overdrive);Ready();Place(first,1);Call("PlayerSlash");
            Check(game.PlayerShield<=game.PlayerMaxHealth*.65f,"Stacked support obeys the common shield cap.");
            yield return Capture("raphael");

            Setup("gabriel");Enemy(20);
            game.DashOverride=true;Call("Update");game.DashOverride=false;Set("previousHeroPosition",game.hero.pos);
            CrossBullet();Check(game.run.Oath.Charge==2,"An actual early dash crossing grants two oath charge.");
            CrossBullet();Check(game.run.Oath.Charge==2,"Multiple bullets in one dash grant charge only once.");
            game.run.Oath.Acquire(OathState.Dodge);game.dashCooldown=0;
            game.DashOverride=true;Call("Update");game.DashOverride=false;Set("previousHeroPosition",game.hero.pos);CrossBullet();
            Check(game.run.Oath.Ready,"Dodge augmentation fills the gauge through real projectile collision.");

            Setup("michael");first=Enemy(1);game.run.Oath.Acquire(OathState.Echo);Ready();Call("PlayerSlash");
            ClearEnemies();Call("Update");
            Check(game.run.IsRewardPending&&!game.OathEchoPending&&Shots.Count==0,"Real floor clear cancels pending echoes and projectiles before rewards.");
            yield return Capture("reward");
            game.SelectOffer(game.run.Offers[0].Id);game.NextFloor();
            Check(game.run.Floor==2&&!game.OathEchoPending&&Areas.Count==0,"Advancing floors clears delayed and area effects.");
            for(int floor=2;floor<=10;floor++)
            {
                ClearEnemies();Call("Update");
                if(floor<10){Check(game.run.IsRewardPending,"Intermediate floor "+floor+" offers a reward.");game.SelectOffer(game.run.Offers[0].Id);game.NextFloor();}
            }
            Check(game.run.IsComplete&&!game.run.IsRewardPending&&game.run.Offers.Count==0&&game.run.ClearedFloors==10,"The production floor loop ends on floor ten without a final reward.");
            yield return Capture("complete");
            Setup("michael");first=Enemy(1);game.run.Oath.Acquire(OathState.Echo);Ready();Call("PlayerSlash");
            game.invulnerable=0;Call("HurtHero",10000f);
            Check(game.hero.hp==0&&!game.OathEchoPending,"Death cancels a pending echo.");
            game.NewRun(74);
            Check(game.run.Oath==null&&!game.OathEchoPending&&game.run.MaxFloor==25&&game.run.Companions.Count==0,"Returning to the normal game resets all trial state.");
            Finish(null);
        }
        private void CrossBullet()
        {
            Vector2 center=game.hero.pos+Vector2.up*.9f;
            Call("Fire",center+Vector2.left*2,center+Vector2.right*2,200f,12f,true,Color.red,0f,null,1,0f);
            Call("TickShots",.02f);
        }
        private void Finish(Exception error)
        {
            if(finished)return;finished=true;
            if(error!=null)report.errors.Add(error is TargetInvocationException&&error.InnerException!=null?error.InnerException.ToString():error.ToString());
            report.finishedUtc=DateTime.UtcNow.ToString("o");report.success=report.errors.Count==0;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=Log;
            if(quitOnComplete)Application.Quit(report.success?0:1);
        }
    }
}
