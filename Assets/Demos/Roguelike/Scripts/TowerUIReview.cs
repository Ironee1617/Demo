using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Opt-in visual review of production screens. Never runs during ordinary play.
    public sealed class TowerUIReview : MonoBehaviour
    {
        [Serializable] private sealed class Review
        {
            public bool success;
            public List<string> screenshots=new List<string>(),errors=new List<string>();
        }
        private TowerGame game;private TowerSmokeDriver capture;private string prefix;private bool finished;
        private readonly Review report=new Review();
        private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        private object Call(string method,params object[] args){return typeof(TowerGame).GetMethod(method,Hidden).Invoke(game,args);}
        private void Set(string name,object value){typeof(TowerGame).GetField(name,Hidden).SetValue(game,value);}
        public void Initialize(TowerGame tower,TowerSmokeDriver captureDriver,string output)
        {
            game=tower;capture=captureDriver;prefix=output;
            game.SuppressProgressPersistence=true;game.SmokeControlled=true;game.enabled=false;
            capture.PrepareForSuite(game);Application.logMessageReceived+=Log;StartCoroutine(Guard(Suite()));
        }
        private void Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception)report.errors.Add(message+"\n"+trace);}
        private void OnDestroy(){Application.logMessageReceived-=Log;}
        private IEnumerator Guard(IEnumerator body)
        {
            var stack=new Stack<IEnumerator>();stack.Push(body);
            while(stack.Count>0)
            {
                bool more=false;object next=null;Exception error=null;
                try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){report.errors.Add(error.ToString());Finish();yield break;}
                if(!more){stack.Pop();continue;}
                var nested=next as IEnumerator;if(nested!=null)stack.Push(nested);else yield return next;
            }
            Finish();
        }
        private IEnumerator Capture(string name)
        {
            yield return null;yield return null;
            yield return capture.CaptureForSuite(prefix,name);
            report.screenshots.Add(prefix+"-"+name+".png");
        }
        private void SetFloor(int floor)
        {
            typeof(TowerRun).GetField("<Floor>k__BackingField",Hidden).SetValue(game.run,floor);Call("SpawnFloor");game.noticeTime=0;
        }
        private IEnumerator Suite()
        {
            // Production runs open with a recruit; fixture runs skip it, so create this one as a real climb.
            game.progress=new TowerProgress();game.SmokeControlled=false;game.NewRun(74);game.SmokeControlled=true;game.intro=true;
            yield return Capture("title");
            game.intro=false;yield return Capture("recruit");game.intro=true;
            game.oathLobby=true;yield return Capture("oaths");
            Set("oathSelection","raphael");yield return Capture("oaths-raphael");
            game.StartOathTrial("michael",74);game.noticeTime=0;
            yield return Capture("hud");
            foreach(var e in game.enemies){e.stunTime=e.cooldown=999;}
            Call("TickHUD",.02f);game.hero.hp=67;Set("playerShield",31f);Set("shieldTime",3f);
            Call("TickHUD",.02f);game.run.Oath.OnPerfectDodge();game.run.Oath.OnPerfectDodge();
            yield return Capture("health-shield-ready");
            if(!(bool)Call("HUDHit",new Vector2(1340,45))||(bool)Call("HUDHit",new Vector2(800,450)))
                throw new InvalidOperationException("UI interaction regions overlap the arena or miss menu buttons.");
            SetFloor(5);game.hero.hp=96;
            foreach(var e in game.enemies){e.stunTime=e.cooldown=999;if(e.isBoss)e.hp=e.maxHp*.6f;}
            yield return Capture("boss");
            game.menu=true;yield return Capture("pause");game.help=true;yield return Capture("help");game.help=game.menu=false;
            game.run.Player.Apply(TowerRun.GetAugment("player.fireball"));
            game.run.Player.Apply(TowerRun.GetAugment("player.might"));
            foreach(var a in OathState.Augments)if(game.run.Oath.CanAcquire(a))game.run.Oath.Acquire(a.Id);
            game.OpenParty("player");yield return Capture("abilities");
            Set("detailScroll",new Vector2(0,650));yield return Capture("abilities-scroll");
            game.OpenParty("michael");yield return Capture("companion");game.party=false;
            game.run.ClearFloor();yield return Capture("reward");
            game.SelectOffer(game.run.Offers[0].Id);
            game.hero.hp=0;yield return Capture("defeat");
            game.StartOathTrial("raphael",74);game.noticeTime=0;SetFloor(10);game.run.ClearFloor();
            yield return Capture("victory");
            game.SmokeControlled=false;game.NewRun(74);game.SmokeControlled=true;game.intro=false;game.SelectOffer(game.run.Offers[0].Id);game.noticeTime=0;
            yield return Capture("normal-hud");
            // Branch exits on a fifth floor, then the shop floor they lead to.
            SetFloor(5);foreach(var e in game.enemies)if(e.root)Destroy(e.root);game.enemies.Clear();
            game.run.ClearFloor();if(game.run.IsRewardPending)game.SelectOffer(game.run.Offers[0].Id);
            game.hero.pos=new Vector2(TowerWorld.GateX(FloorType.Shop),TowerWorld.Ground);game.hero.root.transform.position=game.hero.pos;game.noticeTime=0;
            game.view.transform.position=new Vector3(19,0,-10);
            yield return Capture("branch-exits");
            game.progress=TowerProgress.Load("2|30|");game.NextFloor(FloorType.Shop);Call("Update");game.noticeTime=0;
            game.hero.pos=TowerWorld.FeaturePosition+Vector2.left*1.2f;game.hero.root.transform.position=game.hero.pos;game.view.transform.position=new Vector3(3,0,-10);
            yield return Capture("shop-floor");
            // The game's Update is paused during review, so toasts are aged by hand to their resting state.
            game.InteractMerchant();Call("TickToasts",10f);yield return Capture("shop");
            game.BuyShopItem(ShopItem.Augment);Call("TickToasts",.5f);yield return Capture("shop-bought");
            if(game.run.IsRewardPending)game.SelectOffer(game.run.Offers[0].Id);
            Call("TickToasts",10f);game.InteractMerchant();Call("TickToasts",.5f);yield return Capture("shop-closed");
        }
        private void Finish()
        {
            if(finished)return;finished=true;report.success=report.errors.Count==0;
            Directory.CreateDirectory(Path.GetDirectoryName(prefix));
            File.WriteAllText(prefix+"-report.json",JsonUtility.ToJson(report,true));
            Application.Quit(report.success?0:1);
        }
    }
}
