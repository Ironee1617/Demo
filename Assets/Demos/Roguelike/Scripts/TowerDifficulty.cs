using System;

namespace PrototypeLab.Roguelike
{
    // Linear tuning across the 100-floor climb. Warnings retain their full preparation time as stats rise.
    // Floor 100 lands close to the former 25th floor; cards now arrive every five floors.
    public static class TowerDifficulty
    {
        public const int MaxFloor = 100;
        public const float EliteHealth = 2.2f, EliteDamage = 1.25f, EliteScale = 1.22f;
        private static int Floor(int floor){return Math.Max(1,Math.Min(MaxFloor,floor));}
        // A floor runs about four screens, so its ground monsters come in packs along the way.
        public static int RegularCount(int floor){return 6+(Floor(floor)-1)/12;}
        public static int EliteCount(int floor){return Math.Max(5,RegularCount(floor)-2);}
        /// <summary>Flyers perched on the high shelves: none on the first floor, then one, two, three as the climb goes on.</summary>
        public static int FlyerCount(int floor){int f=Floor(floor);return f<2?0:f<20?1:f<50?2:3;}
        public static bool IsBossFloor(int floor){return Floor(floor)%5==0;}
        public static float HealthMultiplier(int floor){return 1+.016f*(Floor(floor)-1);}
        public static float DamageMultiplier(int floor){return 1+.008f*(Floor(floor)-1);}
        public static float MoveMultiplier(int floor){return 1+.002f*(Floor(floor)-1);}
        public static float BulletMultiplier(int floor){return 1+.0012f*(Floor(floor)-1);}
        public static float AttackRateMultiplier(int floor){return 1+.002f*(Floor(floor)-1);}
        public static float BossHealth(int floor){return 420*(1+.019f*Math.Max(0,Floor(floor)-5));}
        // The ten-floor oath trial keeps its former pace by sampling the long curve four floors at a time.
        public static int TrialFloor(int floor){return 1+(Math.Max(1,floor)-1)*4;}
        // Trial bosses on floors 5 and 10 keep their former 420 and 577.5 health.
        public static int TrialBossFloor(int floor){return 5+(Math.Max(5,floor)-5)*4;}
    }
}
