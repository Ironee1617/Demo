using System;
using System.Diagnostics;
using PrototypeLab.Aquarium;
public static class ModelPerformance
{
    public static int Main()
    {
        foreach(bool feeding in new[]{false,true})
        {
            var model=new AquariumModel(73);
            if(feeding) model.Effects.Add(new WaterEffect{kind="feed",position=new Point(2.3f,7,4),duration=100});
            for(int i=0;i<30;i++)model.Tick(1f/30);
            var samples=new double[300];long allocated=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<samples.Length;i++){var timer=Stopwatch.StartNew();model.Tick(1f/30);timer.Stop();samples[i]=timer.Elapsed.TotalMilliseconds;}
            allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;Array.Sort(samples);
            double total=0;foreach(var sample in samples)total+=sample;
            Console.WriteLine((feeding?"feed":"cruise")+" mean_ms="+(total/samples.Length).ToString("F3")+" p95_ms="+samples[285].ToString("F3")+" max_ms="+samples[299].ToString("F3")+" bytes_per_tick="+(allocated/samples.Length));
        }
        return 0;
    }
}