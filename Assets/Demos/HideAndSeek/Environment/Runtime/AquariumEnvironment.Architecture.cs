using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    public static partial class AquariumEnvironment
    {
        private static Vector3 EntryAt(float x,float z,float y)
        { float elevated=Elevated(y);return ToVector(AquariumEntryLayout.At(x,z,elevated),elevated); }

        // One clipped surface and one continuous exposed boundary replace overlapping swept slabs.
        // The signed fields use the same paths/radii as gameplay collision. No union seam can
        // produce an interior railing, duplicated transparent wall, bulging end cap or Z fighting.
        private static void BuildJoinedArchitecture(Transform root,Material stone,Material paving,
            Material light,Material glass,Material structuralGlass,Material water,Material interiorFloor,Material sand,AquariumEnvironmentRoot marker)
        {
            var field=new DeckField();
            BuildSandBed(root,sand,field);
            var deck=new Geo();var soffit=new Geo();var dryFloor=new Geo();var waterGeo=new Geo();
            field.Surface(deck,field.Deck,Elevated(7.275f),Stone);
            // The lowest animated water crest is 7.016 m. Keep the sealed underside below it
            // so the water cannot poke through the cantilever as circles or triangle-shaped bands.
            field.Surface(soffit,field.Deck,Elevated(6.96f),Stone*.78f,true);
            field.Surface(dryFloor,field.InteriorFloor,.23f,C(0xb8aa8f));
            var waterLoops=field.Contours(field.Water);
            if(waterLoops.Count!=1)throw new InvalidOperationException("The connected lagoon must have one exposed glass boundary.");
            var waterLoop=waterLoops[0];
            FillWaterPolygon(waterGeo,waterLoop);
            deck.Emit("06 • Joined limestone promenade and observation walks",root,paving);
            soffit.Emit("06b • Continuous sealed deck soffit",root,stone);
            dryFloor.Emit("06c • Unified dry gallery foundation",root,interiorFloor);
            waterGeo.Emit("05 • Animated water surface / trimmed to glass",root,water,false);

            var panels=new Geo();var deep=new Geo();var perimeter=new Geo();var framing=new Geo();
            var glow=new Geo();var fascia=new Geo();var trim=new Geo();
            float postTravel=0;
            for(int i=0;i<waterLoop.Count;i++)
            {
                var a=waterLoop[i];var b=waterLoop[(i+1)%waterLoop.Count];
                var middle=(a+b)*.5f;bool central=IsWalkBoundary(middle);
                (central?deep:perimeter).Quad(Height(a,.27f),Height(b,.27f),Height(b,Elevated(7.02f)),Height(a,Elevated(7.02f)),Color.white);
                postTravel+=Vector3.Distance(a,b);
                if(postTravel>=1.5f)
                {
                    framing.Cylinder(Height(a,.27f),Height(a,Elevated(7.02f)),.024f,.024f,Brass,8);
                    framing.Cylinder(Height(a,.18f),Height(a,.33f),.075f,.075f,Navy,10);
                    postTravel=0;
                }
            }
            BoundaryBand(framing,waterLoop,.19f,.27f,.14f,Navy);
            BoundaryBand(framing,waterLoop,Elevated(6.98f),Elevated(7.025f),.09f,Brass);
            BoundaryBand(glow,waterLoop,.32f,.35f,.035f,C(0x29d9f1));

            var deckLoops=field.Contours(field.OpenWater);
            if(deckLoops.Count!=1)throw new InvalidOperationException("Entry apron must join the main promenade.");
            var deckLoop=deckLoops[0];
            BoundaryBand(fascia,deckLoop,Elevated(6.96f),Elevated(7.12f),.045f,Navy);
            BoundaryBand(fascia,deckLoop,Elevated(7.15f),Elevated(7.275f),.045f,Navy);
            BoundaryBand(trim,deckLoop,Elevated(7.26f),Elevated(7.29f),.11f,C(0xd3bd91));
            BoundaryBand(glow,deckLoop,Elevated(7.12f),Elevated(7.15f),.045f,C(0x77e2db));
            postTravel=0;
            for(int i=0;i<deckLoop.Count;i++)
            {
                var a=deckLoop[i];var b=deckLoop[(i+1)%deckLoop.Count];var mid=(a+b)*.5f;
                var local=AquariumEntryLayout.Local(new Point(mid.x,mid.z));
                bool entry=Mathf.Abs(AquariumEntryLayout.DeckDistance(new Point(mid.x,mid.z)))<.15f && local.z>-.2f;
                bool entryOpening=entry && Mathf.Abs(local.x)<1.3f && local.z>3.05f;
                if((!IsWalkBoundary(mid)&&!entry)||entryOpening){postTravel=1.5f;continue;}
                panels.Quad(Height(a,Elevated(7.36f)),Height(b,Elevated(7.36f)),Height(b,Elevated(8.12f)),Height(a,Elevated(8.12f)),Color.white);
                trim.Cylinder(Height(a,Elevated(8.15f)),Height(b,Elevated(8.15f)),.024f,.024f,Brass,8);
                postTravel+=Vector3.Distance(a,b);
                if(postTravel>=1.45f)
                { trim.Cylinder(Height(a,Elevated(7.28f)),Height(a,Elevated(8.15f)),.025f,.025f,Navy,8);postTravel=0; }
            }
            deep.Emit("20 • Full-depth walkway glass / continuous joined envelope",root,structuralGlass,false);
            perimeter.Emit("04 • Curved aquarium glass / trimmed at deck junction",root,glass,false);
            framing.Emit("21 • Continuous glass plinths and bronze mullions",root,stone);
            fascia.Emit("06d • Joined deck edge fascia",root,stone);
            trim.Emit("07 • Continuous rounded coping and handrails",root,stone);
            panels.Emit("08 • Continuous observation and entry balustrades",root,glass,false);
            glow.Emit("22 • Continuous underwater and deck lighting",root,light,false);
            BuildDiveEntry(root,stone,light,marker);
        }

        private static bool IsWalkBoundary(Vector3 p)
        {
            return AquariumWalkwayLayout.SignedDistance(new Point(p.x,p.z))<.12f;
        }

        private static Vector3 Height(Vector3 p,float y) { p.y=y;return p; }

        private static void FillWaterPolygon(Geo mesh,List<Vector3> boundary)
        {
            var points=new List<Point>();
            for(int i=0;i<boundary.Count;i++)
            {
                var a=boundary[(i+boundary.Count-1)%boundary.Count];var b=boundary[i];var c=boundary[(i+1)%boundary.Count];
                float cross=(b.x-a.x)*(c.z-a.z)-(b.z-a.z)*(c.x-a.x);
                if(Mathf.Abs(cross)>.00005f)points.Add(new Point(b.x,b.z));
            }
            float area=0;for(int i=0;i<points.Count;i++)area+=points[i].x*points[(i+1)%points.Count].z-points[(i+1)%points.Count].x*points[i].z;
            if(area>0)points.Reverse();
            var polygon=points.ToArray();var indices=AquariumMapOutline.Triangulate(polygon);
            for(int i=0;i<indices.Length;i+=3)
                mesh.Triangle(ToVector(polygon[indices[i]],Elevated(7.04f)),ToVector(polygon[indices[i+1]],Elevated(7.04f)),ToVector(polygon[indices[i+2]],Elevated(7.04f)),Color.white);
        }

        private static void BoundaryBand(Geo g,List<Vector3> loop,float low,float high,float width,Color tint)
        {
            var sides=new Vector3[loop.Count];
            for(int i=0;i<loop.Count;i++)
            {
                var d=(loop[(i+1)%loop.Count]-loop[(i+loop.Count-1)%loop.Count]).normalized;
                sides[i]=Vector3.Cross(d,Vector3.up)*(width*.5f);
            }
            for(int i=0;i<loop.Count;i++)
            {
                int j=(i+1)%loop.Count;var a=loop[i];var b=loop[j];var u=sides[i];var v=sides[j];
                g.Quad(Height(a-u,high),Height(a+u,high),Height(b+v,high),Height(b-v,high),tint);
                g.Quad(Height(a-u,low),Height(a-u,high),Height(b-v,high),Height(b-v,low),tint*.85f);
                g.Quad(Height(a+u,high),Height(a+u,low),Height(b+v,low),Height(b+v,high),tint*.85f);
                g.Quad(Height(a-u,low),Height(b-v,low),Height(b+v,low),Height(a+u,low),tint*.74f);
            }
        }

        private static void BuildDiveEntry(Transform root,Material stone,Material light,AquariumEnvironmentRoot marker)
        {
            var deck=new Geo();var hardware=new Geo();var lamps=new Geo();
            var right=ToVector(AquariumEntryLayout.Right,0);var inward=ToVector(AquariumEntryLayout.Inward,0);
            var rotation=Quaternion.LookRotation(inward);
            // A shallow inset of individually fitted teak boards, with a flush limestone approach.
            for(int row=0;row<16;row++)
            {
                float z=-.75f+row*.235f;float width=z>2.55f?6.55f:6.8f;
                deck.Box(EntryAt(0,z,7.297f),new Vector3(width,.044f,.217f),Color.Lerp(C(0x9d7046),C(0xc59a64),(row%4)/4f),rotation);
                for(int bolt=-1;bolt<=1;bolt+=2)
                    hardware.Cylinder(EntryAt(bolt*(width*.5f-.12f),z,7.319f),EntryAt(bolt*(width*.5f-.12f),z,7.325f),.017f,.017f,Brass,7);
            }
            // Non-slip take-off pad and a clear 2.6 m opening in the rail at the water's edge.
            deck.Box(EntryAt(0,2.28f,7.33f),new Vector3(2.42f,.025f,1.45f),C(0x243f4a),rotation);
            for(int i=0;i<10;i++)deck.Box(EntryAt(0,1.65f+i*.135f,7.346f),new Vector3(2.25f,.008f,.016f),C(0x637a7c),rotation);
            deck.Box(EntryAt(0,3.02f,7.33f),new Vector3(2.5f,.032f,.16f),C(0xe6d5a5),rotation);
            for(int side=-1;side<=1;side+=2)
            {
                var p=EntryAt(side*1.35f,3.1f,7.29f);
                hardware.Cylinder(p,p+Vector3.up*.9f,.035f,.035f,Brass,12);
                lamps.Cylinder(p+Vector3.up*.85f,p+Vector3.up*.91f,.045f,.045f,C(0xffde9b),10);
            }
            // Cantilever brackets leave the entry water column completely open.
            foreach(float x in new[]{-2.6f,0f,2.6f})
            {
                hardware.Cylinder(EntryAt(x,.08f,5.65f),EntryAt(x,2.7f,7.00f),.065f,.065f,Navy,10);
                hardware.Box(EntryAt(x,.02f,5.64f),new Vector3(.26f,.42f,.1f),Brass,rotation);
            }
            // Stainless return ladder is offset to the right of the entry opening.
            foreach(float x in new[]{2.35f,2.95f})
            {
                var curve=new List<Vector3>();
                curve.Add(EntryAt(x,3.48f,5.25f));curve.Add(EntryAt(x,3.48f,7.83f));
                for(int i=0;i<=14;i++)
                { float a=Mathf.PI*i/14;curve.Add(EntryAt(x,3.18f+Mathf.Cos(a)*.30f,7.83f+Mathf.Sin(a)*.30f)); }
                curve.Add(EntryAt(x,2.88f,7.31f));
                for(int i=0;i<curve.Count-1;i++)hardware.Cylinder(curve[i],curve[i+1],.036f,.036f,C(0xb8cfd0),12);
            }
            for(int i=0;i<8;i++)
            {
                float y=5.42f+i*.29f;
                hardware.Cylinder(EntryAt(2.35f,3.48f,y),EntryAt(2.95f,3.48f,y),.045f,.045f,C(0xa5baba),12);
            }
            // Preparation bench and neatly retained air cylinders provide a readable staging backdrop.
            for(int row=0;row<4;row++)deck.Box(EntryAt(-1.8f,-2.6f+row*.12f,7.78f),new Vector3(2.0f,.065f,.1f),C(0xad7b49),rotation);
            foreach(float x in new[]{-2.55f,-1.05f})hardware.Box(EntryAt(x,-2.42f,7.51f),new Vector3(.07f,.46f,.5f),Navy,rotation);
            for(int i=0;i<3;i++)
            {
                float x=2.0f+i*.42f;
                hardware.Cylinder(EntryAt(x,-2.2f,7.29f),EntryAt(x,-2.2f,8.18f),.15f,.14f,C(0xa3bfc1),16);
                hardware.Ellipsoid(EntryAt(x,-2.2f,8.17f),new Vector3(.14f,.12f,.14f),C(0xa3bfc1),16,8,0);
                hardware.Cylinder(EntryAt(x,-2.2f,7.32f),EntryAt(x,-2.2f,7.40f),.158f,.158f,Navy,16);
                hardware.Cylinder(EntryAt(x,-2.2f,7.90f),EntryAt(x,-2.2f,7.98f),.151f,.151f,Navy,16);
                hardware.Cylinder(EntryAt(x,-2.2f,8.26f),EntryAt(x,-2.2f,8.36f),.038f,.038f,Brass,10);
            }
            hardware.Box(EntryAt(2.42f,-2.42f,7.73f),new Vector3(1.38f,.07f,.08f),Navy,rotation);
            hardware.Box(EntryAt(.0f,-3.35f,7.69f),new Vector3(2.05f,.58f,.12f),Navy,rotation);
            Label(root,"DIVE  /  01",EntryAt(0,-3.27f,7.73f),Quaternion.LookRotation(-inward),.035f,C(0xead5a3));
            deck.Emit("23 • North dive preparation deck / fitted teak and entry pad",root,stone);
            hardware.Emit("24 • North dive ladder, brackets and preparation equipment",root,stone);
            lamps.Emit("25 • Dive entry guide lights",root,light,false);

            var anchors=new GameObject("Dive entry staging | static anchors only").transform;anchors.SetParent(root,false);
            marker.divePreparation=EntryMarker(anchors,"01 Preparation",EntryAt(0,.1f,7.36f),inward);
            marker.diveEdge=EntryMarker(anchors,"02 Entry edge",EntryAt(0,3.0f,7.36f),inward);
            marker.diveWaterTarget=EntryMarker(anchors,"03 Clear water target",EntryAt(0,6.0f,5.8f),inward);
            marker.diveCamera=EntryMarker(anchors,"04 Suggested establishing camera",EntryAt(-8.0f,10.5f,12.4f),inward);
            marker.diveCamera.LookAt(EntryAt(0,1.3f,7.3f));
        }

        private static Transform EntryMarker(Transform parent,string name,Vector3 position,Vector3 direction)
        {
            var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;
            t.localRotation=Quaternion.LookRotation(direction);return t;
        }

        // Shared triangle clipping yields disjoint top faces and a watertight boundary. Keeping the
        // clipping lattice at 18 cm makes the visible/collision discrepancy under a few centimetres.
        private sealed class DeckField
        {
            private const float Step=.18f;
            private readonly Vector3[] points;
            private readonly int width,height;
            public readonly float[] Water,OpenWater,Deck,Dry,InteriorFloor;
            public DeckField()
            {
                var outer=AquariumMapOutline.Sample(true,496);var inner=AquariumMapOutline.Sample(false,496);
                float minX=99,maxX=-99,minZ=99,maxZ=-99;
                foreach(var p in outer){minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minZ=Mathf.Min(minZ,p.z);maxZ=Mathf.Max(maxZ,p.z);}
                minX=Mathf.Floor(minX/Step)*Step-Step;minZ=Mathf.Floor(minZ/Step)*Step-Step;
                width=Mathf.CeilToInt((maxX-minX)/Step)+2;height=Mathf.CeilToInt((maxZ-minZ)/Step)+2;
                int size=width*height;points=new Vector3[size];Water=new float[size];OpenWater=new float[size];Deck=new float[size];Dry=new float[size];InteriorFloor=new float[size];
                for(int z=0;z<height;z++)for(int x=0;x<width;x++)
                {
                    int i=z*width+x;var p=new Point(minX+x*Step,minZ+z*Step);points[i]=ToVector(p,0);
                    float innerDistance=InsideDistance(p,inner),outsideDistance=InsideDistance(p,outer),wet=innerDistance;
                    wet=Mathf.Min(wet,AquariumWalkwayLayout.SignedDistance(p));
                    float open=Mathf.Min(wet,AquariumEntryLayout.DeckDistance(p));
                    Water[i]=Nudge(wet);OpenWater[i]=Nudge(open);Deck[i]=Nudge(Mathf.Min(outsideDistance,-open));Dry[i]=Nudge(Mathf.Min(innerDistance,-wet));InteriorFloor[i]=Nudge(Mathf.Min(outsideDistance,-wet));
                }
            }
            private static float Nudge(float d) { return Mathf.Abs(d)<.00001f?.00001f:d; }
            private static float InsideDistance(Point p,Point[] polygon)
            {
                float closest=float.MaxValue;bool inside=false;
                for(int i=0;i<polygon.Length;i++)
                {
                    var a=polygon[i];var b=polygon[(i+1)%polygon.Length];float dx=b.x-a.x,dz=b.z-a.z;
                    if((a.z>p.z)!=(b.z>p.z)&&p.x<dx*(p.z-a.z)/dz+a.x)inside=!inside;
                    float t=Mathf.Clamp01(((p.x-a.x)*dx+(p.z-a.z)*dz)/Mathf.Max(.000001f,dx*dx+dz*dz));
                    float ex=p.x-a.x-t*dx,ez=p.z-a.z-t*dz;closest=Mathf.Min(closest,ex*ex+ez*ez);
                }
                return Mathf.Sqrt(closest)*(inside?1:-1);
            }
            public void Surface(Geo output,float[] values,float y,Color tint,bool reverse=false)
            {
                for(int z=0;z<height-1;z++)for(int x=0;x<width-1;x++)
                {
                    int a=z*width+x,b=a+1,c=a+width,d=c+1;
                    Clip(output,values,a,c,d,y,tint,reverse);Clip(output,values,a,d,b,y,tint,reverse);
                }
            }
            private void Clip(Geo g,float[] f,int a,int b,int c,float y,Color tint,bool reverse)
            {
                if(f[a]<0&&f[b]<0&&f[c]<0)return;
                if(f[a]>0&&f[b]>0&&f[c]>0){Face(g,points[a],points[b],points[c],y,tint,reverse);return;}
                int[] indices={a,b,c};var poly=new List<Vector3>(4);
                for(int i=0;i<3;i++)
                {
                    int j=indices[i],k=indices[(i+1)%3];
                    if(f[j]>0)poly.Add(points[j]);
                    if((f[j]>0)!=(f[k]>0))poly.Add(Crossing(j,k,f));
                }
                for(int i=1;i<poly.Count-1;i++)Face(g,poly[0],poly[i],poly[i+1],y,tint,reverse);
            }
            private static void Face(Geo g,Vector3 a,Vector3 b,Vector3 c,float y,Color tint,bool reverse)
            { if(Vector3.Cross(b-a,c-a).sqrMagnitude<.0000000001f)return;g.Triangle(Height(a,y),Height(reverse?c:b,y),Height(reverse?b:c,y),tint); }
            private Vector3 Crossing(int a,int b,float[] f)
            { if(a>b){int swap=a;a=b;b=swap;}return Vector3.LerpUnclamped(points[a],points[b],f[a]/(f[a]-f[b])); }
            private long EdgeKey(int a,int b) { return ((long)Math.Min(a,b)<<32)|(uint)Math.Max(a,b); }
            public List<List<Vector3>> Contours(float[] f)
            {
                var nodes=new Dictionary<long,Vector3>();var neighbours=new Dictionary<long,List<long>>();
                for(int z=0;z<height-1;z++)for(int x=0;x<width-1;x++)
                { int a=z*width+x,b=a+1,c=a+width,d=c+1;ContourTriangle(a,c,d,f,nodes,neighbours);ContourTriangle(a,d,b,f,nodes,neighbours); }
                var loops=new List<List<Vector3>>();var visited=new HashSet<long>();
                foreach(var start in nodes.Keys)
                {
                    if(visited.Contains(start))continue;var loop=new List<Vector3>();long previous=-1,current=start;
                    do
                    {
                        if(!visited.Add(current))throw new InvalidOperationException("Deck boundary is not a simple closed loop.");
                        loop.Add(nodes[current]);var adjacent=neighbours[current];
                        if(adjacent.Count!=2)throw new InvalidOperationException("Open deck boundary.");
                        long next=adjacent[0]==previous?adjacent[1]:adjacent[0];previous=current;current=next;
                    }while(current!=start);
                    // Resample at an even arc length so mullions and coping cannot bunch at cell corners.
                    loops.Add(Resample(loop,.22f));
                }
                return loops;
            }
            private void ContourTriangle(int a,int b,int c,float[] f,Dictionary<long,Vector3> nodes,Dictionary<long,List<long>> neighbours)
            {
                if((f[a]>0)==(f[b]>0)&&(f[b]>0)==(f[c]>0))return;
                int[] ids={a,b,c};long first=-1,second=-1;
                for(int i=0;i<3;i++)
                {
                    int j=ids[i],k=ids[(i+1)%3];if((f[j]>0)==(f[k]>0))continue;
                    long key=EdgeKey(j,k);if(!nodes.ContainsKey(key)){nodes.Add(key,Crossing(j,k,f));neighbours.Add(key,new List<long>(2));}
                    if(first<0)first=key;else second=key;
                }
                neighbours[first].Add(second);neighbours[second].Add(first);
            }
            private static List<Vector3> Resample(List<Vector3> source,float spacing)
            {
                float total=0;for(int i=0;i<source.Count;i++)total+=Vector3.Distance(source[i],source[(i+1)%source.Count]);
                int count=Mathf.CeilToInt(total/spacing);float step=total/count,accumulated=0,target=0;
                var result=new List<Vector3>(count);
                for(int i=0;i<source.Count&&result.Count<count;i++)
                {
                    var a=source[i];var b=source[(i+1)%source.Count];float length=Vector3.Distance(a,b);
                    while(target<=accumulated+length && result.Count<count)
                    {result.Add(Vector3.LerpUnclamped(a,b,(target-accumulated)/Mathf.Max(length,.000001f)));target+=step;}
                    accumulated+=length;
                }
                return result;
            }
        }
    }
}
