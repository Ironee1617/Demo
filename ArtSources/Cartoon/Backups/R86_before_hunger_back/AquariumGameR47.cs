using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R47 UI: endings and the results table, ability slots (alien F / diver R), the diver's drain compass and
    // lid alarm, the drone camera hint, the diver picker in the lobby, the rules page and
    // the three practice tutorials.
    public sealed partial class AquariumGame
    {
        // Drain work (screws, lids) is heard up to this far (was 14 m).
        private const float DrainHearing = 25f;
        private int selectedDiver;
        private AquariumAlienPreview diverPreview;
        private int lobbyTab;
        // R78a drain alert (diver view): the moment a drain's last screw is out, and the moment its lid is fully
        // off, the warning sound plays and that drain blinks on the compass for DrainAlertSeconds. It is read from
        // the synced drain state, so the host, clients and spectators all get it.
        private const float DrainAlertSeconds = 5f;
        private static readonly Color AlertRed = new Color(1f, .22f, .18f), AlertOrange = new Color(1f, .62f, .12f);
        private int r47Round = -1;
        private bool drainBaseline, alarmLid;
        // R79: every single screw that comes out is an alert (it was only the last one), and so is the lid coming off.
        private bool[] drainOpen = new bool[0], drainAlertLid = new bool[0];
        private int[] drainScrews = new int[0];
        private AudioSource alarmSource;
        // R83: the diver is never told how many screws are out: every screw alert reads the same (no n / 6, no
        // special line for the last one).
        private const string ScrewTag = "나사 풀림", ScrewBanner = "배수구 나사가 풀렸습니다!";
        private float[] drainAlertUntil = new float[0];
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
            if (!session.Connected || state == null || state.phase != RoundPhase.Playing) { r47Round = -1; drainBaseline = false; return; }
            if (state.round != r47Round) { r47Round = state.round; alarmUntil = -100; drainBaseline = false; }
            int drains = state.drains == null ? 0 : state.drains.Length;
            if (drainAlertUntil.Length != drains)
            { drainAlertUntil = new float[drains]; drainScrews = new int[drains]; drainOpen = new bool[drains]; drainAlertLid = new bool[drains]; drainBaseline = false; }
            if (!drainBaseline) for (int i = 0; i < drains; i++) drainAlertUntil[i] = -100;
            var local = Local();
            bool diverView = local != null && (local.team == Team.Seeker || session.IsSpectator || followCamera.SpectatingDiver);
            // The first look at a round only notes how the drains are; after that a change is an alert.
            for (int i = 0; i < drains; i++)
            {
                var d = state.drains[i];
                int loose = AquariumDrainLayout.ScrewsOut(d); bool open = d.isOpen;
                if (drainBaseline && diverView)
                {
                    if (open && !drainOpen[i]) DrainAlert(i, d, true, loose);
                    else if (loose > drainScrews[i]) DrainAlert(i, d, false, loose);   // R79: one alert per screw
                }
                drainScrews[i] = loose; drainOpen[i] = open;
            }
            drainBaseline = true;
        }

        private void DrainAlert(int index, AquariumDrainPlacement d, bool lid, int loose)
        {
            drainAlertUntil[index] = Time.unscaledTime + DrainAlertSeconds; drainAlertLid[index] = lid;
            alarmUntil = Time.unscaledTime + DrainAlertSeconds; alarmLid = lid; alarmAt = AquariumDrainLayout.Mouth(d);
            // R79: the alarm has its own voice and starts over for every alert, so screws coming out close together
            // are each heard without two alarms piling on top of each other.
            var clip = Real("warn"); float volume = .9f * SfxScale;
            if (clip == null) { if (!sounds.TryGetValue("alarm", out clip)) return; volume = .8f * audioSource.volume; }
            if (alarmSource == null) alarmSource = NewSource(false);
            alarmSource.Stop(); alarmSource.clip = clip; alarmSource.volume = volume; alarmSource.pitch = 1; alarmSource.panStereo = 0; alarmSource.Play();
        }

        // ---- Lobby: previews, character panel, diver pick, tips ---------------------------------------------
        // The lobby's 3D rows only exist while a lobby is on screen: the alien row for aliens, the diver row for the diver.
        private void UpdateAlienPreview()
        {
            bool inLobby = session.Connected && session.State.phase == RoundPhase.Lobby && !session.IsSpectator;
            var local = inLobby ? Local() : null;
            // Keep the picks in step with the host (it clamps, and a room may have been joined with an older pick).
            if (local != null && session.IsHost && local.alienKind != selectedAlien) session.SetAlien(selectedAlien);
            if (local != null && session.IsHost && local.diverKind != selectedDiver) session.SetDiver(selectedDiver);
            bool shown = inLobby && lobbyTab == 0;
            bool diver = local != null && local.team == Team.Seeker;   // R78a: the tab is the role (or the 술래 wish)
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
            bool practice = session.Practice, wish = local != null && local.team == Team.Seeker;
            if (lobbyTab == MapTab) MapSelect(x, y, width, height);
            else if (wish) DiverSelect(x, y, width, height, local);
            else AlienSelect(x, y, width, height, local);
            // R78a: exactly two buttons that never move or change size: 외계인 선택 and 잠수부 선택. The tab is the role.
            // In practice it makes the player that side. Online 잠수부 선택 asks to be 술래 (and picks the diver to play
            // if drawn); 외계인 선택 takes the wish back.
            var alienRect = new Rect(x + width - 340, y + 18, 154, 32);
            var diverRect = new Rect(x + width - 178, y + 18, 154, 32);
            bool alienOn = lobbyTab == 0 && !wish, diverOn = lobbyTab == 0 && wish, was = GUI.enabled;
            if (Button(alienRect, alienOn ? "✓ 외계인 선택" : "외계인 선택", alienOn))
            {
                lobbyTab = 0; lobbyAlienPick = true;
                if (wish)
                {
                    selectedTeam = Team.Hider; notice = "";
                    if (practice) session.PracticeRole(Team.Hider); else session.Role(Team.Hider, local.ready);
                }
            }
            GUI.enabled = was && local != null;
            if (Button(diverRect, diverOn ? "✓ 잠수부 선택" : "잠수부 선택", diverOn))
            {
                lobbyTab = 0; lobbyAlienPick = false;
                if (!wish)
                {
                    selectedTeam = Team.Seeker;
                    if (practice) session.PracticeRole(Team.Seeker);
                    else { session.Role(Team.Seeker, local.ready); notice = "술래 희망으로 표시했어요. 취소하려면 외계인 선택을 누르세요."; }
                }
            }
            GUI.enabled = was;
        }

        private void DiverSelect(float x, float y, float width, float height, Swimmer local)
        {
            var picked = AquariumDiverKinds.Get(selectedDiver);
            var pickedColor = new Color(picked.r, picked.g, picked.b);
            Card(new Rect(x, y, width, height), new Color(picked.r, picked.g, picked.b, .55f), true, 18);
            Label(x + 24, y + 18, 300, 34, "잠수부 선택", heading);
            Label(x + 24, y + 54, width - 48, 23, session.Practice ? "잠수부로 연습합니다. 능력(R)은 사냥 시작 3분 뒤부터 쓸 수 있습니다."
                : "술래 희망! 희망자 중 한 명이 잠수부가 됩니다. 취소하려면 외계인 선택을 누르세요.", small);
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
                Label(card.x + 10, card.y + 13, card.width - 16, 28, (chosen ? "✓ " : "") + kind.name, chosen ? Tinted(body, color) : body);
                if (canPick && UiButton(card, GUIContent.none, GUIStyle.none)) PickDiver(kind.id);
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

        private string[] Tips()
        {
            if (tips != null) return tips;
            var list = new List<string>();
            foreach (var s in SpeciesCatalog())
                list.Add(SpeciesName(s) + ": " + HabitText(AquariumFishTraits.Habit(s.speciesId))
                    + (AquariumFishTraits.IgnoresFeed(s.speciesId) ? ", 사육사 잠수부의 먹이에만 관심" : ""));
            list.Add("외계인은 F로 고유 능력을 씁니다, 초록 외계인은 패시브뿐");
            list.Add("배수구 나사는 둘이 같이 풀면 10% 빨라집니다");
            list.Add("나사가 하나 빠질 때마다, 그리고 뚜껑이 열릴 때 잠수부에게 경보가 울립니다");
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
        // R73i: where on the hunger ring fish can be eaten (below 80%): a thin green arc outside the ring from the top round
        // to the 80% mark, with a tick at the mark; bright while the alien is hungry enough, dim while it is full.
        private void EatableZone(float cx, float cy, float diameter, bool canEat)
        {
            var zone = canEat ? new Color(.45f, 1f, .55f, .95f) : new Color(.45f, 1f, .55f, .35f);
            float outer = diameter + 12; var rect = new Rect(cx - outer / 2, cy - outer / 2, outer, outer);
            Arc(rect, 3, 0, AquariumModel.FeedingHungerFraction, zone);
            float a = AquariumModel.FeedingHungerFraction * Mathf.PI * 2, s = Mathf.Sin(a), c = Mathf.Cos(a);
            float r0 = diameter / 2 - 5, r1 = outer / 2 + 3;
            Stroke(cx + s * r0, cy - c * r0, cx + s * r1, cy - c * r1, new Color(zone.r, zone.g, zone.b, 1));
            Stroke(cx + s * r0 + c, cy - c * r0 + s, cx + s * r1 + c, cy - c * r1 + s, new Color(zone.r, zone.g, zone.b, 1));
        }
        // R29 vitals with the R47 per-kind maxima (hunger large, stamina and lives small).
        private void AlienVitals(AquariumSnapshot state, Swimmer p, float height)
        {
            // R84: the fullness ring (포만감): full right after a meal, it empties over the eat cooldown (10 s, 5 s late).
            float fullUntil = AquariumModel.EatReadyAt(p, state.time, state.endsAt), fullFor = AquariumModel.EatCooldownAt(state.time, state.endsAt);
            float fullness = p.alive ? Mathf.Clamp01((fullUntil - state.time) / fullFor) : 0;
            Vital(76, height - 82, 100, 7, fullness, Warm, HudIcon.Hunger, fullness > 0 ? Cat(Int(Mathf.CeilToInt(fullUntil - state.time)), "s") : "0", "포만감");
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
            CatchPill(hudWidth - 260, 100, p);   // R76: top right, with the aliens still in the tank
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
            AliveAliensPill(rect.xMax + 8, y);   // R76
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
            // R73: fish are eaten only below 80% hunger again ("배부름" until then).
            Slot(actionsX + (slotW + gap), actionsY, slotW, "E", "포식",
                laying ? "산란 중" : inside ? "사용 불가" : Ready(Mathf.Max(Mathf.Max(state.huntAt, AquariumModel.EatReadyAt(p, state.time, state.endsAt)), Mathf.Max(p.nextAction, p.stunnedUntil)), state.time), EatProgress(p, state), HudIcon.Bite);   // R75
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
            if (p.droneControl) DartSlot(state, p, actionsX, actionsY, slotW); else   // R76
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
            UnlockFlash(rx, actionsY, slotW, state.time - unlock);   // R76
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
                if (egg.hatchAt <= 0) continue;   // R76: still waiting for a fallen ally
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
                bool open = d.isOpen || d.lid > 0;
                Color color = d.glued ? new Color(.55f, .6f, .62f, .85f) : open ? Warm : new Color(.72f, .95f, 1f, .85f);
                // R70b: the drain grate icon on a dark disc. (R78a: the alert blink is drawn on top, CompassDrainAlerts.)
                var rect = new Rect(x - 9, 6, 18, 18);
                Shape(rect, new Color(0.01f, 0.05f, 0.08f, .8f), 9);
                Icon(x - 7, 8, HudIcon.Drain, color, 14);
            }
        }
        // R78a: a drain whose screws all came out, or whose lid is off, blinks red and orange for DrainAlertSeconds:
        // a large beating disc with a ring growing out of it and a caption above. It is drawn over the strip (not
        // clipped by it); a drain outside the visible arc sits at that end of the strip with an arrow pointing on.
        private void CompassDrainAlerts(AquariumSnapshot state, float yaw, float left, float y, float mid, float perDegree, float inner)
        {
            if (state.drains == null || Event.current.type != EventType.Repaint) return;
            float now = Time.unscaledTime;
            var camera = view.Camera.transform.position;
            for (int i = 0; i < state.drains.Length && i < drainAlertUntil.Length; i++)
            {
                if (now >= drainAlertUntil[i]) continue;
                var d = state.drains[i];
                float bearing = Mathf.Atan2(d.position.x - camera.x, d.position.z - camera.z) * Mathf.Rad2Deg;
                float delta = Mathf.DeltaAngle(yaw, bearing), raw = mid + delta * perDegree;
                bool off = raw < 14 || raw > inner - 14;
                float x = left + Mathf.Clamp(raw, 14, inner - 14), cy = y + 15;
                float beat = Mathf.Repeat((DrainAlertSeconds - (drainAlertUntil[i] - now)) * 2.5f, 1f);
                bool hot = beat < .5f;
                Color c = hot ? AlertRed : AlertOrange;
                float grow = 14 + 12 * beat;
                Shape(new Rect(x - grow, cy - grow, grow * 2, grow * 2), new Color(c.r, c.g, c.b, .9f * (1 - beat)), grow, 3f);
                float r = hot ? 15 : 12;
                var disc = new Rect(x - r, cy - r, r * 2, r * 2);
                Shape(disc, hot ? new Color(.55f, .04f, .03f, .97f) : new Color(.4f, .16f, .02f, .97f), r);
                Shape(disc, c, r, 2.5f);
                float icon = hot ? 20 : 16;
                Icon(x - icon / 2, cy - icon / 2, HudIcon.Drain, Color.white, icon);
                if (off) Label(delta < 0 ? x - 36 : x + 18, cy - 11, 18, 22, delta < 0 ? "◀" : "▶", Tinted(centred, c));
                string caption = drainAlertLid[i] ? "뚜껑 열림" : ScrewTag;
                float tagW = Mathf.Max(68, Measure(smallCentred, caption).x + 18);
                var tag = new Rect(x - tagW / 2, y - 15, tagW, 16);
                Shape(tag, new Color(.3f, .03f, .02f, .92f), 8);
                Label(tag.x, tag.y - 1, tag.width, tag.height, caption, Tinted(smallCentred, hot ? Color.white : AlertOrange));
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
            Label(rect.x, rect.y + 4, rect.width, 24, alarmLid ? "배수구 뚜껑이 열렸습니다!" : ScrewBanner, Tinted(centred, new Color(1f, .85f, .8f)));
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
                TitleArt(width / 2, height * .36f, "ESCAPED!", new Color(.4f, .98f, .92f), R77Clock(R77KeyOf(p.escapedAt, 1), age), Mathf.Clamp01((4.5f - age) / .6f));
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
        // R77: the title pops in letter by letter with a squash and stretch, then breathes (AquariumGameR77.cs).
        private void TitleArt(float cx, float cy, string text, Color main, float age, float alpha) { R77TitleArt(cx, cy, text, main, age, alpha); }

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
            // R77: the title pops in letter by letter, the subtitle and the table follow with a lighter pop.
            float titleAge = R77Clock(R77KeyOf(state.finishedAt, 3) + state.round * 1048576, state.time - state.finishedAt);
            TitleArt(width / 2, 84, title, EndingColor(state, me, title), titleAge, 1);
            float subAge = titleAge - R77Settled(title) * .62f;
            if (subAge >= 0)
            {
                float lineW = Measure(endingSub, line).x + 40;
                var sub = new Rect(width / 2 - lineW / 2, 142, lineW, 36);
                var keep = R77PushJelly(sub.center, subAge, .3f, .16f, true);
                Pill(sub, Rim);
                Label(sub.x, sub.y + 2, sub.width, 32, line, endingSub);
                GUI.matrix = keep;
            }
            if (remaining > 0)
            {
                float tableAge = subAge - .12f;
                if (tableAge >= 0)
                {
                    var keep = R77PushJelly(new Vector2(width - 24 - 280, 196), tableAge, .82f, .07f, false, -18);
                    ResultsTable(state, width - 24 - 560, 196, 560);
                    GUI.matrix = keep;
                }
                Card(new Rect(24, height - 88, width - 48, 64), false, 14);
                Label(44, height - 80, width - 88, 28, "자유시점  " + Mathf.CeilToInt(remaining) + "s", heading);
                Label(44, height - 48, width - 88, 22, "WASD 이동   Space 위로   Ctrl/C 아래로   Shift 빠르게   마우스 시점   Esc 메뉴", small);
                return;
            }
            float tableW = 660, tx = width / 2 - tableW / 2;
            var keepTable = R77PushJelly(new Vector2(width / 2, 194), R77FinalAge(), .82f, .07f, false, -18);
            float h = ResultsTable(state, tx, 194, tableW);
            GUI.matrix = keepTable;
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
            Label(c2, top + 30, col, 200, "Q 위장 (쿨 10초), C 해제\nE 포식 (쿨 10초, 남은 시간 3분부터 5초), 배수구 작업\nR 산란 (외계인 상태에서만, 포식 10마리마다, 보라 8)\nF 고유 능력, 휠 클릭 핑 (외계인끼리만)\n\n작살 2번 (초록 3번)에 잡힘, 동료가 쓰러지면 알이 20초 뒤 부화해 부활", small);
            float c3 = c2 + col + 20;
            Label(c3, top, col, 28, "잠수부", body);
            Label(c3, top + 30, col, 200, "클릭 작살 (재장전 5초)\nF 먹이 뿌리기 (게임당 3회)\nE 열린 뚜껑 덮기 (4초)\nR 능력 (사냥 3분 뒤부터)\n\n나침반에 배수구 4곳 표시, 나사가 하나 빠질 때마다, 그리고 뚜껑이 열릴 때 경보가 울리고 나침반이 깜빡임, 배수구 소리는 25m까지", small);
            Separator(x + 26, y + 312, w - 52);
            Label(x + 26, y + 324, col, 24, "외계인 능력 (F)", body);
            var sb = new System.Text.StringBuilder();
            foreach (var k in AquariumAlienKinds.All) sb.Append(k.name).Append(": ").Append(k.abilityName).Append('\n');
            Label(x + 26, y + 352, col + 10, 150, sb.ToString(), small);
            Label(c2, y + 324, col, 24, "잠수부 캐릭터 (R / 패시브)", body);
            sb.Length = 0;
            foreach (var k in AquariumDiverKinds.All) sb.Append(k.name).Append(": ").Append(k.activeName).Append(" / ").Append(k.passiveName).Append('\n');
            sb.Append("접착제: 닫힌 뚜껑을 끝까지 봉인 (1회)\n탐지: 10초 투시, 쿨타임 60초 (횟수 제한 없음)");
            Label(c2, y + 352, col + 10, 150, sb.ToString(), small);
            Label(c3, y + 324, col, 24, "점수", body);
            Label(c3, y + 352, col, 160, "외계인: 물고기 +5, 산란 +10, 나사 +5, 탈출 +20 (마지막 탈출 +20), 동료 포식 −10, 잡힘 −20, 팀 승리 시 생존자 +40, 전멸 −20\n잠수부: 포획 +30, 알 파괴 +10, 진짜 물고기 −5, 전멸시킴 +20, 해고 −20", small);
            Separator(x + 26, y + 520, w - 52);
            Label(x + 26, y + 532, w - 290, 80, "엔딩: WIPED OUT! 전원 포획 / HUNT OVER! 일부 탈출, 나머지 포획 / ESCAPED! 전원 탈출 / ALL EATEN! 물고기 전멸 / SURVIVED! 10분 생존 / YOU'RE FIRED! 잠수부 패배", small);
            if (Button(x + w - 236, y + h - 62, 210, "돌아가기", true)) showRules = false;
        }

        // ---- Tutorials -------------------------------------------------------------------------------------
        // R78b: the lessons themselves are in AquariumGameR78Tutorial.cs (StartTutorial, UpdateTutorial, TutorialCard).
        // R70b: the main menu's tutorial row (moved out of the practice waiting room).
        private void TutorialButtons(float x, float y, float width)
        {
            Label(x, y, width, 18, "튜토리얼  (혼자 1~2분)", caption);
            const float gap = 8; float w = (width - gap * 2) / 3;
            for (int i = 1; i <= 3; i++)
                if (Button(new Rect(x + (i - 1) * (w + gap), y + 18, w, 34), TutorialShort[i])) StartTutorial(i);
        }
    }
}
