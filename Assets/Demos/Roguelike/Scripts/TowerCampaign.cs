using System;
using System.Collections.Generic;
using System.Globalization;

namespace PrototypeLab.Roguelike
{
    /// <summary>A page of story text: a short heading and its paragraph.</summary>
    public sealed class TowerStoryPage
    {
        public string Title { get; private set; }
        public string Body { get; private set; }
        internal TowerStoryPage(string title, string body) { Title = title; Body = body; }
    }

    /// <summary>
    /// One ten-floor chapter of the tower. Clearing it restores a lost mythology, after which the hero
    /// returns to the village and the next ten floors open.
    /// </summary>
    public sealed class TowerChapter
    {
        public int Index { get; private set; }
        public TowerThemeId Theme { get; private set; }
        public string Title { get; private set; }
        public string Subtitle { get; private set; }
        /// <summary>Companion mythology whose gods are offered before this chapter; null for the final ascent.</summary>
        public string MythId { get; private set; }
        /// <summary>The two gods of the mythology; exactly one of them may join for this chapter.</summary>
        public IReadOnlyList<string> Candidates { get; private set; }
        public string Intro { get; private set; }
        public string RestoredTitle { get; private set; }
        public string RestoredBody { get; private set; }
        private readonly Dictionary<string, string> lines;
        public int FirstFloor { get { return Index * TowerCampaign.FloorsPerChapter + 1; } }
        public int LastFloor { get { return (Index + 1) * TowerCampaign.FloorsPerChapter; } }
        public bool IsFinal { get { return Index == TowerCampaign.Chapters.Count - 1; } }
        public string Numeral { get { return IsFinal ? "최종장" : "제" + (Index + 1) + "장"; } }
        public string Floors { get { return FirstFloor + " – " + LastFloor + "층"; } }
        internal TowerChapter(int index, TowerThemeId theme, string title, string subtitle, string mythId, string[] candidates,
            string[] candidateLines, string intro, string restoredTitle, string restoredBody)
        {
            Index = index; Theme = theme; Title = title; Subtitle = subtitle; MythId = mythId;
            Candidates = Array.AsReadOnly(candidates); Intro = intro; RestoredTitle = restoredTitle; RestoredBody = restoredBody;
            lines = new Dictionary<string, string>();
            for (int i = 0; i < candidates.Length; i++) lines[candidates[i]] = candidateLines[i];
        }
        /// <summary>What a candidate god says when asked to join.</summary>
        public string Line(string companionId)
        { string line; return companionId != null && lines.TryGetValue(companionId, out line) ? line : ""; }
    }

    /// <summary>
    /// The story campaign. The hero is the only god of the planet Lumen; the evil god Nihilos devoured Earth,
    /// planted this tower on Lumen and imprisoned Earth's mythologies on its floors. The gods who lost their
    /// myths followed the wreckage here, and each chapter they climb with the hero restores one of them.
    /// </summary>
    public static class TowerCampaign
    {
        public const int FloorsPerChapter = 10;
        public const string Planet = "루멘", EvilGod = "니힐로스", Village = "등불 마을";

