using System;
using System.Diagnostics;
using PrototypeLab.Aquarium;
public static class ModelPerformance
{
    public static int Main()
    {
        foreach(string scenario in new[]{"cruise","feed","crowded_feed","round_cruise"})
        {
            var model=new AquariumModel(73);
            if(scenario=="round_cruise")
            {
                model.AddPlayer(1,"Hider",Team.Hider);model.AddPlayer(2,"Diver",Team.Seeker);model.SetRole(2,Team.Seeker,true);model.StartRound();
                foreach(var p in model.Players){p.bot=false;p.position=new Point(-25,9,-5-p.id*.8f);}
            }
            if(scenario.Contains("feed"))model.Effects.Add(new WaterEffect{kind="feed",position=new Point(2.3f,8,3.8f),duration=100});
            if(scenario=="crowded_feed")for(int i=0;i<model.Fish.Count;i++)
            {var f=model.Fish[i];f.position=new Point(2.3f+(i%4)*.7f,8+(i/16)*.8f,3+(i/4%4)*.7f);f.facing=new Point(1,0,0);f.turnAt=float.MaxValue;}
            for(int i=0;i<30;i++)model.Tick(1f/30);
            var samples=new double[180];long allocated=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<samples.Length;i++){long start=Stopwatch.GetTimestamp();model.Tick(1f/30);samples[i]=(Stopwatch.GetTimestamp()-start)*1000d/Stopwatch.Frequency;}
            allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;Array.Sort(samples);
            double total=0;foreach(var sample in samples)total+=sample;
            Console.WriteLine(scenario+" mean_ms="+(total/samples.Length).ToString("F3")+" p95_ms="+samples[(int)(samples.Length*.95)].ToString("F3")+" max_ms="+samples[samples.Length-1].ToString("F3")+" bytes_per_tick="+(allocated/samples.Length));
        }
        return 0;
    }
}