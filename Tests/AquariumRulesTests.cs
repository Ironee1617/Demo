using System;
using System.Collections.Generic;
using System.Reflection;
using PrototypeLab.Aquarium;
static class RulesTests
{
    static int passed,failed;
    static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Near(float a,float b,float eps,string message){Assert(Math.Abs(a-b)<=eps,message+" actual="+a+" expected="+b);}
    static void Test(string name,Action test){try{test();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.GetBaseException().Message);}}
    static void Advance(AquariumModel m,float seconds){for(int i=0;i<(int)Math.Round(seconds/.05f);i++)m.Tick(.05f);}
    static AquariumModel Match()
    {
        var m=new AquariumModel(71,7);m.AddPlayer(1,"alien",Team.Hider);m.AddPlayer(2,"diver",Team.Seeker);m.SetRole(2,Team.Seeker,true);Assert(m.StartRound(),"start");
        m.Players.RemoveAll(p=>p.filled);Advance(m,5.1f);
        m.Fish.Clear();m.Fish.Add(new Prey{id=999,species=0,position=new Point(-10,8,-8),facing=new Point(0,1),turnAt=9999});
        Point at=new Point();bool found=false;
        for(float x=-AquariumModel.HalfWidth+4;x<AquariumModel.HalfWidth-4&&!found;x+=3)
        for(float z=-AquariumModel.HalfDepth+4;z<AquariumModel.HalfDepth-6&&!found;z+=3)
        {var a=new Point(x,8,z);var b=a+new Point(0,0,3);if((AquariumModel.Constrain(a,3)-a).Length<.01f&&(AquariumModel.Constrain(b,3)-b).Length<.01f){at=a;found=true;}}
        Assert(found,"open test corridor");
        m.Player(2).position=at;m.Player(2).facing=new Point(0,1);
        m.Player(1).position=at+new Point(0,0,2.4f);m.Player(1).facing=new Point(0,-1);
        return m;
    }
    static void Hit(AquariumModel m)
    {
        var victim=m.Player(1);m.Harpoons.Add(new Harpoon{id=100+m.Harpoons.Count,owner=2,position=victim.position-new Point(0,0,1),direction=new Point(0,1),born=m.Time,retrievedAt=m.Time+3});
        m.Tick(.05f);
    }
    static void Invoke(AquariumModel m,string method,Swimmer p){typeof(AquariumModel).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(m,new object[]{p});}
    static int Main()
    {
        Test("10-minute match and all species spawn at least twice",()=>{foreach(int n in new[]{3,7,17,32,99}){var m=new AquariumModel(1,n);for(int s=0;s<n;s++){int id=s;Assert(m.Fish.FindAll(f=>f.species==id).Count>=2,"species "+s);}}var g=Match();Near(g.EndsAt-g.HuntAt,600,.001f,"duration");});
        Test("hunger starts at 100 and drains one each second including 75 boundary",()=>{var p=new Swimmer{lastMealAt=5};Near(AquariumModel.HungerLeft(p,5),100,0,"start");Near(AquariumModel.HungerLeft(p,15),90,0,"drain");Assert(!AquariumModel.HungryEnough(p,29.99f),"75.01 blocked");Assert(AquariumModel.HungryEnough(p,30),"75 accepted");});
        Test("feeding recovers exactly 50 and caps at100",()=>{var m=Match();var p=m.Player(1);p.lastMealAt=m.Time-80;Invoke(m,"RecoverMeal",p);Near(AquariumModel.HungerLeft(p,m.Time),70,.001f,"20+50");p.lastMealAt=m.Time-25;Invoke(m,"RecoverMeal",p);Near(AquariumModel.HungerLeft(p,m.Time),100,.001f,"cap");});
        Test("real action rejects full hunger, accepts 75 and restores50",()=>{var m=Match();var p=m.Player(1);m.Fish[0].position=p.position+p.facing*1.4f;m.Fish[0].turnAt=999;m.SetInput(1,new SwimInput{action=true});m.Tick(.05f);Assert(!m.Fish[0].eaten,"full rejected");p.lastMealAt=m.Time-25;m.SetInput(1,new SwimInput{action=true});m.Tick(.05f);Assert(m.Fish[0].eaten,"75 eats");Near(AquariumModel.HungerLeft(p,m.Time),100,.001f,"capped recovery");});
        Test("first hit unmorphs, halves hunger and grants no corpse",()=>{var m=Match();var p=m.Player(1);p.species=2;p.revealUntil=0;p.lastMealAt=m.Time-20;Hit(m);Assert(p.alive&&p.harpoonHits==1&&p.species==-1,"first survives/reveals");Near(AquariumModel.HungerLeft(p,m.Time),39.975f,.002f,"half current");Near(p.injuredUntil-m.Time,5,.001f,"5secs");Assert(m.Harpoons.TrueForAll(h=>h.caughtPlayer==0),"no catch first hit");Assert(m.Effects.Exists(e=>e.kind=="harpoonAlien"),"ink burst");});
        Test("second hit kills revealed alien; corpse retained for death review",()=>{var m=Match();var p=m.Player(1);Hit(m);m.Harpoons.Clear();p.species=1;p.revealUntil=0;Hit(m);Assert(!p.alive&&p.harpoonHits==2&&p.deathSpecies==-1,"second death revealed");var shot=m.Harpoons[0];Assert(shot.caughtPlayer==p.id&&shot.retrievedAt>=p.diedAt+4,"review retained");Advance(m,3.8f);Assert(!p.collected&&m.Phase==RoundPhase.Playing,"body still visible at3.8sec");Advance(m,1.6f);Assert(p.collected&&m.Harpoons.Count==0&&m.Phase==RoundPhase.Finished,"stowed then victory");});
        Test("injury is20percent slow, blocks sprint, expires after5seconds",()=>{var m=Match();var p=m.Player(1);Hit(m);m.Harpoons.Clear();var start=p.position;for(int i=0;i<8;i++){m.SetInput(1,new SwimInput{x=1,sprint=true});m.Tick(.05f);}Near(p.velocity.Length,AquariumModel.HiderSpeed*.8f,.001f,"hurt speed");Near(p.stamina,100,.001f,"no sprint spending");Advance(m,4.7f);p.position=start;p.velocity=new Point();for(int i=0;i<8;i++){m.SetInput(1,new SwimInput{x=1,sprint=true});m.Tick(.05f);}Assert(p.velocity.Length>AquariumModel.HiderSpeed&&p.stamina<100,"sprint resumes");});
        Test("egg channel takes3seconds and consumes exactly one earned egg",()=>{var m=Match();var p=m.Player(1);p.personalEaten=10;m.SetInput(1,new SwimInput{layEgg=true});m.Tick(.05f);Assert(m.Eggs.Count==0&&p.layEndsAt>0&&p.eggsLaid==0,"start no egg");Advance(m,2.9f);Assert(m.Eggs.Count==0,"not early");Advance(m,.15f);Assert(m.Eggs.Count==1&&p.eggsLaid==1&&p.layEndsAt==0,"finish once");Near(m.Eggs[0].hatchAt-m.Time,12,.10f,"hatch starts afterlay");});
        Test("hit cancels egg channel without consuming egg",()=>{var m=Match();var p=m.Player(1);p.personalEaten=10;m.SetInput(1,new SwimInput{layEgg=true});m.Tick(.05f);Advance(m,1);Hit(m);Advance(m,2.2f);Assert(p.layEndsAt==0&&p.eggsLaid==0&&m.Eggs.Count==0,"cancelled");});
        Test("C input voluntarily returns from fish to alien",()=>{var m=Match();var p=m.Player(1);p.species=3;p.revealUntil=0;m.SetInput(1,new SwimInput{unmorph=true});m.Tick(.05f);Assert(p.species==-1&&p.morphFrom==3,"unmorph input");Assert(m.Effects.Exists(e=>e.kind=="morph"),"transition event");});
        Test("ordinary fish meal keeps injury trail separate and leaves a two-second wake after swallowing",()=>{
            var m=Match();var p=m.Player(1);p.lastMealAt=m.Time-30;
            m.Fish[0].position=p.position+p.facing*1.4f;
            m.Fish.Add(new Prey{id=998,species=0,position=new Point(8,9,-9),turnAt=9999});
            m.SetInput(1,new SwimInput{action=true});m.Tick(.05f);
            Assert(m.Fish[0].eaten,"ate ordinary fish");var trail=m.Effects.Find(e=>e.kind=="mealTrail");
            Assert(trail!=null&&trail.owner==p.id,"single following event");Near(trail.duration,2,.001f,"two-second duration");
            Near(trail.born-p.visualAt,AquariumModel.TentacleFeedingSeconds,.001f,"begins after swallowing");
            Assert(p.bloodTrailUntil==0&&!m.Effects.Exists(e=>e.kind=="bloodTrail"),"no green injury droplets from fish meal");
            Advance(m,5);Assert(!m.Effects.Exists(e=>e.kind=="mealTrail"),"wake expired");
        });
        Test("ally meal has alien ink and never generates fish blood wake",()=>{
            var m=Match();var p=m.Player(1);p.lastMealAt=m.Time-30;
            m.Players.Add(new Swimmer{id=3,team=Team.Hider,species=2,position=p.position+p.facing*1.4f,facing=p.facing});
            m.SetInput(1,new SwimInput{action=true});m.Tick(.05f);
            Assert(m.Effects.Exists(e=>e.kind=="allyEat"),"alien capture event");Assert(!m.Effects.Exists(e=>e.kind=="mealTrail"),"no fish wake");
        });
        Test("exactly one drain opens in every round layout",()=>{
            for(int seed=0;seed<100;seed++){int open=0;foreach(var drain in AquariumDrainLayout.Create(new Random(seed)))if(drain.isOpen)open++;Assert(open==1,"one open for seed "+seed);}
        });
        Test("fish corpse follows tether then left-side stow and disappears",()=>{var m=Match();var p=m.Player(2);var f=m.Fish[0];f.position=p.position+p.facing*2;f.turnAt=999;m.SetInput(2,new SwimInput{action=true});m.Tick(.05f);Advance(m,.1f);Assert(f.dead&&m.Harpoons.Count==1,"fish killed");var h=m.Harpoons[0];Advance(m,1);Near((f.position-(h.position+h.catchOffset)).Length,0,.001f,"actual body tether");Advance(m,2.1f);Assert(!f.collected,"hand still carrying");Advance(m,1.2f);Assert(f.collected&&m.Harpoons.Count==0,"stowed removed");});
        Test("alien can move sideways/backwards/down; diver is faster",()=>{var m=Match();var p=m.Player(1);var at=p.position;for(int i=0;i<6;i++){m.SetInput(1,new SwimInput{y=-1});m.Tick(.05f);}Assert(p.position.y<at.y-.3f,"down");Assert(AquariumModel.SeekerSpeed>AquariumModel.HiderSpeed,"diver faster");});
        Test("full body wall clearance survives turns at tank boundary",()=>{foreach(var facing in new[]{new Point(1,0),new Point(0,1),new Point(0,1,0)}){var at=AquariumBodyCollision.ConstrainBody(new Point(AquariumModel.HalfWidth,11.8f,0),facing,.7f,1.1f,.7f);Assert(AquariumModel.InsideTank(at,2.02f-.01f),"horizontal hull inside");Assert(at.y<=AquariumModel.WaterHeight-.7f-Math.Abs(facing.y)*1.1f+.001f,"ceiling");}});
        Test("revival clears damage, injury and hidden corpse flag",()=>{var m=Match();var p=m.Player(1);p.alive=false;p.harpoonHits=2;p.collected=true;p.injuredUntil=100;m.Eggs.Add(new RevivalEgg{id=50,owner=1,revivePlayer=1,position=p.position,hatchAt=m.Time+.1f});Advance(m,.15f);Assert(p.alive&&p.harpoonHits==0&&!p.collected&&p.injuredUntil==0,"fresh life");});
        Console.WriteLine("RESULT "+passed+" passed / "+failed+" failed");return failed==0?0:1;
    }
}
