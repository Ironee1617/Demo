using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using PrototypeLab.Aquarium;
using UnityEditor;
using UnityEngine;

// R76: Aquarium/밸런스 조정 툴. Every public static number in AquariumModel (speeds, cooldowns, durations, ranges,
// scores) can be changed here. During play the change applies at once (the host's rules read these fields);
// 저장 writes Resources/AquariumBalance.txt, which the game applies at start (AquariumBalance.ApplySaved), builds too.
[InitializeOnLoad]
public sealed class AquariumBalanceWindow : EditorWindow
{
    private const string SavePath = "Assets/Demos/HideAndSeek/Environment/Resources/AquariumBalance.txt";
    private static readonly Dictionary<string, float> defaults = new Dictionary<string, float>();

    // The code's own values, taken when the editor loads the scripts (before the game applies the saved file).
    static AquariumBalanceWindow() { defaults.Clear(); foreach (var f in AllFields()) defaults[f.Name] = Get(f); }

    private sealed class Group { public string title; public string[][] items; public bool open = true; }
    private static readonly Group[] groups =
    {
        new Group { title = "경기", items = new[] {
            new[] { "HideSeconds", "숨는 시간 (초)" }, new[] { "RoundSeconds", "사냥 시간 (초)" },
            new[] { "DiverAbilityDelay", "잠수부 스킬 해금 (사냥 시작 후 초)" }, new[] { "FishCount", "물고기 수" } } },
        new Group { title = "잠수부 공통", items = new[] {
            new[] { "SeekerSpeed", "이동 속도" }, new[] { "MaxStamina", "스태미나 최대" },
            new[] { "HarpoonCooldown", "작살 쿨타임 (초)" }, new[] { "HarpoonReelSeconds", "작살 회수 시간 (초)" },
            new[] { "HarpoonSpeed", "작살 기본 속도" }, new[] { "DiverSprintBoost", "질주 가속 (0.6 = +60%)" },
            new[] { "HarpoonHitScale", "작살 피격 범위 배율" },
            new[] { "FeedsPerGame", "먹이 횟수" }, new[] { "FeedRadius", "먹이 반경 (m)" }, new[] { "FeedDuration", "먹이 지속 (초)" } } },
        new Group { title = "사육사 잠수부", items = new[] { new[] { "KeeperHarpoonRange", "작살 사거리 (m)" }, new[] { "KeeperHarpoonSpeed", "작살 속도 배율" }, new[] { "GlueUses", "접착제 횟수" } } },
        new Group { title = "추적 잠수부", items = new[] { new[] { "TrackerHarpoonRange", "작살 사거리 (m)" }, new[] { "TrackerHarpoonSpeed", "작살 속도 배율" }, new[] { "TrackerStamina", "스태미나 최대" }, new[] { "TrackerSprintBoost", "질주 가속 (0.76 = +76%)" }, new[] { "XraySeconds", "탐지 지속 (초)" }, new[] { "XrayCooldown", "탐지 쿨타임 (초)" } } },
        new Group { title = "드론 잠수부", items = new[] {
            new[] { "DroneHarpoonRange", "작살 사거리 (m)" }, new[] { "DroneHarpoonSpeed", "작살 속도 배율" }, new[] { "DroneSpeed", "드론 속도" }, new[] { "DartCooldown", "마취침 쿨타임 (초)" },
            new[] { "DartStun", "마취 시간 (초)" }, new[] { "DartRange", "마취침 사거리 (m)" } } },
        new Group { title = "외계인 공통", items = new[] {
            new[] { "HiderSpeed", "이동 속도" }, new[] { "AlienHits", "작살 몇 번에 잡힘" }, new[] { "MorphCooldown", "위장 쿨타임 (초)" }, new[] { "MorphReach", "위장 대상 거리 (m)" },
            new[] { "EatCooldown", "포식 쿨타임 (초)" }, new[] { "EatCooldownLate", "포식 쿨타임, 막판 (초)" },
            new[] { "EatLateSeconds", "막판 기준 (남은 초)" }, new[] { "EatStun", "포식 경직 (초)" },
            new[] { "InjurySeconds", "부상 시간 (초)" }, new[] { "EggFoodRequired", "산란에 필요한 포식" },
            new[] { "HatchSeconds", "알 부화 (동료 사망 후 초)" }, new[] { "AlienAbilityCooldown", "고유 능력 쿨타임 (초)" },
            new[] { "ScrewSeconds", "나사 풀기 (초)" }, new[] { "LidSeconds", "뚜껑 치우기 (초)" }, new[] { "SqueezeSeconds", "배수구 탈출 (초)" } } },
        new Group { title = "초록 외계인", items = new[] {
            new[] { "GreenStamina", "스태미나 최대" }, new[] { "GreenHits", "작살 몇 번에 잡힘" } } },
        new Group { title = "보라 외계인", items = new[] {
            new[] { "InkBombSeconds", "먹물 지속 (초)" }, new[] { "InkBombRadius", "먹물 반경 (m)" }, new[] { "PurpleHatchSeconds", "알 부화 (초)" } } },
        new Group { title = "파랑 외계인", items = new[] { new[] { "InvisibleSeconds", "투명화 지속 (초)" } } },
        new Group { title = "노랑 외계인", items = new[] { new[] { "ShockRadius", "전기 반경 (m)" }, new[] { "ShockStun", "전기 경직 (초)" } } },
        new Group { title = "빨강 외계인", items = new[] {
            new[] { "DashDistance", "돌진 거리 (m)" }, new[] { "DashSeconds", "돌진 시간 (초)" }, new[] { "DashCooldown", "돌진 쿨타임 (초)" }, new[] { "RedStunScale", "경직 배율 (0.7 = 30% 감소)" } } },
    };
    private bool othersOpen;
    private string search = "";
    private Vector2 scroll;
    private string status = "";

