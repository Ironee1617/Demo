using System;
using System.Collections.Generic;

namespace PrototypeLab.Roguelike
{
    public readonly struct TowerPlatform
    {
        public float X { get; }
        public float Y { get; }
        public float Width { get; }

        public TowerPlatform(float x,float y,float width)
        {
            X=x;Y=y;Width=width;
        }
    }

    public sealed class TowerPlatformPattern
    {
        public int Id { get; }
        public string Name { get; }
        public IReadOnlyList<TowerPlatform> Platforms { get; }

        internal TowerPlatformPattern(int id,string name,params TowerPlatform[] platforms)
        {
            Id=id;Name=name;
            var extended=new List<TowerPlatform>(platforms);
            // Continue each authored crown into three more ordinary-jump tiers.
            // Narrower, alternating shelves preserve the original routes below.
            float direction=id%2==0?1:-1;
            foreach(var platform in platforms)
            {
                if(Math.Abs(platform.Y-1)>.01f)continue;
                float width=Math.Min(platform.Width,3.2f);
                extended.Add(new TowerPlatform(platform.X+direction*.65f,2.4f,width));
                extended.Add(new TowerPlatform(platform.X-direction*.25f,3.8f,width));
                extended.Add(new TowerPlatform(platform.X+direction*.45f,5.2f,width));
            }
            Platforms=extended.AsReadOnly();
        }
    }

    public static class TowerPlatformCatalog
    {
        // Surface heights: the first step rises 1.2 units from the ground at -3,
        // and subsequent tiers rise 1.4. Normal jumps can climb every pattern.
        // All patterns keep the player entrance at -12 and exit at 26 clear.
        private static readonly IReadOnlyList<TowerPlatformPattern> patterns=Array.AsReadOnly(new[]
        {
            new TowerPlatformPattern(0,"오르막 지그재그",
                P(-9.6f,-1.8f,3.2f),P(-6.8f,-.4f,3),P(-3.6f,1,3.2f),
                P(.1f,-.4f,3.2f),P(3.4f,-1.8f,3.6f),P(7.3f,-.4f,3.2f),
                P(10.8f,1,3.2f),P(14.6f,-.4f,3.2f),P(18.3f,-1.8f,3.4f),P(21.6f,-.4f,3.4f)),

            new TowerPlatformPattern(1,"쌍둥이 탑",
                P(-9.6f,-1.8f,3),P(-7,-.4f,2.8f),P(-4.2f,1,3.6f),
                P(-2.4f,-1.8f,3.2f),P(.7f,-.4f,2.6f),P(6,-1.8f,4.8f),
                P(11.7f,-1.8f,3.2f),P(14.8f,-.4f,2.8f),P(17.7f,1,3.6f),
                P(20.3f,-1.8f,3.4f),P(23.1f,-.4f,2.8f)),

            new TowerPlatformPattern(2,"분지 회랑",
                P(-9.6f,-1.8f,3),P(-8.2f,-.4f,2.8f),P(-5.4f,1,3.8f),
                P(-1.8f,-.4f,3),P(1.8f,-1.8f,3),P(6,-1.8f,3.2f),
                P(10.2f,-1.8f,3),P(13.8f,-.4f,3),P(17.4f,1,3.8f),
                P(20.2f,-.4f,2.8f),P(23,-1.8f,3)),

            new TowerPlatformPattern(3,"중앙 왕관",
                P(-9.6f,-1.8f,3.2f),P(-5.5f,-1.8f,3),P(0,-1.8f,3.4f),
                P(6,-1.8f,3.4f),P(12,-1.8f,3.4f),P(17,-1.8f,3),P(22,-1.8f,3.4f),
                P(-2.2f,-.4f,3.4f),P(4,-.4f,3.4f),P(10.2f,-.4f,3.4f),P(4,1,4.8f)),

            new TowerPlatformPattern(4,"끊긴 수로",
                P(-9.6f,-1.8f,3.2f),P(-7,-.4f,4.8f),P(-2,-1.8f,3.2f),
                P(2.1f,-.4f,4.4f),P(7,-1.8f,3.2f),P(11.1f,-.4f,4.4f),
                P(16,-1.8f,3.2f),P(20.1f,-.4f,4.8f),P(.6f,1,2.8f),P(13.4f,1,2.8f)),

            new TowerPlatformPattern(5,"섬 사다리",
                P(-9,-1.8f,3.4f),P(-7.3f,-.4f,2.8f),P(-9,1,2.6f),
                P(1.3f,-1.8f,3.4f),P(3,-.4f,2.8f),P(1.3f,1,2.6f),
                P(11.5f,-1.8f,3.4f),P(13.2f,-.4f,2.8f),P(11.5f,1,2.6f),
                P(22,-1.8f,3.4f),P(20.2f,-.4f,2.8f),P(22,1,2.6f)),

            new TowerPlatformPattern(6,"긴 징검길",
                P(-9.6f,-1.8f,3),P(-6.8f,-.4f,4.8f),P(-1.6f,1,4.8f),
                P(3.5f,-.4f,4.8f),P(8.2f,-1.8f,3.4f),P(12.2f,-.4f,4.8f),
                P(17.4f,1,4.8f),P(21.5f,-1.8f,3.2f),P(23.1f,-.4f,2.8f)),

            new TowerPlatformPattern(7,"엇갈린 발판",
                P(-9.6f,-1.8f,3.2f),P(-1,-1.8f,3.4f),P(8,-1.8f,3.4f),P(21,-1.8f,3.4f),
                P(-7,-.4f,3.2f),P(2,-.4f,3.2f),P(11,-.4f,3.2f),P(18,-.4f,3.2f),
                P(-4,1,2.6f),P(5,1,2.6f),P(14,1,2.6f)),

            new TowerPlatformPattern(8,"내리막 계단",
                P(-9.6f,-1.8f,3),P(-7.2f,-.4f,3.2f),P(-3.8f,1,4.8f),
                P(1.8f,1,4),P(6.5f,-.4f,4.4f),P(11.5f,-.4f,4.4f),
                P(16,-1.8f,4),P(21,-1.8f,4),P(23,-.4f,3)),

            new TowerPlatformPattern(9,"양쪽 계단",
                P(-9.6f,-1.8f,3.2f),P(-5.8f,-1.8f,3.6f),P(-2.2f,-.4f,4),
                P(2.3f,1,4.4f),P(7.4f,1,4.4f),P(12.2f,-.4f,4),
                P(16.3f,-1.8f,3.6f),P(20.7f,-1.8f,3.6f),P(23,-.4f,3)),

            new TowerPlatformPattern(10,"갈라진 수관",
                P(-9.6f,-1.8f,3.2f),P(-7,-.4f,3.2f),P(-3.7f,1,4.6f),P(1.6f,1,4.4f),
                P(3.5f,-1.8f,3.4f),P(8.5f,-1.8f,3.4f),P(11.1f,1,4.4f),P(16.4f,1,4.6f),
                P(19.8f,-.4f,3.2f),P(23.1f,-1.8f,3.2f),P(11.5f,-.4f,3)),

            new TowerPlatformPattern(11,"교차 쉼터",
                P(-9.6f,-1.8f,3.2f),P(-7.1f,-.4f,3.6f),P(-3.4f,-1.8f,3.2f),
                P(.1f,-.4f,3.6f),P(3.8f,-1.8f,3.2f),P(7.3f,-.4f,3.6f),
                P(11,-1.8f,3.2f),P(14.5f,-.4f,3.6f),P(18.2f,-1.8f,3.2f),P(21.7f,-.4f,3.6f),
                P(7.3f,1,2.6f)),

            new TowerPlatformPattern(12,"징검 아치",
                P(-9.6f,-1.8f,3),P(-7.3f,-.4f,3.4f),P(-3.7f,1,3.8f),P(.6f,1,3.4f),
                P(4.7f,1,3.4f),P(8.8f,1,3.4f),P(13.1f,1,3.8f),P(16.7f,-.4f,3.4f),
                P(20.1f,-1.8f,3),P(23.6f,-1.8f,2.6f)),

            new TowerPlatformPattern(13,"낮은 섬길",
                P(-9.6f,-1.8f,3.2f),P(-5.6f,-1.8f,3.6f),P(-.7f,-1.8f,3.6f),
                P(4.2f,-1.8f,3.6f),P(9.1f,-1.8f,3.6f),P(14,-1.8f,3.6f),
                P(18.9f,-1.8f,3.6f),P(23.2f,-1.8f,3.2f),P(-3.2f,-.4f,3),P(11.6f,-.4f,3),
                P(11.6f,1,2.6f)),

            new TowerPlatformPattern(14,"세 갈래 왕관",
                P(-9.6f,-1.8f,3.2f),P(-7.2f,-.4f,2.8f),P(-4.3f,1,3),P(-2,-1.8f,3.2f),
                P(3,-1.8f,3.2f),P(6.2f,-.4f,2.8f),P(9.1f,1,3),P(11.4f,-1.8f,3.2f),
                P(16.4f,-1.8f,3.2f),P(19.6f,-.4f,2.8f),P(22.5f,1,3),P(23,-1.8f,3.2f)),

            new TowerPlatformPattern(15,"순환 회랑",
                P(-9.3f,-1.8f,3.8f),P(-10,-.4f,2.6f),P(-6.5f,-.4f,2.8f),P(-3.2f,1,3.2f),
                P(-.5f,-1.8f,4),P(2.8f,-.4f,3.2f),P(6.6f,-.4f,3.2f),P(10.2f,1,3.2f),
                P(13,-1.8f,4),P(16.5f,-.4f,3.2f),P(20.3f,-.4f,3.2f),P(23.4f,-1.8f,2.8f))
        });