        public static readonly IReadOnlyList<TowerChapter> Chapters = Array.AsReadOnly(new[]
        {
            new TowerChapter(0, TowerThemeId.Olympus, "올림포스", "번개가 잠든 산", "olympus", new[] { "zeus", "poseidon" },
                new[] { "내 번개는 아직 식지 않았네. 그대의 적을 하늘에서 내리치겠네.", "삼지창을 되찾게 해 주게. 파도가 그대의 방패가 되리라." },
                "올림포스의 신전은 악신의 안개에 잠겼습니다. 신들의 왕좌를 되찾고 잠든 번개를 다시 깨우십시오.",
                "올림포스의 신화를 되찾았습니다", "번개가 다시 하늘을 가릅니다. 함께 오른 신이 당신의 영원한 동료가 되었습니다."),
            new TowerChapter(1, TowerThemeId.Asgard, "아스가르드", "서리에 묶인 세계수", "asgard", new[] { "odin", "thor" },
                new[] { "눈 하나를 바쳐 얻은 지혜로 길을 비추겠소.", "묠니르만 있으면 된다! 거인이든 악신이든 부숴 주지." },
                "세계수 이그드라실의 가지가 서리에 묶였습니다. 뿌리를 갉는 니드호그를 쓰러뜨리고 발할라의 노래를 되찾으십시오.",
                "아스가르드의 신화를 되찾았습니다", "세계수가 다시 숨을 쉽니다. 발할라의 함성이 루멘의 하늘에 울려 퍼집니다."),
            new TowerChapter(2, TowerThemeId.Dangun, "단군신화", "신단수 아래의 약속", "korea", new[] { "hwanung", "dangun" },
                new[] { "풍백과 우사, 운사를 다시 부르겠소. 하늘의 뜻을 함께 지킵시다.", "널리 인간을 이롭게 하라. 그 약속을 다시 세우러 갑시다." },
                "신단수 아래의 약속이 잊혔습니다. 악신에게 사로잡힌 태백산 산신을 풀어 홍익인간의 뜻을 되찾으십시오.",
                "단군신화를 되찾았습니다", "신단수에 다시 꽃이 핍니다. 널리 인간을 이롭게 하라는 뜻이 되살아났습니다."),
            new TowerChapter(3, TowerThemeId.Eden, "에덴", "닫혀 버린 낙원", "eden", new[] { "michael", "raphael" },
                new[] { "불꽃의 검이 다시 타오를 때까지 앞에 서겠습니다.", "상처는 제가 맡겠습니다. 당신은 앞만 보십시오." },
                "낙원의 문은 닫히고 선악과 나무가 독에 물들었습니다. 타락한 나무의 정령을 정화하고 낙원의 샘을 되찾으십시오.",
                "에덴의 신화를 되찾았습니다", "낙원의 샘이 다시 흐르고 천사들의 노래가 돌아왔습니다."),
            new TowerChapter(4, TowerThemeId.Veda, "베다", "멈춰 버린 윤회", "india", new[] { "shiva", "vishnu" },
                new[] { "파괴 없이는 재생도 없다. 나의 춤으로 길을 열겠다.", "세계를 지키는 것이 나의 일. 원반이 그대를 지키리라." },
                "윤회의 수레바퀴가 멈췄습니다. 강을 가로막은 브리트라를 베어 물길과 윤회를 다시 여십시오.",
                "베다의 신화를 되찾았습니다", "윤회의 수레바퀴가 다시 구르기 시작합니다. 강물이 루멘의 들판까지 흘러듭니다."),
            new TowerChapter(5, TowerThemeId.Papyrus, "파피루스", "저물지 않는 밤", "egypt", new[] { "ra", "anubis" },
                new[] { "태양선이 다시 뜨면 그대의 길도 밝아지리라.", "심장의 무게를 다는 저울은 거짓을 용서하지 않는다." },
                "태양선이 두아트의 어둠에 갇혀 긴 밤이 끝나지 않습니다. 불멸의 파라오를 잠재우고 새벽을 되찾으십시오.",
                "이집트 신화를 되찾았습니다", "태양선이 다시 떠올라 긴 밤이 끝났습니다."),
            new TowerChapter(6, TowerThemeId.Chinese, "중국신화", "불타는 천궁", "china", new[] { "sun_wukong", "erlang_shen" },
                new[] { "하하! 여의봉 한 번이면 천궁의 불도 꺼지지!", "제삼의 눈은 거짓을 꿰뚫는다. 원숭이보다 확실하게 돕지." },
                "천궁이 불타고 곤륜의 구름이 재로 변했습니다. 축융의 화신을 진정시키고 하늘의 질서를 되찾으십시오.",
                "중국 신화를 되찾았습니다", "천궁의 불길이 잦아들고 구름 위에 다시 질서가 섰습니다."),
            new TowerChapter(7, TowerThemeId.Hell, "지옥", "봉인이 깨진 지옥", "solomon", new[] { "bael", "asmodeus" },
                new[] { "첫 번째 왕으로서 명하노니, 지옥의 길을 열어라.", "악신 따위에게 지옥의 불을 빼앗길 수는 없지." },
                "솔로몬의 반지에 묶여 있던 72악마의 지옥마저 악신에게 짓밟혔습니다. 봉인이 깨진 불구덩이를 지나 타락한 우상 몰록을 쓰러뜨리십시오.",
                "솔로몬의 72악마가 이야기를 되찾았습니다", "지옥의 봉인이 다시 맺어졌습니다. 악마들마저 당신의 서약 앞에 고개를 숙입니다."),
            new TowerChapter(8, TowerThemeId.Abyss, "심연", "악신의 뱃속", null, new string[0], new string[0],
                "탑의 꼭대기는 악신의 몸속과 다름없습니다. 살과 뼈로 빚은 심연을 지나, 지구를 삼킨 악신 니힐로스와 마주하십시오. 되찾은 신들 가운데 셋을 데려갈 수 있습니다.",
                "악신 니힐로스가 쓰러졌습니다", "탑을 짓누르던 어둠이 걷히고, 삼켜졌던 지구의 빛이 별가루가 되어 루멘의 하늘로 흩어집니다.")
        });