    [MenuItem("Aquarium/밸런스 조정 툴")]
    private static void Open() { GetWindow<AquariumBalanceWindow>("밸런스 조정").minSize = new Vector2(380, 420); }

    private static IEnumerable<FieldInfo> AllFields()
    {
        foreach (var f in typeof(AquariumModel).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (!f.IsLiteral && !f.IsInitOnly && (f.FieldType == typeof(float) || f.FieldType == typeof(int))) yield return f;
    }
    private static FieldInfo Field(string name)
    {
        var f = typeof(AquariumModel).GetField(name, BindingFlags.Public | BindingFlags.Static);
        return f != null && !f.IsLiteral && !f.IsInitOnly && (f.FieldType == typeof(float) || f.FieldType == typeof(int)) ? f : null;
    }
    private static float Get(FieldInfo f) { return f.FieldType == typeof(int) ? (int)f.GetValue(null) : (float)f.GetValue(null); }
    private static void Set(FieldInfo f, float v) { if (f.FieldType == typeof(int)) f.SetValue(null, Mathf.RoundToInt(v)); else f.SetValue(null, v); }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(Application.isPlaying
            ? "플레이 중: 바꾼 값이 바로 적용됩니다 (방장 기준). 저장해야 다음 실행과 빌드에도 남아요."
            : "값을 바꾸고 저장하면 다음 플레이와 빌드에 적용됩니다.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("저장")) Save();
            if (GUILayout.Button("저장값 불러오기")) Load();
            if (GUILayout.Button("전부 기본값으로") && EditorUtility.DisplayDialog("밸런스 조정", "모든 값을 코드 기본값으로 되돌릴까요? (저장 전까지 파일은 그대로)", "되돌리기", "취소")) ResetAll();
        }
        if (status.Length > 0) EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
        search = EditorGUILayout.TextField("검색", search);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        var shown = new HashSet<string>();
        foreach (var g in groups)
        {
            var rows = new List<string[]>();
            foreach (var it in g.items) { if (Field(it[0]) != null && Match(it)) rows.Add(it); shown.Add(it[0]); }
            if (rows.Count == 0) continue;
            g.open = EditorGUILayout.Foldout(g.open, g.title, true, EditorStyles.foldoutHeader);
            if (!g.open && search.Length == 0) continue;
            EditorGUI.indentLevel++;
            foreach (var it in rows) Row(Field(it[0]), it[1]);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }
        othersOpen = EditorGUILayout.Foldout(othersOpen || search.Length > 0, "기타 전체 (영어 이름)", true, EditorStyles.foldoutHeader);
        if (othersOpen)
        {
            EditorGUI.indentLevel++;
            var rest = new List<FieldInfo>();
            foreach (var f in AllFields()) if (!shown.Contains(f.Name) && Match(new[] { f.Name, "" })) rest.Add(f);
            rest.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            foreach (var f in rest) Row(f, f.Name);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndScrollView();
    }

    private bool Match(string[] it)
    {
        if (search.Length == 0) return true;
        return it[0].IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0 || it[1].Contains(search);
    }

    private void Row(FieldInfo f, string label)
    {
        float v = Get(f), d; bool has = defaults.TryGetValue(f.Name, out d); bool changed = has && !Mathf.Approximately(v, d);
        using (new EditorGUILayout.HorizontalScope())
        {
            var style = changed ? EditorStyles.boldLabel : EditorStyles.label;
            EditorGUILayout.LabelField(new GUIContent(label, f.Name + (has ? "  (기본 " + d.ToString(CultureInfo.InvariantCulture) + ")" : "")), style, GUILayout.MinWidth(170));
            float nv = f.FieldType == typeof(int) ? EditorGUILayout.IntField(Mathf.RoundToInt(v), GUILayout.Width(80)) : EditorGUILayout.FloatField(v, GUILayout.Width(80));
            if (!Mathf.Approximately(nv, v)) Set(f, nv);
            using (new EditorGUI.DisabledScope(!changed))
                if (GUILayout.Button(changed ? "기본 " + d.ToString(CultureInfo.InvariantCulture) : "기본", EditorStyles.miniButton, GUILayout.Width(70))) Set(f, d);
        }
    }

    private void Save()
    {
        var sb = new StringBuilder("# Aquarium balance (Aquarium/밸런스 조정 툴). Only values that differ from the code are kept.\n");
        int n = 0;
        foreach (var f in AllFields())
        {
            float v = Get(f), d;
            if (defaults.TryGetValue(f.Name, out d) && Mathf.Approximately(v, d)) continue;
            sb.Append(f.Name).Append('=').Append(v.ToString(CultureInfo.InvariantCulture)).Append('\n'); n++;
        }
        System.IO.File.WriteAllText(SavePath, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(SavePath);
        status = "저장함: 기본값과 다른 값 " + n + "개 (" + System.DateTime.Now.ToString("HH:mm:ss") + ")";
    }

    private void Load()
    {
        var text = AssetDatabase.LoadAssetAtPath<TextAsset>(SavePath);
        if (text == null) { status = "저장된 파일이 없어요."; return; }
        ResetAll();
        status = "불러옴: " + AquariumBalance.Apply(text.text) + "개";
    }

    private static void ResetAll() { foreach (var f in AllFields()) { float d; if (defaults.TryGetValue(f.Name, out d)) Set(f, d); } }

    private void OnInspectorUpdate() { if (Application.isPlaying) Repaint(); }
}