        public static IReadOnlyList<TowerPlatformPattern> All { get { return patterns; } }

        public static TowerPlatformPattern Select(int runSeed,int floor)
        {
            int ordinal=floor>0?floor-1:0;
            int cycle=ordinal/patterns.Count;
            int[] bag=ShuffledBag(runSeed,cycle);
            // Keep the last item intact, so the previous unadjusted bag provides
            // its exact boundary item without recursively generating older bags.
            if(cycle>0&&bag[0]==ShuffledBag(runSeed,cycle-1)[patterns.Count-1])
            {
                int swap=bag[0];bag[0]=bag[1];bag[1]=swap;
            }
            return patterns[bag[ordinal%patterns.Count]];
        }

        private static TowerPlatform P(float x,float y,float width)
        {
            return new TowerPlatform(x,y,width);
        }

        private static int[] ShuffledBag(int runSeed,int cycle)
        {
            int[] bag=new int[patterns.Count];
            for(int i=0;i<bag.Length;i++)bag[i]=i;
            // Separate deterministic RNG: decoration and combat random calls
            // never change which platform layout a floor receives.
            uint state=unchecked((uint)runSeed^((uint)cycle*0x9E3779B9u)^0xA511E9B3u);
            if(state==0)state=0x6D2B79F5u;
            for(int i=bag.Length-1;i>0;i--)
            {
                state^=state<<13;state^=state>>17;state^=state<<5;
                int next=(int)(state%(uint)(i+1));
                int swap=bag[i];bag[i]=bag[next];bag[next]=swap;
            }
            return bag;
        }
    }
}
