using System;
using System.Collections.Generic;
using System.Diagnostics;
using PrototypeLab.Aquarium;

public static class GeometryPerformance
{
    private struct Ray
    {
        public Point start,end;public float padding;
        public Ray(Point a,Point b,float p=0){start=a;end=b;padding=p;}
    }
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    private static float BaselineHit(Point start,Point end,float padding=0)
    {
        var ray=end-start;float length=ray.Length;
        if(length<.00001f)return AquariumMonolithLayout.Distance(start)<=padding?0:2;
        float t=0;
        for(int step=0;step<400&&t<=1;step++)
        {
            float d=AquariumMonolithLayout.Distance(start+ray*t)-padding;
            if(d<.0015f)return t;
            t+=Math.Max(.000001f,d*.38f/length);
        }
        return 2;
    }
    private static double Dot(Point a,Point b){return (double)a.x*b.x+(double)a.y*b.y+(double)a.z*b.z;}
    private static Point Cross(Point a,Point b){return new Point(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);}

    // Distance(group) can only grow when edge cuts are applied. Its weather term
    // lies in [-.2525,.2525], and each lobe has a .02 surface offset. Every point
    // accepted by Hit is therefore inside at least one lobe whose planes have
    // all been expanded by padding+.0015+.2525+.02.
    private static bool CheckBounds(float padding,out double excess,out int worstGroup)
    {
        excess=double.NegativeInfinity;worstGroup=-1;int vertices=0;
        for(int group=0;group<AquariumMonolithLayout.Groups.Length;group++)
        {
            var min=AquariumMonolithLayout.Minimum[group];var max=AquariumMonolithLayout.Maximum[group];
            foreach(var lobe in AquariumMonolithLayout.Groups[group])
            {
                var planes=lobe.planes;double offset=padding+.0015+.2525+.02;
                for(int a=0;a<planes.Length-2;a++)for(int b=a+1;b<planes.Length-1;b++)for(int c=b+1;c<planes.Length;c++)
                {
                    var bc=Cross(planes[b].normal,planes[c].normal);double determinant=Dot(planes[a].normal,bc);
                    if(Math.Abs(determinant)<.00000001)continue;
                    var ca=Cross(planes[c].normal,planes[a].normal);var ab=Cross(planes[a].normal,planes[b].normal);
                    double ha=planes[a].offset+offset,hb=planes[b].offset+offset,hc=planes[c].offset+offset;
                    var p=new Point((float)((bc.x*ha+ca.x*hb+ab.x*hc)/determinant),(float)((bc.y*ha+ca.y*hb+ab.y*hc)/determinant),(float)((bc.z*ha+ca.z*hb+ab.z*hc)/determinant));
                    bool inside=true;foreach(var plane in planes)if(Dot(p,plane.normal)>plane.offset+offset+.00002){inside=false;break;}
                    if(!inside)continue;vertices++;
                    p.y+=p.x*.13f-p.z*.07f;p+=lobe.centre;
                    double violation=Math.Max(Math.Max(Math.Max(min.x-padding-.002-p.x,p.x-max.x-padding-.002),Math.Max(min.y-padding-.002-p.y,p.y-max.y-padding-.002)),Math.Max(min.z-padding-.002-p.z,p.z-max.z-padding-.002));
                    if(violation>excess){excess=violation;worstGroup=group;}
                }
            }
        }
        Check(vertices>0,"No fracture-plane vertices tested");return excess<=.00005;
    }
    private static float Range(Random random,float a,float b){return a+(float)random.NextDouble()*(b-a);}
    private static Ray[] Rays(string scenario)
    {
        var rays=new List<Ray>();var random=new Random(48291);
        if(scenario=="random")for(int i=0;i<12000;i++)
            rays.Add(new Ray(new Point(Range(random,-38,38),Range(random,-2,16),Range(random,-22,22)),new Point(Range(random,-38,38),Range(random,-2,16),Range(random,-22,22)),new[]{0f,.2f,.5f}[i%3]));
        if(scenario=="aim")for(int i=0;i<12000;i++)
        {
            var start=new Point(Range(random,-30,30),Range(random,1,11),Range(random,-16,16));double yaw=random.NextDouble()*Math.PI*2;
            rays.Add(new Ray(start,start+new Point((float)Math.Cos(yaw)*2.8f,0,(float)Math.Sin(yaw)*2.8f)));
        }
        if(scenario=="bounds")for(int group=0;group<AquariumMonolithLayout.Groups.Length;group++)
        {
            var min=AquariumMonolithLayout.Minimum[group];var max=AquariumMonolithLayout.Maximum[group];var middle=(min+max)*.5f;
            foreach(float padding in new[]{0f,.2f,.5f})foreach(float edge in new[]{-.00201f,-.002f,-.00199f,0f,.00199f,.002f,.00201f})
            for(int axis=0;axis<3;axis++)for(int side=-1;side<=1;side+=2)
            {
                var p=middle;float face=axis==0?(side<0?min.x:max.x):axis==1?(side<0?min.y:max.y):(side<0?min.z:max.z);face+=side*(padding+edge);
                if(axis==0)p.x=face;else if(axis==1)p.y=face;else p.z=face;
                var delta=axis==0?new Point(0,0,30):new Point(30,0,0);
                rays.Add(new Ray(p-delta,p+delta,padding));rays.Add(new Ray(p,p,padding));rays.Add(new Ray(p,p+new Point(.000001f,0,0),padding));
                rays.Add(new Ray(p,p+new Point(0,8,0),padding));rays.Add(new Ray(p,p+new Point(0,-8,0),padding));
            }
        }
        return rays.ToArray();
    }
    private static void Compare(Ray[] rays,string scenario)
    {
        int hits=0;foreach(var ray in rays)
        {
            float before=BaselineHit(ray.start,ray.end,ray.padding),after=AquariumMonolithLayout.Hit(ray.start,ray.end,ray.padding);
            Check(before==after,"Hit mismatch "+scenario+" start="+ray.start.x+","+ray.start.y+","+ray.start.z+" end="+ray.end.x+","+ray.end.y+","+ray.end.z+" padding="+ray.padding+" baseline="+before+" current="+after);
            if(before<=1)hits++;
        }
        Console.WriteLine(scenario+" exact_matches="+rays.Length+" hits="+hits);
    }
    private static double Time(Ray[] rays,bool baseline,out long bytes)
    {
        var timer=new Stopwatch();long allocated=GC.GetAllocatedBytesForCurrentThread();timer.Start();float total=0;
        foreach(var ray in rays)total+=baseline?BaselineHit(ray.start,ray.end,ray.padding):AquariumMonolithLayout.Hit(ray.start,ray.end,ray.padding);
        timer.Stop();bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;Check(total>=0,"Invalid timing accumulator");return timer.Elapsed.TotalMilliseconds*1000/rays.Length;
    }
    public static int Main(string[] args)
    {
        try
        {
            foreach(float padding in new[]{0f,.08f,.2f,.22f,.35f,.5f,.75f,1f,2f,4f,8f,16f})
            {
                double excess;int group;bool safe=CheckBounds(padding,out excess,out group);
                Console.WriteLine("bounds_padding="+padding+" safe="+safe+" max_excess="+excess.ToString("F6")+" group="+group);
                if(padding==0)Check(safe,"The unpadded hit field exceeds the broadphase bounds");
            }
            Console.WriteLine("broadphase_condition=padding==0; other_padding_uses_original_march; baseline=3e12b65 Hit loop with public Distance");
            if(args.Length>0 && args[0]=="--bounds-only")return 0;
            foreach(string scenario in new[]{"random","bounds","aim"})
            {
                var rays=Rays(scenario);Compare(rays,scenario);var before=new double[5];var after=new double[5];long beforeBytes=0,afterBytes=0;
                for(int i=0;i<5;i++)
                {
                    if(i%2==0){before[i]=Time(rays,true,out beforeBytes);after[i]=Time(rays,false,out afterBytes);}
                    else{after[i]=Time(rays,false,out afterBytes);before[i]=Time(rays,true,out beforeBytes);}
                }
                Array.Sort(before);Array.Sort(after);Console.WriteLine(scenario+" baseline_us="+before[2].ToString("F3")+" current_us="+after[2].ToString("F3")+" baseline_bytes="+beforeBytes+" current_bytes="+afterBytes);
            }
            Console.WriteLine("GEOMETRY_PERFORMANCE_OK");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
