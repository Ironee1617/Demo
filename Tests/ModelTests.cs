using System;
using PrototypeLab.Aquarium;

public static class ModelTests
{
    private static int passed, failed;
    private static Point At(float x, float z, float y = 1.6f) { return new Point(x, y, z); }
    private static Point Layout(float x,float z,float y=1.6f) { return AquariumMapOutline.LayoutPoint(x,y,z); }
    private static Point CoverSide(float offset) { var p=AquariumModel.Rocks[1];return new Point(p.x+offset,1,p.z); }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static float FloatNeighbour(float value,int direction)
    {
        if(value==0)return direction<0?-float.Epsilon:float.Epsilon;
        int bits=BitConverter.ToInt32(BitConverter.GetBytes(value),0);
        bits+=(value>0)==(direction>0)?1:-1;
        return BitConverter.ToSingle(BitConverter.GetBytes(bits),0);
    }
    private static void Test(string name, Action test)
    { try { test(); passed++; Console.WriteLine("PASS " + name); } catch(Exception ex) { failed++;Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); } }
    private static AquariumModel Match(bool friend = false, bool warmup = false)
    {
        var m = new AquariumModel(); m.AddPlayer(1, "Hider", Team.Hider); m.AddPlayer(2, "Seeker", Team.Seeker); m.SetRole(2, Team.Seeker, true);
        if (friend) { m.AddPlayer(3, "Friend", Team.Hider); m.SetRole(3, Team.Hider, true); }
        Check(m.StartRound(), "start");
        // Most rule scenarios isolate one human per role; roster fill has its own checks below.
        m.Players.RemoveAll(p => p.filled);
        for(int i=0;i<m.Fish.Count;i++) { var fish=m.Fish[i];fish.position=AquariumModel.Constrain(new Point(-25+(i%4)*1.6f,8,-6+(i/4)*1.5f),.6f);fish.facing=new Point();fish.turnAt=float.MaxValue; }
        m.Player(1).position = At(0, 0); m.Player(1).facing = new Point(1, 0);
        m.Player(2).position = At(12, 7);
        if (!warmup) Advance(m, 5.1f);
        return m;
    }
    private static void Advance(AquariumModel m, float seconds)
    { for (int i = 0; i < (int)Math.Ceiling(seconds * 30); i++) m.Tick(1f / 30); }
    private static void Act(AquariumModel m, int id, bool morph = false)
    { m.SetInput(id, new SwimInput { aimX = 1, action = !morph, morph = morph }); m.Tick(1f / 30); }
    private static AquariumModel ReleasePair(bool reversed=false,float step=1f/30)
    {
        var m=Match(true);m.Player(1).position=At(0,0,7);m.Player(3).position=At(1.3f,0,7);m.Player(3).species=0;
        Act(m,1);Check(m.Player(3).swallowedBy==1,"release setup swallowed the ally");
        if(reversed)m.Players.Reverse();
        for(int i=0;i<400 && m.Player(3).swallowedBy!=0;i++)m.Tick(step);
        Check(m.Player(3).swallowedBy==0,"release setup completed");return m;
    }
    public static int Main()
    {
        try
        {
            Test("clearance caches preserve exact perimeter and walkway classification", () => {
                var nearest=typeof(AquariumMapOutline).GetMethod("Nearest",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
                var random=new Random(2451);var query=new object[4];
                for(int i=0;i<4000;i++)
                {
                    var point=i<1000?AquariumMapOutline.PointOn(i/1000f):new Point((float)random.NextDouble()*84-42,(float)random.NextDouble()*56-28);
                    float padding=new[]{-.12f,0,.002f,.35f,.54f,1.2f}[i%6];query[0]=point;query[1]=new Point();query[2]=new Point();query[3]=0f;
                    bool inside=(bool)nearest.Invoke(null,query);float squared=(float)query[3];
                    Check(AquariumMapOutline.Contains(point,padding)==(inside&&squared+.00001f>=padding*padding),"cached perimeter matches exact winding and clearance");
                    for(int except=-1;except<3;except++)Check(AquariumWalkwayLayout.Contains(point,padding,except)==(AquariumWalkwayLayout.SignedDistance(point,except)<padding),"cached corridor matches exact rounded union");
                }
            });
            Test("clearance cell corners and grid bounds preserve classification across adjacent floats", () => {
                var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                var nearest=typeof(AquariumMapOutline).GetMethod("Nearest",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
                var query=new object[4];int checkedPoints=0,cachedQueries=0;
                foreach(bool perimeter in new[]{true,false})
                {
                    var owner=perimeter?typeof(AquariumMapOutline):typeof(AquariumWalkwayLayout);
                    var grid=(AquariumClearanceGrid)owner.GetField("clearance",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetValue(null);
                    float originX=(float)typeof(AquariumClearanceGrid).GetField("originX",flags).GetValue(grid),originZ=(float)typeof(AquariumClearanceGrid).GetField("originZ",flags).GetValue(grid);
                    var cells=(float[,])typeof(AquariumClearanceGrid).GetField("distance",flags).GetValue(grid);
                    float cell=(float)typeof(AquariumClearanceGrid).GetField("Cell",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetRawConstantValue();
                    for(int x=0;x<=cells.GetLength(0);x++)for(int z=0;z<=cells.GetLength(1);z++)
                    {
                        float cornerX=originX+x*cell,cornerZ=originZ+z*cell;
                        foreach(float px in new[]{FloatNeighbour(cornerX,-1),cornerX,FloatNeighbour(cornerX,1)})
                        foreach(float pz in new[]{FloatNeighbour(cornerZ,-1),cornerZ,FloatNeighbour(cornerZ,1)})
                        {
                            var point=new Point(px,5.5f,pz);float exact=0;bool inside=false;float squared=0;
                            if(perimeter)
                            {
                                query[0]=point;query[1]=new Point();query[2]=new Point();query[3]=0f;
                                inside=(bool)nearest.Invoke(null,query);squared=(float)query[3];exact=(inside?1:-1)*(float)Math.Sqrt(squared);
                            }
                            else exact=AquariumWalkwayLayout.SignedDistance(point);
                            foreach(float padding in new[]{0f,.002f,.35f,.54f,1.2f})
                            {
                                bool expected=perimeter?inside&&squared+.00001f>=padding*padding:exact<padding;
                                bool actual=perimeter?AquariumMapOutline.Contains(point,padding):AquariumWalkwayLayout.Contains(point,padding);
                                Check(actual==expected,"cell corner classification differs at "+px+","+pz+" padding="+padding+" perimeter="+perimeter);
                                bool cached;float threshold=perimeter?(float)Math.Sqrt(Math.Max(0,padding*padding-.00001f)):padding;
                                if(grid.TryAtLeast(point,threshold,out cached))
                                {cachedQueries++;Check(cached==(exact>=threshold),"grid promised an incorrect signed-distance bound");}
                            }
                            checkedPoints++;
                        }
                    }
                    bool ignored;
                    Check(!grid.TryAtLeast(new Point(FloatNeighbour(originX,-1),originZ+cell/2),0,out ignored),"negative origin neighbour uses exact fallback");
                    Check(!grid.TryAtLeast(new Point(originX+cell/2,FloatNeighbour(originZ,-1)),0,out ignored),"negative depth origin neighbour uses exact fallback");
                    Check(!grid.TryAtLeast(new Point(FloatNeighbour(originX+cells.GetLength(0)*cell,1),originZ+cell/2),0,out ignored),"width end neighbour uses exact fallback");
                    Check(!grid.TryAtLeast(new Point(originX+cell/2,FloatNeighbour(originZ+cells.GetLength(1)*cell,1)),0,out ignored),"depth end neighbour uses exact fallback");
                }
                Check(checkedPoints>1000&&cachedQueries>1000,"cell corner checks exercise both grids and their cached decisions");
            });
            Test("perimeter clamp preserves its stricter tolerance inside the contains acceptance band", () => {
                var nearest=typeof(AquariumMapOutline).GetMethod("Nearest",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
                var points=AquariumMapOutline.Sample(false,496);var query=new object[4];const float padding=.4f;int checkedPoints=0;
                for(int i=0;i<points.Length;i++)
                {
                    var a=points[i];var b=points[(i+1)%points.Length];var edge=b-a;
                    var inward=new Point(edge.z,0,-edge.x).Normalized;
                    var point=(a+b)*.5f+inward*(padding-.000006f);point.y=7.25f;
                    query[0]=point;query[1]=new Point();query[2]=new Point();query[3]=0f;
                    bool inside=(bool)nearest.Invoke(null,query);float squared=(float)query[3];
                    if(!inside||squared+.000001f>=padding*padding||squared+.00001f<padding*padding)continue;
                    Check(AquariumMapOutline.Contains(point,padding),"regression point is accepted by the looser contains tolerance");
                    var clamped=AquariumMapOutline.Clamp(point,padding);
                    Check((clamped-point).Length>.0001f,"clamp must move a point that fails its original strict tolerance");
                    query[0]=clamped;query[1]=new Point();query[2]=new Point();query[3]=0f;
                    inside=(bool)nearest.Invoke(null,query);squared=(float)query[3];
                    Check(inside&&squared+.000001f>=padding*padding,"clamp moves the point to a safe inset");
                    Check(clamped.y==point.y,"clamp preserves height in the tolerance band");checkedPoints++;
                }
                Check(checkedPoints>=16,"regression samples cover multiple perimeter edges");
            });
            Test("cruising fish turn through a bounded arc instead of snapping", () => {
                var m=new AquariumModel(73);m.Fish.Clear();
                var fish=new Prey{id=100,position=new Point(2.3f,9,-3),facing=new Point(0,0,1),turnAt=0};m.Fish.Add(fish);
                for(int i=0;i<120;i++)
                {
                    var before=fish.facing;m.Tick(1f/30);
                    Check(Point.Dot(before.Normalized,fish.facing.Normalized)>=(float)Math.Cos(2.8f/30+.002f),"heading must turn continuously at each host tick");
                    Check(Math.Abs(fish.facing.Length-1)<.001f,"heading stays normalized");
                }
            });
            Test("fish meals synchronize one red ink injection with the feeding owner", () => {
                var m=Match();m.Player(1).species=0;m.Fish[0].position=At(1,0);Act(m,1);
                var ink=m.Effects.Find(e=>e.kind=="eat");Check(ink!=null && ink.owner==1 && ink.born==m.Player(1).visualAt,"red injection follows the feeding mouth");
                Act(m,1);Check(m.Effects.FindAll(e=>e.kind=="eat").Count==1,"held action cannot duplicate the plume");
                Check(Array.Exists(m.Snapshot().effects,e=>e.kind=="eat" && e.owner==1),"network snapshot includes plume owner");
            });
            Test("swallowing an alien emits yellow ink immediately before release", () => {
                var m=Match(true);m.Player(3).position=At(1,0);m.Player(3).species=0;var capturedFrom=m.Player(3).position;Act(m,1);
                Check((m.Player(3).swallowedFrom-capturedFrom).Length<.001f,"swallowed alien retains its capture location for head-first suction");
                var ink=m.Effects.Find(e=>e.kind=="allyEat");Check(ink!=null && ink.owner==1 && ink.born==m.Player(1).visualAt,"yellow injection starts at swallowing");
                Check(m.Player(3).swallowedBy==1 && !m.Effects.Exists(e=>e.kind=="burp" || e.kind=="eat"),"distinct from red meal and later release");
                Advance(m,3.1f);Check(m.Player(3).swallowedBy==0 && m.Effects.Exists(e=>e.kind=="burp"),"release behavior preserved");
            });
            Test("rounded hulls separate fish alien and diver in three dimensions", () => {
                foreach(bool diver in new[]{false,true})foreach(bool alien in new[]{false,true})
                {
                    var a=new Point(2.3f,7,4);var b=a+new Point(.2f,.15f,.1f);var ad=new Point(0,0,1);var bd=new Point(1,0,0);
                    float radius=AquariumBodyCollision.Radius(diver,alien),half=AquariumBodyCollision.Half(diver,alien);
                    var correction=AquariumBodyCollision.Penetration(a,ad,radius,half,b,bd,.54f,.16f);
                    Check(correction.Length>.5f,"overlap found for every form");
                    a+=correction;Check(AquariumBodyCollision.Penetration(a,ad,radius,half,b,bd,.54f,.16f).Length<.003f,"contact fully resolved");
                }
            });
            Test("coincident crowds settle without NaN and swallowed prey is exempt", () => {
                var m=Match();m.Fish.Clear();var centre=new Point(2.3f,7,4);
                m.Player(1).position=centre;m.Player(2).position=centre;
                for(int i=0;i<5;i++)m.Fish.Add(new Prey{id=100+i,position=centre,facing=new Point(0,0,1),turnAt=float.MaxValue});
                for(int i=0;i<12;i++)AquariumBodyCollision.Resolve(m.Players,m.Fish,m.Time);
                for(int i=0;i<m.Fish.Count;i++)for(int j=i+1;j<m.Fish.Count;j++)
                    Check(AquariumBodyCollision.Penetration(m.Fish[i].position,m.Fish[i].facing,.54f,.16f,m.Fish[j].position,m.Fish[j].facing,.54f,.16f).Length<.01f,"crowd remains separated");
                var swallowed=new Prey{id=200,eaten=true,position=m.Player(1).position,eater=1};m.Fish.Add(swallowed);var before=swallowed.position;
                AquariumBodyCollision.Resolve(m.Players,m.Fish,m.Time);Check((before-swallowed.position).Length<.0001f,"only captured prey may enter the mouth");
            });
            Test("sprinting players cannot cross through each other during a long tick", () => {
                var m=Match(true);m.Fish.Clear();m.Fish.Add(new Prey{id=100,position=new Point(-24,8,0),turnAt=float.MaxValue});
                var a=m.Player(1);var b=m.Player(3);a.position=new Point(2.3f,7,4);b.position=new Point(2.3f,7,6);
                for(int i=0;i<16;i++)
                {
                    m.SetInput(1,new SwimInput{z=1,aimZ=1,sprint=true});m.SetInput(3,new SwimInput{z=-1,aimZ=-1,sprint=true});m.Tick(.1f);
                    Check(a.position.z<b.position.z,"no tunnelling at maximum accepted tick");
                }
            });
            Test("feeding blocks a new morph until chewing and mouth closure complete", () => {
                var m=Match();var p=m.Player(1);p.species=0;p.revealUntil=0;m.Fish[0].position=At(1.5f,0);Act(m,1);
                Check(p.visualAction=="Bite" && p.nextAction>=p.visualAt+AquariumModel.FishFeedingSeconds,"full feeding sequence reserved");
                int species=p.species;m.Fish[1].position=At(1.5f,0);m.Fish[1].species=2;Advance(m,2.2f);Act(m,1,true);
                Check(p.species==species,"chewing cannot be overwritten by held morph input");
            });
            Test("sculpted sand stops swimmers and camera or projectile rays at its actual surface", () => {
                float low=100,high=-100;
                for(int x=-30;x<=30;x+=2)for(int z=-17;z<=17;z+=2)
                {
                    var p=new Point(x,0,z);if(!AquariumMapOutline.Contains(p,1)||AquariumWalkwayLayout.Contains(p,1))continue;
                    float h=AquariumSeabed.Height(p);low=Math.Min(low,h);high=Math.Max(high,h);
                    var moved=AquariumModel.Constrain(p,.4f);
                    Check(moved.y>=AquariumSeabed.Height(moved)+.399f,"swimmer above local dune");
                    Check((AquariumModel.Constrain(moved,.4f)-moved).Length<.003f,"stable sand contact at " + x + "," + z + " => " + moved.x + "," + moved.y + "," + moved.z);
                    float t=AquariumSeabed.Hit(new Point(x,h+2,z),new Point(x,h-2,z),.2f);
                    Check(t>.43f&&t<.46f,"swept ray contacts sand with padding");
                }
                Check(high-low>.65f,"genuinely contoured floor");
            });
            Test("12 metre water column is navigable above the reefs", () => {
                var p=new Point(2.3f,10.8f,4);
                Check((AquariumModel.Constrain(p,.5f)-p).Length<.002f,"deep upper water is accessible");
                var top=AquariumModel.Constrain(new Point(2.3f,30,4),.5f);
                Check(Math.Abs(top.y-11.5f)<.002f,"correct raised surface collision");
            });
            Test("giant limestone walls block swimmers, camera rays and swept harpoons", () => {
                foreach(var centre in new[]{new Point(-24.5f,2,-1.5f),new Point(27,3,3.7f),new Point(-2.3f,2,3.8f),new Point(-13.5f,1.25f,10.5f),new Point(-24.4f,1.45f,9.9f),new Point(-20,1.15f,-8.5f),new Point(24.7f,1.2f,-7.8f)})
                {
                    Check(AquariumMonolithLayout.Distance(centre)<0,"solid cliff interior");
                    var resolved=AquariumModel.Constrain(centre,.5f);
                    Check(AquariumMonolithLayout.Distance(resolved)>=.499f,"swimmer exits stone");
                    Check((AquariumModel.Constrain(resolved,.5f)-resolved).Length<.002f,"stable reef contact");
                    var from=centre+new Point(0,0,-10);var to=centre+new Point(0,0,10);
                    Check(AquariumModel.ObstacleHit(from,to,.15f)<=1,"camera and aim occlusion");
                    Check(AquariumModel.SolidHit(from,to,.15f)<=1,"fast projectile hits cliff");
                }
            });
            Test("reference cantilever has a navigable undercut and solid projecting ledge", () => {
                var under=new Point(23.3f,3.3f,3.4f);
                Check((AquariumModel.Constrain(under,.5f)-under).Length<.002f,"diver can enter the cliff recess");
                Check(AquariumMonolithLayout.Hit(new Point(19,3.3f,3.4f),under,.5f)>1,"clear horizontal route below cantilever");
                Check(AquariumMonolithLayout.Hit(under,new Point(23.3f,8,3.4f),.2f)<1,"projectile hits the real shelf overhead");
            });
            Test("natural arch leaves a continuous diver-sized swimming passage", () => {
                for(float z=-1;z<=9;z+=.1f)
                {
                    var p=new Point(2.3f,2,z);
                    Check(AquariumMonolithLayout.Distance(p)>.65f,"arch clearance at "+z);
                    Check((AquariumModel.Constrain(p,.5f)-p).Length<.002f,"arch path stays open");
                }
                Check(AquariumMonolithLayout.Hit(new Point(2.3f,2,-1),new Point(2.3f,2,9),.5f)>1,"ray passes below arch");
                Check(AquariumMonolithLayout.Hit(new Point(2.3f,4.9f,-1),new Point(2.3f,4.9f,9))<1,"stone lintel occludes ray");
            });
            Test("new terrain keeps generated actors outside all limestone masses", () => {
                var m=new AquariumModel();m.AddPlayer(1,"Diver",Team.Seeker);m.SetRole(1,Team.Seeker,true);m.StartRound();
                foreach(var fish in m.Fish)Check(AquariumMonolithLayout.Distance(fish.position)>.349f,"fish spawn inside stone");
                foreach(var player in m.Players)Check(AquariumMonolithLayout.Distance(player.position)>.399f,"player spawn inside stone");
            });
            Test("curved perimeter matches art and keeps swimmers inside at every heading", () => {
                for (int i = 0; i < 720; i++) {
                    float angle = i * (float)Math.PI / 360;
                    var outside = new Point((float)Math.Cos(angle) * 90, 5, (float)Math.Sin(angle) * 90);
                    var constrained = AquariumModel.Constrain(outside, 0.4f);
                    Check(AquariumModel.InsideTank(constrained, 0.399f), "perimeter " + i);
                    Check(!AquariumModel.InsideTank(outside), "reject outside point " + i);
                    var again = AquariumModel.Constrain(constrained, 0.4f);
                    Check((again - constrained).Length < 0.001f, "stable boundary " + i);
                }
                for (int i = 0; i < AquariumModel.Rocks.Length; i++) {
                    var p = AquariumModel.Rocks[i];
                    var start = p + new Point(-AquariumModel.RockRadii[i] - 1, 0, 0);
                    var end = p + new Point(AquariumModel.RockRadii[i] + 1, 0, 0);
                    Check(AquariumModel.ObstacleHit(start, end) <= 1, "reef collision " + i);
                }
            });
            Test("reference outline preserves the inlet and triangulates without overlap", () => {
                Check(!AquariumMapOutline.Contains(Layout(6.96f, 4.255f)), "top-right inlet stays outside water");
                Check(AquariumMapOutline.Contains(Layout(8.52f, 4.255f)), "right lagoon remains connected");
                Check(!AquariumMapOutline.Contains(Layout(-1.2f, -6.785f)), "entrance valley stays outside water");
                Check(AquariumMapOutline.Contains(Layout(-4.08f, -6.095f)), "left lagoon lobe remains open");
                var contour = AquariumMapOutline.Sample(false, 248);
                var indices = AquariumMapOutline.Triangulate(contour);
                Check(indices.Length == (contour.Length - 2) * 3, "complete triangulation");
                double polygonArea = 0, triangleArea = 0;
                for (int i = 0; i < contour.Length; i++) {
                    var a = contour[i]; var b = contour[(i + 1) % contour.Length];
                    polygonArea += a.x * b.z - b.x * a.z;
                    Check(AquariumMapOutline.Contains(AquariumMapOutline.Clamp(a, 0.4f), 0.399f), "inset near control " + i);
                }
                for (int i = 0; i < indices.Length; i += 3) {
                    var a = contour[indices[i]]; var b = contour[indices[i + 1]]; var c = contour[indices[i + 2]];
                    triangleArea += Math.Abs((b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x));
                }
                Check(Math.Abs(Math.Abs(polygonArea) - triangleArea) < 0.01, "triangle area exactly covers polygon");
                for (int x = -15; x <= 15; x++) for (int z = -10; z <= 10; z++)
                    Check(AquariumModel.InsideTank(AquariumModel.Constrain(Layout(x,z,.6f), 0.4f), 0.399f), "floor boundary " + x + "," + z);
            });
            Test("full-depth walkway glass excludes swimmers and catches swept projectiles", () => {
                for (int path=0;path<AquariumWalkwayLayout.Paths.Length;path++)
                {
                    var points=AquariumWalkwayLayout.Paths[path];
                    for(int i=4;i<points.Length-4;i+=5)
                    {
                        var p=points[i];p.y=4;
                        var resolved=AquariumModel.Constrain(p,.4f);
                        Check(!AquariumWalkwayLayout.Contains(resolved,.399f),"outside dry walkway "+path+":"+i);
                        Check(AquariumModel.InsideTank(resolved,.399f),"walkway joint remains in water "+path+":"+i);
                    }
                }
                var a=Layout(-8.8f,4.3f,5);var b=Layout(-6.4f,4.3f,5);
                Check(AquariumModel.ObstacleHit(a,b)>1,"transparent wall leaves camera boom unobstructed");
                Check(AquariumModel.SolidHit(a,b)<1,"glass blocks a fast projectile above rocks");
                Check(AquariumWalkwayLayout.Hit(new Point(0,2,0),new Point(3,2,0))>1,"open water remains clear");
                for(int x=-15;x<=15;x++)for(int z=-10;z<=10;z++)
                {
                    var p=AquariumModel.Constrain(Layout(x,z,.6f),.4f);
                    Check(!AquariumWalkwayLayout.Contains(p,.399f),"floor grid clear of glass "+x+","+z);
                }
            });
            Test("rounded deck junctions share swim and swept projectile collision", () => {
                int filletSamples=0;
                foreach(var centre in new[]{Layout(-5.4f,2,4),Layout(-.8f,-5.4f,4)})
                for(int x=-12;x<=12;x++)for(int z=-12;z<=12;z++)
                {
                    var p=centre+new Point(x*.25f,z*.25f);float raw=100;
                    for(int path=0;path<AquariumWalkwayLayout.Paths.Length;path++)
                    {Point q,t;raw=Math.Min(raw,AquariumWalkwayLayout.DistanceToPath(p,path,out q,out t)-AquariumWalkwayLayout.HalfWidths[path]);}
                    if(raw<.01f||AquariumWalkwayLayout.SignedDistance(p)>-.015f)continue;
                    var resolved=AquariumWalkwayLayout.Constrain(p,.3f);
                    Check(AquariumWalkwayLayout.SignedDistance(resolved)>=.299f,"filleted corner excludes swimmer");
                    var start=resolved+(resolved-p).Normalized;
                    Check(AquariumWalkwayLayout.Hit(start,p)<1,"projectile hits rounded fillet before endpoint");
                    filletSamples++;
                }
                Check(filletSamples>=2,"both joins contain rounded transition geometry");
            });
            Test("spacious layout keeps reef dimensions and spreads valid spawns", () => {
                var outline=AquariumMapOutline.Sample(true,496);float minX=100,maxX=-100,minZ=100,maxZ=-100;
                foreach(var p in outline){minX=Math.Min(minX,p.x);maxX=Math.Max(maxX,p.x);minZ=Math.Min(minZ,p.z);maxZ=Math.Max(maxZ,p.z);}
                Check(maxX-minX>77 && maxX-minX<79 && maxZ-minZ>52 && maxZ-minZ<54,"human-scale 78 by 53 metre footprint");
                Check(AquariumWalkwayLayout.HalfWidths[0]*2>2.7f,"central promenade has room for people to pass");
                Check(AquariumModel.WaterHeight==12 && AquariumModel.RockHalfHeight==1.4f,"deeper water and original small reef height");
                float[] original={1.15f,1.35f,.8f,1.05f,.85f,1,1.05f,.85f,.9f,.85f,.75f};
                for(int i=0;i<original.Length;i++)Check(AquariumModel.RockRadii[i]==original[i],"individual reef size unchanged "+i);
                var m=new AquariumModel();m.AddPlayer(1,"Hider",Team.Hider);m.AddPlayer(2,"Diver",Team.Seeker);m.SetRole(2,Team.Seeker,true);Check(m.StartRound(),"start enlarged map");
                foreach(var p in m.Players){float radius=p.team==Team.Seeker?.5f:.4f;Check(AquariumModel.InsideTank(p.position,radius-.001f)&&!AquariumWalkwayLayout.Contains(p.position,radius-.001f),"valid player spawn "+p.id);}
                bool left=false,right=false;
                foreach(var fish in m.Fish){Check(AquariumModel.InsideTank(fish.position,.349f)&&!AquariumWalkwayLayout.Contains(fish.position,.349f),"valid fish spawn");left|=fish.position.x<-16;right|=fish.position.x>16;}
                Check(left&&right,"fish populate the enlarged map instead of clustering in old bounds");
            });
            Test("lobby readiness and both roles", () => {
                var m = new AquariumModel(); m.AddPlayer(1, "One", Team.Hider); Check(!m.StartRound(), "solo must fail");
                m.AddPlayer(2, "Two", Team.Hider); m.SetRole(2, Team.Hider, true); Check(!m.StartRound(), "no seeker");
                m.SetRole(2, Team.Seeker, false); Check(!m.StartRound(), "unready"); m.SetRole(2, Team.Seeker, true); Check(m.StartRound(), "valid lobby");
                Check(m.AddPlayer(4, "Late", Team.Seeker) == null, "midround join");
            });
            Test("animation snapshots identify the eater and preserve the victim origin", () => {
                var m=Match();m.Player(1).species=0;m.Player(1).revealUntil=0;
                m.Fish[0].position=At(1,0);var origin=m.Fish[0].position;Act(m,1);
                var snapshot=m.Snapshot();var victim=Array.Find(snapshot.fish,f=>f.id==m.Fish[0].id);
                Check(victim.eaten && victim.eater==1,"explicit victim relationship");
                Check(victim.visualAt==m.Player(1).visualAt && victim.visualAt>=0,"shared authoritative timestamp");
                Check(m.Player(1).visualAction=="Bite" && m.Player(1).visualTarget==victim.id,"disguised mouth action");
                Advance(m,.5f);Check((victim.position-origin).Length<.2f,"victim origin retained during presentation");
            });
            Test("new catalog species participate without changing spawning rules", () => {
                var m=new AquariumModel(73,5);
                Check(m.Fish.Count==AquariumModel.FishCount && m.Fish.Exists(f=>f.species==4),"fifth species spawned");
                Check(m.Fish.TrueForAll(f=>f.species>=0 && f.species<5),"catalog bounds respected");
            });
            Test("fish mouth is exclusive to an active fish disguise", () => {
                var m=Match();var p=m.Player(1);p.species=0;p.revealUntil=m.Time+5;m.Fish[0].position=At(1,0);Act(m,1);
                Check(p.visualAction=="TentacleEat" && !AquariumModel.HasFishDisguise(p,m.Time),"revealed alien never opens fish mouth");
                p.species=-1;p.revealUntil=0;Check(!AquariumModel.HasFishDisguise(p,m.Time),"alien body cannot use fish mouth");
                p.species=0;Check(AquariumModel.HasFishDisguise(p,m.Time),"active disguise can use mouth");
                p.alive=false;Check(!AquariumModel.HasFishDisguise(p,m.Time),"death interrupts mouth eligibility");
            });
            Test("undisguised feeding selects tentacles and death has a stable timestamp", () => {
                var m=Match();m.Player(1).species=-1;m.Fish[0].position=At(1,0);Act(m,1);
                Check(m.Player(1).visualAction=="TentacleEat","tentacle action");
                var k=Match();Advance(k,5.1f);k.Player(2).position=At(0,0);k.Player(1).position=At(2,0);Act(k,2);Advance(k,.3f);
                var born=k.Player(1).deathAt;Check(born>=0 && k.Player(1).visualAction=="Harpoon","death event");
                Advance(k,.4f);Check(k.Player(1).deathAt==born,"repeated snapshot does not restart death");
            });
            Test("morph snapshot retains previous species for continuous transitions", () => {
                var m=Match();m.Player(1).species=0;m.Fish[0].species=2;m.Fish[0].position=At(1,0);Act(m,1,true);
                Check(m.Player(1).morphFrom==0 && m.Player(1).species==2 && m.Player(1).morphAt>=0,"both endpoints and start time");
            });
            Test("copy nearby living fish and brief reveal", () => {
                var m = Match(); m.Fish[0].dead = false; m.Fish[0].species = 2; m.Fish[0].position = At(1, 0);
                Act(m, 1, true); Check(m.Player(1).species == 2, "morph species"); Check(m.Player(1).revealUntil > m.Time, "reveal");
                Advance(m, 0.7f); Check(m.Player(1).revealUntil < m.Time, "reveal expires");
            });
            Test("living fish count toward food victory", () => {
                var m = Match(); m.Fish[0].position = At(1, 0); Act(m, 1);
                Check(m.Fish[0].eaten && m.Snapshot().remaining == 31, "fish eaten");
                for (int i = 1; i < m.Fish.Count; i++) m.Fish[i].eaten = true;
                m.Tick(1f / 30); Check(m.Winner == (int)Team.Hider && m.WinReason == WinReason.FoodGoalReached, "all eaten victory");
            });
            Test("three-second friendly swallow and six-second morph lock", () => {
                var m = Match(true); var p = m.Player(3); p.position = At(1, 0); p.species = 0;
                Act(m, 1); Check(p.swallowedBy == 1, "swallowed"); Advance(m, 2.9f); Check(p.swallowedBy == 1, "early release");
                Advance(m, 0.14f); Check(p.swallowedBy == 0 && Point.Dot(p.position-m.Player(1).position,p.facing)>1.5f, "released forward with body clearance");
                Check(p.revealUntil > m.Time && m.Player(1).revealUntil > m.Time && m.Effects.Exists(e => e.kind == "burp"), "burp reveal");
            });
            Test("captured disguise survives in snapshots until alien expulsion", () => {
                for(int species=0;species<5;species++)
                {
                    var m=Match(true);var p=m.Player(3);p.position=At(1,0);p.species=species;p.revealUntil=0;
                    Act(m,1);Check(p.swallowedBy==1 && p.swallowedSpecies==species,"capture retains visible fish species");
                    Advance(m,1.7f);Check(Array.Find(m.Snapshot().players,x=>x.id==3).swallowedSpecies==species,"remote snapshot retains eaten fish");
                    Advance(m,1.4f);Check(p.swallowedBy==0 && p.species==-1 && p.swallowedSpecies==-1 && p.visualAction=="SpitOut","release changes to alien inside carrier");
                }
                var exposed=Match(true);var ally=exposed.Player(3);ally.position=At(1,0);ally.species=2;ally.revealUntil=exposed.Time+4;
                Act(exposed,1);Check(ally.swallowedBy==0 && ally.swallowedSpecies==-1 && !exposed.Effects.Exists(e=>e.kind=="allyEat"),"already exposed alien cannot be swallowed");
            });
            Test("undisguised and revealing allies cannot be swallowed", () => {
                foreach(int state in new[]{0,1,2})
                {
                    var m=Match(true);var ally=m.Player(3);ally.position=At(1.3f,0,7);m.Player(1).position=At(0,0,7);
                    if(state>0){ally.species=1;ally.revealUntil=m.Time+(state==1?4f:.65f);}
                    if(state==2)ally.morphAt=m.Time;
                    Act(m,1);
                    Check(ally.swallowedBy==0 && ally.alive && ally.visualAction!="Eaten","visible alien remains free for state "+state);
                    Check(m.Player(1).score==0 && !m.Effects.Exists(e=>e.kind=="allyEat"),"no mistaken-meal penalty or ink");
                }
            });
            Test("fish carrier keeps its disguise through expulsion and reveals afterward", () => {
                var m=Match(true);var carrier=m.Player(1);var ally=m.Player(3);
                carrier.position=At(0,0,7);ally.position=At(1.3f,0,7);carrier.species=2;ally.species=1;
                Act(m,1);
                Check(carrier.species==2 && carrier.visualAction=="AllyGulp" && ally.swallowedBy==1,"fish gulps the disguised ally");
                Check(ally.swallowedSpecies==1 && carrier.score==-150,"captured form and penalty are retained");
                Advance(m,AquariumModel.AllyStun+.1f);
                Check(ally.swallowedBy==0 && ally.visualAction=="SpitOut" && carrier.species==2 && carrier.visualAction=="SpitRecoil","fish expels ally before losing disguise");
                Advance(m,Math.Max(0,carrier.revealAt-m.Time-.1f));
                Check(carrier.species==2,"disguise survives until the delayed reveal");
                Advance(m,.2f);
                Check(carrier.species==-1 && carrier.revealAt==0 && carrier.morphLockedUntil>m.Time,"reveal preserves remaining morph lock");
            });
            Test("feeding lunge moves the eater without moving its captured meal", () => {
                foreach(int form in new[]{-1,0})
                {
                    var m=Match();var eater=m.Player(1);eater.position=At(0,0,7);eater.species=form;
                    var meal=m.Fish[0];meal.position=At(1.4f,0,7);Act(m,1);Check(meal.eaten,"meal captured");
                    var mealOrigin=meal.position;float before=(mealOrigin-eater.position).Length;
                    m.SetInput(1,new SwimInput{x=-1,aimX=-1,sprint=true});m.Tick(.1f);
                    var toward=mealOrigin-eater.position;
                    Check(toward.Length<before && eater.stunnedUntil>m.Time,"automatic lunge closes the gap during stun");
                    Check((meal.position-mealOrigin).Length<.001f,"captured meal stays at its original position");
                    Check(Point.Dot(eater.facing,toward.Normalized)>.99f,"held opposite aim cannot turn away from the meal");
                }
            });
            Test("carrier disconnect safely releases friend", () => {
                var m = Match(true); m.Player(3).position = At(1, 0); m.Player(3).species = 0; Act(m, 1);
                Check(m.Player(3).swallowedBy == 1, "disconnect scenario captures its ally first"); m.RemovePlayer(1);
                Check(m.Player(3).swallowedBy == 0 && m.Player(3).alive && m.Phase == RoundPhase.Playing, "disconnect release");
            });
            Test("release moves both aliens apart with continuous deceleration", () => {
                var m=ReleasePair();var carrier=m.Player(1);var expelled=m.Player(3);
                Check(carrier.visualAction=="SpitRecoil" && expelled.visualAction=="SpitOut" && carrier.visualAt==expelled.visualAt,"synchronized two-sided reaction");
                Check(expelled.releaseSource==carrier.id && Array.Find(m.Snapshot().players,p=>p.id==expelled.id).releaseSource==carrier.id,"snapshot retains carrier for mouth-origin emergence");
                Check(m.Effects.Exists(e=>e.kind=="burp" && e.owner==carrier.id),"release plume follows carrier lower mouth");
                Check(Point.Dot(carrier.facing,expelled.facing)>.99f && carrier.facing.x>.99f,"carrier faces the spit direction while recoiling backwards");
                var a=carrier.position;var b=expelled.position;m.Tick(.1f);
                float first=(carrier.position-a).Length;
                Check(Point.Dot(carrier.position-a,new Point(-1,0))>.35f && Point.Dot(expelled.position-b,new Point(1,0))>.35f,"both leave the release position");
                a=carrier.position;m.Tick(.1f);Check((carrier.position-a).Length<first,"water drag reduces the kick speed");
                Advance(m,1);a=carrier.position;b=expelled.position;m.Tick(.1f);
                Check((carrier.position-a).Length<.001f && (expelled.position-b).Length<.001f,"impulse ends without residual drift");
            });
            Test("release displacement is independent of roster order and tick rate", () => {
                var a=ReleasePair(false,.01f);var b=ReleasePair(true,.05f);
                var a0=a.Player(1).position;var a1=a.Player(3).position;var b0=b.Player(1).position;var b1=b.Player(3).position;
                for(int i=0;i<100;i++)a.Tick(.01f);for(int i=0;i<20;i++)b.Tick(.05f);
                Check(((a.Player(1).position-a0)-(b.Player(1).position-b0)).Length<.005f,"same carrier recoil");
                Check(((a.Player(3).position-a1)-(b.Player(3).position-b1)).Length<.005f,"same ejected travel");
                Check(Math.Abs((a.Player(1).position-a0).Length-1.53f)<.01f,"complete integrated kick distance");
            });
            Test("held controls cannot reverse a release impulse and resume afterwards", () => {
                var m=ReleasePair();var carrier=m.Player(1);var expelled=m.Player(3);var a=carrier.position;var b=expelled.position;
                m.SetInput(1,new SwimInput{x=1,aimX=1,sprint=true,action=true,morph=true});m.SetInput(3,new SwimInput{x=-1,aimX=-1,sprint=true});m.Tick(.1f);
                Check(carrier.position.x<a.x && expelled.position.x>b.x && carrier.facing.x>0 && expelled.facing.x>0,"backward recoil preserves forward gaze despite held input");
                Advance(m,1);a=carrier.position;m.SetInput(1,new SwimInput{z=1,aimZ=1});m.Tick(.1f);
                Check(carrier.position.z>a.z+.3f && carrier.facing.z>.9f,"movement and aim resume");
            });
            Test("release beside glass stays contained and separates the two hulls", () => {
                var m=Match(true);var carrier=m.Player(1);var expelled=m.Player(3);
                carrier.position=AquariumModel.Constrain(At(90,0,7),.4f);carrier.facing=new Point(1,0);expelled.position=carrier.position;
                expelled.swallowedBy=1;expelled.releaseAt=m.Time+.025f;m.Tick(.05f);
                for(int i=0;i<60;i++)
                {
                    m.Tick(1f/60);
                    Check((carrier.position-AquariumModel.Constrain(carrier.position,.4f)).Length<.01f && (expelled.position-AquariumModel.Constrain(expelled.position,.4f)).Length<.01f,"glass and terrain constraints");
                    Check(AquariumBodyCollision.Penetration(carrier.position,carrier.facing,.62f,.25f,expelled.position,expelled.facing,.62f,.25f).Length<.03f,"released bodies do not overlap beside glass");
                }
            });
            Test("every fish species cruises at the alien base swim speed", () => {
                foreach(int species in new[]{0,1,2,17,98})
                {
                    var m=Match();m.Fish.Clear();var alien=m.Player(1);
                    alien.position=At(0,0,7);alien.velocity=new Point();
                    m.Player(2).position=At(18,12,7);
                    var fish=new Prey{id=100,species=species,position=At(0,6,7),facing=new Point(1,0),turnAt=float.MaxValue};m.Fish.Add(fish);
                    for(int i=0;i<12;i++){m.SetInput(1,new SwimInput{x=1,aimX=1});m.Tick(.1f);}
                    var fishBefore=fish.position;var alienBefore=alien.position;
                    m.SetInput(1,new SwimInput{x=1,aimX=1});m.Tick(.1f);
                    float fishTravel=(fish.position-fishBefore).Length,alienTravel=(alien.position-alienBefore).Length;
                    Check(Math.Abs(fishTravel-AquariumModel.HiderSpeed*.1f)<.001f,"fish reaches shared cruising speed for species "+species);
                    Check(Math.Abs(fishTravel-alienTravel)<.001f,"fish and alien travel equally at base speed for species "+species);
                    m.Player(2).position=fish.position-new Point(2,0);fishBefore=fish.position;alienBefore=alien.position;
                    m.SetInput(1,new SwimInput{x=1,aimX=1});m.Tick(.1f);
                    Check(Math.Abs((fish.position-fishBefore).Length-(alien.position-alienBefore).Length)<.001f,"fleeing fish keeps the shared speed for species "+species);
                }
            });
            Test("seeker warmup and speed advantage", () => {
                var m = Match(warmup: true); var s = m.Player(2); s.position = At(0, 6);
                m.SetInput(2, new SwimInput { x = 1 }); m.Tick(0.1f); Check(s.position.x == 0, "head start freeze"); Advance(m, 5);
                m.Player(1).position = At(0, 0); m.SetInput(1, new SwimInput { x = 1 }); m.SetInput(2, new SwimInput { x = 1 }); m.Tick(0.1f);
                Check(m.Player(1).position.x > 0 && m.Player(1).position.x < 0.4f && s.position.x < 0.47f, "smooth start");
                m.Tick(0.1f); Check(Math.Abs(m.Player(1).velocity.x - 4) < 0.001f && Math.Abs(s.velocity.x - 4.7f) < 0.001f, "base speeds reached");
            });
            Test("harpoon kills hider and seekers win", () => {
                var m = Match(); Advance(m, 5.1f); m.Player(2).position = At(0, 0); m.Player(1).position = At(2, 0);
                Act(m, 2); Advance(m, 0.3f); Check(!m.Player(1).alive && m.Winner == (int)Team.Seeker, "kill");
            });
            Test("nearest fish intercepts harpoon before hider", () => {
                var m = Match(); Advance(m, 5.1f); m.Player(2).position = At(0, 0); m.Player(1).position = At(2, 0);
                m.Fish[0].position = At(1.3f, 0); m.Fish[0].dead = false; m.Fish[0].facing = new Point(1, 0);
                Act(m, 2); Advance(m, 0.25f); Check(m.Fish[0].dead && !m.Fish[0].eaten && m.Player(1).alive, "nearest collision");
                Check(m.Snapshot().target == 31 && m.Snapshot().remaining == 31 && m.Snapshot().killed == 1 && m.Snapshot().eaten == 0, "death reduces goal without counting as eaten");
            });
            Test("rock cover blocks harpoons", () => {
                var m = Match(); Advance(m, 5.1f); m.Player(2).position = CoverSide(-3); m.Player(1).position = CoverSide(3);
                Act(m, 2); Advance(m, 0.8f); Check(m.Player(1).alive && m.Harpoons.Count == 1 && m.Harpoons[0].returning, "cover stops damage and starts retrieval");
                Advance(m, 2.3f); Check(m.Harpoons.Count == 0, "retrieved at end of cooldown");
            });
            Test("carrier death releases swallowed player", () => {
                var m = Match(true); Advance(m, 5.1f); m.Player(1).position = At(2, 0); m.Player(3).position = At(3, 0); m.Player(3).species = 0; Act(m, 1);
                Check(m.Player(3).swallowedBy == 1, "death scenario captures its ally first");
                m.Player(2).position = At(0, 0); Act(m, 2); Advance(m, 0.2f);
                Check(!m.Player(1).alive && m.Player(3).alive && m.Player(3).swallowedBy == 0, "death release");
            });
            Test("timeout and rematch reset", () => {
                var m = Match(); m.Player(1).lastMealAt = m.EndsAt; Advance(m, 186); Check(m.Winner == (int)Team.Seeker && m.WinReason == WinReason.TimeExpired, "timeout");
                Advance(m, AquariumModel.ReviewSeconds); m.ReturnToLobby();
                Check(m.Phase == RoundPhase.Lobby && m.Fish.Count == 32 && m.Player(1).alive && !m.Player(2).ready, "lobby reset");
                m.SetRole(2, Team.Seeker, true); Check(m.StartRound() && m.Round == 2 && m.Winner == -1, "rematch");
            });
            Test("input expiry, diagonal clamp and malformed input", () => {
                var m = Match(); m.SetInput(1, new SwimInput { x = 1, z = 1 }); m.Tick(0.1f); Check(Math.Abs((m.Player(1).position - At(0, 0)).Length - 0.32f) < 0.001f, "diagonal acceleration clamp");
                Advance(m, 0.5f); var before = m.Player(1).position; Advance(m, 0.5f); Check((before - m.Player(1).position).Length < 0.001f, "stale input stopped");
                m.SetInput(1, new SwimInput { x = float.NaN }); m.SetInput(1, new SwimInput { x = float.MaxValue, aimX = float.PositiveInfinity });
                m.Tick(float.NaN); m.Tick(0.1f); Check(!float.IsNaN(m.Time) && (before - m.Player(1).position).Length < 0.001f, "invalid input");
            });
            Test("seeker leaves: surviving hiders win", () => { var m = Match(); m.RemovePlayer(2); Check(m.Winner == (int)Team.Hider, "forfeit"); });
            Test("3D movement normalizes all axes and bounds swimming height", () => {
                var m = Match(); var origin = m.Player(1).position;
                m.SetInput(1, new SwimInput { x = 1, y = 1, z = 1 }); m.Tick(0.1f);
                Check(Math.Abs((m.Player(1).position - origin).Length - 0.32f) < 0.001f, "3D diagonal acceleration speed");
                for (int i = 0; i < 100; i++) { m.SetInput(1, new SwimInput { y = 1 }); m.Tick(0.1f); }
                Check(Math.Abs(m.Player(1).position.y - (AquariumModel.WaterHeight - 0.4f)) < 0.001f, "water surface");
                for (int i = 0; i < 100; i++) { m.SetInput(1, new SwimInput { y = -1 }); m.Tick(0.1f); }
                Check(Math.Abs(m.Player(1).position.y - (AquariumSeabed.Height(m.Player(1).position) + 0.4f)) < 0.001f, "floor");
                var before = m.Player(1).position; Advance(m, 0.5f);
                m.SetInput(1, new SwimInput { y = float.NaN, aimY = float.PositiveInfinity }); m.Tick(0.1f);
                Check(!float.IsNaN(m.Player(1).position.y), "invalid vertical input");
            });
            Test("bite uses depth and can eat directly above", () => {
                var m = Match(); m.Player(1).position = At(0, 0, 2); m.Fish[0].position = At(0, 0, 5);
                m.SetInput(1, new SwimInput { aimY = 1, action = true }); m.Tick(0.1f); Check(!m.Fish[0].eaten, "different height must miss");
                Advance(m, 0.7f); m.Fish[0].position = At(0, 0, 3);
                m.SetInput(1, new SwimInput { aimY = 1, action = true }); m.Tick(0.1f); Check(m.Fish[0].eaten, "vertical bite");
            });
            Test("3D harpoon misses wrong height and hits an elevated target", () => {
                var m = Match(); Advance(m, 5.1f); m.Player(2).position = At(0, 0, 2); m.Player(1).position = At(2, 0, 5);
                Act(m, 2); Advance(m, 3.1f); Check(m.Player(1).alive, "horizontal shot must miss above");
                m.SetInput(2, new SwimInput { aimX = 2, aimY = 3, action = true }); Advance(m, 0.4f);
                Check(!m.Player(1).alive, "elevated shot");
            });
            Test("rock volume permits swimming and shooting over it", () => {
                var centre=AquariumModel.Rocks[0];
                var at = new Point(centre.x, 4, centre.z); Check((AquariumModel.Constrain(at, 0.4f) - at).Length < 0.001f, "above rock movement");
                Check(AquariumModel.ObstacleHit(at-new Point(3,0),at+new Point(3,0)) > 1, "above rock shot");
                Check(AquariumModel.ObstacleHit(centre-new Point(3,0),centre+new Point(3,0)) < 1, "rock blocks low shot");
                var grounded = AquariumModel.Constrain(new Point(centre.x, 0.2f, centre.z), 0.4f);
                Check(grounded.y >= AquariumModel.Floor + 0.4f && (grounded - new Point(centre.x, grounded.y, centre.z)).Length > 1, "cannot become trapped under rock");
            });
            Test("harpoon enforces three-second host cooldown", () => {
                var m = Match(); Advance(m, 5.1f); m.Player(2).position = At(0, 6);
                Act(m, 2); float ready = m.Player(2).nextAction;
                Check(Math.Abs(ready - m.Time - 3) < 0.001f, "3s cooldown");
                Advance(m, 1); Act(m, 2); Check(m.Player(2).nextAction == ready, "early shot rejected");
                Advance(m, 2.1f); Act(m, 2); Check(m.Player(2).nextAction > ready && m.Harpoons.Count == 1, "ready shot accepted");
            });
            Test("spent harpoon reels to moving owner without a second hit", () => {
                var m = Match(); Advance(m, 5.1f); m.Player(2).position = At(0, 0); m.Player(1).position = At(8, 5);
                var prey = m.Fish[0]; prey.position = At(2, 0); prey.dead = false;
                Act(m, 2); Advance(m, .25f);
                Check(prey.dead && m.Harpoons.Count == 1 && m.Harpoons[0].returning, "hit retains spent tethered spear");
                var h = m.Harpoons[0]; float ready = m.Player(2).nextAction;
                Check(Math.Abs(h.retrievedAt - ready) < .001f, "retrieval uses authoritative cooldown");
                m.Player(1).position = h.position; m.Player(2).position = At(-2, 1);
                Advance(m, .4f); Check(m.Player(1).alive, "returning spear has no damage");
                Advance(m, Math.Max(0, ready - m.Time - .12f));
                Check(m.Harpoons.Count == 1 && (h.position - (m.Player(2).position + m.Player(2).facing * .7f)).Length < .15f, "reel follows moving owner");
                Advance(m, .2f); Check(m.Harpoons.Count == 0, "only removed when fully retrieved");
            });
            Test("harpooned fish corpse follows the spear and is delivered to a moving diver", () => {
                var m = Match(); var diver = m.Player(2); diver.position = At(0, 0, 7); m.Player(1).position = At(8, 5, 7);
                var prey = m.Fish[0]; prey.position = At(2, 0, 7); prey.dead = false; prey.facing = new Point();
                Act(m, 2); Advance(m, .1f); Check(prey.dead && m.Harpoons.Count == 1, "fish is impaled");
                var h = m.Harpoons[0]; var offset = prey.position - h.position; var origin = prey.position;
                diver.position = At(-2, 1, 7); Advance(m, 1);
                Check((prey.position - h.position - offset).Length < .001f, "corpse stays attached during return");
                Check((prey.position - origin).Length > .2f && !prey.eaten, "corpse is reeled without counting as food");
                Advance(m, Math.Max(0, h.retrievedAt - m.Time + .02f));
                Check(m.Harpoons.Count == 0 && (prey.position - (diver.position + diver.facing * .7f + offset)).Length < .08f, "corpse arrives with spear before detaching");
                Check((diver.position - At(-2, 1, 7)).Length < .01f, "reeled corpse does not push its owner");
                float deliveredHeight = prey.position.y; Advance(m, .2f);
                Check(prey.position.y < deliveredHeight - .1f, "delivered fish resumes sinking");
                Check((diver.position - At(-2, 1, 7)).Length < .01f, "detached catch does not push its owner");
            });
            Test("harpooned disguised alien corpse follows the returning spear", () => {
                var m = Match(true); var diver = m.Player(2); var alien = m.Player(1);
                diver.position = At(0, 0, 7); alien.position = At(2, 0, 7); alien.species = 1; alien.revealUntil = 0;
                m.Player(3).position = At(8, 5, 7); Act(m, 2); Advance(m, .1f);
                Check(!alien.alive && alien.deathSpecies == 1 && m.Phase == RoundPhase.Playing, "disguised corpse is available during play");
                var h = m.Harpoons[0]; var offset = alien.position - h.position; var origin = alien.position;
                diver.position = At(-2, 1, 7); Advance(m, 1);
                Check((alien.position - h.position - offset).Length < .001f && (alien.position - origin).Length > .2f, "alien corpse is reeled with spear");
            });
            Test("revival detaches a harpooned alien before the spear finishes returning", () => {
                var m = Match(true); var diver = m.Player(2); var alien = m.Player(1);
                diver.position = At(0, 0, 7); alien.position = At(2, 0, 7); m.Player(3).position = At(8, 5, 7);
                Act(m, 2); Advance(m, .1f); var h = m.Harpoons[0];
                Check(!alien.alive && h.caughtPlayer == alien.id, "alien corpse is tethered");
                m.Eggs.Add(new RevivalEgg { id = 99, owner = 3, revivePlayer = alien.id, position = At(8, 2, 7), hatchAt = m.Time + .1f });
                Advance(m, .2f); var revivedAt = alien.position; Advance(m, .3f);
                Check(alien.alive && h.caughtPlayer == 0 && (alien.position - revivedAt).Length < .001f, "revived alien is free of the returning spear");
            });
            Test("missed harpoon stops at range and remains tethered until ready", () => {
                var m = Match(); Advance(m, 5.1f); m.Player(2).position = At(0, 6);
                Act(m, 2); var h = m.Harpoons[0]; var launch = h.position; Advance(m, .8f);
                Check(h.returning && m.Harpoons.Count == 1 && (h.returnFrom - launch).Length <= 15.1f, "range limit starts return");
                Advance(m, 1); Check(m.Harpoons.Count == 1, "rope remains during cooldown");
                Advance(m, 1.3f); Check(m.Harpoons.Count == 0, "miss retrieved");
            });
            Test("feed attracts only nearby live fish for five seconds and cools down for twenty", () => {
                var m = Match(); Advance(m, 5.1f); m.Player(2).position = At(0, 0, 7);
                var near = m.Fish[0]; near.position = At(4, 0, 7);
                var far = m.Fish[1]; far.position = At(-10, 0, 7); // Clear water inside the curved gallery perimeter.
                var dead = m.Fish[2]; dead.position = At(4, 3, 9); dead.dead = true; // Inside bait range, beside the living fish's turn arc.
                m.SetInput(2, new SwimInput { aimX = 1, feed = true }); m.Tick(1f / 30);
                var food = m.Effects.Find(e => e.kind == "feed"); Check(food != null && food.duration == 5, "feed spawned");
                float ready = m.Player(2).nextFeed, before = (near.position - food.position).Length;
                Check(Math.Abs(ready - m.Time - 20) < 0.001f, "20s cooldown");
                Advance(m, 1.5f);
                Check((near.position - food.position).Length < before && far.position.x == -10 && dead.position.x == 4, "only nearby living fish converge after a bounded turn");
                m.SetInput(2, new SwimInput { feed = true }); m.Tick(1f / 30);
                Check(m.Player(2).nextFeed == ready && m.Effects.FindAll(e => e.kind == "feed").Count == 1, "cannot spam food");
                Advance(m, 5); Check(!m.Effects.Exists(e => e.kind == "feed"), "food expires");
                Advance(m, 15); m.SetInput(2, new SwimInput { feed = true }); m.Tick(1f / 30);
                Check(m.Player(2).nextFeed > ready, "feed becomes ready again");
            });
            Test("feed is restricted to divers after hiding countdown", () => {
                var m = Match(warmup: true); m.SetInput(1, new SwimInput { feed = true }); m.SetInput(2, new SwimInput { feed = true }); m.Tick(0.1f);
                Check(m.Effects.Count == 0 && m.Player(2).nextFeed == 0, "warmup and role restrictions");
                Advance(m, 5.1f); Check(!m.Effects.Exists(e => e.kind == "feed"), "warmup click is not queued");
            });
            Test("eating resumes normally while red ink disperses for nine seconds", () => {
                var m = Match(); var p = m.Player(1); m.Fish[0].position = At(1, 0); m.Fish[1].position = At(1.2f, 0);
                Act(m, 1); float ateAt = m.Time;
                Check(Math.Abs(p.stunnedUntil - ateAt - AquariumModel.EatStun) < 0.001f, "eat stun");
                Advance(m, AquariumModel.LungeSeconds + .1f); var origin = p.position;
                m.SetInput(1, new SwimInput { x = 1, action = true, morph = true }); m.Tick(0.1f);
                Check((p.position - origin).Length < 0.001f && p.species == -1 && !m.Fish[1].eaten, "no movement, morph or bite during stun");
                Advance(m, 2f); var beforeMove = p.position;
                // Move sideways: the second uneaten fish still blocks forward travel after the lunge.
                m.SetInput(1, new SwimInput { z = 1 }); m.Tick(0.1f);
                Check(p.position.z > beforeMove.z, "movement resumes on a clear path");
                Advance(m, 4f); Check(m.Effects.Exists(e => e.kind == "eat" && e.duration == 9), "trace persists beyond the old five-second cutoff");
                Advance(m, 3); Check(!m.Effects.Exists(e => e.kind == "eat"), "diluted trace expires");
            });
            Test("swallowing stuns carrier and yellow spit ink disperses for nine seconds", () => {
                var m = Match(true); m.Player(3).position = At(1, 0); m.Player(3).species = 0; var p = m.Player(1); Act(m, 1);
                Check(m.Player(3).swallowedBy == 1, "ink scenario captures its ally first");
                Advance(m, AquariumModel.LungeSeconds + .1f); var origin = p.position;
                m.SetInput(1, new SwimInput { x = 1 }); m.Tick(0.1f);
                Check((p.position - origin).Length < 0.001f && p.stunnedUntil > m.Time, "carrier is stunned");
                Advance(m, 3); var trace = m.Effects.Find(e => e.kind == "burp");
                Check(m.Player(3).swallowedBy == 0 && trace != null && trace.duration == 9, "automatic spit and yellow trace");
                Advance(m, 7); Check(m.Effects.Contains(trace), "spit ink persists while diluted");
                Advance(m, 2.1f); Check(!m.Effects.Contains(trace), "spit trace expires");
            });
            Test("dead fish lower goal once and cannot be eaten again", () => {
                var m = Match(); m.Fish[0].dead = true; m.Fish[0].position = At(1, 0); Act(m, 1);
                Check(!m.Fish[0].eaten && m.Snapshot().remaining == 31 && m.Snapshot().target == 31 && m.Snapshot().eaten == 0, "corpse cannot count twice");
                for (int i = 1; i < m.Fish.Count; i++) m.Fish[i].eaten = true;
                m.Tick(1f / 30); Check(m.Winner == (int)Team.Hider && m.Snapshot().remaining == 0 && m.Snapshot().eaten == 31, "reduced goal victory");
            });
            Test("last fish killed by diver completes alien goal", () => {
                var m = Match(); Advance(m, 5.1f); for (int i = 1; i < m.Fish.Count; i++) m.Fish[i].eaten = true;
                m.Player(2).position = At(0, 6); m.Fish[0].position = At(2.1f, 6);
                Act(m, 2); Advance(m,.12f);Check(m.Fish[0].dead && m.Winner == (int)Team.Hider && m.WinReason == WinReason.FoodGoalReached, "last kill finishes goal");
            });
            Test("finished review freezes gameplay for twenty seconds before rematch", () => {
                var m = Match(true); m.Player(3).position = At(1, 0); m.Player(3).species = 0; Act(m, 1);
                Check(m.Player(3).swallowedBy == 1, "finish scenario captures its ally first"); m.RemovePlayer(2);
                Check(m.WinReason == WinReason.DiversLeft && m.Player(3).swallowedBy == 0, "finish releases swallowed alien");
                Check(Math.Abs(m.ReviewEndsAt - m.Time - 20) < 0.001f && m.Snapshot().reviewEndsAt == m.ReviewEndsAt, "20s synchronized timer");
                var origin = m.Player(1).position; m.SetInput(1, new SwimInput { x = 1, action = true });
                m.ReturnToLobby(); Advance(m, 19);
                Check(m.Phase == RoundPhase.Finished && (m.Player(1).position - origin).Length < 0.001f, "frozen and cannot skip review");
                Advance(m, 1.1f); m.ReturnToLobby();
                Check(m.Phase == RoundPhase.Lobby && m.WinReason == WinReason.None && m.ReviewEndsAt == 0 && m.Player(1).stunnedUntil == 0, "review reset");
            });
            Test("five-player roster fills four aliens and removes only fill bots for rematch", () => {
                var m = new AquariumModel(); m.AddPlayer(1, "Human", Team.Hider); m.AddPlayer(2, "Diver", Team.Seeker);
                m.AddPlayer(3, "Practice friend", Team.Hider, true); m.SetRole(2, Team.Seeker, true);
                Check(m.StartRound() && m.Players.Count == 5 && m.Players.FindAll(p => p.team == Team.Hider).Count == 4, "one diver four aliens");
                Check(m.Players.FindAll(p => p.filled && p.id >= 1000).Count == 2, "distinct fill IDs");
                m.RemovePlayer(2); Advance(m, 20.1f); m.ReturnToLobby();
                Check(m.Players.Count == 2 && m.Player(3).bot && !m.Player(3).filled && !m.Players.Exists(p => p.filled), "preserve practice bot only");
                var invalid = new AquariumModel(); invalid.AddPlayer(1, "A", Team.Hider); invalid.AddPlayer(2, "B", Team.Seeker, true); invalid.AddPlayer(3, "C", Team.Seeker, true);
                Check(!invalid.StartRound() && invalid.StartProblem().Length > 0, "reject two divers");
            });
            Test("hunger begins after countdown and starvation is fatal", () => {
                var m = Match(warmup: true); var p = m.Player(1);
                Check(p.lastMealAt == m.HuntAt, "countdown does not spend hunger");
                Advance(m, 64.8f); Check(p.alive, "survive before 60 hungry seconds");
                Advance(m, 0.4f); Check(!p.alive && p.deathReason == "starved" && m.WinReason == WinReason.AllAliensCaught, "starvation and wipe");
            });
            Test("only normal meals refresh hunger and grant food points", () => {
                var m = Match(true); var p = m.Player(1); Advance(m, 8); m.Fish[0].position = At(1, 0);
                Act(m, 1); float meal = p.lastMealAt;
                Check(Math.Abs(meal - m.Time) < 0.001f && p.personalEaten == 1 && p.score == 100, "normal meal rewards");
                Advance(m, AquariumModel.TentacleFeedingSeconds+.1f); m.Player(3).position = At(1, 0); m.Player(3).species = 0; Act(m, 1);
                Check(m.Player(3).swallowedBy == 1, "penalty scenario captures its ally first");
                Check(p.lastMealAt == meal && p.personalEaten == 1 && p.score == -50 && m.Player(3).score == 0, "ally grants no hunger and only eater loses points");
            });
            Test("both resume sprinting after the release impulse while morph lock continues", () => {
                var m = Match(true); var p = m.Player(1); var ally = m.Player(3); ally.position = At(1, 0); p.species = ally.species = 2;
                Act(m, 1); float biteAt = m.Time;
                Check(p.species == 2 && ally.species == -1 && p.visualAction == "AllyGulp" && Math.Abs(p.morphLockedUntil - biteAt - 6) < 0.001f && ally.morphLockedUntil == p.morphLockedUntil, "fish carrier stays disguised while both morphs lock for six seconds");
                Advance(m, 4.1f); Check(p.species == -1 && ally.species == -1, "both are revealed after expulsion"); var before = p.position; m.SetInput(1, new SwimInput { z = 1, sprint = true }); m.Tick(0.1f);
                Check((p.position - before).Length > 0.3f && p.stamina < 100 && ally.swallowedBy == 0, "sprint resumes after the release impulse");
                p.position = At(0, 0); m.Fish[0].position = At(1, 0); Act(m, 1, true); Check(p.species == -1, "morph still blocked");
                Advance(m, 3); m.Fish[0].position = At(1, 0); Act(m, 1, true); Check(p.species == 0, "morph resumes after six seconds");
            });
            Test("aimed morph accepts NPCs and disguised allies without identifying the target", () => {
                var m = Match(true); var p = m.Player(1); var ally = m.Player(3); ally.position = At(1, 0); ally.species = 2;
                Act(m, 1, true); Check(p.species == 2 && ally.species == 2 && ally.revealUntil == 0, "ally copies without revealing target");
                Check(m.Effects.FindAll(e => e.kind == "morph").Count == 1 && (m.Effects[0].position - p.position).Length < 0.001f, "caster-only effect matches NPC morph");
                Advance(m, 0.7f); p.species = -1; ally.position = At(-1, 0); Act(m, 1, true); Check(p.species == -1, "cannot copy behind aim");
                Advance(m, 0.7f); ally.position = At(1, 0); ally.species = -1; Act(m, 1, true); Check(p.species == -1, "unmorphed alien not a fish target");
                Advance(m, 0.7f); var cover=AquariumModel.Rocks[2];p.position=new Point(cover.x-1.3f,1,cover.z);m.Fish[0].position=new Point(cover.x+1.3f,1,cover.z);Act(m,1,true);Check(p.species==-1,"rock blocks morph");
            });
            Test("sprint drains only while moving and restores when released for both roles", () => {
                var m = Match(); Advance(m, 5.1f); var p = m.Player(1); var diver = m.Player(2); diver.position = At(0, 6);
                m.SetInput(1, new SwimInput { x = 1, sprint = true }); m.SetInput(2, new SwimInput { x = 1, sprint = true }); m.Tick(0.1f);
                Check(Math.Abs(p.position.x - 0.32f) < 0.001f && Math.Abs(diver.position.x - 0.32f) < 0.001f, "smooth sprint starts for both roles");
                Check(Math.Abs(p.stamina - 97.5f) < 0.001f && p.stamina == diver.stamina, "25 stamina per second");
                m.SetInput(1, new SwimInput { sprint = true }); m.Tick(0.1f); Check(Math.Abs(p.stamina - 99.3f) < 0.001f, "stationary sprint does not drain");
                p.stamina = 0; var before = p.position; m.SetInput(1, new SwimInput { x = 1, sprint = true }); m.Tick(0.1f);
                Check((p.position - before).Length <= 0.4f && p.stamina == 0, "empty stamina gives no pulse boost");
                m.SetInput(1, new SwimInput()); m.Tick(0.1f); Check(Math.Abs(p.stamina - 1.8f) < 0.001f, "18 stamina per second recovery");
                p.position = At(0, 0, AquariumModel.WaterHeight - 0.4f); p.velocity = new Point(); p.stamina = 40;
                m.SetInput(1, new SwimInput { y = 1, sprint = true }); m.Tick(0.1f); Check(p.stamina >= 40, "blocked movement at the water surface does not consume stamina");
            });
            Test("scan has role countdown and host cooldown without exposing identity", () => {
                var m = Match(warmup: true); m.Player(1).species = 2; m.SetInput(1, new SwimInput { scan = true }); m.SetInput(2, new SwimInput { scan = true }); m.Tick(0.1f);
                Check(m.Player(1).scanUntil == 0 && m.Player(2).scanUntil == 0, "role and countdown restrictions");
                Advance(m, 5.1f); m.SetInput(2, new SwimInput { scan = true }); m.Tick(0.1f); var diver = m.Player(2);
                float ready = diver.nextScan; Check(Math.Abs(diver.scanUntil-m.Time-5)<.001f && Math.Abs(ready-m.Time-18)<.001f, "scan timers");
                Check(m.Player(1).species == 2 && m.Player(1).revealUntil == 0 && m.Player(1).score == 0, "scan alone does not reveal or score");
                Advance(m, 5.1f); m.SetInput(2, new SwimInput { scan = true }); m.Tick(0.1f); Check(diver.nextScan == ready && diver.scanUntil < m.Time, "scan cannot be spammed");
            });
            Test("exposure points require diver view range and line of sight with fifty per life cap", () => {
                var m = Match(); Advance(m, 5.1f); var p = m.Player(1); var diver = m.Player(2);
                p.position = At(2, 0, 4); p.species = 1; diver.position = At(0, 0, 4); diver.facing = new Point(1, 0);
                Advance(m, 2.1f); Check(p.score == 5 && p.exposureScore == 5, "visible disguised fish scores");
                diver.facing = new Point(-1, 0); Advance(m, 2.1f); Check(p.score == 5, "behind diver does not score");
                diver.facing = new Point(1, 0); p.position = At(13, 0, 4); Advance(m, 2.1f); Check(p.score == 5, "distant target does not score");
                diver.position = CoverSide(-3); p.position = CoverSide(3); Advance(m, 2.1f); Check(p.score == 5, "rock blocks exposure");
                diver.position = At(0, 0, 4); p.position = At(2, 0, 4); Advance(m, 24); Check(p.score == 50 && p.exposureScore == 50, "fifty point life cap");
            });
            Test("laid egg sinks smoothly on the host and freezes with the finished scene", () => {
                var m = Match(true); var owner = m.Player(1); owner.position = At(0, 0, 5); owner.personalEaten = 3; m.Player(3).alive = false;
                m.SetInput(1, new SwimInput { layEgg = true }); m.Tick(0.1f);
                var egg = m.Eggs[0]; var bornAt = egg.position; float hatchesAt = egg.hatchAt;
                Advance(m, 0.5f);
                Check(Math.Abs(bornAt.y - egg.position.y - 0.6f) < 0.002f && egg.position.x == bornAt.x && egg.position.z == bornAt.z, "gradual downward movement without sideways drift");
                Check((m.Snapshot().eggs[0].position - egg.position).Length == 0 && egg.hatchAt == hatchesAt, "network snapshot uses the sinking position without delaying hatch");
                m.RemovePlayer(2); var stopped = egg.position; Advance(m, 1);
                Check((egg.position - stopped).Length == 0, "finished scene stops sinking immediately");
            });
            Test("laid egg settles on the actual sand height and stays planted", () => {
                var m = Match(true); var owner = m.Player(1); owner.position = At(0, 0, 7); owner.personalEaten = 3; m.Player(3).alive = false;
                m.SetInput(1, new SwimInput { layEgg = true }); m.Tick(0.1f); var egg = m.Eggs[0];
                Advance(m, 7);
                float clearance = egg.position.y - AquariumModel.EggRadius - AquariumSeabed.Height(egg.position);
                Check(clearance >= -0.0001f && clearance < 0.003f, "egg shell touches the sculpted sand instead of floating or penetrating");
                var landed = egg.position; Advance(m, 1);
                Check((egg.position - landed).Length < 0.0001f && !m.Player(3).alive, "landed egg stays still while awaiting its original hatch time");
            });
            Test("sinking egg stops on a rounded reef without sliding through it", () => {
                var m = Match(true); m.Player(3).alive = false; var rock = AquariumModel.Rocks[1];
                var egg = new RevivalEgg { id = 900, owner = 1, revivePlayer = 3,
                    position = rock + new Point(0.25f, AquariumModel.RockHalfHeight + 1.2f, 0), hatchAt = m.Time + 30 };
                m.Eggs.Add(egg); var start = egg.position; Advance(m, 2);
                Check(egg.position.y < start.y - 0.5f && egg.position.y > rock.y + AquariumModel.RockHalfHeight,
                    "egg lands above the solid reef rather than reaching the seabed through it");
                var landed = egg.position; Advance(m, 1);
                Check((egg.position - landed).Length < 0.002f && egg.position.x == start.x && egg.position.z == start.z, "contact remains stable on the rounded surface");
            });
            Test("eggs require three personal meals dead teammate and once-only use", () => {
                var m = Match(true); var p = m.Player(1); p.personalEaten = 3;
                m.SetInput(1, new SwimInput { layEgg = true }); m.Tick(0.1f); Check(m.Eggs.Count == 0 && !p.eggUsed, "cannot waste egg with no dead teammate");
                m.Player(3).alive = false; p.personalEaten = 2; m.SetInput(1, new SwimInput { layEgg = true }); m.Tick(0.1f);
                Check(m.Eggs.Count == 0 && !p.eggUsed, "three personal food required");
                p.personalEaten = 3; p.stunnedUntil = m.Time + 1; m.SetInput(1, new SwimInput { layEgg = true }); m.Tick(0.1f); Check(m.Eggs.Count == 0, "cannot lay while stunned");
                Advance(m, 1.1f); m.SetInput(1, new SwimInput { layEgg = true }); m.Tick(0.1f);
                Check(m.Eggs.Count == 1 && p.eggUsed && m.Eggs[0].revivePlayer == 3 && m.Snapshot().eggs.Length == 1, "reserved teammate and synchronized egg");
                m.SetInput(1, new SwimInput { layEgg = true }); m.Tick(0.1f); Check(m.Eggs.Count == 1, "once-only egg");
            });
            Test("egg hatches after twelve seconds restores hunger and stamina but preserves lifetime stats", () => {
                var m = Match(true); var owner = m.Player(1); var dead = m.Player(3); owner.personalEaten = 3; dead.alive = false;
                dead.stamina = 0; dead.lastMealAt = -100; dead.personalEaten = 4; dead.score = 225; dead.eggUsed = true; dead.exposureScore = 50;
                m.SetInput(1, new SwimInput { layEgg = true }); m.Tick(0.1f); var egg = m.Eggs[0];
                Advance(m, 11.8f); Check(!dead.alive && m.Eggs.Count == 1, "not hatched early");
                Advance(m, 0.3f); Check(dead.alive && m.Eggs.Count == 0 && (dead.position - egg.position).Length < 1.6f, "one teammate emerges beside the egg");
                Check(AquariumBodyCollision.Penetration(dead.position,dead.facing,.62f,.25f,owner.position,owner.facing,.62f,.25f).Length<.01f,"revival separates from the carrier");
                Check(dead.stamina == 100 && m.Time - dead.lastMealAt < 0.2f && dead.personalEaten == 4 && dead.score == 225 && dead.eggUsed && dead.exposureScore == 0, "new life keeps round stats");
                Check(owner.score == 150, "owner receives hatch points");
            });
            Test("egg target reservations prevent duplicate revival and wipe waits for viable egg", () => {
                var m = Match(true); m.Player(1).personalEaten = 3; m.Player(3).alive = false;
                var owner2 = new Swimmer { id = 4, name = "Other", team = Team.Hider, ready = true, position = At(4, 5), personalEaten = 3, lastMealAt = m.HuntAt };
                m.Players.Add(owner2);
                m.SetInput(1, new SwimInput { layEgg = true }); m.SetInput(4, new SwimInput { layEgg = true }); m.Tick(0.1f);
                Check(m.Eggs.Count == 1 && !owner2.eggUsed, "dead teammate reserved once");
                m.Player(1).alive = owner2.alive = false; m.Tick(0.1f); Check(m.Phase == RoundPhase.Playing, "egg keeps wiped team in game");
                Advance(m, 12.1f); Check(m.Player(3).alive && m.Phase == RoundPhase.Playing, "hatch rescues wiped team");
            });
            Test("one harpoon destroys an egg and completes wipe before hatching", () => {
                var m = Match(true); Advance(m, 5.1f); var owner = m.Player(1); owner.personalEaten = 3; m.Player(3).alive = false;
                m.SetInput(1, new SwimInput { layEgg = true }); m.Tick(0.1f); m.Eggs[0].position = At(1.3f, 0); owner.alive = false;
                m.Player(2).position = At(0, 0); Act(m, 2);
                Check(m.Eggs.Count == 0 && m.WinReason == WinReason.AllAliensCaught && !m.Player(3).alive && m.Effects.Exists(e => e.kind == "eggBreak"), "egg destroyed in one hit");
            });
            Test("NPCs flee a nearby diver but bait takes precedence", () => {
                var m = Match(); Advance(m, 5.1f); var diver = m.Player(2); diver.position = At(0, 0); var fish = m.Fish[0]; fish.position = At(3, 0); fish.facing = new Point();
                m.Tick(0.1f); Check(fish.position.x > 3, "flee diver");
                m.SetInput(2, new SwimInput { aimX = 1, feed = true }); m.Tick(0.1f); float before = fish.position.x; m.Tick(0.1f);
                Advance(m,1.3f);Check(fish.position.x < before, "bait overrides flight after a smooth turn");
            });
            Test("wipe condition takes priority over simultaneous food completion", () => {
                var m = Match(); m.Player(1).alive = false; foreach (var fish in m.Fish) fish.eaten = true; m.Tick(0.1f);
                Check(m.Winner == (int)Team.Seeker && m.WinReason == WinReason.AllAliensCaught, "wipe wins deterministic tie");
            });
            Test("practice diver acts on visible evidence and visible eggs only", () => {
                var m = Match(true); Advance(m, 5.1f); var diver = m.Player(2); diver.bot = true;
                diver.position = At(0, 0, 4); diver.facing = new Point(-1, 0); m.Player(1).position = At(3, 0, 4); m.Player(3).position = At(12, 7, 4);
                m.Tick(0.1f); Check(diver.nextAction == 0, "does not target alien behind view");
                diver.position = CoverSide(-3); diver.facing = new Point(1, 0); m.Player(1).position = CoverSide(3);
                m.Tick(0.1f); Check(diver.nextAction == 0, "does not track alien through rock");
                diver.position = At(0, 0, 4); diver.facing = new Point(1, 0); m.Player(1).position = At(-12, 7, 4); m.Player(3).alive = false;
                m.Eggs.Add(new RevivalEgg { id = 99, owner = 1, revivePlayer = 3, position = At(1.3f, 0, 4), hatchAt = m.Time + 12 });
                m.Tick(0.1f); Check(m.Eggs.Count == 0 && diver.nextAction > m.Time && m.Effects.Exists(e => e.kind == "eggBreak"), "visible egg counterplay");
            });
            Test("countdown blocks human and bot meals including allies but permits movement and disguise", () => {
                var m = Match(true, true); var p = m.Player(1);
                // Both targets remain within bite range without starting inside
                // the rounded collision hulls added by the character rig update.
                m.Fish[0].position = At(1.6f, -0.7f); m.Fish[0].species = 2; m.Player(3).position = At(1.6f, 0.7f);
                m.SetInput(1, new SwimInput { x = 0.5f, aimX = 1, morph = true, action = true }); m.Tick(0.1f);
                Check(p.position.x > 0 && p.species == 2, "warmup movement and morph remain available");
                Check(!m.Fish[0].eaten && m.Player(3).swallowedBy == 0 && p.personalEaten == 0 && p.nextAction == 0, "normal meal blocked without consuming action cooldown");
                m.Fish[0].position = At(4, 0); m.SetInput(1, new SwimInput { aimX = 1, action = true }); m.Tick(0.1f);
                Check(m.Player(3).swallowedBy == 0 && p.stunnedUntil == 0, "ally bite blocked");
                Advance(m, 5.1f); Check(p.personalEaten == 0 && m.Player(3).swallowedBy == 0, "countdown clicks are not queued");
                var botMatch = Match(true, true); var bot = botMatch.Player(1); bot.bot = true; botMatch.Fish[0].position = At(1.2f, 0); botMatch.Player(3).position = At(1, 0);
                botMatch.Tick(0.1f); Check(botMatch.Player(3).swallowedBy == 0 && !botMatch.Fish[0].eaten, "bot cannot eat ally during warmup");
                botMatch.Player(3).position = At(8, 5); botMatch.Tick(0.1f); Check(!botMatch.Fish[0].eaten && bot.personalEaten == 0, "bot cannot eat normal fish during warmup");
            });
            Test("swimming accelerates gradually and stops within a short bounded distance", () => {
                var m = Match(); var p = m.Player(1); float previous = 0;
                for (int i = 0; i < 8; i++)
                {
                    m.SetInput(1, new SwimInput { x = 1 }); m.Tick(1f / 60);
                    Check(p.velocity.x >= previous && p.velocity.x <= AquariumModel.HiderSpeed + 0.001f, "bounded acceleration"); previous = p.velocity.x;
                    if (i == 0) Check(p.velocity.x < 1, "no instantaneous full speed");
                }
                Check(Math.Abs(p.velocity.x - AquariumModel.HiderSpeed) < 0.001f, "base speed reached after ramp");
                var stopAt = p.position; m.SetInput(1, new SwimInput());
                for (int i = 0; i < 12; i++) m.Tick(1f / 60);
                Check(p.velocity.Length < 0.001f && (p.position - stopAt).Length < 0.3f, "bounded deceleration distance");
                stopAt = p.position; Advance(m, 0.3f); Check((p.position - stopAt).Length < 0.0001f, "no residual drift");
            });
            Test("stun swallowed death and wall limits discard swimming momentum immediately", () => {
                var m = Match(true); var p = m.Player(1); p.velocity = new Point(4, 0); p.stunnedUntil = m.Time + 1; var before = p.position;
                m.SetInput(1, new SwimInput { x = 1, sprint = true }); m.Tick(0.1f);
                Check(p.velocity.Length == 0 && (p.position - before).Length == 0, "stun has no drift");
                p.stunnedUntil = 0; p.swallowedBy = 3; p.releaseAt = m.Time + 2; p.velocity = new Point(4, 0); m.Tick(0.1f);
                Check(p.velocity.Length == 0 && (p.position - m.Player(3).position).Length == 0, "swallowed follows carrier without independent velocity");
                p.swallowedBy = 0; p.alive = false; p.velocity = new Point(4, 0); before = p.position; m.Tick(0.1f);
                Check(p.velocity.Length == 0 && (p.position - before).Length == 0, "dead swimmer has no drift");
                p.alive = true; p.position = At(0, 0, AquariumModel.WaterHeight - 0.4f); p.velocity = new Point(0, 4, 0); before = p.position;
                for (int i = 0; i < 10; i++) { m.SetInput(1, new SwimInput { y = 1 }); m.Tick(0.1f); }
                Check(p.velocity.Length == 0 && (p.position - before).Length < 0.0001f, "water surface constraint remains stable without stored velocity");
                var warmup = Match(warmup: true); var diver = warmup.Player(2); diver.velocity = new Point(4, 0); before = diver.position; warmup.Tick(0.1f);
                Check(diver.velocity.Length == 0 && (diver.position - before).Length == 0, "diver warmup discards velocity");
            });
            Test("death records authoritative time and the visible species exactly once", () => {
                var m = Match(true); var p = m.Player(1); var ally = m.Player(3); var diver = m.Player(2);
                p.position = At(2, 0); p.species = 2; p.velocity = new Point(0.1f, 0); ally.position = At(10, 5); diver.position = At(0, 0);
                Act(m, 2); Advance(m, 0.2f);
                Check(!p.alive && p.deathSpecies == 2 && p.diedAt > m.HuntAt && p.velocity.Length == 0, "visible fish body and death clock captured");
                float diedAt = p.diedAt; var bodyAt = p.position; p.species = 0; p.revealUntil = m.Time + 10; Advance(m, 3.1f);
                Check(p.deathSpecies == 2 && p.diedAt == diedAt, "corpse appearance and clock do not follow later reveal changes");
                Check((p.position - bodyAt).Length > .1f && m.Harpoons.Count == 0, "corpse was delivered by the returning spear");
                ally.position = At(2, 0); ally.species = 1; ally.revealUntil = m.Time + 1; Act(m, 2); Advance(m, 0.2f);
                Check(!ally.alive && ally.deathSpecies == -1, "exposed alien dies as alien body");
                Advance(m, 3); Check(ally.deathSpecies == -1, "expired reveal does not turn corpse into fish");
                Check(AquariumModel.DeathReviewSeconds == 2.5f, "shared death camera duration");
            });
            Test("finished review preserves effects harpoons eggs and positions at finishedAt", () => {
                var m = Match(true); var p = m.Player(1); p.velocity = new Point(4, 0);
                m.Effects.Add(new WaterEffect { id = 900, kind = "feed", born = m.Time, duration = 0.1f, position = At(3, 2) });
                m.Harpoons.Add(new Harpoon { id = 900, owner = 1, position = At(6, 4), direction = new Point(1, 0) });
                m.Eggs.Add(new RevivalEgg { id = 900, owner = 1, revivePlayer = 3, position = At(2, 3), hatchAt = m.Time + 1 });
                m.RemovePlayer(2); var endedAt = m.FinishedAt; var playerAt = p.position; var fishAt = m.Fish[0].position; var shotAt = m.Harpoons[0].position; var eggAt = m.Eggs[0].position;
                float shotLife = m.Harpoons[0].life; Advance(m, 21);
                Check(m.Phase == RoundPhase.Finished && m.Time > endedAt + 20 && m.FinishedAt == endedAt && m.Snapshot().finishedAt == endedAt, "review clock advances independently of scene timestamp");
                Check(m.Effects.Count == 1 && m.Harpoons.Count == 1 && m.Eggs.Count == 1 && m.Harpoons[0].life == shotLife, "world objects and effect lifetimes are preserved");
                Check(p.velocity.Length == 0 && (p.position - playerAt).Length == 0 && (m.Fish[0].position - fishAt).Length == 0 && (m.Harpoons[0].position - shotAt).Length == 0 && (m.Eggs[0].position - eggAt).Length == 0, "all game positions frozen");
                m.ReturnToLobby(); Check(m.FinishedAt == 0 && m.Effects.Count == 0 && m.Harpoons.Count == 0 && m.Eggs.Count == 0, "new lobby clears frozen state");
            });
            Test("last death with a viable egg stays playing through death review", () => {
                var m = Match(true); var owner = m.Player(1); var dead = m.Player(3); owner.personalEaten = 3; dead.alive = false;
                m.SetInput(1, new SwimInput { layEgg = true }); m.Tick(0.1f); owner.position = At(2, 0); m.Player(2).position = At(0, 0);
                Act(m, 2); Advance(m, 0.2f);
                Check(!owner.alive && owner.diedAt > 0 && m.Eggs.Count == 1 && m.Phase == RoundPhase.Playing && m.FinishedAt == 0, "egg prevents true match finish");
                Advance(m, 3); Check(m.Phase == RoundPhase.Playing && m.FinishedAt == 0, "death camera duration does not end match");
                dead.diedAt = m.Time - 5; dead.deathSpecies = 1; Advance(m, 9.2f);
                Check(dead.alive && dead.diedAt == 0 && dead.deathSpecies == -1 && dead.velocity.Length == 0, "revival clears death review data");
            });
            Test("morph target selection prioritizes aim alignment then distance for NPCs and allies", () => {
                var m = Match(true); var p = m.Player(1); var ally = m.Player(3); ally.position = At(10, 5);
                m.Fish[0].position = At(1, 0.5f); m.Fish[1].position = At(2.4f, 0);
                var selected = AquariumModel.FindMorphTarget(m.Fish, m.Players, p, new Point(1, 0), m.Time);
                Check(selected != null && selected.id == m.Fish[1].id && !selected.player, "centered farther fish wins over near peripheral fish");
                m.Fish[2].position = At(1.8f, 0); selected = AquariumModel.FindMorphTarget(m.Fish, m.Players, p, new Point(1, 0), m.Time);
                Check(selected.id == m.Fish[2].id, "distance resolves equal alignment");
                ally.position = At(1.2f, 0); ally.species = 2; selected = AquariumModel.FindMorphTarget(m.Fish, m.Players, p, new Point(1, 0), m.Time);
                Check(selected.player && selected.id == ally.id && selected.species == 2, "same selection rule accepts disguised ally");
                ally.revealUntil = m.Time + 1; m.Fish[2].dead = true; m.Fish[1].eaten = true; m.Fish[0].position = At(-1, 0);
                Check(AquariumModel.FindMorphTarget(m.Fish, m.Players, p, new Point(1, 0), m.Time) == null, "dead eaten exposed and behind targets are excluded");
            });
            Test("explicit morph target stays locked across input coalescing and rejects invalid target without fallback", () => {
                var m = Match(true); var p = m.Player(1); m.Player(3).position = At(10, 5); m.Fish[0].position = At(2.4f, 0); m.Fish[0].species = 2; m.Fish[1].position = At(1.5f, 0); m.Fish[1].species = 1;
                m.SetInput(1, new SwimInput { aimX = 1, morph = true, morphTargetId = m.Fish[0].id });
                m.SetInput(1, new SwimInput { aimX = 1 }); m.Tick(0.1f);
                Check(p.species == 2, "queued selection does not switch to nearer fish");
                Advance(m, 0.7f); p.species = -1; m.Fish[0].eaten = true;
                m.SetInput(1, new SwimInput { aimX = 1, morph = true, morphTargetId = m.Fish[0].id }); m.Tick(0.1f);
                Check(p.species == -1, "invalid eaten selection cannot fall back to valid nearby fish");
                Advance(m, 0.7f); m.Fish[0].eaten = false; m.Fish[0].position = At(5, 0);
                m.SetInput(1, new SwimInput { aimX = 1, morph = true, morphTargetId = m.Fish[0].id }); m.Tick(0.1f); Check(p.species == -1, "locked target still validates range");
                Advance(m, 0.7f); m.Fish[0].position = At(-1, 0);
                m.SetInput(1, new SwimInput { aimX = 1, morph = true, morphTargetId = m.Fish[0].id }); m.Tick(0.1f); Check(p.species == -1, "locked target still validates angle");
                Advance(m, 0.7f); var cover = AquariumModel.Rocks[0]; p.position = cover + new Point(-1.4f, 1.3f, 0); p.velocity = new Point(); m.Fish[0].position = cover + new Point(1.2f, 1.3f, 0);
                m.SetInput(1, new SwimInput { aimX = 1, morph = true, morphTargetId = m.Fish[0].id }); m.Tick(0.1f); Check(p.species == -1, "locked target still validates rock occlusion");
            });
            Test("feed sound fades over three-dimensional distance and is silent at ten metres", () => {
                var source = new Point(0, 1, 0);
                Check(AquariumModel.FeedSoundGain(source, source) == 1, "full gain at source");
                Check(Math.Abs(AquariumModel.FeedSoundGain(source, new Point(3, 5, 0)) - 0.5f) < 0.001f, "height contributes to five metre distance");
                Check(AquariumModel.FeedSoundGain(source, new Point(0, 11, 0)) == 0 && AquariumModel.FeedSoundGain(source, new Point(0, 12, 0)) == 0, "vertical boundary and outside radius are silent");
                Check(AquariumModel.FeedSoundGain(source, new Point(9, 1, 0)) > 0 && AquariumModel.FeedSoundGain(source, new Point(10, 1, 0)) == 0, "horizontal boundary is exact");
            });
            Console.WriteLine("RESULT: " + passed + " gameplay tests passed; " + failed + " failed."); return failed==0?0:1;
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL " + ex.Message); return 1; }
    }
}

