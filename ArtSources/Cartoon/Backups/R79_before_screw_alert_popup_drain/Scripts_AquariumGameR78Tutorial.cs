using System;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R78b: the tutorials, rebuilt. Three short lessons in their own small tank (AquariumMaps.Tutorial):
    //   1 외계인 기초: swim, climb, sprint, disguise, eat, drop the disguise, lay an egg, use the ability, then stay
    //                 disguised while the diver swims past.
    //   2 탈출:       the return drain: screws, lid, squeeze in while the diver (called by the alarm) comes over.
    //   3 잠수부:     swim, feed, spear the fake fish that does not come to the food, follow the alarm to the drain,
    //                 finish the alien, close the lid and glue it.
    // One action per step: an objective line with its keycaps and "n / N" under the compass, a marker on the target,
    // a squishy pop and a plip when a step is done (the R77 jelly), no waiting (the hide phase and the ability lock
    // are skipped, drain work is quick). Every step is read from the game state, so nothing can soft-lock: a closed
    // lid or a dropped disguise simply puts the step back. Finishing a lesson is saved (PlayerPrefs), and online play
    // opens once all three are done (R78Unlocked).
    public sealed partial class AquariumGame
    {
        private static readonly string[] TutorialNames = { "", "튜토리얼: 외계인 기초", "튜토리얼: 탈출", "튜토리얼: 잠수부" };
        private static readonly string[] TutorialShort = { "", "외계인 기초", "탈출", "잠수부" };
        private static readonly string[] R78Open = { "", "외계인 기초", "탈출", "잠수부" };
        private static readonly string[] R78Done = { "", "✓  외계인 기초", "✓  탈출", "✓  잠수부" };
        private static readonly string[] R78About = { "", "헤엄, 위장, 포식, 산란, 능력", "배수구 나사와 뚜껑, 탈출", "작살, 먹이, 경보, 뚜껑 덮기" };
        private static readonly string[] R78Next = { "", "다음: 탈출 배우기", "다음: 잠수부 배우기", "" };
        private static readonly string[] R78Praise = { "좋아요!", "잘했어요!", "완벽해요!", "바로 그거예요!" };

        private sealed class R78Step
        {
            public string[] keys; public string text, hint;
            public R78Step(string text, string hint, params string[] keys) { this.text = text; this.hint = hint; this.keys = keys; }
        }
        private static readonly R78Step[][] R78Steps =
        {
            new R78Step[0],
            new[]
            {
                new R78Step("노란 고리까지 헤엄치세요", "마우스로 방향을 바꿔요", "W", "A", "S", "D"),
                new R78Step("위쪽 고리로 올라가세요", "Space는 위로, Ctrl은 아래로", "Space", "Ctrl"),
                new R78Step("질주해서 마지막 고리를 통과하세요", "질주는 스태미나를 써요", "Shift"),
                new R78Step("물고기를 바라보고 위장하세요", "가까이 가서 조준점을 물고기에 맞춰요", "Q"),
                new R78Step("물고기를 한 마리 먹으세요", "먹고 나면 10초 동안은 다시 먹을 수 없어요", "E"),
                new R78Step("위장을 풀고 외계인으로 돌아가세요", "알 낳기와 배수구 작업은 외계인 모습에서만 돼요", "C"),
                new R78Step("능력을 쓰세요 (먹물 폭탄)", "먹물이 5초 동안 잠수부의 눈을 가려요. 외계인마다 능력이 달라요", "F"),
                new R78Step("먹물 속에서 알을 낳으세요", "동료가 쓰러지면 20초 뒤 이 알에서 부활해요", "R"),
                new R78Step("잠수부가 옵니다! 물고기로 위장하세요", "작살을 2번 맞으면 잡혀요", "Q"),
                new R78Step("잠수부가 지나갈 때까지 물고기인 척하세요", "위장을 풀면 들켜서 다시 해야 해요"),
            },
            new[]
            {
                new R78Step("표시된 배수구로 가세요", "배수구로 빠져나가면 탈출 성공이에요", "W", "A", "S", "D"),
                new R78Step("나사를 바라보고 하나씩 푸세요", "실전은 나사 하나에 20초. 다 풀면 잠수부에게 경보가 울려요", "E"),
                new R78Step("뚜껑을 바라보고 치우세요", "뚜껑이 열릴 때도 경보가 울리고 잠수부 나침반이 깜빡여요", "E"),
                new R78Step("잠수부가 옵니다! 구멍으로 비집고 들어가세요", "늦으면 잠수부가 뚜껑을 다시 덮어요", "E"),
            },
            new[]
            {
                new R78Step("노란 고리까지 헤엄치세요", "Space는 위로, Ctrl은 아래로, Shift는 질주", "W", "A", "S", "D"),
                new R78Step("먹이를 뿌려 물고기를 모으세요", "먹이는 한 판에 3번 쓸 수 있어요", "F"),
                new R78Step("먹이에 오지 않는 가짜 물고기를 작살로 맞히세요", "진짜 물고기를 맞히면 감점. 재장전 5초", "좌클릭"),
                new R78Step("경보! 나침반이 깜빡이는 배수구에서 외계인을 잡으세요", "외계인은 작살 2번에 잡혀요. 나사가 다 풀리거나 뚜껑이 열리면 경보가 울려요", "좌클릭"),
                new R78Step("열린 뚜껑을 다시 덮으세요", "4초 걸려요", "E"),
                new R78Step("접착제로 뚜껑을 봉인하세요", "잠수부마다 능력이 달라요. 외계인을 모두 잡으면 승리!", "R"),
            },
        };
        private const string R78SprintHint = "Shift를 누른 채로 통과해야 해요", R78FakeHint = "혼자 위아래로만 움직이는 물고기가 가짜예요";
        private const string R78Caught = "들켰어요! 다시 위장하세요", R78LidShut = "잠수부가 뚜껑을 덮었어요. 다시 치우세요";

        // ---- completion and the multiplayer gate -----------------------------------------------------------
        public const string R78DoneKey = "Aquarium.Tutorial.Done", R78BypassKey = "Aquarium.Dev.SkipTutorialGate", R78BypassArg = "-skipTutorialGate";
        private static bool r78GateRead, r78Bypass; private static readonly bool[] r78Finished = new bool[4];
        // Read again after the keys were changed from outside (editor checks).
        public static void R78ReloadGate() { r78GateRead = false; }
        private static void R78ReadGate()
        {
            if (r78GateRead) return; r78GateRead = true;
            for (int i = 1; i <= 3; i++) r78Finished[i] = PlayerPrefs.GetInt(R78DoneKey + i, 0) == 1;
            r78Bypass = PlayerPrefs.GetInt(R78BypassKey, 0) == 1 || Array.IndexOf(Environment.GetCommandLineArgs(), R78BypassArg) >= 0;
        }
        private static int R78DoneCount() { R78ReadGate(); return (r78Finished[1] ? 1 : 0) + (r78Finished[2] ? 1 : 0) + (r78Finished[3] ? 1 : 0); }
        // Online play (making a room, the room list, joining by code) opens once all three lessons are done.
        public static bool R78Unlocked() { R78ReadGate(); return r78Bypass || (r78Finished[1] && r78Finished[2] && r78Finished[3]); }
        private float r78GateAt = -10;
        // A locked online button was pressed: show the tutorials instead and say why.
        private void R78GateBounce() { menuPanel = MenuPanel.Tutorial; r78GateAt = Time.unscaledTime; roomDialogError = ""; GUI.FocusControl(null); }

        // The main menu's tutorial panel: the three lessons with their state, and what they unlock.
        private void R78TutorialPanel(float px, float pw, float height, bool usable)
        {
            var r = PanelCard(px, Mathf.Min(pw, 560), 392, height, "튜토리얼", "직접 해 보며 익히는 세 가지 수업, 하나에 1분쯤 걸려요.");
            GUI.enabled = usable;
            R78ReadGate();
            int first = !r78Finished[1] ? 1 : !r78Finished[2] ? 2 : !r78Finished[3] ? 3 : 0;
            for (int i = 1; i <= 3; i++)
            {
                var row = new Rect(r.x + 26, r.y + 96 + (i - 1) * 68, r.width - 52, 56);
                bool main = i == first;
                if (Button(row, "", main)) { StartTutorial(i); return; }
                Label(row.x + 20, row.y + 6, row.width - 150, 26, r78Finished[i] ? R78Done[i] : R78Open[i], main ? Tinted(heading, ink) : r78Finished[i] ? Tinted(heading, cyan) : heading);
                Label(row.x + 21, row.y + 32, row.width - 150, 18, R78About[i], main ? Tinted(small, new Color(.03f, .22f, .26f)) : small);
                Label(row.xMax - 118, row.y, 100, row.height, r78Finished[i] ? "완료" : "약 1분", main ? Tinted(R78Right(), ink) : r78Finished[i] ? Tinted(R78Right(), cyan) : R78Right());
            }
            bool open = R78Unlocked();
            float t = Time.unscaledTime - r78GateAt;
            var note = new Rect(r.x + 26, r.y + 306, r.width - 52, 58);
            var keep = GUI.matrix;
            if (t < .7f) keep = R77PushJelly(note.center, t, .86f, .1f, true);
            Shape(note, open ? new Color(.1f, .34f, .3f, .7f) : t < 2.5f ? new Color(.42f, .26f, .05f, .85f) : new Color(.03f, .13f, .19f, .7f), 12);
            Shape(note, open ? cyan : Warm, 12, 1.3f);
            Label(note.x + 18, note.y + 8, note.width - 36, 22, open ? "멀티플레이가 열렸어요" : "멀티플레이는 잠겨 있어요", Tinted(body, open ? cyan : Warm));
            Label(note.x + 18, note.y + 31, note.width - 36, 20, open ? "방 만들기와 방 찾기로 친구와 함께 즐기세요." : Cat("튜토리얼 세 가지를 모두 마치면 열려요  (", Int(R78DoneCount()), " / 3 완료)"), small);
            GUI.matrix = keep;
        }
        private GUIStyle r78Right, r78Objective, r78Mark, r78Toast, r78Pips;
        private GUIStyle R78Right() { R78Styles(); return r78Right; }
        private void R78Styles()
        {
            if (r78Right != null) return;
            r78Right = Line1(13, muted, FontStyle.Bold, TextAnchor.MiddleRight);
            r78Objective = Line1(19, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
            r78Mark = Line1(26, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            r78Toast = Line1(24, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            r78Pips = Line1(13, cyan, FontStyle.Bold, TextAnchor.MiddleRight);
        }

        // ---- lesson state ----------------------------------------------------------------------------------
        private const int R78BotId = 2;
        private float r78StepAt, r78PopAt = -10, r78ToastAt = -10, r78ClearAt = -1, r78LastStamina, r78SprintAt = -10, r78PassAt, r78LegAt, r78BotNext, r78ModelStepAt;
        private string r78ToastText = ""; private bool r78ToastGood, r78Finishing, r78MarkOn, r78RingOn, r78WasOpen, r78Fled;
        private int r78Praise, r78Leg, r78BotMode, r78Count = -1, r78CountOf;
        private Point r78MarkAt, r78PassMid, r78BotMove, r78BotAim; private bool r78BotSprint, r78BotInteract; private int r78BotDrain, r78BotPart;
        private readonly SwimInput r78BotInput = new SwimInput();
        private GameObject r78Ring; private LineRenderer r78RingLine;
        // Editor checks steer the player through this (the keyboard and mouse are not read while it is set).
        public static Func<Swimmer, SwimInput, SwimInput> R78QaInput;
        public int R78Lesson { get { return tutorial; } }
        public int R78StepIndex { get { return tutorialStep; } }
        public bool R78Cleared { get { return tutorialDone; } }
        public Point R78Target { get { return r78MarkAt; } }
        public bool R78HasTarget { get { return r78MarkOn; } }

        private static readonly Point[] R78RingsAlien = { new Point(-3.2f, 2.3f, -1.6f), new Point(1.2f, 5.0f, .4f), new Point(8.2f, 2.6f, .2f) };
        private static readonly Point R78RingDiver = new Point(-2.6f, 2.6f, 0f);
        private static readonly Point R78ParkEast = new Point(9.7f, 1.5f, 5.9f), R78ParkWest = new Point(-9.7f, 1.6f, 5.8f), R78FakeHome = new Point(8.7f, 2.6f, .8f);

        private void StartTutorial(int kind)
        {
            kind = Mathf.Clamp(kind, 1, 3);
            var team = kind == 3 ? Team.Seeker : Team.Hider;
            SaveName(); lastEffect = 0; notice = "";
            // The lessons always use the purple alien (ink bomb) and the keeper diver (glue).
            session.OpenPracticeLobby(playerName, team, 0, 0);
            session.PreferredAlien = selectedAlien; session.PreferredDiver = selectedDiver;
            var model = session.Model; if (model == null) return;
            model.SetMap(AquariumMaps.Tutorial);
            model.DiveEntry = false; model.FillWithAI = false; model.BotsPassive = true;
            model.Players.RemoveAll(p => p.bot);
            var bot = model.AddPlayer(R78BotId, kind == 3 ? "연습 외계인" : "연습 잠수부", kind == 3 ? Team.Hider : Team.Seeker, true);
            if (bot != null) { bot.alienKind = 2; bot.diverKind = 0; }
            string problem = session.StartRound();
            if (problem.Length > 0 || model.Phase != RoundPhase.Playing) { session.Leave(); notice = problem; return; }
            model.TutorialBegin(AquariumTutorialMap.DrainSites, AquariumTutorialMap.DrainYaw, AquariumTutorialMap.SlideYaw);
            var me = model.Player(session.LocalId); bot = model.Player(R78BotId);
            var drains = model.Drains;
            if (kind == 1)
            {
                model.TutorialPlace(me.id, new Point(-8.4f, 2.2f, -2.2f), new Point(1, 0, .1f));
                model.TutorialPlace(bot.id, R78ParkEast, new Point(1, 0, 1));
                me.personalEaten = AquariumModel.EggFoodFor(me) - 1;   // one more fish earns the egg
                R78Fish(model, 3, 3, true);
            }
            else if (kind == 2)
            {
                model.TutorialPlace(me.id, new Point(-2.5f, 2.3f, .6f), new Point(1, -.1f, -.4f));
                model.TutorialPlace(bot.id, R78ParkWest, new Point(-1, 0, 1));
                for (int s = 0; s < 3; s++) drains[0].screws[s * 2] = 1;   // three of the six screws are already out
                R78Fish(model, 2, 2, false);
            }
            else
            {
                model.TutorialPlace(me.id, new Point(-8.6f, 2.4f, 0f), new Point(1, 0, 0));
                model.TutorialPlace(bot.id, R78FakeHome, new Point(-1, 0, .2f));
                bot.species = 1; bot.revealUntil = 0; bot.morphAt = -100;   // the fake: a blue tang that behaves wrong
                for (int s = 0; s < 5; s++) drains[0].screws[s] = 1;      // its escape drain has one screw left
                R78Fish(model, 4, 3, false);
            }
            model.TutorialInput = R78Input;
            tutorial = kind; tutorialStep = 0; tutorialDone = tutorialSawMorph = false;
            r78StepAt = r78ModelStepAt = 0; r78PopAt = Time.unscaledTime - .25f; r78ClearAt = -1; r78Finishing = false; r78ToastAt = -10;
            r78BotMode = 0; r78Leg = 0; r78WasOpen = r78Fled = false; r78BotInteract = false; r78BotMove = new Point(); r78BotAim = bot.facing; r78BotSprint = false;
            r78LastStamina = me.stamina; r78SprintAt = -10; r78Count = -1; r78MarkOn = false;
            pause = false;
            session.Update(.034f);   // a first snapshot of the lesson, so the waiting room is never drawn
        }

        private static void R78Fish(AquariumModel model, int blue, int yellow, bool school)
        {
            var spots = new[] { new Point(1.5f, 2.4f, -1.2f), new Point(4.2f, 3.2f, 1.6f), new Point(2.8f, 1.8f, 2.4f), new Point(5.6f, 2.6f, -1.0f), new Point(.4f, 3.6f, 1.2f), new Point(3.6f, 4.2f, -.4f), new Point(6.2f, 1.9f, 1.4f) };
            int at = 0;
            for (int i = 0; i < blue; i++, at++) model.TutorialFish(1, spots[at % spots.Length], new Point(at % 2 == 0 ? 1 : -1, 0, .4f));
            for (int i = 0; i < yellow; i++, at++) model.TutorialFish(3, spots[at % spots.Length], new Point(at % 2 == 0 ? -1 : 1, 0, -.3f));
            if (school) model.TutorialSchool(0, new Point(-1.5f, 3.4f, 2.6f), new Point(1, 0, -.2f), 5);
        }

        // Every swimmer's input passes here on the host: the scripted bot, and the editor check's player.
        private SwimInput R78Input(Swimmer p, SwimInput input)
        {
            if (!p.bot) return R78QaInput != null ? R78QaInput(p, input) : null;
            var o = r78BotInput;
            o.x = r78BotMove.x; o.y = r78BotMove.y; o.z = r78BotMove.z; o.aimX = r78BotAim.x; o.aimY = r78BotAim.y; o.aimZ = r78BotAim.z;
            o.sprint = r78BotSprint; o.slow = false; o.action = o.morph = o.feed = o.scan = o.layEgg = o.unmorph = o.ability = false;
            o.interact = r78BotInteract; o.interactDrain = r78BotDrain; o.interactPart = r78BotPart; r78BotInteract = false;
            return o;
        }

        private void R78EndLesson() { tutorial = 0; tutorialDone = false; r78MarkOn = false; R78ShowRing(false, new Point(), new Point()); }

        private void UpdateTutorial()
        {
            if (tutorial == 0) { if (r78RingOn) R78ShowRing(false, new Point(), new Point()); return; }
            var model = session.Model;
            if (!session.Connected || !session.Practice || model == null || !model.Tutorial || session.State == null || model.Phase != RoundPhase.Playing) { R78EndLesson(); return; }
            var me = model.Player(session.LocalId); var bot = model.Player(R78BotId);
            if (me == null || bot == null) { R78EndLesson(); return; }
            float now = Time.unscaledTime, dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            if (r78StepAt <= 0) { r78StepAt = now; r78ModelStepAt = model.Time; }
            if (me.stamina < r78LastStamina - .01f) r78SprintAt = now;
            r78LastStamina = me.stamina;
            if (r78Finishing)
            {
                r78MarkOn = false; R78ShowRing(false, new Point(), new Point()); R78BotIdle(bot);
                // (an escape waits for the game's own ESCAPED! title, timed on the round clock)
                if (!tutorialDone && now >= r78ClearAt && (!me.escaped || model.Time - me.escapedAt >= 4.6f)) { tutorialDone = true; pause = false; }
                return;
            }
            // Never expected (nothing here hurts the player), but a fallen player simply starts the lesson again.
            if (!me.alive && !me.escaped) { StartTutorial(tutorial); return; }
            r78MarkOn = false; r78Count = -1; bool ring = false; Point ringAt = new Point(), ringFrom = me.position;
            if (tutorial == 1) R78Alien(model, me, bot, now, ref ring, ref ringAt);
            else if (tutorial == 2) R78Escape(model, me, bot, now, dt);
            else R78Diver(model, me, bot, now, dt, ref ring, ref ringAt);
            R78ShowRing(ring, ringAt, ringFrom);
        }

        private void R78Advance(float now, AquariumModel model)
        {
            tutorialStep++; r78StepAt = now; r78ModelStepAt = model.Time; r78PopAt = now;
            r78ToastText = R78Praise[r78Praise++ % R78Praise.Length]; r78ToastGood = true; r78ToastAt = now;
            R78Plip(true);
            if (tutorialStep >= R78Steps[tutorial].Length) R78Finish(now, .9f);
        }
        private void R78Back(int step, float now, AquariumModel model, string why)
        {
            tutorialStep = step; r78StepAt = now; r78ModelStepAt = model.Time; r78PopAt = now;
            r78ToastText = why; r78ToastGood = false; r78ToastAt = now; R78Plip(false);
        }
        private void R78Finish(float now, float delay)
        {
            r78Finishing = true; r78ClearAt = now + delay; tutorialStep = R78Steps[tutorial].Length;
            R78ReadGate();
            if (!r78Finished[tutorial]) { r78Finished[tutorial] = true; PlayerPrefs.SetInt(R78DoneKey + tutorial, 1); PlayerPrefs.Save(); }
        }
        private void R78Plip(bool good)
        {
            AudioClip clip;
            if (good) { R77Plip(3, 4); if (sounds.TryGetValue("egg", out clip)) audioSource.PlayOneShot(clip, .8f); }
            else if (sounds.TryGetValue("caught", out clip)) audioSource.PlayOneShot(clip, .6f);
        }
        private static float R78Distance(Point a, Point b) { return (a - b).Length; }
        private void R78Mark(Point at) { r78MarkAt = at; r78MarkOn = true; }
        private bool R78NearestFish(AquariumModel model, Point from, out Point at)
        {
            float best = float.MaxValue; at = from; bool found = false;
            foreach (var f in model.Fish)
            {
                if (f.dead || f.eaten) continue;
                float d = (f.position - from).Length; if (d < best) { best = d; at = f.position; found = true; }
            }
            return found;
        }

        // ---- lesson 1: the alien's basics --------------------------------------------------------------------
        private void R78Alien(AquariumModel model, Swimmer me, Swimmer bot, float now, ref bool ring, ref Point ringAt)
        {
            Point fish;
            switch (tutorialStep)
            {
                case 0: case 1: case 2:
                    ring = true; ringAt = R78RingsAlien[tutorialStep]; R78Mark(ringAt);
                    R78BotIdle(bot);
                    if (R78Distance(me.position, ringAt) < 1.45f && (tutorialStep < 2 || now - r78SprintAt < 1.2f)) R78Advance(now, model);
                    break;
                case 3:
                    if (R78NearestFish(model, me.position, out fish)) R78Mark(fish);
                    R78BotIdle(bot);
                    if (me.species >= 0) R78Advance(now, model);
                    break;
                case 4:
                    if (R78NearestFish(model, me.position, out fish)) R78Mark(fish);
                    R78BotIdle(bot);
                    if (me.personalEaten >= AquariumModel.EggFoodFor(me)) R78Advance(now, model);
                    break;
                case 5:
                    R78BotIdle(bot);
                    // wait for the meal to go down before asking for C (the gulp holds the body for a moment)
                    if (me.species < 0 && model.Time - r78ModelStepAt > .6f) R78Advance(now, model);
                    break;
                case 6:
                    R78BotIdle(bot);
                    if (me.nextAbility > model.Time + 1f) R78Advance(now, model);
                    break;
                case 7:
                    R78BotIdle(bot);
                    if (me.eggsLaid > 0 && me.layEndsAt <= 0) R78Advance(now, model);
                    break;
                case 8:
                    R78BotIdle(bot);
                    if (now - r78StepAt < .1f) { me.nextMorph = 0; me.morphLockedUntil = 0; }
                    if (R78NearestFish(model, me.position, out fish)) R78Mark(fish);
                    if (me.species >= 0 && model.Time >= me.revealUntil)
                    {
                        R78Advance(now, model);
                        r78BotMode = 1; r78Leg = 0; r78LegAt = now; r78PassAt = now;
                        // the diver swims up to the "fish", looks it over, then leaves by the far corner
                        var toward = me.position - bot.position; float length = toward.Length;
                        r78PassMid = length > 3.2f ? bot.position + toward * ((length - 2.6f) / length) : bot.position;
                    }
                    break;
                default:
                    R78Mark(bot.position + new Point(0, .9f, 0));
                    if (me.species < 0)
                    {
                        // seen in its own shape: the diver goes back and the alien has to hide again
                        r78BotMode = 0; model.TutorialPlace(bot.id, R78ParkEast, new Point(1, 0, 1));
                        R78Back(8, now, model, R78Caught);
                        break;
                    }
                    R78DiverPass(model, me, bot, now);
                    break;
            }
        }
        private void R78BotIdle(Swimmer bot) { r78BotMove = new Point(); r78BotSprint = false; }
        private void R78BotSwim(Swimmer bot, Point to, float pace, bool sprint)
        {
            var d = to - bot.position; float length = d.Length;
            if (length < .05f) { r78BotMove = new Point(); return; }
            d = d * (1f / length); r78BotMove = d * (length < 1f ? pace * Mathf.Max(.35f, length) : pace); r78BotAim = d; r78BotSprint = sprint;
        }
        private void R78DiverPass(AquariumModel model, Swimmer me, Swimmer bot, float now)
        {
            var exit = new Point(-9.6f, 2.2f, -5.6f);
            float total = R78Distance(R78ParkEast, r78PassMid) + R78Distance(r78PassMid, exit) + 1;
            if (r78Leg == 0)
            {
                R78BotSwim(bot, r78PassMid, .62f, false);
                if (R78Distance(bot.position, r78PassMid) < .9f || now - r78LegAt > 9f) { r78Leg = 1; r78LegAt = now; }
                r78Count = Mathf.RoundToInt(100 * Mathf.Clamp01(1 - R78Distance(bot.position, r78PassMid) / Mathf.Max(1, R78Distance(R78ParkEast, r78PassMid))) * .45f);
            }
            else if (r78Leg == 1)
            {
                // the long look
                r78BotMove = new Point(); var look = me.position - bot.position; if (look.Length > .1f) r78BotAim = look.Normalized;
                r78Count = 45 + Mathf.RoundToInt(15 * Mathf.Clamp01((now - r78LegAt) / 2.2f));
                if (now - r78LegAt > 2.2f) { r78Leg = 2; r78LegAt = now; }
            }
            else
            {
                R78BotSwim(bot, exit, .7f, false);
                r78Count = 60 + Mathf.RoundToInt(40 * Mathf.Clamp01(1 - R78Distance(bot.position, exit) / Mathf.Max(1, R78Distance(r78PassMid, exit))));
                if (R78Distance(bot.position, exit) < 1f || now - r78LegAt > 12f) { r78BotMode = 0; R78BotIdle(bot); R78Advance(now, model); }
            }
            r78CountOf = 100;
        }

        // ---- lesson 2: the escape ------------------------------------------------------------------------------
        private void R78Escape(AquariumModel model, Swimmer me, Swimmer bot, float now, float dt)
        {
            var drains = model.Drains; var d = drains[0];
            R78Boost(model, me, dt, 2.4f, 3.6f, 3.6f, 1f);
            if (me.escaped) { r78BotMode = 0; R78BotIdle(bot); R78Plip(true); R78Finish(now, 1f); return; }
            if (tutorialStep == 0)
            {
                R78Mark(d.position + new Point(0, .5f, 0)); R78BotIdle(bot);
                if (R78Distance(me.position, d.position) < 3.2f) R78Advance(now, model);
                return;
            }
            bool loose = AquariumDrainLayout.AllScrewsOut(d), open = d.isOpen;
            int want = !loose ? 1 : !open ? 2 : 3;
            if (want > tutorialStep) R78Advance(now, model);
            else if (want < tutorialStep) R78Back(want, now, model, R78LidShut);
            if (tutorialStep == 1)
            {
                r78Count = AquariumDrainLayout.ScrewsOut(d); r78CountOf = AquariumDrainLayout.ScrewCount;
                int next = -1; for (int s = 0; s < d.screws.Length; s++) if (d.screws[s] < 1) { next = s; break; }
                R78Mark(next >= 0 ? AquariumDrainLayout.ScrewPoint(d, next) + new Point(0, .25f, 0) : d.position);
            }
            else if (tutorialStep == 2) R78Mark(AquariumDrainLayout.LidCenter(d) + new Point(0, .3f, 0));
            else R78Mark(AquariumDrainLayout.Mouth(d) + new Point(0, .3f, 0));
            // The diver: called by the alarm once the lid is open, it swims over (slowly) and pushes the lid back.
            var above = d.position + new Point(-.9f, 1.3f, .7f);
            if (open && r78BotMode == 0 && me.drainTask != DrainTask.Enter) { r78BotMode = 1; r78LegAt = now; }
            if (r78BotMode == 1)
            {
                R78BotSwim(bot, above, .55f, false);
                // close enough to reach the lid (the alien's body may be in the way of the spot above it)
                if (R78Distance(bot.position, above) < .7f || R78Distance(bot.position, AquariumDrainLayout.LidCenter(d)) < AquariumModel.CoverReach - .3f || now - r78LegAt > 14f) { r78BotMode = 2; r78BotNext = now; }
            }
            else if (r78BotMode == 2)
            {
                var look = AquariumDrainLayout.LidCenter(d) - bot.position;
                if (look.Length > AquariumModel.CoverReach - .2f && bot.drainTask == DrainTask.None) R78BotSwim(bot, above, .4f, false); else r78BotMove = new Point();
                if (look.Length > .1f) r78BotAim = look.Normalized;
                // done, or the alien has hold of the lid again (pushing against it would never end): the diver leaves
                if (d.lid <= 0 || me.drainTask == DrainTask.Lid || me.drainTask == DrainTask.Enter) { r78BotMode = 3; r78LegAt = now; }
                else if (bot.drainTask == DrainTask.None && me.drainTask != DrainTask.Enter && now >= r78BotNext)
                { r78BotInteract = true; r78BotDrain = 0; r78BotPart = AquariumModel.DrainLidPart; r78BotNext = now + .6f; }
            }
            else if (r78BotMode == 3)
            {
                R78BotSwim(bot, R78ParkWest, .7f, false);
                // (it only turns back for an open lid once it is home again, so the alien always gets its chance)
                if (R78Distance(bot.position, R78ParkWest) < 1.2f || now - r78LegAt > 16f) { r78BotMode = 0; R78BotIdle(bot); }
            }
            else R78BotIdle(bot);
        }
        // Drain work goes quickly in a lesson (the real times are told in the hints): extra progress on top of the rules' own.
        private static void R78Boost(AquariumModel model, Swimmer s, float dt, float screwSeconds, float lidSeconds, float enterSeconds, float enterCap)
        {
            var drains = model.Drains; int i = s.drainIndex;
            if (s.drainTask == DrainTask.None || i < 0 || i >= drains.Length) return;
            if (s.drainTask == DrainTask.Screw && s.drainScrew >= 0 && s.drainScrew < drains[i].screws.Length)
            { if (drains[i].screws[s.drainScrew] < .985f) drains[i].screws[s.drainScrew] = Mathf.Min(.985f, drains[i].screws[s.drainScrew] + dt / screwSeconds); }
            else if (s.drainTask == DrainTask.Lid) { if (drains[i].lid < .985f) drains[i].lid = Mathf.Min(.985f, drains[i].lid + dt / lidSeconds); }
            else if (s.drainTask == DrainTask.Enter)
            {
                if (s.drainProgress < enterCap - .02f) s.drainProgress = Mathf.Min(enterCap - .02f, s.drainProgress + dt / enterSeconds);
                if (enterCap < 1 && s.drainProgress > enterCap) s.drainProgress = enterCap;
            }
        }

        // ---- lesson 3: the diver -------------------------------------------------------------------------------
        private void R78Diver(AquariumModel model, Swimmer me, Swimmer bot, float now, float dt, ref bool ring, ref Point ringAt)
        {
            var drains = model.Drains; var d = drains[0];
            R78FakeScript(model, bot, now, dt);
            switch (tutorialStep)
            {
                case 0:
                    ring = true; ringAt = R78RingDiver; R78Mark(ringAt);
                    if (R78Distance(me.position, ringAt) < 1.5f) R78Advance(now, model);
                    break;
                case 1:
                    if (me.feedsLeft < AquariumModel.FeedsPerGame) R78Advance(now, model);
                    break;
                case 2:
                    // a nudge when the fake has not been found for a while
                    if (now - r78StepAt > 16f && bot.alive) R78Mark(bot.position + new Point(0, .6f, 0));
                    if (!bot.alive) { R78Advance(now, model); R78Advance(now, model); }
                    else if (bot.harpoonHits >= 1) R78Advance(now, model);
                    break;
                case 3:
                    r78Count = bot.alive ? Mathf.Min(bot.harpoonHits, 1) : 2; r78CountOf = 2;
                    R78Mark(bot.alive ? bot.position + new Point(0, .7f, 0) : d.position);
                    if (!bot.alive) R78Advance(now, model);
                    break;
                case 4:
                    // the alien's lid has to be open for this step, however the chase went
                    if (!r78WasOpen) { if (d.lid < 1) model.TutorialOpenLid(0, bot.id); r78WasOpen = true; }
                    R78Mark(AquariumDrainLayout.LidCenter(d) + new Point(0, .3f, 0));
                    if (d.lid <= 0 && !d.isOpen) R78Advance(now, model);
                    break;
                default:
                    R78Mark(AquariumDrainLayout.LidCenter(d) + new Point(0, .3f, 0));
                    if (d.lid > 0) { r78WasOpen = true; R78Back(4, now, model, R78LidShut); break; }
                    if (d.glued) R78Advance(now, model);
                    break;
            }
        }
        // The fake fish: bobs straight up and down on its own (no fish does), ignores the food; speared once it drops
        // the disguise, bolts for its drain, pulls the last screw and the lid (the alarms) and wedges itself in the hole.
        private void R78FakeScript(AquariumModel model, Swimmer bot, float now, float dt)
        {
            if (!bot.alive) { R78BotIdle(bot); return; }
            var drains = model.Drains; var d = drains[0];
            if (bot.harpoonHits == 0)
            {
                float wave = Mathf.Sin(now * 1.9f);
                var home = R78FakeHome; home.y = 2.7f + 1.5f * wave;
                var to = home - bot.position;
                r78BotMove = new Point(Mathf.Clamp(to.x, -1, 1) * .4f, Mathf.Clamp(to.y * 2f, -1, 1) * .8f, Mathf.Clamp(to.z, -1, 1) * .4f);
                r78BotAim = new Point(Mathf.Cos(now * .5f), 0, Mathf.Sin(now * .5f)); r78BotSprint = false;
                return;
            }
            R78Boost(model, bot, dt, 1.3f, 2.4f, 4f, .85f);
            var hover = d.position + new Point(.5f, .85f, .5f);
            if (bot.drainTask == DrainTask.Enter) { R78BotIdle(bot); return; }
            float away = R78Distance(bot.position, hover);
            if (away > .9f && bot.drainTask == DrainTask.None) { R78BotSwim(bot, hover, 1f, true); return; }
            r78BotMove = new Point(); var look = d.position - bot.position; if (look.Length > .1f) r78BotAim = look.Normalized;
            if (bot.drainTask != DrainTask.None || now < r78BotNext || model.Time < bot.stunnedUntil) return;
            int part;
            if (!AquariumDrainLayout.AllScrewsOut(d)) { part = 0; for (int s = 0; s < d.screws.Length; s++) if (d.screws[s] < 1) { part = s; break; } }
            else if (!d.isOpen) part = AquariumModel.DrainLidPart;
            else part = AquariumModel.DrainHolePart;
            r78BotInteract = true; r78BotDrain = 0; r78BotPart = part; r78BotNext = now + .5f;
        }

        // ---- the yellow ring in the water --------------------------------------------------------------------
        private void R78ShowRing(bool on, Point at, Point from)
        {
            if (!on) { if (r78RingOn && r78Ring != null) r78Ring.SetActive(false); r78RingOn = false; return; }
            if (r78Ring == null)
            {
                r78Ring = new GameObject("R78 tutorial ring"); r78Ring.transform.SetParent(transform, false);
                r78RingLine = r78Ring.AddComponent<LineRenderer>();
                r78RingLine.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { renderQueue = 3100 };
                r78RingLine.useWorldSpace = false; r78RingLine.loop = true; r78RingLine.positionCount = 48;
                r78RingLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r78RingLine.receiveShadows = false;
                for (int i = 0; i < 48; i++) { float a = i * Mathf.PI * 2 / 48; r78RingLine.SetPosition(i, new Vector3(Mathf.Cos(a) * 1.25f, Mathf.Sin(a) * 1.25f, 0)); }
            }
            if (!r78RingOn) { r78Ring.SetActive(true); r78RingOn = true; }
            var p = new Vector3(at.x, at.y, at.z); var toward = p - new Vector3(from.x, from.y, from.z);
            r78Ring.transform.position = p;
            if (toward.sqrMagnitude > .25f) r78Ring.transform.rotation = Quaternion.Slerp(r78Ring.transform.rotation, Quaternion.LookRotation(toward.normalized), .1f);
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 5f);
            r78RingLine.widthMultiplier = .07f + .05f * pulse;
            var c = new Color(1f, .86f, .25f, .75f + .25f * pulse); r78RingLine.startColor = r78RingLine.endColor = c;
        }

        // ---- the HUD -------------------------------------------------------------------------------------------
        private void TutorialCard(AquariumSnapshot state, Swimmer p, float width, float height)
        {
            TitleStyles(); R78Styles();
            float now = Time.unscaledTime;
            var steps = R78Steps[tutorial]; int total = steps.Length;
            // The round clock means nothing in a lesson: its pill reads "튜토리얼" instead.
            var clock = new Rect(width / 2 - 58, 52, 116, 32);
            Shape(clock, new Color(.02f, .1f, .15f, 1f), 16); Shape(clock, Rim, 16, 1.2f);
            Label(clock.x, clock.y, clock.width, clock.height, "튜토리얼", centred);
            if (tutorialDone) { R78ClearScreen(width, height); return; }
            if (pause) return;
            if (!r78Finishing && tutorialStep < total)
            {
                var step = steps[tutorialStep];
                // the objective: [keys] text ......... n / N, with the step pips under it
                float keysW = 0; for (int k = 0; k < step.keys.Length; k++) keysW += Mathf.Max(34, 18 + Measure(keyInk, step.keys[k]).x) + 6;
                float textW = Measure(r78Objective, step.text).x;
                float w = Mathf.Max(430, 36 + keysW + (keysW > 0 ? 8 : 0) + textW + 96), h = 62;
                var card = new Rect(width / 2 - w / 2, 96, w, h);
                var keep = GUI.matrix; float t = now - r78PopAt;
                if (t < .8f) keep = R77PushJelly(new Vector2(card.center.x, card.center.y), t, .72f, .14f, true, -10);
                Card(card, new Color(cyan.r, cyan.g, cyan.b, .75f), true, 16);
                float x = card.x + 18;
                for (int k = 0; k < step.keys.Length; k++) x += R78Key(x, card.y + 11, step.keys[k]) + 6;
                if (keysW > 0) x += 8;
                GUI.Label(new Rect(x, card.y + 7, textW + 8, 36), step.text, r78Objective);
                Label(card.xMax - 96, card.y + 7, 78, 36, Cat(Int(tutorialStep + 1), " / ", Int(total)), r78Pips);
                float pipW = (card.width - 36 - (total - 1) * 4) / total;
                for (int i = 0; i < total; i++)
                    Shape(new Rect(card.x + 18 + i * (pipW + 4), card.y + h - 13, pipW, 4), i < tutorialStep ? cyan : i == tutorialStep ? new Color(1f, .86f, .25f) : Track, 2);
                GUI.matrix = keep;
                // the short hint under it, and the count or gauge when the step has one
                string hintText = step.hint;
                if (tutorial == 1 && tutorialStep == 2 && R78Distance(p.position, R78RingsAlien[2]) < 2.2f && now - r78SprintAt > 1.2f) hintText = R78SprintHint;
                if (tutorial == 3 && tutorialStep == 2 && now - r78StepAt > 16f) hintText = R78FakeHint;
                float hintW = Measure(hint, hintText).x + 32, hx = width / 2 - hintW / 2;
                Shape(new Rect(hx, card.yMax + 8, hintW, 26), new Color(0, .04f, .07f, .62f), 13);
                Label(hx, card.yMax + 8, hintW, 26, hintText, hint);
                if (r78Count >= 0)
                {
                    var bar = new Rect(width / 2 - 110, card.yMax + 42, 220, 22);
                    Shape(bar, new Color(0, .04f, .07f, .7f), 11);
                    Shape(new Rect(bar.x + 3, bar.y + 3, (bar.width - 6) * Mathf.Clamp01(r78Count / (float)Mathf.Max(1, r78CountOf)), bar.height - 6), new Color(1f, .86f, .25f, .95f), 8);
                    if (r78CountOf <= 10) Label(bar.x, bar.y, bar.width, bar.height, Cat(Int(r78Count), " / ", Int(r78CountOf)), centred);
                }
                if (r78MarkOn) R78DrawMark(p, width, height, now);
            }
            // the pop of praise (or the reason a step went back)
            float age = now - r78ToastAt;
            if (age < 1.3f && r78ToastText.Length > 0)
            {
                float a = Mathf.Clamp01((1.3f - age) / .35f);
                float tw = Measure(r78Toast, r78ToastText).x + 56; var toast = new Rect(width / 2 - tw / 2, 214 - 18 * Mathf.Clamp01(age / .25f), tw, 44);
                var keep = R77PushJelly(toast.center, age, .4f, .3f, false);
                var tint = r78ToastGood ? new Color(.45f, 1f, .62f) : Warm;
                Shape(toast, new Color(.02f, .1f, .12f, .9f * a), 22); Shape(toast, new Color(tint.r, tint.g, tint.b, a), 22, 2f);
                var old = GUI.color; GUI.color = new Color(tint.r, tint.g, tint.b, a);
                GUI.Label(toast, r78ToastText, r78Toast); GUI.color = old;
                GUI.matrix = keep;
            }
        }
        private float R78Key(float x, float y, string key)
        {
            float w = Mathf.Max(34, 18 + Measure(keyInk, key).x);
            Shape(new Rect(x, y + 3, w, 29), new Color(.32f, .42f, .48f, .95f), 7);
            var top = new Rect(x, y, w, 26);
            Shape(top, new Color(.9f, .97f, 1f, .97f), 7); Shape(top, new Color(1f, .86f, .25f, .95f), 7, 1.5f);
            GUI.Label(top, key, keyInk);
            return w;
        }
        // A bobbing arrow over the target with its distance; it slides along the screen edge when the target is out of view.
        private void R78DrawMark(Swimmer p, float width, float height, float now)
        {
            var camera = view != null ? view.Camera : null; if (camera == null || !camera.enabled) return;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            var world = new Vector3(r78MarkAt.x, r78MarkAt.y, r78MarkAt.z);
            var sp = camera.WorldToScreenPoint(world);
            float x = sp.x / scale, y = (Screen.height - sp.y) / scale; bool behind = sp.z < .05f;
            if (behind) { x = width - x; y = height - 80; }
            bool off = behind || x < 60 || x > width - 60 || y < 190 || y > height - 130;
            x = Mathf.Clamp(x, 60, width - 60); y = Mathf.Clamp(y, 190, height - 130);
            float bob = off ? 0 : 6 * Mathf.Sin(now * 6f);
            var gold = new Color(1f, .86f, .25f);
            var old = GUI.color;
            GUI.color = new Color(0, .05f, .08f, .8f); GUI.Label(new Rect(x - 30 + 1.5f, y - 46 + bob + 2, 60, 34), off ? "◆" : "▼", r78Mark);
            GUI.color = gold; GUI.Label(new Rect(x - 30, y - 46 + bob, 60, 34), off ? "◆" : "▼", r78Mark);
            GUI.color = old;
            int metres = Mathf.RoundToInt(R78Distance(p.position, r78MarkAt));
            if (metres >= 2)
            {
                var tag = new Rect(x - 24, y - 66 + bob, 48, 20);
                Shape(tag, new Color(0, .05f, .08f, .7f), 10);
                Label(tag.x, tag.y, tag.width, tag.height, Cat(Int(metres), "m"), centred);
            }
        }

        private void R78ClearScreen(float width, float height)
        {
            Panel(new Rect(0, 0, width, height), new Color(0, 0.03f, 0.06f, 0.42f));
            float age = R77Clock(R77KeyOf(tutorial * 10 + r78ClearAt, 2), 0);
            TitleArt(width / 2, height * .3f, "CLEAR!", new Color(.55f, 1f, .6f), age, 1);
            bool all = r78Finished[1] && r78Finished[2] && r78Finished[3];
            bool more = R78Next[tutorial].Length > 0;
            var box = new Rect(width / 2 - 250, height * .3f + 74, 500, more ? 236 : 184);
            float t = age - R77Settled("CLEAR!") + .15f;
            if (t < 0) return;
            var keep = R77PushJelly(new Vector2(box.center.x, box.y), t, .7f, .1f, true, 14);
            Card(box, cyan, true, 16);
            Label(box.x, box.y + 14, box.width, 30, "튜토리얼 완료!", bigCentred);
            Label(box.x, box.y + 46, box.width, 22, TutorialShort[tutorial], smallCentred);
            Label(box.x, box.y + 70, box.width, 24, all ? "세 가지를 모두 마쳤어요. 멀티플레이가 열렸습니다!" : Cat("멀티플레이까지  ", Int(R78DoneCount()), " / 3"), Tinted(centred, all ? cyan : Warm));
            float by = box.y + 106;
            if (more)
            {
                int next = tutorial + 1;
                if (Button(new Rect(box.x + 24, by, box.width - 48, 46), R78Next[tutorial], true)) { GUI.matrix = keep; StartTutorial(next); return; }
                by += 56;
            }
            float hw = (box.width - 48 - 12) / 2; int again = tutorial;
            if (Button(new Rect(box.x + 24, by, hw, 42), "메인 메뉴로", !more)) { GUI.matrix = keep; Leave(); return; }
            if (Button(new Rect(box.x + 24 + hw + 12, by, hw, 42), "한 번 더")) { GUI.matrix = keep; StartTutorial(again); return; }
            GUI.matrix = keep;
        }
    }
}
