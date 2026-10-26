using System;
using System.Collections.Generic;
using System.Globalization;
using PrototypeLab.Roguelike;

public static class TowerLootTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static float Mean(int seed, TowerCacheDefinition cache, int floor, int rolls, out float heartRate, out float goldCoinShare)
    {
        var random = new Random(seed); long gold = 0, hearts = 0, coins = 0, large = 0;
        for (int i = 0; i < rolls; i++)
        {
            var drops = TowerLoot.Roll(random, cache, floor); int heartsHere = 0, coinsHere = 0;
            foreach (var drop in drops)
            {
                gold += drop.Gold;
                if (drop.Kind == LootKind.Heart) heartsHere++; else { coinsHere++; if (drop.Kind == LootKind.GoldCoin) large++; }
            }
            Check(heartsHere <= 1 && coinsHere >= 1, "a cache always scatters coins and at most one heart");
            hearts += heartsHere; coins += coinsHere;
        }
        heartRate = hearts / (float)rolls; goldCoinShare = large / (float)coins; return gold / (float)rolls;
    }

    public static void Register(Action<string, Action> test)
    {
        test("every tower theme has a fragile vessel and a sturdy container; the village has none", () => {
            var ids = new HashSet<string>();
            foreach (var theme in TowerThemeCatalog.All)
            {
                var caches = TowerLoot.ForTheme(theme.Id);
                Check(caches.Count == 2 && caches[0].Hits + caches[1].Hits == 3 && caches[0].Hits != caches[1].Hits, "one fragile and one sturdy cache: " + theme.Id);
                foreach (var cache in caches)
                {
                    int hex;
                    Check(cache.Theme == theme.Id && ids.Add(cache.Id) && cache.Id.StartsWith(theme.Id.ToString().ToLowerInvariant() + "-"), "unique themed painting id: " + cache.Id);
                    Check(!String.IsNullOrEmpty(cache.Name) && cache.ChipHex.Length == 6 && Int32.TryParse(cache.ChipHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hex), "named cache with a chip colour: " + cache.Id);
                }
            }
            Check(TowerLoot.All.Count == 18 && ids.Count == 18 && TowerLoot.ForTheme(TowerThemeId.Village).Count == 0, "nine themes, two caches each, none in the village");
        });
        test("broken caches scatter coins every time, a heart about three times in ten and more gold deeper in the tower", () => {
            Check(TowerLoot.GoldOf(LootKind.Coin) == 1 && TowerLoot.GoldOf(LootKind.GoldCoin) == 5 && TowerLoot.GoldOf(LootKind.Heart) == 0, "coin values");
            Check(Math.Abs(TowerLoot.HeartHeal(160) - 19.2f) < .001f && TowerLoot.HeartHeal(0) >= 1, "a heart heals twelve percent of max health");
            foreach (var theme in TowerThemeCatalog.All)
                foreach (var cache in TowerLoot.ForTheme(theme.Id))
                {
                    float hearts, share, shallow = Mean(11, cache, 1, 6000, out hearts, out share), deep = Mean(12, cache, 61, 6000, out _, out _);
                    Check(hearts > .27f && hearts < .33f, "heart rate near thirty percent: " + cache.Id + " " + hearts);
                    Check(share > .08f && share < .12f, "one coin in ten is a five-gold coin: " + cache.Id + " " + share);
                    Check(shallow > (cache.Sturdy ? 5.5f : 4.2f) && shallow < (cache.Sturdy ? 7.5f : 6.2f), "first-floor gold per cache: " + cache.Id + " " + shallow);
                    Check(deep > shallow + 2.2f, "deeper floors pay more: " + cache.Id);
                }
            var a = TowerLoot.Roll(new Random(5), TowerLoot.All[0], 3); var b = TowerLoot.Roll(new Random(5), TowerLoot.All[0], 3);
            Check(a.Count == b.Count && a.TrueForAll(d => b[a.IndexOf(d)].Kind == d.Kind), "rolls are seeded");
            // About fifteen gold a floor: five battle floors before the branch can afford a card, but not every ware.
            float perFloor = (TowerLoot.MinCaches + TowerLoot.MaxCaches) * .5f * (Mean(13, TowerLoot.All[0], 1, 4000, out _, out _) + Mean(14, TowerLoot.All[1], 1, 4000, out _, out _)) * .5f;
            Check(perFloor * 5 >= TowerRun.AugmentPrice && perFloor * 5 < TowerRun.AugmentPrice + TowerRun.VitalityPrice + TowerRun.HealPrice, "five floors of caches pay for one card: " + perFloor);
        });
        test("caches stand on level ground or platforms spread along the floor, away from the entry, exits, merchant and traps", () => {
            int placed = 0, raised = 0, spreadFloors = 0; float spread = 0;
            var extra = new List<float> { 2, 12 };
            for (int seed = 1; seed <= 12; seed++)
                for (int floor = 1; floor <= 90; floor += 7)
                {
                    var layout = TowerTerrainGenerator.Generate(seed, floor); var theme = TowerThemeCatalog.ForFloor(floor).Id; float exit = layout.ExitX;
                    foreach (var type in new[] { FloorType.Normal, FloorType.Elite, FloorType.Rest, FloorType.Shop })
                        foreach (bool branch in new[] { false, true })
                        {
                            var spots = TowerLoot.Place(layout, theme, seed, floor, type, branch, extra);
                            bool peaceful = type == FloorType.Rest || type == FloorType.Shop;
                            Check(peaceful ? spots.Count == TowerLoot.PeacefulCaches : spots.Count >= TowerLoot.MinCaches && spots.Count <= TowerLoot.MaxCaches, "cache count " + seed + "/" + floor + "/" + type + ": " + spots.Count);
                            var again = TowerLoot.Place(layout, theme, seed, floor, type, branch, extra);
                            float right = exit - (branch ? TowerLoot.BranchClear : TowerLoot.ExitClear), least = float.MaxValue, most = float.MinValue;
                            for (int i = 0; i < spots.Count; i++)
                            {
                                var s = spots[i]; placed++; if (s.Support >= 0) raised++; least = Math.Min(least, s.X); most = Math.Max(most, s.X);
                                Check(again[i].X == s.X && again[i].Y == s.Y && again[i].Definition == s.Definition, "placement is seeded");
                                Check(s.Definition.Theme == theme, "the floor's own theme");
                                Check(s.X >= TowerLoot.EntryClearX && s.X <= right, "clear of the entry and exits: " + s.X);
                                Check(!peaceful || Math.Abs(s.X - TowerTerrainGenerator.FeatureX) >= TowerLoot.FeatureClear, "clear of the merchant and campfire");
                                foreach (float spot in extra) Check(Math.Abs(s.X - spot) >= TowerLoot.HazardClear, "clear of positions the caller keeps clear");
                                foreach (var hazard in layout.Hazards) Check(s.X <= hazard.Left - TowerLoot.HazardClear || s.X >= hazard.Right + TowerLoot.HazardClear, "clear of the floor's traps: " + hazard.Kind + " at " + hazard.X);
                                Check(Math.Abs(s.Y - layout.SurfaceHeight(s.Support, s.X)) < .0001f, "standing on its support");
                                var surface = s.Support < 0 ? layout.Ground : layout.Platforms[s.Support].Surface;
                                Check(Math.Abs(surface.HeightAt(s.X - .45f) - surface.HeightAt(s.X + .45f)) <= TowerLoot.MaxTilt + .0001f, "a nearly level footing");
                                for (int j = 0; j < i; j++)
                                    Check(Math.Abs(spots[j].X - s.X) >= TowerLoot.MinSpacing || Math.Abs(spots[j].Y - s.Y) >= TowerLoot.Headroom, "caches keep apart");
                            }
                            if (!peaceful) { spread += (most - least) / (right - TowerLoot.EntryClearX); spreadFloors++; }
                        }
                }
            Check(raised > placed / 10 && raised < placed * 6 / 10, "some caches wait on platforms: " + raised + " of " + placed);
            Check(spread / spreadFloors > .45f, "caches spread along the floor instead of bunching: " + spread / spreadFloors);
            Check(TowerLoot.Place(null, TowerThemeId.Olympus, 1, 1, FloorType.Normal, false).Count == 0, "no layout, no caches");
            Check(TowerLoot.Place(TowerTerrainGenerator.Generate(1, 1), TowerThemeId.Village, 1, 1, FloorType.Normal, false).Count == 0, "the village has no caches");
        });
    }
}