        public static readonly IReadOnlyList<TowerStoryPage> Prologue = Array.AsReadOnly(new[]
        {
            new TowerStoryPage("행성 루멘", "별빛이 고요히 흐르는 행성 루멘. 이 세계에는 오직 하나의 신, 당신만이 있었습니다."),
            new TowerStoryPage("지구의 멸망", "어느 날 먼 하늘에서 지구가 부서지는 빛이 보였습니다. 악신 니힐로스가 지구를 삼키고, 그 땅의 신화마저 갈가리 찢어 버린 것입니다."),
            new TowerStoryPage("탑의 강림", "니힐로스는 다음 먹잇감으로 루멘을 골랐습니다. 대지에 거대한 탑을 꽂고, 지구에서 빼앗은 신화들을 층마다 가두어 썩게 하였습니다."),
            new TowerStoryPage("신화를 잃은 신들", "부서진 지구를 건너 신들이 루멘에 닿았습니다. 이름도 권능도 희미해진 그들은 자신의 신화를 되찾기 위해 탑에 오르려 합니다."),
            new TowerStoryPage("유일신의 서약", "당신은 루멘의 유일신으로서 그들과 서약을 맺습니다. 열 층마다 갇힌 신화를 하나씩 되찾고, 탑의 꼭대기에서 악신을 쓰러뜨리십시오.")
        });

        public static readonly IReadOnlyList<TowerStoryPage> Epilogue = Array.AsReadOnly(new[]
        {
            new TowerStoryPage("악신의 최후", "니힐로스가 무너지자 탑을 짓누르던 어둠이 걷혔습니다. 삼켜졌던 지구의 빛이 별가루가 되어 루멘의 하늘로 흩어집니다."),
            new TowerStoryPage("되찾은 신화", "신화를 되찾은 신들은 루멘에 새 신전을 세웠습니다. 이제 이 행성의 밤하늘에는 수많은 이야기가 함께 빛납니다."),
            new TowerStoryPage("함께 지키는 하늘", "당신은 더 이상 홀로 된 신이 아닙니다. 서약의 탑은 이제 모든 신이 함께 지키는 등대가 되었습니다.")
        });

        public static int TotalFloors { get { return Chapters.Count * FloorsPerChapter; } }
        public static TowerChapter ForFloor(int floor)
        {
            if (floor < 1) throw new ArgumentOutOfRangeException("floor");
            return Chapters[Math.Min(Chapters.Count - 1, (floor - 1) / FloorsPerChapter)];
        }
    }

