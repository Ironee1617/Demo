using System;

namespace PrototypeLab.Roguelike
{
    // Linear demo tuning. Warnings retain their full preparation time as stats rise.
    public static class TowerDifficulty
    {
        private static int Floor(int floor){return Math.Max(1,Math.Min(25,floor));}
        public static int RegularCount(int floor){return 4+(Floor(floor)-1)/3;}
        public static bool IsBossFloor(int floor){return Floor(floor)%3==0;}
        public static float HealthMultiplier(int floor){return 1+.06f*(Floor(floor)-1);}
        public static float DamageMultiplier(int floor){return 1+.03f*(Floor(floor)-1);}
        public static float MoveMultiplier(int floor){return 1+.008f*(Floor(floor)-1);}
        public static float BulletMultiplier(int floor){return 1+.005f*(Floor(floor)-1);}
        public static float AttackRateMultiplier(int floor){return 1+.008f*(Floor(floor)-1);}
        public static float BossHealth(int floor){return 420*(1+.075f*Math.Max(0,Floor(floor)-3));}
    }
}
