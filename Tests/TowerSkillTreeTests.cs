using System;
using System.Collections.Generic;
using PrototypeLab.Roguelike;

// The village skill tree: its layout, points per floor, learning rules, the bonuses heroes carry and its saves.
public static class TowerSkillTreeTests
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static bool Near(float a, float b) { return Math.Abs(a - b) < .001f; }
    // Takes every companion pick the expedition opens with.
    private static void Draft(TowerRun run) { while (run.IsRewardPending && run.IsRecruitOffer) Require(run.ChooseOffer(run.Offers[0].Id), "draft pick"); }
    private static TowerCampaignSave Rich(int points) { var save = new TowerCampaignSave(); save.AddSkillPoints(points); return save; }
    // Learns every rank on the way from the root to the node, then the node's ranks.
    private static void LearnPath(TowerCampaignSave save, string id, int ranks = 1)
    {
        var path = new List<SkillNode>();
        for (var node = TowerSkillTree.Get(id); node != null; node = TowerSkillTree.Get(node.ParentId)) path.Insert(0, node);
        foreach (var node in path) if (save.SkillTree.Rank(node.Id) == 0) Require(save.LearnSkill(node.Id), "learn " + node.Id);
        while (save.SkillTree.Rank(id) < ranks) Require(save.LearnSkill(id), "rank up " + id);
    }

    public static void Run(Action<string, Action> test)
    {
        test("the skill tree grows from one root on a grid with short straight links and no crossings", () => {
            var nodes = TowerSkillTree.Nodes; var ids = new HashSet<string>(); var cells = new Dictionary<int, SkillNode>();
            Require(nodes.Count >= 80 && nodes[0].IsRoot && Object.ReferenceEquals(TowerSkillTree.Root, nodes[0]), "a large tree with the root first: " + nodes.Count);
            foreach (var node in nodes)
            {
                Require(ids.Add(node.Id) && !String.IsNullOrEmpty(node.Name) && node.Name.Length <= 8, "unique id and short name: " + node.Id);
                Require(node.Column >= 0 && node.Column < TowerSkillTree.Columns && node.Row >= 0 && node.Row < TowerSkillTree.Rows, "inside the grid: " + node.Id);
                Require(!cells.ContainsKey(node.Row * 100 + node.Column), "one node per cell: " + node.Id);
                cells[node.Row * 100 + node.Column] = node;
                Require(node.MaxRank >= 1 && node.MaxRank <= 3 && node.Cost >= 1 && node.Value > 0, "ranks, cost and value: " + node.Id);
                Require(node.Effect(1).Length > 0 && node.Effect(node.MaxRank) != node.Effect(1) || node.MaxRank == 1, "effect text grows with rank: " + node.Id);
                if (node.IsRoot) { Require(Object.ReferenceEquals(node, nodes[0]), "a single root"); continue; }
                var parent = TowerSkillTree.Get(node.ParentId);
                Require(parent != null && ids.Contains(parent.Id), "parents come before their children: " + node.Id);
                // Links join neighbouring cells only, so no link runs through another node.
                Require(Math.Max(Math.Abs(parent.Column - node.Column), Math.Abs(parent.Row - node.Row)) == 1, "a short link: " + node.Id);
                Require(node.Branch == parent.Branch || parent.Branch == SkillBranch.Origin || node.Branch == SkillBranch.Origin || node.Branch == SkillBranch.Fortune || parent.Branch == SkillBranch.Fortune, "branches keep their colour: " + node.Id);
            }
            // Two diagonal links across the same square would cross.
            foreach (var node in nodes)
            {
                var parent = TowerSkillTree.Get(node.ParentId); if (parent == null || parent.Column == node.Column || parent.Row == node.Row) continue;
                SkillNode a, b;
                if (cells.TryGetValue(parent.Row * 100 + node.Column, out a) && cells.TryGetValue(node.Row * 100 + parent.Column, out b))
                    Require(a.ParentId != b.Id && b.ParentId != a.Id, "crossing links at " + node.Id);
            }
            foreach (SkillBranch branch in Enum.GetValues(typeof(SkillBranch)))
            { int count = 0; foreach (var node in nodes) if (node.Branch == branch) count++; Require(count >= 3, "every branch has nodes: " + branch); }
            int keystones = 0; foreach (var node in nodes) if (node.IsKeystone) keystones++;
            Require(keystones >= 8, "keystones cap the branches: " + keystones);
            Require(TowerSkillTree.TotalCost >= 600 && TowerSkillTree.TotalCost <= 800, "the whole tree costs several campaigns' worth of points: " + TowerSkillTree.TotalCost);
            // The third crossroads opens the lower half of the tree, and its ranks cost more than those above it.
            int upper = 0, lower = 0, upperCost = 0, lowerCost = 0;
            foreach (var node in nodes)
                if (node.Row > 12) { lower++; lowerCost += node.Cost; } else { upper++; upperCost += node.Cost; }
            Require(lower >= 50 && nodes[0].Row == 0 && TowerSkillTree.Get("cross_heaven").Row == 13, "a deep lower tier: " + lower);
            Require(lowerCost / (float)lower > upperCost / (float)upper, "deeper ranks cost more");
            foreach (var node in nodes) if (node.Branch == SkillBranch.Heaven || node.Branch == SkillBranch.Underworld) Require(node.Row >= 13, "heaven and the underworld lie below: " + node.Id);
        });
        test("cleared floors pay one point, boss and elite floors two, and the scholar perk adds to boss floors", () => {
            Require(TowerSkillTree.PointsFor(1, FloorType.Normal) == 1 && TowerSkillTree.PointsFor(4, FloorType.Normal) == 1, "ordinary floors");
            Require(TowerSkillTree.PointsFor(5, FloorType.Normal) == 2 && TowerSkillTree.PointsFor(90, FloorType.Normal) == 2, "boss floors");
            Require(TowerSkillTree.PointsFor(6, FloorType.Elite) == 2 && TowerSkillTree.PointsFor(6, FloorType.Rest) == 1 && TowerSkillTree.PointsFor(6, FloorType.Shop) == 1, "elite, rest and shop floors");
            var save = Rich(500); LearnPath(save, "ft_scholar");
            Require(TowerSkillTree.PointsFor(10, FloorType.Normal, save.SkillTree) == 3 && TowerSkillTree.PointsFor(9, FloorType.Normal, save.SkillTree) == 1, "the scholar's boss-floor point");
            int total = 0; for (int floor = 1; floor <= 10; floor++) total += TowerSkillTree.PointsFor(floor, FloorType.Normal);
            Require(total == 12, "ten floors pay twelve points");
        });
        test("nodes open from the root, cost their points per rank and stop at their last rank", () => {
            var save = Rich(3); var seed = TowerSkillTree.Get("arc_seed");
            Require(!save.CanLearn(seed) && !save.LearnSkill("arc_seed") && save.SkillPoints == 3, "a node waits for its parent");
            Require(save.LearnSkill("origin") && save.SkillPoints == 2 && save.SkillTree.Rank("origin") == 1, "the root is always open");
            Require(!save.LearnSkill("origin") && save.SkillPoints == 2, "a single-rank node is full");
            Require(save.LearnSkill("arc_seed") && save.LearnSkill("arc_seed") && save.SkillPoints == 0, "ranks cost points");
            Require(!save.LearnSkill("arc_seed") && save.SkillTree.Rank("arc_seed") == 2, "no rank without points");
            save.AddSkillPoints(10);
            Require(save.LearnSkill("arc_seed") && !save.LearnSkill("arc_seed") && save.SkillTree.IsMaxed(seed), "three ranks at most");
            Require(!save.LearnSkill("nonsense") && !save.LearnSkill(null), "unknown nodes are refused");
            var core = TowerSkillTree.Get("arc_core");
            Require(core.Cost == 2 && save.LearnSkill("arc_core") && save.SkillPoints == 7, "costlier nodes take more points");
            Require(save.SkillTree.Spent == 1 + 3 + 2 && save.EarnedPoints == 13 && save.SkillTree.LearnedRanks == 5, "spent points add up");
            Require(save.ResetSkillTree() == 6 && save.SkillPoints == 13 && save.SkillTree.Spent == 0 && save.SkillTree.Rank("arc_seed") == 0, "a reset refunds every point");
            Require(save.AddSkillPoints(0) == 0 && save.AddSkillPoints(-3) == 0 && save.SkillPoints == 13, "only positive points are banked");
        });
        test("player nodes raise the hero, companion nodes the companions and party nodes everyone, from the first floor", () => {
            var save = Rich(500); var plain = new TowerCampaignSave().BeginChapter(3); Draft(plain);
            LearnPath(save, "mt_blade", 3);          // origin 5% + crossroads 0% + blade 9% damage for the hero
            LearnPath(save, "bd_call", 2);           // companions +8% damage
            LearnPath(save, "bd_pantheon");          // everyone +6% damage
            LearnPath(save, "gd_skin", 2);           // hero armour +4
            var run = save.BeginChapter(3); Draft(run);
            Require(run.Player.Level == 1 && run.Player.Traits.Count == 0, "the tree is not an augment");
            // Damage scales its base by one plus every bonus, so the tree's share adds to the plain multiplier.
            float hero = .05f + .09f + .06f + save.SkillTree.Rank("cross_fate") * .04f;
            float expected = plain.Player.Damage / plain.Player.DamageMultiplier * (plain.Player.DamageMultiplier + hero);
            Require(Near(run.Player.Damage, expected), "hero damage " + run.Player.Damage + " vs " + expected);
            Require(Near(run.Player.Armor, plain.Player.Armor + 4), "hero armour");
            var ally = run.Companions[0]; var bare = plain.Companions[0];
            float allies = .08f + .05f * save.SkillTree.Rank("bd_wrath") + .06f;
            expected = bare.Damage / bare.DamageMultiplier * (bare.DamageMultiplier + allies);
            Require(ally.Id == bare.Id, "the same first pick");
            Require(Near(ally.Damage, expected), "companion damage " + ally.Damage + " vs " + expected);
            Require(Near(ally.Armor, bare.Armor + 2 * save.SkillTree.Rank("bd_ward")), "the hero's armour stays with the hero");
            Require(Near(save.SkillTree.Bonus(run.Player, HeroStat.Damage), hero) && Near(save.SkillTree.Bonus(ally, HeroStat.Damage), allies), "bonus by target");
            // Learning in the village shows at once in the preview that reads the same tree.
            var preview = save.Preview(1, new string[0]); float before = preview.Player.MaxHealth;
            LearnPath(save, "gd_heart", 1);
            Require(preview.Player.MaxHealth > before && Near(preview.Player.MaxHealth, before * (1 + .05f * save.SkillTree.Rank("cross_oath") + .04f) / (1 + .05f * save.SkillTree.Rank("cross_oath"))), "the preview follows the tree");
            var trial = new TowerRun(4, 10, false, "michael");
            Require(trial.SkillTree == null && trial.OpeningAugments == 0, "the oath trial ignores the tree");
        });
        test("foresight deals its stat cards after the companion picks and before the first fight", () => {
            var save = Rich(500); LearnPath(save, "ft_foresight", 2);
            Require(Near(save.SkillTree.Perk(SkillPerk.OpeningAugment), 2), "two ranks of foresight");
            var run = save.BeginChapter(11);
            Require(run.OpeningAugments == 2 && run.IsRecruitOffer && !run.DealOpeningAugment(), "the picks come first");
            Draft(run);
            Require(run.DealOpeningAugment() && run.IsRewardPending && !run.IsRecruitOffer && run.Draft == RewardDraft.Stats && run.OpeningAugments == 1, "the first stat cards");
            foreach (var offer in run.Offers) Require(offer.Kind == RewardKind.Trait, "foresight deals stats only");
            Require(!run.DealOpeningAugment() && !run.ClearFloor(), "one set at a time, before the fight");
            Require(run.ChooseOffer(run.Offers[0].Id) && run.DealOpeningAugment() && run.ChooseOffer(run.Offers[0].Id), "the second set");
            Require(run.OpeningAugments == 0 && !run.DealOpeningAugment() && run.Player.Level + run.Companions[0].Level == 4 && run.ClearFloor(), "two augments, then the fight");
            var late = save.BeginChapter(12); Draft(late);
            Require(late.ClearFloor() && (!late.IsRewardPending || late.ChooseOffer(late.Offers[0].Id)) && !late.DealOpeningAugment(), "foresight lapses once the first floor is cleared");
        });
        test("the tree's perks sum by rank and its summary lists every learned effect once", () => {
            var save = Rich(500);
            LearnPath(save, "ft_treasure", 2);       // gold find on the way and 30% more, foresight, scholar
            LearnPath(save, "ft_banquet", 2);        // clear heal 15 + 16, heart heal 60%, regeneration .9
            var tree = save.SkillTree;
            Require(Near(tree.Perk(SkillPerk.GoldFind), .1f * tree.Rank("ft_coin") + .1f * tree.Rank("ft_bounty") + .3f), "gold find " + tree.Perk(SkillPerk.GoldFind));
            Require(Near(tree.Perk(SkillPerk.ClearHeal), 5 * tree.Rank("ft_feast") + 16), "clear heal " + tree.Perk(SkillPerk.ClearHeal));
            Require(Near(tree.Perk(SkillPerk.HeartHeal), .2f * tree.Rank("ft_harvest")) && Near(tree.Perk(SkillPerk.BossPoints), 1), "heart heal and boss points");
            var summary = tree.Summary(); var seen = new HashSet<string>();
            foreach (string line in summary) Require(line.Length > 0 && seen.Add(line.Split('+')[0]), "one summary line per kind: " + line);
            Require(new List<string>(summary).Exists(l => l.StartsWith("골드 획득 +")) && new List<string>(summary).Exists(l => l.StartsWith("공격력 +")), "perks and stats in the summary");
            Require(new TowerSkillTreeState().Summary().Count == 0, "an empty tree lists nothing");
        });
        test("tree saves round-trip and drop unknown, excess or orphaned ranks", () => {
            var save = Rich(500); LearnPath(save, "sw_gale"); LearnPath(save, "arc_volley");
            var text = save.SkillTree.Save(); var loaded = TowerSkillTreeState.Load(text);
            Require(loaded.Save() == text && loaded.Spent == save.SkillTree.Spent && Near(loaded.Bonus(new TowerRun(1).Player, HeroStat.AttackSpeed), save.SkillTree.Bonus(new TowerRun(1).Player, HeroStat.AttackSpeed)), "round trip " + text);
            var odd = TowerSkillTreeState.Load("origin=1,arc_seed=9,ghost=2,arc_core=0,sw_step=2,bd_call=x,=,origin");
            Require(odd.Rank("arc_seed") == 3 && odd.Rank("ghost") == 0 && odd.Rank("arc_core") == 0, "unknown and excess ranks");
            Require(odd.Rank("sw_step") == 0 && odd.Save() == "arc_seed=3,origin=1", "a node cut off from the root falls away: " + odd.Save());
            Require(TowerSkillTreeState.Load(null).Spent == 0 && TowerSkillTreeState.Load("").Save() == "", "empty saves");
        });
    }
}