    /// <summary>
    /// Persistent campaign progress: restored chapters, the gods who joined, and the growth every hero keeps
    /// between chapters. An expedition starts from this state; dying discards that expedition's gains, while
    /// clearing its last floor records them. Save() can be stored directly in PlayerPrefs.
    /// </summary>
    public sealed class TowerCampaignSave
    {
        private const string Version = "c1";
        private readonly List<string> roster = new List<string>();
        private readonly List<string> party = new List<string>();
        // Augment id -> level (skill level or trait stack); each augment names its owner.
        private readonly Dictionary<string, int> growth = new Dictionary<string, int>();
        public int ClearedChapters { get; private set; }
        public bool PrologueSeen { get; set; }
        public bool TutorialDone { get; set; }
        public bool EndingSeen { get; set; }
        public float Vitality { get; private set; }
        /// <summary>Gods who joined permanently, in the order their myths were restored.</summary>
        public IReadOnlyList<string> Roster { get { return roster.AsReadOnly(); } }
        /// <summary>The companions brought on the most recent expedition, used as the next default party.</summary>
        public IReadOnlyList<string> LastParty { get { return party.AsReadOnly(); } }
        public bool IsComplete { get { return ClearedChapters >= TowerCampaign.Chapters.Count; } }
        public TowerChapter NextChapter { get { return IsComplete ? null : TowerCampaign.Chapters[ClearedChapters]; } }
        /// <summary>The highest floor the hero may climb to now.</summary>
        public int UnlockedFloor { get { return Math.Min(TowerCampaign.TotalFloors, (ClearedChapters + 1) * TowerCampaign.FloorsPerChapter); } }
        public bool IsRestored(TowerChapter chapter) { return chapter != null && chapter.Index < ClearedChapters; }
        public int GrowthLevel(string augmentId) { int level; return growth.TryGetValue(augmentId, out level) ? level : 0; }

        /// <summary>
        /// Checks an expedition party: the chosen god must be one of the chapter's candidates, the others must
        /// have joined earlier, and no more than three companions may come.
        /// </summary>
        public bool CanDepart(TowerChapter chapter, string chosen, IList<string> companions)
        {
            if (chapter == null || chapter != NextChapter || companions == null || companions.Count > TowerRun.MaxCompanions) return false;
            bool needsChoice = chapter.Candidates.Count > 0;
            if (needsChoice && (chosen == null || !Contains(chapter.Candidates, chosen) || !companions.Contains(chosen))) return false;
            if (!needsChoice && chosen != null) return false;
            var seen = new HashSet<string>();
            foreach (string id in companions)
            {
                if (id == null || !seen.Add(id) || TowerRun.GetDefinition(id) == null) return false;
                if (id != chosen && !roster.Contains(id)) return false;
            }
            return true;
        }

        /// <summary>Builds the expedition run and replays every saved upgrade for the heroes who come along.</summary>
        public TowerRun BeginChapter(int seed, string chosen, IList<string> companions)
        {
            var chapter = NextChapter;
            if (!CanDepart(chapter, chosen, companions)) throw new InvalidOperationException("Invalid expedition party.");
            var run = TowerRun.ForChapter(seed, chapter, companions);
            if (Vitality > 0) run.Player.AddVitality(Vitality);
            foreach (var entry in growth)
            {
                var augment = TowerRun.GetAugment(entry.Key);
                var hero = augment == null ? null : run.GetHero(augment.OwnerId);
                if (hero == null) continue;
                for (int level = 0; level < entry.Value; level++) if (!hero.Apply(augment)) break;
            }
            party.Clear(); party.AddRange(companions);
            return run;
        }

        /// <summary>A party-only preview of the next expedition, shown in the village before departure.</summary>
        public TowerRun Preview(int seed, IEnumerable<string> companions)
        {
            var chapter = NextChapter ?? TowerCampaign.Chapters[TowerCampaign.Chapters.Count - 1];
            var valid = new List<string>();
            foreach (string id in companions ?? new string[0])
                if (valid.Count < TowerRun.MaxCompanions && !valid.Contains(id) && TowerRun.GetDefinition(id) != null) valid.Add(id);
            var run = TowerRun.ForChapter(seed, chapter, valid);
            if (Vitality > 0) run.Player.AddVitality(Vitality);
            foreach (var entry in growth)
            {
                var augment = TowerRun.GetAugment(entry.Key);
                var hero = augment == null ? null : run.GetHero(augment.OwnerId);
                if (hero == null) continue;
                for (int level = 0; level < entry.Value; level++) if (!hero.Apply(augment)) break;
            }
            return run;
        }

