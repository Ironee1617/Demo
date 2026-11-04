using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R70b UI rework: textured HUD icons (Resources/AquariumHud/Hud_*.png, crisp anti-aliased glyphs drawn white
    // with a dark outline and tinted at draw time), the controls guide on the right edge of the play HUD (K shows
    // and hides it, remembered in PlayerPrefs), the waiting room's portraits with team-coloured frames
    // (Resources/AquariumHud/PortraitAlien{k}.png / PortraitDiver{k}.png, baked from the lobby's 3D stages),
    // and the species guide's trait sentences.
    public sealed partial class AquariumGame
    {
        // ---- team colours (waiting room frames, chat names) ------------------------------------------------
        private static readonly Color DiverTeamColor = new Color(1f, .66f, .24f), AlienTeamColor = new Color(.45f, .95f, .58f);
        private static Color TeamColor(Team team) { return team == Team.Seeker ? DiverTeamColor : AlienTeamColor; }

        // ---- icons ------------------------------------------------------------------------------------------
        private static readonly string[] IconFiles =
        {
            "stamina", "hunger", "fish", "egg", "score", "clock", "heart", "harpoon", "drain", "disguise", "bite", "layegg", "unmorph",
            "back", "ink", "shield", "invisible", "electric", "dash", "feed", "glue", "xray", "drone", "ping"
        };
        private Texture2D[] iconTextures;
        private bool iconsLoaded;
        private Texture2D IconTexture(HudIcon icon)
        {
            if (!iconsLoaded)
            {
                iconsLoaded = true; iconTextures = new Texture2D[IconFiles.Length];
                for (int i = 0; i < IconFiles.Length; i++) iconTextures[i] = Resources.Load<Texture2D>("AquariumHud/Hud_" + IconFiles[i]);
            }
            int index = (int)icon;
            return index >= 0 && index < iconTextures.Length ? iconTextures[index] : null;
        }
        // A tinted icon in a size x size box (top left at x, y). Falls back to the old line glyphs.
        private void Icon(float x, float y, HudIcon icon, Color color, float size)
        {
            if (Event.current.type != EventType.Repaint) return;
            var texture = IconTexture(icon);
            if (texture != null) { GUI.DrawTexture(new Rect(x, y, size, size), texture, ScaleMode.ScaleToFit, true, 0, color, 0, 0); return; }
            if ((int)icon > (int)HudIcon.Clock) return;
            var matrix = GUI.matrix;
            GUI.matrix = matrix * Matrix4x4.TRS(new Vector3(x, y, 0), Quaternion.identity, new Vector3(size / 24f, size / 24f, 1));
            Icon(0, 0, icon, color);
            GUI.matrix = matrix;
        }
        private static HudIcon AlienAbilityIcon(int kind)
        {
            switch (kind) { case 0: return HudIcon.Ink; case 1: return HudIcon.Shield; case 2: return HudIcon.Invisible; case 3: return HudIcon.Electric; default: return HudIcon.Dash; }
        }
        private static HudIcon DiverAbilityIcon(int kind) { return kind == 0 ? HudIcon.Glue : kind == 1 ? HudIcon.Xray : HudIcon.Drone; }

        // ---- portraits --------------------------------------------------------------------------------------
        private Texture2D[] alienPortraits, diverPortraits;
        private Texture2D Portrait(Swimmer p)
        {
            if (alienPortraits == null)
            {
                alienPortraits = new Texture2D[AquariumAlienKinds.Count]; diverPortraits = new Texture2D[AquariumDiverKinds.Count];
                for (int i = 0; i < alienPortraits.Length; i++) alienPortraits[i] = Resources.Load<Texture2D>("AquariumHud/PortraitAlien" + i);
                for (int i = 0; i < diverPortraits.Length; i++) diverPortraits[i] = Resources.Load<Texture2D>("AquariumHud/PortraitDiver" + i);
            }
            return p.team == Team.Seeker ? diverPortraits[AquariumDiverKinds.Clamp(p.diverKind)] : alienPortraits[AquariumAlienKinds.Clamp(p.alienKind)];
        }
        // A rounded portrait with the team's frame (diver orange, alien green).
        private void PortraitFrame(Rect rect, Swimmer p)
        {
            var team = TeamColor(p.team); float r = Mathf.Min(9, rect.height * .28f);
            Shape(rect, new Color(team.r * .25f, team.g * .25f, team.b * .25f, .95f), r);
            var picture = Portrait(p);
            if (picture != null && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), picture, ScaleMode.ScaleAndCrop, false, 0, Color.white, 0, r - 2);
            else if (picture == null) Icon(rect.x + rect.width * .2f, rect.y + rect.height * .2f, p.team == Team.Seeker ? HudIcon.Harpoon : HudIcon.Unmorph, team, rect.width * .6f);
            Shape(rect, team, r, 2.2f);
        }

        // ---- controls guide (right edge of the play HUD) ------------------------------------------------------
        private const string ControlsPref = "Aquarium.ControlsGuide";
        private bool showControls = true, controlsLoaded;
        private static readonly string[] AlienGuideKeys = { "WASD", "Space", "Ctrl", "Shift", "Q", "C", "E", "R", "F", "휠 클릭" };
        private static readonly string[] AlienGuideText = { "이동", "상승", "하강", "질주", "위장", "위장 해제", "포식, 배수구", "알 낳기", "능력", "핑 찍기" };
        private static readonly string[] DiverGuideKeys = { "WASD", "Space", "Ctrl", "Shift", "클릭", "F", "E", "R" };
        private static readonly string[] DiverGuideText = { "이동", "상승", "하강", "질주", "작살", "먹이 뿌리기", "배수구 뚜껑", "능력" };
        private const string GuideTitle = "조작키", GuideHide = "조작키 숨기기", GuideShow = "조작키", GuideKey = "K";
        private bool ControlsShown
        {
            get { if (!controlsLoaded) { controlsLoaded = true; showControls = PlayerPrefs.GetInt(ControlsPref, 1) == 1; } return showControls; }
        }
        private void ToggleControlsGuide()
        {
            showControls = !ControlsShown; PlayerPrefs.SetInt(ControlsPref, showControls ? 1 : 0); PlayerPrefs.Save();
        }
        // Called from Update: K toggles the guide while a round is on (never while typing).
        private void ControlsGuideInput()
        {
            if (!Input.GetKeyDown(KeyCode.K) || chatTyping || !session.Connected || session.State == null || session.State.phase != RoundPhase.Playing) return;
            ToggleControlsGuide();
        }
        private void ControlsGuide(Swimmer p, float width, float height)
        {
            bool alien = p.team == Team.Hider;
            if (!ControlsShown)
            {
                // a small tab on the right edge: [K] 조작키
                float tw = Measure(caption, GuideShow).x + 50; var tab = new Rect(width - 24 - tw, height / 2 - 14, tw, 28);
                Pill(tab, new Color(Rim.r, Rim.g, Rim.b, .3f));
                Key(tab.x + 8, tab.y + 3, GuideKey);
                Label(tab.x + 38, tab.y + 6, tw - 40, 16, GuideShow, caption);
                return;
            }
            var keys = alien ? AlienGuideKeys : DiverGuideKeys; var text = alien ? AlienGuideText : DiverGuideText;
            const float w = 212, rowH = 26;
            float h = 36 + keys.Length * rowH + 40, x = width - 24 - w, y = Mathf.Max(112, height / 2 - h / 2 + 10);
            Card(new Rect(x, y, w, h), new Color(Rim.r, Rim.g, Rim.b, .3f), true, 12);
            Label(x + 14, y + 10, w - 28, 16, GuideTitle, caption);
            Separator(x + 12, y + 30, w - 24);
            for (int i = 0; i < keys.Length; i++)
            {
                float ry = y + 37 + i * rowH;
                float kw = Key(x + 14, ry + 1, keys[i]);
                Label(x + 14 + Mathf.Max(kw, 62) + 10, ry, w - 96, rowH, text[i], promptText);
            }
            float fy = y + h - 34;
            Separator(x + 12, fy - 4, w - 24);
            Key(x + 14, fy + 3, GuideKey);
            Label(x + 48, fy + 3, w - 60, 22, GuideHide, Tinted(promptText, muted));
        }

        // ---- species guide texts -------------------------------------------------------------------------------
        // One or two sentences per species: how it moves, where it lives, what gives a copy away.
        private static string SpeciesTrait(AquariumSpecies s)
        {
            string key = s != null && s.normal != null ? s.normal.name : "";
            switch (key)
            {
                case "Fish0": return "특징: 5~7마리가 무리 지어 우두머리를 따라다니고, 놀라면 함께 달아납니다. 무리에 섞여 움직이면 들키기 어렵습니다.";
                case "FishBlueTang": return "특징: 혼자서 수조 여기저기를 바쁘게 돌아다니며 자주 방향을 바꿉니다. 먹이를 뿌리면 곧장 몰려듭니다.";
                case "FishClownfish": return "특징: 자기 말미잘 위를 맴돌다가 위험하면 촉수 속으로 쏙 숨어듭니다. 말미잘에서 멀리 떨어진 흰동가리는 의심받기 쉽습니다.";
                case "FishYellowTang": return "특징: 블루탱처럼 혼자 이곳저곳을 누비며 산호와 바위 사이를 자주 오갑니다. 먹이를 뿌리면 금방 몰려듭니다.";
                case "FishShark": return "특징: 절대 멈추지 않고 크고 느긋한 원을 그리며 수조를 순찰합니다. 먹이에는 관심이 없어서 먹이로 다가가는 상어는 수상합니다.";
                case "FishMantaRay": return "특징: 넓게 선회합니다. 커다란 지느러미를 펼치고 천천히 미끄러지듯 떠다니며, 먹이에는 관심이 없습니다.";
                case "FishFlatfish": return "특징: 바닥 모래 위에 납작하게 엎드려 쉬다가 가끔 짧게 미끄러지듯 자리를 옮깁니다. 높이 떠서 헤엄치는 넙치는 금방 눈에 띕니다.";
            }
            return "특징: " + HabitText(AquariumFishTraits.Habit(s != null ? s.speciesId : 0)) + ".";
        }

        // ---- ability boxes (alien and diver select) --------------------------------------------------------------
        // "F 먹물 폭탄: 설명, 패시브 번식: 설명" split once into titled parts (cached; no per-frame strings).
        private string[][] alienAbilityParts;
        private string[] AlienAbilityParts(AquariumAlienKind kind)
        {
            if (alienAbilityParts == null) alienAbilityParts = new string[AquariumAlienKinds.Count][];
            int id = AquariumAlienKinds.Clamp(kind.id);
            if (alienAbilityParts[id] != null) return alienAbilityParts[id];
            string text = kind.abilityText ?? ""; string active = "", passive = text;
            int split = text.IndexOf(", 패시브 ", System.StringComparison.Ordinal);
            if (text.StartsWith("F ", System.StringComparison.Ordinal)) { active = split >= 0 ? text.Substring(0, split) : text; passive = split >= 0 ? text.Substring(split + 2) : ""; }
            var parts = new string[4];
            SplitTitle(active, out parts[0], out parts[1]); SplitTitle(passive, out parts[2], out parts[3]);
            return alienAbilityParts[id] = parts;
        }
        private static void SplitTitle(string text, out string title, out string body)
        {
            int colon = text.IndexOf(':');
            if (colon < 0) { title = text; body = ""; return; }
            title = text.Substring(0, colon).Trim(); body = text.Substring(colon + 1).Trim();
            if (title.StartsWith("F ", System.StringComparison.Ordinal)) title = "F  " + title.Substring(2);
            else if (title.StartsWith("패시브 ", System.StringComparison.Ordinal)) title = "패시브  " + title.Substring(4);
        }
        private GUIStyle abilityBody;
        private GUIStyle AbilityBody { get { return abilityBody ?? (abilityBody = Text(15, new Color(.86f, .93f, .96f))); } }
        // One titled ability box.
        private void AbilityBox(Rect box, Color rim, string title, string text, Color titleColor)
        {
            Card(box, rim, false, 12);
            Label(box.x + 16, box.y + 12, box.width - 30, 24, title, Tinted(body, titleColor));
            Separator(box.x + 16, box.y + 40, box.width - 32);
            Label(box.x + 16, box.y + 50, box.width - 30, box.height - 58, text, AbilityBody);
        }
    }
}
