using System;
using System.Collections.Generic;

namespace PrototypeLab.Roguelike
{
    public enum LootKind { Coin, GoldCoin, Heart }

    /// <summary>
    /// A breakable prop painted for one theme (Tools/TowerArt/StoryPainter, Resources/Tower/Breakables/<see cref="Id"/>).
    /// Fragile vessels shatter at the first blow; sturdy ones take two unless the blow is a heavy one.
    /// </summary>
    public sealed class TowerCacheDefinition
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public TowerThemeId Theme { get; private set; }
        public int Hits { get; private set; }
        public bool Sturdy { get { return Hits > 1; } }
        /// <summary>Colour of the chips and dust its material sheds.</summary>
        public string ChipHex { get; private set; }
        internal TowerCacheDefinition(string id, string name, TowerThemeId theme, int hits, string chip)
        { Id = id; Name = name; Theme = theme; Hits = hits; ChipHex = chip; }
    }

    /// <summary>Where a cache stands: its base on the ground (support -1) or on a platform.</summary>
    public struct TowerCacheSpot
    {
        public float X, Y; public int Support; public TowerCacheDefinition Definition;
    }

    public struct TowerLootDrop
    {
        public LootKind Kind;
        public int Gold { get { return TowerLoot.GoldOf(Kind); } }
        public TowerLootDrop(LootKind kind) { Kind = kind; }
    }

    /// <summary>
    /// Unity-independent rules for the breakable caches placed on every tower floor and the hearts and coins they
    /// scatter. Placement is seeded by the run and floor, so a floor always offers the same caches.
    /// </summary>
    public static class TowerLoot
    {
        public const int CoinGold = 1, GoldCoinGold = 5;
        public const int MinCaches = 2, MaxCaches = 4, PeacefulCaches = 2;
        public const float HeartChance = .3f, GoldCoinChance = .1f, HeartHealFraction = .12f;
        // Caches keep clear of the hero's entry lane, the exits (the three branch doors span the last eleven
        // units before the floor's exit), the merchant stall or campfire and every trap on the floor.
        public const float EntryClearX = -8.6f, ExitClear = 2.6f, BranchClear = 11f, FeatureClear = 3.6f, HazardClear = 1.5f;
        public const float MinSpacing = 2.4f, MaxTilt = .2f, Headroom = 1.9f, EdgeMargin = .6f;
        private static readonly TowerCacheDefinition[] all = {
            new TowerCacheDefinition("olympus-amphora", "붉은 그림 암포라", TowerThemeId.Olympus, 1, "c0653a"),
            new TowerCacheDefinition("olympus-chest", "청동 테 두른 궤짝", TowerThemeId.Olympus, 2, "8a5c3a"),
            new TowerCacheDefinition("asgard-ice", "보물이 얼어붙은 얼음덩이", TowerThemeId.Asgard, 1, "bfe8ff"),
            new TowerCacheDefinition("asgard-barrel", "서리 맺힌 벌꿀술 통", TowerThemeId.Asgard, 2, "7a5a3a"),
            new TowerCacheDefinition("dangun-onggi", "옹기 항아리", TowerThemeId.Dangun, 1, "6a4a30"),
            new TowerCacheDefinition("dangun-sack", "볏짚 가마니", TowerThemeId.Dangun, 2, "c8a050"),
            new TowerCacheDefinition("eden-basket", "선악과 바구니", TowerThemeId.Eden, 1, "a07840"),
            new TowerCacheDefinition("eden-crate", "덩굴 감긴 궤짝", TowerThemeId.Eden, 2, "6c864c"),
            new TowerCacheDefinition("veda-matka", "흙 물동이", TowerThemeId.Veda, 1, "b0583a"),
            new TowerCacheDefinition("veda-kalasha", "놋쇠 칼라샤", TowerThemeId.Veda, 2, "d8ac4c"),
            new TowerCacheDefinition("papyrus-canopic", "카노푸스 단지", TowerThemeId.Papyrus, 1, "d8cba8"),
            new TowerCacheDefinition("papyrus-chest", "채색 나무 궤", TowerThemeId.Papyrus, 2, "2a5a8a"),
            new TowerCacheDefinition("chinese-vase", "청화 자기", TowerThemeId.Chinese, 1, "e8eef4"),
            new TowerCacheDefinition("chinese-chest", "붉은 칠기 함", TowerThemeId.Chinese, 2, "a0281e"),
            new TowerCacheDefinition("hell-urn", "해골 유골함", TowerThemeId.Hell, 1, "4a3a34"),
            new TowerCacheDefinition("hell-crate", "불탄 형틀 상자", TowerThemeId.Hell, 2, "ff7a2a"),
            new TowerCacheDefinition("abyss-sac", "고동치는 알주머니", TowerThemeId.Abyss, 1, "8a2a4a"),
            new TowerCacheDefinition("abyss-crystal", "공허 수정", TowerThemeId.Abyss, 2, "9a6ae0"),
        };
        public static readonly IReadOnlyList<TowerCacheDefinition> All = Array.AsReadOnly(all);

        public static IReadOnlyList<TowerCacheDefinition> ForTheme(TowerThemeId theme)
        { var list = new List<TowerCacheDefinition>(); foreach (var d in all) if (d.Theme == theme) list.Add(d); return list.AsReadOnly(); }
        /// <summary>Every floor of a free climb or chapter holds caches; the oath trial keeps its fixed challenge.</summary>
        public static bool HasCaches(TowerRun run) { return run != null && !run.IsOathTrial; }
        public static int GoldOf(LootKind kind) { return kind == LootKind.Coin ? CoinGold : kind == LootKind.GoldCoin ? GoldCoinGold : 0; }
        public static float HeartHeal(float maxHealth) { return Math.Max(1, maxHealth * HeartHealFraction); }

        /// <summary>What one broken cache scatters: sometimes a heart, always coins, a little more on deeper floors.</summary>
        public static List<TowerLootDrop> Roll(Random random, TowerCacheDefinition cache, int floor)
        {
            if (random == null) throw new ArgumentNullException("random");
            if (cache == null) throw new ArgumentNullException("cache");
            var drops = new List<TowerLootDrop>();
            bool heart = random.NextDouble() < HeartChance;
            if (heart) drops.Add(new TowerLootDrop(LootKind.Heart));
            int coins = (heart ? 1 : 3) + random.Next(3) + (cache.Sturdy ? 1 : 0) + Math.Max(0, floor - 1) / 30;
            for (int i = 0; i < coins; i++) drops.Add(new TowerLootDrop(random.NextDouble() < GoldCoinChance ? LootKind.GoldCoin : LootKind.Coin));
            return drops;
        }

        /// <summary>
        /// Two to four caches on a battle floor, two on a rest or shop floor, each on a nearly level stretch of ground
        /// or a platform with room above it, kept apart from the others and from the floor's traps. Every cache gets its
        /// own stretch of the floor between the entry and the exit (the doors on a branch floor), so a long floor spreads
        /// them out. <paramref name="keepClear"/> adds positions to avoid like traps.
        /// </summary>
        public static List<TowerCacheSpot> Place(TowerFloorLayout layout, TowerThemeId theme, int seed, int floor, FloorType type, bool branch,
            IList<float> keepClear = null)
        {
            var spots = new List<TowerCacheSpot>();
            var kinds = ForTheme(theme);
            if (layout == null || kinds.Count == 0) return spots;
            var random = new Random(unchecked(seed * 486187739 ^ floor * 92821 ^ (int)theme * 7919));
            bool peaceful = type == FloorType.Rest || type == FloorType.Shop;
            int count = peaceful ? PeacefulCaches : MinCaches + random.Next(MaxCaches - MinCaches + 1);
            float right = layout.ExitX - (branch ? BranchClear : ExitClear), stretch = (right - EntryClearX) / count;
            for (int k = 0; k < count; k++)
                for (int attempt = 0; attempt < 48; attempt++)
                {
                    // A crowded stretch hands its cache to the whole floor after a few tries.
                    float lo = attempt < 32 ? EntryClearX + k * stretch : EntryClearX, hi = attempt < 32 ? lo + stretch : right;
                    var platforms = new List<int>();
                    for (int i = 0; i < layout.Platforms.Count; i++)
                    {
                        var s = layout.Platforms[i].Surface;
                        if (s.Width >= EdgeMargin * 2 + .8f && s.Right - EdgeMargin > lo && s.Left + EdgeMargin < hi) platforms.Add(i);
                    }
                    int support = platforms.Count > 0 && random.NextDouble() < .35 ? platforms[random.Next(platforms.Count)] : -1;
                    var surface = support < 0 ? layout.Ground : layout.Platforms[support].Surface;
                    float min = Math.Max(lo, support < 0 ? lo : surface.Left + EdgeMargin), max = Math.Min(hi, support < 0 ? hi : surface.Right - EdgeMargin);
                    if (max <= min) continue;
                    float x = min + (float)random.NextDouble() * (max - min);
                    if (peaceful && Math.Abs(x - TowerTerrainGenerator.FeatureX) < FeatureClear || Trapped(layout, x, keepClear)) continue;
                    if (Math.Abs(surface.HeightAt(x - .45f) - surface.HeightAt(x + .45f)) > MaxTilt) continue;
                    float y = surface.HeightAt(x);
                    if (Blocked(layout, support, x, y)) continue;
                    bool crowded = false;
                    foreach (var other in spots) if (Math.Abs(other.X - x) < MinSpacing && Math.Abs(other.Y - y) < Headroom) crowded = true;
                    if (crowded) continue;
                    spots.Add(new TowerCacheSpot { X = x, Y = y, Support = support, Definition = kinds[random.Next(kinds.Count)] });
                    break;
                }
            return spots;
        }
        private static bool Trapped(TowerFloorLayout layout, float x, IList<float> keepClear)
        {
            foreach (var hazard in layout.Hazards) if (x > hazard.Left - HazardClear && x < hazard.Right + HazardClear) return true;
            if (keepClear != null) foreach (float spot in keepClear) if (Math.Abs(x - spot) < HazardClear) return true;
            return false;
        }
        // A platform hanging just above the spot would cut through the prop.
        private static bool Blocked(TowerFloorLayout layout, int support, float x, float y)
        {
            for (int i = 0; i < layout.Platforms.Count; i++)
            {
                if (i == support) continue;
                var s = layout.Platforms[i].Surface;
                if (s.Contains(x, .55f) && s.MaxHeight > y && s.MinHeight - y < Headroom) return true;
            }
            return false;
        }
    }
}