        /// <summary>
        /// Records a finished chapter: the chosen god joins for good and every hero who came keeps the upgrades
        /// gathered on the way. Returns false unless the run is the completed next chapter.
        /// </summary>
        public bool RecordChapterClear(TowerRun run, string chosen)
        {
            if (run == null || run.Chapter == null || run.Chapter != NextChapter || !run.IsComplete) return false;
            if (run.Chapter.Candidates.Count > 0 && (chosen == null || run.GetCompanion(chosen) == null || !Contains(run.Chapter.Candidates, chosen))) return false;
            Capture(run.Player);
            foreach (var companion in run.Companions) Capture(companion);
            Vitality = Math.Max(Vitality, run.Player.BonusHealth);
            if (chosen != null && !roster.Contains(chosen)) roster.Add(chosen);
            ClearedChapters++;
            return true;
        }

        private void Capture(HeroState hero)
        {
            foreach (var skill in hero.Skills) growth[skill.Definition.Id] = Math.Max(GrowthLevel(skill.Definition.Id), skill.Level);
            foreach (var trait in hero.Traits) growth[trait.Definition.Id] = Math.Max(GrowthLevel(trait.Definition.Id), trait.Level);
        }

        private static bool Contains(IReadOnlyList<string> list, string value)
        { for (int i = 0; i < list.Count; i++) if (list[i] == value) return true; return false; }

        public string Save()
        {
            var upgrades = new List<string>();
            foreach (var entry in growth) upgrades.Add(entry.Key + "=" + entry.Value.ToString(CultureInfo.InvariantCulture));
            upgrades.Sort(StringComparer.Ordinal);
            int flags = (PrologueSeen ? 1 : 0) | (TutorialDone ? 2 : 0) | (EndingSeen ? 4 : 0);
            return String.Join(";", new[] { Version, ClearedChapters.ToString(CultureInfo.InvariantCulture), flags.ToString(CultureInfo.InvariantCulture),
                String.Join(",", roster.ToArray()), String.Join(",", party.ToArray()), Vitality.ToString("R", CultureInfo.InvariantCulture),
                String.Join(",", upgrades.ToArray()) });
        }

        public static TowerCampaignSave Load(string saved)
        {
            var save = new TowerCampaignSave();
            if (String.IsNullOrEmpty(saved)) return save;
            string[] fields = saved.Split(';'); int cleared, flags; float vitality;
            if (fields.Length != 7 || fields[0] != Version) return save;
            if (!Int32.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out cleared) ||
                !Int32.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out flags) ||
                !Single.TryParse(fields[5], NumberStyles.Float, CultureInfo.InvariantCulture, out vitality) ||
                Single.IsNaN(vitality) || Single.IsInfinity(vitality)) return save;
            save.ClearedChapters = Math.Max(0, Math.Min(TowerCampaign.Chapters.Count, cleared));
            save.PrologueSeen = (flags & 1) != 0; save.TutorialDone = (flags & 2) != 0; save.EndingSeen = (flags & 4) != 0;
            save.Vitality = Math.Max(0, vitality);
            // Only gods whose chapter is restored can be in the roster.
            foreach (string id in fields[3].Split(','))
                for (int c = 0; c < save.ClearedChapters; c++)
                    if (Contains(TowerCampaign.Chapters[c].Candidates, id) && !save.roster.Contains(id)) save.roster.Add(id);
            foreach (string id in fields[4].Split(','))
                if (save.roster.Contains(id) && save.party.Count < TowerRun.MaxCompanions) save.party.Add(id);
            foreach (string entry in fields[6].Split(','))
            {
                string[] pair = entry.Split('='); int level;
                if (pair.Length != 2 || TowerRun.GetAugment(pair[0]) == null || !Int32.TryParse(pair[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out level) || level < 1) continue;
                save.growth[pair[0]] = Math.Min(level, 999);
            }
            return save;
        }
    }
}
