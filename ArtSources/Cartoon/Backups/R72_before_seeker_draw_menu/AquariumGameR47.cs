using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R47 UI: endings and the results table, ability slots (alien F / diver R), the diver's drain compass and
    // lid alarm, the drone camera hint, the diver picker and the fish guide in the lobby, the rules page and
    // the three practice tutorials.
    public sealed partial class AquariumGame
    {
        // Drain work (screws, lids) is heard up to this far (was 14 m).
        private const float DrainHearing = 25f;
        private int selectedDiver;
        private AquariumAlienPreview diverPreview;
        private int lobbyTab;
        // Drain compass: flash a drain's icon after a screw comes out there; the lid-open alarm banner.
        private readonly HashSet<int> r47Seen = new HashSet<int>();
        private int r47Round = -1;
        private float[] drainFlashUntil = new float[0];
        private float alarmUntil = -100;
        private Point alarmAt;
        // Tutorials: 0 none, 1 alien basics, 2 escape, 3 diver.
        private int tutorial, tutorialStep;
        private bool tutorialDone, tutorialSawMorph;
        private GUIStyle titleArt, endingSub, bigCentred;
        private AquariumSpecies[] speciesCatalog;
        private string[] tips;

        // ---- Effects the HUD reacts to -------------------------------------------------------------------
        private void TrackR47Effects()
        {
            var state = session.State;
            if (!session.Connected || state == null || state.phase != RoundPhase.Playing || state.effects == null) { r47Seen.Clear(); r47Round = -1; return; }
            if (state.round != r47Round) { r47Seen.Clear(); r47Round = state.round; alarmUntil = -100; }
            int drains = state.drains == null ? 0 : state.drains.Length;
            if (drainFlashUntil.Length != drains) drainFlashUntil = new float[drains];
            var local = Local();
            bool diverView = local != null && (local.team == Team.Seeker || session.IsSpectator || followCamera.SpectatingDiver);
            foreach (var e in state.effects)
            {
                if (!r47Seen.Add(e.id) || state.time - e.born > 1.5f) continue;
                if (e.kind == "screwOut" && drains > 0)
                {
                    int best = -1; float nearest = 4f;
                    for (int i = 0; i < drains; i++) { float d = (state.drains[i].position - e.position).Length; if (d < nearest) { nearest = d; best = i; } }
                    if (best >= 0) drainFlashUntil[best] = Time.unscaledTime + 2f;
                }
                else if (e.kind == "lidOpen" && diverView)
                {
                    alarmUntil = Time.unscaledTime + 5f; alarmAt = e.position;
                    AudioClip clip; if (sounds.TryGetValue("alarm", out clip)) audioSource.PlayOneShot(clip, .8f);
                }
            }
        }

        // ---- Lobby: previews, character panel, diver pick, fish guide, tips -------------------------------
        // The lobby's 3D rows only exist while a lobby is on screen: the alien row for aliens, the diver row for the diver.
        private void UpdateAlienPreview()
        {
            bool inLobby = session.Connected && session.State.phase == RoundPhase.Lobby && !session.IsSpectator;
            var local = inLobby ? Local() : null;
            // Keep the picks in step with the host (it clamps, and a room may have been joined with an older pick).
            if (local != null && session.IsHost && local.alienKind != selectedAlien) session.SetAlien(selectedAlien);
            if (local != null && session.IsHost && local.diverKind != selectedDiver) session.SetDiver(selectedDiver);
            bool shown = inLobby && lobbyTab == 0;
            bool diver = local != null && local.team == Team.Seeker;
            if (alienPreview != null && (!shown || diver)) alienPreview.Show(false);
            if (diverPreview != null && (!shown || !diver)) diverPreview.Show(false);
            if (!shown) return;
            if (diver)
            {
                if (diverPreview == null) diverPreview = new AquariumAlienPreview(true);
                diverPreview.Show(true); diverPreview.Select(selectedDiver, Time.unscaledTime); diverPreview.Update(Time.unscaledTime);
            }
            else
            {
                if (alienPreview == null) alienPreview = new AquariumAlienPreview();
                alienPreview.Show(true); alienPreview.Select(selectedAlien, Time.unscaledTime); alienPreview.Update(Time.unscaledTime);
            }
        }

        private void CharacterPanel(float x, float y, float width, float height, Swimmer local)
        {
            if (lobbyTab == MapTab) MapSelect(x, y, width, height);
            else if (lobbyTab == 1) FishGuide(x, y, width, height);
            else if (local != null && local.team == Team.Seeker) DiverSelect(x, y, width, height, local);
            else AlienSelect(x, y, width, height, local);
            // R70b: the character tab is named for what it picks (외계인 선택 / 잠수부 선택), and the role switch sits
            // where the fish guide tab was (the fish guide button moved to the left card; the map tab opens from the
            // map card there).
            bool diverTab = local != null && local.team == Team.Seeker;
            string pick = diverTab ? (lobbyTab == 0 ? "✓ 잠수부 선택" : "잠수부 선택") : (lobbyTab == 0 ? "✓ 외계인 선택" : "외계인 선택");
            float pickX = local != null ? x + width - 340 : x + width - 178;
            if (Button(new Rect(pickX, y + 18, 154, 32), pick, lobbyTab == 0)) lobbyTab = 0;
            if (local != null)
            {
                Team other = local.team == Team.Hider ? Team.Seeker : Team.Hider;
                if (Button(new Rect(x + width - 178, y + 18, 154, 32), other == Team.Seeker ? "잠수부로 바꾸기" : "외계인으로 바꾸기"))
                {
                    notice = ""; selectedTeam = other; lobbyTab = 0;
                    if (session.Practice) session.PracticeRole(other); else session.Role(other, false);
                }
            }
        }

        private void DiverSelect(float x, float y, float width, float height, Swimmer local)
        {
            var picked = AquariumDiverKinds.Get(selectedDiver);
            var pickedColor = new Color(picked.r, picked.g, picked.b);
            Card(new Rect(x, y, width, height), new Color(picked.r, picked.g, picked.b, .55f), true, 18);
            Label(x + 24, y + 18, 300, 34, "잠수부 선택", heading);
            Label(x + 24, y + 54, width - 48, 23, "잠수부는 한 명, 능력(R)은 사냥 시작 3분 뒤부터 쓸 수 있습니다.", small);
            float stageWidth = width - 48, stageHeight = stageWidth / AquariumAlienPreview.Aspect;
            var stage = new Rect(x + 24, y + 86, stageWidth, stageHeight);
            if (diverPreview != null && diverPreview.Texture != null) GUI.DrawTexture(stage, diverPreview.Texture, ScaleMode.StretchToFill, false);
            else Shape(stage, new Color(0.06f, 0.15f, 0.21f, .8f), 6);
            Shape(stage, Rim, 6, 1.2f);
            bool canPick = local != null && GUI.enabled;
            var e = Event.current;
            if (canPick && diverPreview != null && e.type == EventType.MouseDown && e.button == 0 && stage.Contains(e.mousePosition))
            {
                int slot = diverPreview.SlotAt((e.mousePosition.x - stage.x) / stage.width);
                if (slot >= 0) { PickDiver(slot); e.Use(); }
            }
            int count = AquariumDiverKinds.Count; const float gap = 8;
            float cardWidth = (stageWidth - gap * (count - 1)) / count, cardY = stage.yMax + 12;
            for (int slot = 0; slot < count; slot++)
            {
                var kind = AquariumDiverKinds.Get(slot);
                var color = new Color(kind.r, kind.g, kind.b);
                var card = new Rect(stage.x + slot * (cardWidth + gap), cardY, cardWidth, 50);
                bool chosen = kind.id == selectedDiver, hover = canPick && card.Contains(e.mousePosition);
                Shape(card, chosen ? new Color(0.12f, 0.3f, 0.38f, 0.85f) : hover ? new Color(0.1f, 0.25f, 0.32f, 0.8f) : new Color(0.06f, 0.17f, 0.23f, 0.7f), 10);
                Shape(card, chosen ? color : new Color(color.r, color.g, color.b, .45f), 10, chosen ? 2f : 1.2f);
                Label(card.x + 12, card.y + 13, card.width - 18, 28, (chosen ? "✓ " : "") + kind.name, chosen ? Tinted(body, color) : body);
                if (canPick && GUI.Button(card, GUIContent.none, GUIStyle.none)) PickDiver(kind.id);
            }
            // R70b: no name block (as on the alien panel); the R ability and the passive across the full width.
            float infoY = cardY + 66, boxH = height - (infoY - y) - 24, half = (stageWidth - 12) / 2;
            AbilityBox(new Rect(x + 24, infoY, half, boxH), new Color(picked.r, picked.g, picked.b, .6f), Cat("R  ", picked.activeName), picked.activeText, pickedColor);
            AbilityBox(new Rect(x + 36 + half, infoY, half, boxH), Rim, Cat("패시브  ", picked.passiveName), picked.passiveText, Color.white);
        }

        private void PickDiver(int kind)
        {
            kind = AquariumDiverKinds.Clamp(kind);
            if (kind == selectedDiver) return;
            selectedDiver = kind; PlayerPrefs.SetInt("Aquarium.Diver", kind); PlayerPrefs.Save();
            session.SetDiver(kind);
        }

        private AquariumSpecies[] SpeciesCatalog()
        {
            if (speciesCatalog != null) return speciesCatalog;
            var all = Resources.LoadAll<AquariumSpecies>("AquariumCharacters");
            Array.Sort(all, (a, b) => a.speciesId.CompareTo(b.speciesId));
            return speciesCatalog = all;
        }
        private static string SpeciesName(AquariumSpecies s)
        {
            string key = s != null && s.normal != null ? s.normal.name : "";
            switch (key)
            {
                case "Fish0": return "하늘색 물고기";
                case "FishBlueTang": return "블루탱";
                case "FishClownfish": return "흰동가리";
                case "FishYellowTang": return "옐로탱";
                case "FishShark": return "상어";
                case "FishMantaRay": return "만타가오리";
                case "FishFlatfish": return "넙치";
            }
            return "어종 " + (s != null ? s.speciesId : 0);
        }
        private static string HabitText(FishHabit habit)
        {
            switch (habit)
            {
                case FishHabit.Wander: return "혼자 이리저리";
                case FishHabit.Cruise: return "절대 멈추지 않음";
                case FishHabit.Glide: return "넓게 선회";
                case FishHabit.Bottom: return "바닥에서 쉼";
                case FishHabit.Anemone: return "말미잘 근처";
                default: return "5~7마리 떼";
            }
        }
        private static string FeedText(int species)
        { return AquariumFishTraits.IgnoresFeed(species) ? "먹이 무시 (사육사 잠수부의 먹이에는 몰려듦)" : "먹이에 몰려듦"; }

        // Character pictures for the guide, rendered offline into Resources/AquariumCharacters/Guide/Species{id}.png.
        private readonly Dictionary<int, Texture2D> guideImages = new Dictionary<int, Texture2D>();
        private Texture2D GuideImage(int species)
        {
            Texture2D image;
            if (!guideImages.TryGetValue(species, out image)) { image = Resources.Load<Texture2D>("AquariumCharacters/Guide/Species" + species); guideImages[species] = image; }
            return image;
        }

        // R70b: one row per species: picture, name and a short tag, and a sentence or two on its habits (no size or
        // speed figures any more).
        private void FishGuide(float x, float y, float width, float height)
        {
            Card(new Rect(x, y, width, height), true, 18);
            Label(x + 24, y + 18, 300, 34, "어종 도감", heading);
            Label(x + 24, y + 54, width - 48, 23, "위장한 외계인은 그 물고기처럼 헤엄칩니다. 습성을 흉내 내야 들키지 않아요!", small);
            var all = SpeciesCatalog();
            float rowY = y + 86, rowH = Mathf.Min(80, (height - 104) / Mathf.Max(1, all.Length));
            Label(x + 132, rowY - 4, 150, 18, "어종", caption); Label(x + 270, rowY - 4, 200, 18, "습성", caption);
            rowY += 16;
            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i]; int id = s.speciesId; float ry = rowY + i * rowH;
                Shape(new Rect(x + 18, ry, width - 36, rowH - 6), i % 2 == 0 ? new Color(0.08f, 0.22f, 0.29f, 0.5f) : new Color(0.06f, 0.17f, 0.23f, 0.4f), 9);
                var tint = s.tint; tint.a = 1;
                var image = GuideImage(id);
                if (image != null) GUI.DrawTexture(new Rect(x + 26, ry + 2, 98, rowH - 10), image, ScaleMode.ScaleToFit, true);
                else Shape(new Rect(x + 34, ry + (rowH - 6) / 2 - 10, 20, 20), tint, 10);
                float mid = ry + (rowH - 6) / 2;
                Label(x + 132, mid - 22, 132, 24, SpeciesName(s), body);
                Label(x + 132, mid + 2, 132, 20, AquariumFishTraits.Habit(id) == FishHabit.School ? "떼 지어 다님" : AquariumFishTraits.IgnoresFeed(id) ? "단독, 먹이 무시" : "단독",
                    AquariumFishTraits.IgnoresFeed(id) ? Tinted(small, Warm) : small);
                Label(x + 270, ry + 6, width - 300, rowH - 16, SpeciesTrait(s), GuideText);
            }
        }
        private GUIStyle guideText;
        private GUIStyle GuideText
        {
            get
            {
                if (guideText != null) return guideText;
                guideText = Text(14, new Color(.88f, .95f, .97f)); guideText.alignment = TextAnchor.MiddleLeft; return guideText;
            }
        }

        private string[] Tips()
        {
            if (tips != null) return tips;
            var list = new List<string>();
            foreach (var s in SpeciesCatalog())
                list.Add(SpeciesName(s) + ": " + HabitText(AquariumFishTraits.Habit(s.speciesId))
                    + (AquariumFishTraits.IgnoresFeed(s.speciesId) ? ", 먹이 무시" : ""));
            list.Add("외계인은 F로 고유 능력을 씁니다, 초록 외계인은 패시브뿐");
            list.Add("배수구 나사는 둘이 같이 풀면 10% 빨라집니다");
            list.Add("사육사 잠수부의 접착제가 발린 배수구는 더 이상 열 수 없습니다");
            list.Add("잠수부의 먹이는 게임당 3번, 아껴 쓰세요");
            list.Add("잠수부가 열린 뚜껑을 덮는 데는 4초가 걸립니다");
            list.Add("잠수부 능력(R)은 사냥 시작 3분 뒤에 열립니다");
            list.Add("진짜 물고기를 쏘면 잠수부 −5점, 외계인을 잡으면 +30점");
            list.Add("마지막으로 수조를 빠져나간 외계인은 보너스 +20점");
            return tips = list.ToArray();
        }
        private void TipPill(float cx, float y, float maxWidth)
        {
            var all = Tips(); if (all.Length == 0) return;
            string tip = all[(int)(Time.unscaledTime / 7f) % all.Length];
            float w = Mathf.Min(maxWidth, Measure(promptText, tip).x + 76);
            var rect = new Rect(cx - w / 2, y, w, 30);
            Pill(rect, Rim);
            Label(rect.x + 16, y + 8, 40, 16, "TIP", caption);
            GUI.Label(new Rect(rect.x + 50, y, w - 60, 30), tip, promptText);
        }

        // ---- HUD: vitals and slots ------------------------------------------------------------------------
        // R29 vitals with the R47 per-kind maxima (hunger large, stamina and lives small).
        private void AlienVitals(AquariumSnapshot state, Swimmer p, float height)
        {
            float hungerMax = AquariumModel.HungerMax(p), hunger = p.alive ? AquariumModel.HungerLeft(p, state.time) : 0;
            var hungerColor = hunger <= 15 ? Danger : Warm;
            Vital(76, height - 82, 100, 7, hunger / hungerMax, hungerColor, HudIcon.Hunger, Int(Mathf.CeilToInt(hunger)), "배고픔");
            Vital(166, height - 62, 60, 5, p.stamina / AquariumModel.StaminaMax(p), cyan, HudIcon.Stamina, Int(Mathf.RoundToInt(p.stamina)), "스태미나");
            int lives = AquariumModel.HitsToKill(p);
            LivesOf(238, height - 62, 60, 5, p.alive ? Mathf.Max(0, lives - p.harpoonHits) : 0, lives);
            var stats = new Rect(24, height - 168, 196, 30);
            Pill(stats, Rim);
            Icon(stats.x + 14, stats.y + 7, HudIcon.Fish, muted, 16); Label(stats.x + 36, stats.y + 5, 60, 22, Int(p.personalEaten), small);
            Icon(stats.x + 92, stats.y + 7, HudIcon.Score, muted, 16); Label(stats.x + 114, stats.y + 5, 76, 22, Int(p.score), small);
        }
        private void DiverVitals(AquariumSnapshot state, Swimmer p, float height)
        {
            Vital(76, height - 82, 100, 7, p.stamina / AquariumModel.StaminaMax(p), cyan, HudIcon.Stamina, Int(Mathf.RoundToInt(p.stamina)), "스태미나");
            string line = Cat("오사  ", Int(state.killed), "     점수  ", Int(p.score));
            var stats = new Rect(24, height - 168, 54 + Measure(small, line).x, 30);
            Pill(stats, Rim);
            Icon(stats.x + 14, stats.y + 7, HudIcon.Fish, muted, 16); Label(stats.x + 36, stats.y + 5, 260, 22, line, small);
            CatchPill(24, height - 208, p);
        }
        // R49: how many aliens this diver has caught so far (never the total), with a small harpoon glyph.
        private void CatchPill(float x, float y, Swimmer diver)
        {
            string text = Cat("포획  ", Int(diver.kills));
            float w = 48 + Measure(body, text).x;
            var rect = new Rect(x, y, w, 32);
            Pill(rect, new Color(cyan.r, cyan.g, cyan.b, .75f));
            Icon(rect.x + 11, rect.y + 6, HudIcon.Harpoon, cyan, 20);
            Label(rect.x + 38, rect.y + 2, w - 40, 28, text, body);
        }
        // The lives ring split into HitsToKill parts (the green alien takes three hits).
        private void LivesOf(float cx, float cy, float diameter, float thickness, int left, int total)
        {
            var rect = new Rect(cx - diameter / 2, cy - diameter / 2, diameter, diameter);
            Shape(rect, Disc, diameter / 2);
            var alive = new Color(1f, 0.45f, 0.5f); float part = 1f / Mathf.Max(1, total);
            for (int i = 0; i < total; i++) Arc(rect, thickness, i * part + .02f, part - .04f, i < left ? alive : Track);
            Icon(cx - 8, cy - 18, HudIcon.Heart, left > 0 ? alive : muted, 16);
            Label(cx - diameter / 2, cy + 1, diameter, 20, Cat(Int(left), " / ", Int(total)), vitalSmall);
        }
        // A quick slot whose lit state is given (for counters such as 2/3 that are ready).
        // R70b: the action's icon in the middle, the key badge in the corner, the state along the bottom and the
        // action's name under the slot.
        private void SlotLit(float x, float y, float size, string key, string name, string status, float recharge, bool ready, HudIcon icon)
        {
            var rect = new Rect(x, y, size, size);
            Card(rect, ready ? new Color(cyan.r, cyan.g, cyan.b, .85f) : Rim, false, 8);
            float iconSize = size * .5f;
            Icon(x + (size - iconSize) / 2, y + size * .2f, icon, ready ? cyan : new Color(muted.r, muted.g, muted.b, .85f), iconSize);
            if (recharge >= 0 && recharge < 1)
                Shape(new Rect(x + 2, y + 2, size - 4, (size - 4) * (1 - Mathf.Clamp01(recharge))), new Color(0, 0.02f, 0.04f, .5f), 6);
            float kw = Mathf.Max(15, 8 + Measure(keyTiny, key).x);
            var badge = new Rect(x + 4, y + 4, kw, 14);
            Shape(badge, new Color(0.86f, 0.97f, 1f, 0.92f), 4);
            GUI.Label(badge, key, keyTiny);
            Label(x, y + size - 17, size, 15, status, ready ? slotStateReady : slotState);
            Shape(new Rect(x + 2, y + size + 3, size - 4, 15), new Color(0, 0.03f, 0.06f, .55f), 7.5f);
            Label(x - 8, y + size + 2, size + 16, 16, name, ready ? slotTitle : slotTitleDim);
        }
        private static string ShortAbility(int kind)
        {
            switch (kind) { case 0: return "먹물"; case 1: return "패시브"; case 2: return "투명"; case 3: return "전기"; default: return "돌진"; }
        }
        private static string MinSec(float seconds) { int s = Mathf.Max(0, Mathf.CeilToInt(seconds)); return MinSecText(s); }

        private void AlienSlots(AquariumSnapshot state, Swimmer p, float actionsX, float actionsY, float slotW, float gap)
        {
            int eggFood = AquariumModel.EggFoodFor(p), eggsReady = AquariumModel.EggsReady(p);
            bool laying = p.layEndsAt > state.time, inside = p.swallowedBy != 0;
            string eggStatus = laying ? Cat("산란 ", Tenths(p.layEndsAt - state.time), "s") : inside ? "사용 불가" : p.stunnedUntil > state.time ? Ready(p.stunnedUntil, state.time)
                : eggsReady > 0 && p.species >= 0 ? "위장 중"   // R68: eggs are laid in the alien form only
                : eggsReady > 0 ? (eggsReady > 1 ? Cat("준비 ×", Int(eggsReady)) : "준비") : Cat(Int(p.personalEaten % eggFood), " / ", Int(eggFood));
            float morphAt = Mathf.Max(p.stunnedUntil, Mathf.Max(p.nextMorph, p.morphLockedUntil));
            Slot(actionsX, actionsY, slotW, "Q", "위장", laying ? "산란 중" : inside ? "사용 불가" : Ready(morphAt, state.time),
                p.nextMorph > state.time ? 1 - (p.nextMorph - state.time) / AquariumModel.MorphCooldown : -1, HudIcon.Disguise);
            // R47: aliens can eat at any time (no "full" gate).
            Slot(actionsX + (slotW + gap), actionsY, slotW, "E", "포식",
                laying ? "산란 중" : inside ? "사용 불가" : Ready(Mathf.Max(state.huntAt, Mathf.Max(p.nextAction, p.stunnedUntil)), state.time), -1, HudIcon.Bite);
            Slot(actionsX + 2 * (slotW + gap), actionsY, slotW, "R", "산란", eggStatus,
                eggsReady > 0 || laying ? -1 : (p.personalEaten % eggFood) / (float)eggFood, HudIcon.LayEgg);
            if (eggsReady > 0 && !laying && !inside && p.species >= 0)
            {
                // R68: an egg is ready but the alien is disguised: R works only in the alien form (C drops the disguise).
                const string only = "외계인 상태에서만";
                float w = Measure(slotState, only).x + 16, cx = actionsX + 2 * (slotW + gap) + slotW / 2;
                Shape(new Rect(cx - w / 2, actionsY - 22, w, 17), new Color(0, 0.02f, 0.04f, .72f), 8);
                Label(cx - w / 2, actionsY - 22, w, 17, only, slotStateReady);
            }
            Slot(actionsX + 3 * (slotW + gap), actionsY, slotW, "C", p.drainTask == DrainTask.Enter ? "그만두기" : "해제", p.drainTask == DrainTask.Enter ? "준비" : p.species < 0 ? "기본형" : inside ? "사용 불가" : "준비", -1, p.drainTask == DrainTask.Enter ? HudIcon.Back : HudIcon.Unmorph);
            float fx = actionsX + 4 * (slotW + gap);
            if (!AquariumModel.HasAlienAbility(p)) { SlotLit(fx, actionsY, slotW, "F", "패시브", "체력", -1, false, AlienAbilityIcon(p.alienKind)); return; }
            float total = AquariumModel.AlienAbilityCooldownOf(p);
            string status; float recharge = -1; bool ready = false;
            if (inside) status = "사용 불가";
            else if (p.alienKind == 2 && state.time < p.abilityUntil && AquariumModel.Invisibility(p, state.time) > 0)
            { status = Cat("투명 ", Tenths(p.abilityUntil - state.time), "s"); recharge = 1 - (p.abilityUntil - state.time) / AquariumModel.InvisibleSeconds; }
            else if (state.time < state.huntAt) status = Ready(state.huntAt, state.time);
            else if (p.nextAbility > state.time) { status = Ready(p.nextAbility, state.time); recharge = 1 - Mathf.Clamp01((p.nextAbility - state.time) / total); }
            else { status = "준비"; ready = true; }
            SlotLit(fx, actionsY, slotW, "F", ShortAbility(p.alienKind), status, recharge, ready, AlienAbilityIcon(p.alienKind));
        }

        private void DiverSlots(AquariumSnapshot state, Swimmer p, float actionsX, float actionsY, float slotW, float gap)
        {
            Slot(actionsX, actionsY, slotW, "LMB", "작살", Ready(Mathf.Max(p.nextAction, state.huntAt), state.time),
                p.nextAction > state.time && state.time >= state.huntAt ? 1 - Mathf.Clamp01((p.nextAction - state.time) / AquariumModel.HarpoonCooldown) : -1, HudIcon.Harpoon);
            // R47: three feeds per game, no cooldown.
            SlotLit(actionsX + (slotW + gap), actionsY, slotW, "F", "먹이", Cat(Int(p.feedsLeft), "/", Int(AquariumModel.FeedsPerGame)), -1, p.feedsLeft > 0 && state.time >= state.huntAt, HudIcon.Feed);
            string name = p.diverKind == 0 ? "접착제" : p.diverKind == 1 ? "탐지" : "드론";
            var rIcon = DiverAbilityIcon(p.diverKind);
            float unlock = AquariumModel.DiverAbilityAt(state.huntAt), rx = actionsX + 2 * (slotW + gap);
            if (state.time < unlock)
            {
                float progress = state.time < state.huntAt ? 0 : (state.time - state.huntAt) / AquariumModel.DiverAbilityDelay;
                SlotLit(rx, actionsY, slotW, "R", name, Cat("잠김 ", MinSec(unlock - state.time)), progress, false, rIcon);
                return;
            }
            if (p.diverKind == 0)
                SlotLit(rx, actionsY, slotW, "R", name, p.abilityUses > 0 ? Cat("준비 ", Int(p.abilityUses), "/", Int(AquariumModel.GlueUses)) : "사용함", -1, p.abilityUses > 0, rIcon);
            else if (p.diverKind == 1)
            {
                if (AquariumModel.XrayOn(p, state.time))
                    SlotLit(rx, actionsY, slotW, "R", name, Cat(Tenths(p.abilityUntil - state.time), "s"), 1 - (p.abilityUntil - state.time) / AquariumModel.XraySeconds, true, rIcon);
                // R49: no use limit any more; a 60 s cooldown after each scan.
                else if (p.nextAbility > state.time)
                    SlotLit(rx, actionsY, slotW, "R", name, Cat(Int(Mathf.CeilToInt(p.nextAbility - state.time)), "s"), 1 - (p.nextAbility - state.time) / AquariumModel.XrayCooldown, false, rIcon);
                else SlotLit(rx, actionsY, slotW, "R", name, "준비", -1, true, rIcon);
            }
            else SlotLit(rx, actionsY, slotW, "R", name, p.droneControl ? "조종 중" : p.droneOut ? "배회 중" : "준비", -1, true, rIcon);
        }

        // The most advanced laid egg's hatch progress (per its owner's hatch time) under the egg card.
        private void EggMeter(AquariumSnapshot state, float eggY)
        {
            float best = -1;
            foreach (var egg in state.eggs)
            {
                var owner = PlayerById(state.players, egg.owner);
                float total = AquariumModel.HatchSecondsFor(owner), left = egg.hatchAt - state.time;
                best = Mathf.Max(best, left <= 0 ? 1 : 1 - left / total);
            }
            if (best >= 0) Meter(68, eggY + 32, 176, best, cyan, 3);
        }

        // ---- Diver: drains on the compass, lid alarm --------------------------------------------------------
        private void CompassDrains(AquariumSnapshot state, float yaw, float mid, float perDegree, float inner)
        {
            if (state.drains == null) return;
            var camera = view.Camera.transform.position;
            for (int i = 0; i < state.drains.Length; i++)
            {
                var d = state.drains[i];
                float bearing = Mathf.Atan2(d.position.x - camera.x, d.position.z - camera.z) * Mathf.Rad2Deg;
                float x = Mathf.Clamp(mid + Mathf.DeltaAngle(yaw, bearing) * perDegree, 7, inner - 7);
                bool flash = i < drainFlashUntil.Length && Time.unscaledTime < drainFlashUntil[i];
                bool open = d.isOpen || d.lid > 0;
                Color color = d.glued ? new Color(.55f, .6f, .62f, .85f) : open ? Warm : new Color(.72f, .95f, 1f, .85f);
                if (flash && Mathf.Repeat(Time.unscaledTime * 6, 1) < .6f) color = Danger;
                // R70b: the drain grate icon on a dark disc (a bright ring while it flashes).
                var rect = new Rect(x - 9, 6, 18, 18);
                Shape(rect, new Color(0.01f, 0.05f, 0.08f, .8f), 9);
                if (flash) Shape(rect, color, 9, 2f);
                Icon(x - 7, 8, HudIcon.Drain, color, 14);
            }
        }
        private void LidAlarm(AquariumSnapshot state, Swimmer p, float width)
        {
            if (Time.unscaledTime >= alarmUntil || state.phase != RoundPhase.Playing) return;
            var camera = view.Camera.transform.position;
            float bearing = Mathf.Atan2(alarmAt.x - camera.x, alarmAt.z - camera.z) * Mathf.Rad2Deg;
            int dir = Mathf.RoundToInt(Mathf.Repeat(bearing, 360) / 45) * 45 % 360;
            float metres = (alarmAt - (p != null ? p.position : AquariumThirdPersonCamera.ToPoint(camera))).Length;
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 9);
            var rect = new Rect(width / 2 - 200, 92, 400, 50);
            Shape(rect, new Color(.35f, .03f, .02f, .82f), 12);
            Shape(rect, new Color(1f, .3f + .2f * pulse, .25f, .95f), 12, 2f);
            Label(rect.x, rect.y + 4, rect.width, 24, "배수구 뚜껑이 열렸습니다!", Tinted(centred, new Color(1f, .85f, .8f)));
            Label(rect.x, rect.y + 26, rect.width, 20, Cat(Cardinal(dir), " 방향, ", Int(Mathf.RoundToInt(metres)), "m"), smallCentred);
        }

        // ---- Prompts and overlays --------------------------------------------------------------------------
        private float R47Prompts(AquariumSnapshot state, Swimmer p, float mx, float promptY)
        {
            if (p.team == Team.Seeker && p.diverKind == 0 && p.abilityUses > 0 && !p.droneControl && state.time >= AquariumModel.DiverAbilityAt(state.huntAt)
                && AquariumModel.GlueTarget(p, state.drains) >= 0)
            { KeyPrompt(mx, promptY, "R", "접착제 바르기", new Color(1f, .82f, .3f, .9f)); promptY += PromptStep; }
            return promptY;
        }

        private void R47Overlay(AquariumSnapshot state, Swimmer p, float width, float height)
        {
            if (state.time < state.huntAt) TipPill(width / 2, 148, 760);
            if (!session.IsSpectator && p.team == Team.Seeker && p.alive)
            {
                if (p.droneControl) KeyHint(width / 2, height - 150, "드론 조종 중", "R", "잠수부 시점으로", cyan);
                else if (p.droneOut) KeyHint(width / 2, height - 150, "드론 배회 중", "R", "드론 시점으로", Rim);
                if (AquariumModel.XrayOn(p, state.time))
                {
                    var pill = new Rect(width / 2 - 90, 148, 180, 28);
                    Pill(pill, new Color(1f, .4f, .35f, .8f));
                    Label(pill.x, pill.y, pill.width, pill.height, Cat("생체 탐지  ", Tenths(p.abilityUntil - state.time), "s"), centred);
                }
            }
            if (!session.IsSpectator && p.escaped && state.time - p.escapedAt < 4.5f)
            {
                float age = state.time - p.escapedAt;
                TitleArt(width / 2, height * .36f, "ESCAPED!", new Color(.4f, .98f, .92f), age, Mathf.Clamp01((4.5f - age) / .6f));
            }
            if (tutorial != 0) TutorialCard(state, p, width, height);
        }
        // [text] [key] [text] in a glass pill.
        private void KeyHint(float cx, float y, string before, string key, string after, Color rim)
        {
            float a = Measure(promptText, before).x, b = Measure(promptText, after).x;
            float kw = Mathf.Max(24, 12 + Measure(keyInk, key).x);
            float w = a + b + kw + 44; var rect = new Rect(cx - w / 2, y, w, 30);
            Pill(rect, rim);
            float x = rect.x + 14;
            GUI.Label(new Rect(x, y, a + 4, 30), before, promptText); x += a + 8;
            Key(x, y + 4, key); x += kw + 8;
            GUI.Label(new Rect(x, y, b + 4, 30), after, promptText);
        }

        private void TitleStyles()
        {
            if (titleArt != null) return;
            titleArt = Line1(84, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            endingSub = Line1(18, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            bigCentred = Line1(22, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        }
        // Chunky display text: a stacked drop shadow, a dark outline, a two-tone fill; pops in.
        private void TitleArt(float cx, float cy, string text, Color main, float age, float alpha)
        {
            TitleStyles();
            float pop = age < .35f ? Mathf.Lerp(1.45f, 1f, Mathf.SmoothStep(0, 1, age / .35f)) : 1f + .015f * Mathf.Sin(age * 3f);
            var matrix = GUI.matrix;
            GUI.matrix = matrix * Matrix4x4.TRS(new Vector3(cx, cy, 0), Quaternion.identity, new Vector3(pop, pop, 1)) * Matrix4x4.TRS(new Vector3(-cx, -cy, 0), Quaternion.identity, Vector3.one);
            var rect = new Rect(cx - 500, cy - 60, 1000, 120);
            var old = GUI.color;
            var deep = new Color(main.r * .22f, main.g * .22f, main.b * .3f, alpha);
            for (int i = 9; i >= 1; i--) { GUI.color = deep; GUI.Label(new Rect(rect.x + i * .7f, rect.y + i, rect.width, rect.height), text, titleArt); }
            GUI.color = new Color(0.02f, 0.06f, 0.1f, alpha);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6;
                GUI.Label(new Rect(rect.x + Mathf.Cos(a) * 4.5f, rect.y + Mathf.Sin(a) * 4.5f, rect.width, rect.height), text, titleArt);
            }
            GUI.color = new Color(main.r, main.g, main.b, alpha);
            GUI.Label(rect, text, titleArt);
            // the lighter upper half of the letters
            GUI.BeginGroup(new Rect(rect.x, rect.y, rect.width, rect.height * .5f));
            GUI.color = new Color(Mathf.Lerp(main.r, 1, .55f), Mathf.Lerp(main.g, 1, .55f), Mathf.Lerp(main.b, 1, .55f), alpha);
            GUI.Label(new Rect(0, 0, rect.width, rect.height), text, titleArt);
            GUI.EndGroup();
            GUI.color = old; GUI.matrix = matrix;
        }

        // ---- Ending ----------------------------------------------------------------------------------------
        private static Color EndingColor(AquariumSnapshot state, Swimmer me, string title)
        {
            if (me == null || title == "ESCAPED!") return new Color(.4f, .98f, .92f);
            bool won = state.winner == (int)me.team;
            if (title == "SURVIVED!") return won ? new Color(.55f, 1f, .5f) : new Color(1f, .4f, .35f);
            return won ? new Color(1f, .82f, .28f) : new Color(1f, .4f, .35f);
        }
        private static string StatusOf(Swimmer s)
        {
            if (s.team == Team.Seeker) return "잠수부";
            if (s.escaped) return "탈출";
            if (s.alive) return "생존";
            if (s.deathReason == "harpoon") return "잡힘";
            if (s.deathReason == "starved") return "굶주림";
            return "사망";
        }
        private static Color KindColor(Swimmer s)
        {
            if (s.team == Team.Seeker) { var d = AquariumDiverKinds.Get(s.diverKind); return new Color(d.r, d.g, d.b); }
            var a = AquariumAlienKinds.Get(s.alienKind); return new Color(a.r, a.g, a.b);
        }
        private float ResultsTable(AquariumSnapshot state, float x, float y, float width)
        {
            var all = (Swimmer[])state.players.Clone();
            Array.Sort(all, (a, b) => a.score == b.score ? a.id.CompareTo(b.id) : b.score.CompareTo(a.score));
            const float rowH = 36;
            float h = 64 + all.Length * rowH + 8;
            Card(new Rect(x, y, width, h), true, 14);
            Label(x + 18, y + 10, 200, 26, "결과표", body);
            float c0 = x + 18, c1 = x + 56, c2 = x + width * .40f, c3 = x + width * .66f, c4 = x + width * .79f, c5 = x + width * .88f;
            Label(c0, y + 40, 40, 18, "순위", caption); Label(c1, y + 40, 120, 18, "이름", caption); Label(c2, y + 40, 120, 18, "역할", caption);
            Label(c3, y + 40, 80, 18, "상태", caption); Label(c4, y + 40, 60, 18, "포식", caption); Label(c5, y + 40, 60, 18, "점수", caption);
            Separator(x + 14, y + 60, width - 28);
            var gold = new Color(1f, .84f, .3f);
            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i]; float ry = y + 66 + i * rowH;
                bool me = s.id == session.LocalId && !session.IsSpectator, mvp = i == 0;
                if (me) Shape(new Rect(x + 8, ry, width - 16, rowH - 4), new Color(0.12f, 0.34f, 0.4f, 0.6f), 8);
                else if (mvp) Shape(new Rect(x + 8, ry, width - 16, rowH - 4), new Color(0.35f, 0.27f, 0.06f, 0.35f), 8);
                Label(c0 + 6, ry + 4, 30, 26, (i + 1).ToString(), mvp ? Tinted(body, gold) : body);
                string name = (s.name.Length > 10 ? s.name.Substring(0, 10) + "…" : s.name) + (me ? " (나)" : "");
                float nameW = body.CalcSize(new GUIContent(name)).x;
                Label(c1, ry + 4, c2 - c1 - 4, 26, name, body);
                if (mvp)
                {
                    var badge = new Rect(Mathf.Min(c1 + nameW + 8, c2 - 50), ry + 7, 42, 18);
                    Shape(badge, new Color(1f, .8f, .25f, .95f), 9);
                    GUI.Label(badge, "MVP", keyInk);
                }
                var color = KindColor(s);
                Shape(new Rect(c2, ry + 12, 10, 10), color, s.team == Team.Seeker ? 2 : 5);
                Label(c2 + 16, ry + 6, c3 - c2 - 18, 24, s.team == Team.Seeker ? AquariumDiverKinds.Get(s.diverKind).name : AquariumAlienKinds.Get(s.alienKind).name, Tinted(small, color));
                string status = StatusOf(s);
                var statusColor = status == "탈출" ? cyan : status == "생존" ? new Color(.6f, 1f, .55f) : status == "잠수부" ? muted : Danger;
                Label(c3, ry + 6, c4 - c3, 24, status, Tinted(small, statusColor));
                Label(c4, ry + 4, c5 - c4, 26, s.team == Team.Hider ? s.personalEaten.ToString() : "-", body);
                Label(c5, ry + 4, 70, 26, s.score.ToString(), mvp ? Tinted(body, gold) : body);
            }
            return h;
        }

        private void FinishedHUD(float width, float height)
        {
            var state = session.State;
            TitleStyles();
            // The result scene contains only survivors; the table still records everyone.
            foreach (var alien in state.players)
            {
                if (alien.team != Team.Hider || !alien.alive || alien.swallowedBy != 0) continue;
                var screen = view.Camera.WorldToViewportPoint(view.PlayerPosition(alien) + Vector3.up * 0.8f);
                bool outside = screen.z <= 0 || screen.x < 0.08f || screen.x > 0.92f || screen.y < 0.14f || screen.y > 0.8f;
                if (screen.z <= 0) { screen.x = 1 - screen.x; screen.y = 1 - screen.y; }
                float x = Mathf.Clamp(screen.x * width, 105, width - 105);
                float y = Mathf.Clamp((1 - screen.y) * height, 200, height - 125);
                Card(new Rect(x - 96, y - 16, 192, 51), false, 10);
                Label(x - 84, y - 13, 175, 26, (outside ? "◆ " : "▼ ") + alien.name, body);
                Label(x - 84, y + 12, 175, 22, "생존", small);
            }
            var me = session.IsSpectator ? null : Local();
            string title = AquariumModel.EndingTitle(state.winReason, me);
            string line = AquariumModel.EndingLine(state.winReason, me);
            float remaining = Mathf.Max(0, state.reviewEndsAt - state.time);
            if (remaining <= 0) Panel(new Rect(0, 0, width, height), new Color(0, 0.03f, 0.06f, 0.45f));
            TitleArt(width / 2, 84, title, EndingColor(state, me, title), state.time - state.finishedAt, 1);
            float lineW = endingSub.CalcSize(new GUIContent(line)).x + 40;
            var sub = new Rect(width / 2 - lineW / 2, 142, lineW, 36);
            Pill(sub, Rim);
            Label(sub.x, sub.y + 2, sub.width, 32, line, endingSub);
            if (remaining > 0)
            {
                ResultsTable(state, width - 24 - 560, 196, 560);
                Card(new Rect(24, height - 88, width - 48, 64), false, 14);
                Label(44, height - 80, width - 88, 28, "자유시점  " + Mathf.CeilToInt(remaining) + "s", heading);
                Label(44, height - 48, width - 88, 22, "WASD 이동   Space 위로   Ctrl/C 아래로   Shift 빠르게   마우스 시점   Esc 메뉴", small);
                return;
            }
            float tableW = 660, tx = width / 2 - tableW / 2;
            float h = ResultsTable(state, tx, 194, tableW);
            float by = Mathf.Min(194 + h + 16, height - 60);
            if (session.IsHost && Button(tx, by, 320, session.Practice ? "연습 대기실로" : "대기실로", true))
            {
                if (session.Practice) BackToPracticeLobby();
                else session.Rematch();
            }
            if (!session.IsHost) Label(tx, by + 8, 320, 30, "방장의 재시작을 기다리는 중", small);
            if (Button(tx + 340, by, 320, "나가기")) Leave();
        }
        private void BackToPracticeLobby()
        {
            lastEffect = 0; var self = Local();
            var team = self != null ? self.team : selectedTeam;
            tutorial = 0; tutorialDone = false;
            session.OpenPracticeLobby(playerName, team, selectedAlien, selectedDiver);
        }

        // ---- Rules -----------------------------------------------------------------------------------------
        private void Rules(float width, float height)
        {
            Panel(new Rect(0, 0, width, height), new Color(0, 0.025f, 0.04f, 0.86f));
            float w = 1000, h = 640, x = width / 2 - w / 2, y = height / 2 - h / 2;
            Card(new Rect(x, y, w, h), true, 18);
            Label(x + 26, y + 18, 600, 40, "조작과 규칙", heading);
            Shape(new Rect(x + 28, y + 56, 40, 3), cyan, 1.5f);
            Label(x + 300, y + 24, w - 330, 24, "숨기 20초 → 사냥 10분, 외계인 4 vs 잠수부 1", Tinted(body, cyan));
            Separator(x + 26, y + 70, w - 52);
            float col = (w - 52 - 40) / 3, top = y + 84;
            Label(x + 26, top, col, 28, "공통 조작", body);
            Label(x + 26, top + 30, col, 200, "WASD 이동, Space 상승, Ctrl 하강\n마우스 시점, Shift 질주, 휠 카메라\nEsc 메뉴, Enter 채팅 (대기실과 경기 후)\n\n배수구: 나사 6개 (각 20초, 둘이 같이 풀면 +10%) → 뚜껑 치우기 10초 → 비집고 들어가기 10초 = 탈출", small);
            float c2 = x + 26 + col + 20;
            Label(c2, top, col, 28, "외계인", body);
            Label(c2, top + 30, col, 200, "Q 위장 (쿨 10초), C 해제\nE 포식 (언제든), 배수구 작업\nR 산란 (외계인 상태에서만, 포식 10마리마다, 보라 8)\nF 고유 능력, 휠 클릭 핑 (외계인끼리만)\n\n배고픔 0이면 굶어 죽음, 작살 2번 (초록 3번)에 잡힘, 알이 부화하면 (60초, 보라 40초) 쓰러진 동료 부활", small);
            float c3 = c2 + col + 20;
            Label(c3, top, col, 28, "잠수부", body);
            Label(c3, top + 30, col, 200, "클릭 작살 (재장전 5초)\nF 먹이 뿌리기 (게임당 3회)\nE 열린 뚜껑 덮기 (4초)\nR 능력 (사냥 3분 뒤부터)\n\n나침반에 배수구 4곳 표시, 나사가 빠지면 깜빡이고 뚜껑이 열리면 경보, 배수구 소리는 25m까지", small);
            Separator(x + 26, y + 312, w - 52);
            Label(x + 26, y + 324, col, 24, "외계인 능력 (F)", body);
            var sb = new System.Text.StringBuilder();
            foreach (var k in AquariumAlienKinds.All) sb.Append(k.name).Append(": ").Append(k.abilityName).Append('\n');
            Label(x + 26, y + 352, col + 10, 150, sb.ToString(), small);
            Label(c2, y + 324, col, 24, "잠수부 캐릭터 (R / 패시브)", body);
            sb.Length = 0;
            foreach (var k in AquariumDiverKinds.All) sb.Append(k.name).Append(": ").Append(k.activeName).Append(" / ").Append(k.passiveName).Append('\n');
            sb.Append("접착제: 닫힌 뚜껑을 끝까지 봉인 (1회)\n탐지: 5초 투시, 쿨타임 60초 (횟수 제한 없음)");
            Label(c2, y + 352, col + 10, 150, sb.ToString(), small);
            Label(c3, y + 324, col, 24, "점수", body);
            Label(c3, y + 352, col, 160, "외계인: 물고기 +5, 산란 +10, 나사 +5, 탈출 +20 (마지막 탈출 +20), 동료 포식 −10, 잡힘 −20, 팀 승리 시 생존자 +40, 전멸 −20\n잠수부: 포획 +30, 알 파괴 +10, 진짜 물고기 −5, 전멸시킴 +20, 해고 −20", small);
            Separator(x + 26, y + 520, w - 52);
            Label(x + 26, y + 532, w - 290, 80, "엔딩: WIPED OUT! 전원 포획 / HUNT OVER! 일부 탈출, 나머지 포획 / ESCAPED! 전원 탈출 / ALL EATEN! 물고기 전멸 / SURVIVED! 10분 생존 / YOU'RE FIRED! 잠수부 패배", small);
            if (Button(x + w - 236, y + h - 62, 210, "돌아가기", true)) showRules = false;
        }

        // ---- Tutorials -------------------------------------------------------------------------------------
        private static readonly string[] TutorialNames = { "", "튜토리얼: 외계인 기초", "튜토리얼: 탈출", "튜토리얼: 잠수부" };
        private static readonly string[][] TutorialSteps =
        {
            new string[0],
            new[] { "위장 (Q)", "물고기 포식 (E)", "위장 해제 (C)" },
            new[] { "나사 풀기", "뚜껑 치우기", "비집고 들어가기" },
            new[] { "작살 쏘기", "먹이 뿌리기", "뚜껑 덮기" },
        };
        private static readonly string[][] TutorialHints =
        {
            new string[0],
            new[] { "가까운 물고기를 바라보고 Q로 위장하세요", "숨기 20초가 끝나면 물고기에 다가가 E로 먹으세요", "C로 원래 모습으로 돌아가세요" },
            new[] { "외계인 모습으로 배수구 나사를 바라보고 E로 풀어요 (튜토리얼에선 금방 풀려요)", "나사가 다 빠지면 뚜껑을 바라보고 E로 밀어내세요", "열린 구멍을 바라보고 E로 비집고 들어가세요" },
            new[] { "입수가 끝나면 클릭으로 작살을 쏘세요", "F로 먹이를 뿌려 보세요 (게임당 3회)", "나침반의 주황 배수구로 가서 열린 뚜껑을 E로 덮으세요 (4초)" },
        };

        // R70b: the main menu's tutorial row (moved out of the practice waiting room).
        private static readonly string[] TutorialShort = { "", "외계인 기초", "탈출", "잠수부" };
        private void TutorialButtons(float x, float y, float width)
        {
            Label(x, y, width, 18, "튜토리얼  (혼자 1~2분)", caption);
            const float gap = 8; float w = (width - gap * 2) / 3;
            for (int i = 1; i <= 3; i++)
                if (Button(new Rect(x + (i - 1) * (w + gap), y + 18, w, 34), TutorialShort[i])) StartTutorial(i);
        }

        private void StartTutorial(int kind)
        {
            var team = kind == 3 ? Team.Seeker : Team.Hider;
            SaveName(); lastEffect = 0; notice = "";
            session.OpenPracticeLobby(playerName, team, selectedAlien, selectedDiver);
            selectedTeam = team;
            if (session.Model == null) return;
            session.Model.BotsPassive = true;
            string problem = session.StartRound();
            if (problem.Length > 0) { notice = problem; return; }
            tutorial = kind; tutorialStep = 0; tutorialDone = tutorialSawMorph = false;
            // Keep it short (practice host only): the escape tutorial starts with the screws nearly out,
            // the diver's with one drain's lid pushed aside.
            var drains = session.Model.Drains;
            if (kind == 2)
                for (int i = 0; i < drains.Length; i++) if (drains[i].screws != null) for (int s = 0; s < drains[i].screws.Length; s++) drains[i].screws[s] = Mathf.Max(drains[i].screws[s], .88f);
            if (kind == 3 && drains.Length > 0)
            {
                if (drains[0].screws != null) for (int s = 0; s < drains[0].screws.Length; s++) drains[0].screws[s] = 1;
                drains[0].lid = 1; drains[0].isOpen = true;
            }
        }

        private void UpdateTutorial()
        {
            if (tutorial == 0) return;
            if (!session.Connected || !session.Practice || session.State == null || session.State.phase == RoundPhase.Lobby) { tutorial = 0; tutorialDone = false; return; }
            var state = session.State;
            if (state.phase != RoundPhase.Playing || tutorialDone) return;
            var p = PlayerById(state.players, session.LocalId);
            if (p == null) return;
            bool next = false;
            if (tutorial == 1)
            {
                if (tutorialStep == 0) next = p.species >= 0;
                else if (tutorialStep == 1) next = p.personalEaten > 0;
                else { if (p.species >= 0) tutorialSawMorph = true; next = tutorialSawMorph && p.species < 0; }
            }
            else if (tutorial == 2)
            {
                if (tutorialStep == 0) next = state.drains != null && Array.Exists(state.drains, d => AquariumDrainLayout.AllScrewsOut(d));
                else if (tutorialStep == 1) next = state.drains != null && Array.Exists(state.drains, d => d.isOpen);
                else next = p.drainTask == DrainTask.Enter || p.escaped;
            }
            else
            {
                if (tutorialStep == 0) next = state.harpoons != null && Array.Exists(state.harpoons, h => h.owner == p.id);
                else if (tutorialStep == 1) next = p.feedsLeft < AquariumModel.FeedsPerGame;
                else next = state.effects != null && Array.Exists(state.effects, e => e.kind == "lidClosed" && e.owner == p.id);
            }
            if (!next) return;
            tutorialStep++;
            if (tutorial == 1 && tutorialStep == 2) tutorialSawMorph = p.species >= 0;
            AudioClip clip; if (sounds.TryGetValue("egg", out clip)) audioSource.PlayOneShot(clip);
            if (tutorialStep >= 3) tutorialDone = true;
        }

        private void TutorialCard(AquariumSnapshot state, Swimmer p, float width, float height)
        {
            TitleStyles();
            var card = new Rect(24, 196, 330, 196);
            Card(card, new Color(cyan.r, cyan.g, cyan.b, .6f), true, 14);
            Label(card.x + 18, card.y + 12, card.width - 36, 26, TutorialNames[tutorial], body);
            Separator(card.x + 18, card.y + 42, card.width - 36);
            var steps = TutorialSteps[tutorial];
            for (int i = 0; i < steps.Length; i++)
            {
                float ry = card.y + 52 + i * 30;
                bool done = i < tutorialStep, current = i == tutorialStep && !tutorialDone;
                var circle = new Rect(card.x + 20, ry + 4, 18, 18);
                if (done) { Shape(circle, cyan, 9); GUI.Label(circle, "✓", keyTiny); }
                else Shape(circle, current ? cyan : Rim, 9, 1.5f);
                string text = steps[i];
                if (tutorial == 2 && i == 0 && current && state.drains != null)
                {
                    int most = 0; foreach (var d in state.drains) most = Mathf.Max(most, AquariumDrainLayout.ScrewsOut(d));
                    text += "  " + most + " / " + AquariumDrainLayout.ScrewCount;
                }
                Label(card.x + 48, ry + 2, card.width - 66, 24, text, done ? Tinted(body, muted) : current ? Tinted(body, cyan) : body);
            }
            if (!tutorialDone && tutorialStep < 3)
            {
                string hint = TutorialHints[tutorial][tutorialStep];
                if (tutorial == 1 && tutorialStep == 2 && !tutorialSawMorph) hint = "Q로 다시 위장한 뒤 C로 해제해 보세요";
                Label(card.x + 18, card.y + 146, card.width - 36, 44, hint, small);
                return;
            }
            Panel(new Rect(0, 0, width, height), new Color(0, 0.03f, 0.06f, 0.35f));
            TitleArt(width / 2, height * .32f, "CLEAR!", new Color(.55f, 1f, .6f), 1, 1);
            var box = new Rect(width / 2 - 220, height * .32f + 70, 440, 150);
            Card(box, cyan, true, 16);
            Label(box.x, box.y + 14, box.width, 30, "튜토리얼 완료!", bigCentred);
            Label(box.x, box.y + 48, box.width, 22, TutorialNames[tutorial], smallCentred);
            if (Button(box.x + 24, box.y + 88, 188, "메인 메뉴로", true)) Leave();   // R70b: tutorials start from the main menu
            int again = tutorial;
            if (Button(box.x + 228, box.y + 88, 188, "한 번 더")) StartTutorial(again);
        }
    }
}
