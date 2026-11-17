using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R81: the fish guide is read on the changing room's computer (it replaced the wall posters).
    // The diver waiting in the room looks at the monitor from nearby and gets the keycap prompt [E] 어종 도감 보기;
    // E brings the monitor's screen up over the view: one species per page (picture, name, tag, the R70b trait
    // sentences and its feeding line), A / D or the arrow keys or the on-screen arrows turn the page, E or Esc
    // closes it. The countdown to the dive stays on the screen and the view closes by itself when the wait ends.
    // All of it is this client's own presentation (nothing is sent): the room's monitor (AquariumGuideComputer)
    // shows the page being read, and its idle title page otherwise. The texts are the lobby guide's own
    // (SpeciesName, SpeciesTrait, FeedText), kept in one place.
    public sealed partial class AquariumGame
    {
        private AquariumGuideComputer guideComputer;
        private GuidePage[] guidePages;
        private string[] guidePageText;
        private string[] guideTraitText;
        private float[] guideTraitHeight;
        private bool guideOpen, guideNear;
        private int guidePage, guideTurn;
        private float guideOpenedAt = -100, guideTurnedAt = -100;
        private GUIStyle guideHead, guideName, guideTagInk, guideTrait, guideFeed, guideFeedWarm, guidePageStyle, guideKeyHint, guideCount;
        private const float GuideW = 1060, GuideH = 596, GuideTextX = 524, GuideTextW = 470, GuideReach = 3.6f;
        private const string GuidePromptText = "어종 도감 보기", GuideTitleText = "어종 도감", GuidePrev = "이전 페이지", GuideNext = "다음 페이지", GuideClose = "닫기";
        private static readonly Color GuideBack = new Color(.04f, .153f, .2f), GuideBar = new Color(.06f, .23f, .295f);

        // One page per species, in the catalogue's order (every species there is).
        private GuidePage[] GuidePages()
        {
            if (guidePages != null) return guidePages;
            var all = SpeciesCatalog();
            var pages = new GuidePage[all.Length]; guidePageText = new string[all.Length];
            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i]; int id = s.speciesId; bool keeper = AquariumFishTraits.IgnoresFeed(id);
                pages[i] = new GuidePage
                {
                    image = GuideImage(id), title = SpeciesName(s), tint = s.tint, warn = keeper,
                    tag = AquariumFishTraits.Habit(id) == FishHabit.School ? "떼 지어 다님" : keeper ? "단독, 사육사 먹이만" : "단독",
                    trait = SpeciesTrait(s), feed = FeedText(id)
                };
                guidePageText[i] = (i + 1) + " / " + all.Length;
            }
            return guidePages = pages;
        }

        // Update: who may use the computer (the local diver waiting in the room), the prompt's range and the keys.
        private void UpdateGuideComputer(Swimmer p)
        {
            bool can = guidePages != null && guidePages.Length > 0 && !automated && session.Connected && session.State != null
                && session.State.phase == RoundPhase.Playing && !session.IsSpectator && p != null && AquariumLockerRoom.Waiting(p, session.State.time);
            if (!can)
            {
                guideNear = false;
                if (guideOpen) CloseGuide(false);   // the wait is over (or the round is): the dive goes ahead
                else if (guideComputer != null) guideComputer.Show(-1);
                return;
            }
            if (pause || showRules || tutorialDone) { guideNear = false; return; }
            if (guideOpen)
            {
                if (Input.GetKeyDown(KeyCode.E)) CloseGuide(true);
                else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) TurnGuide(-1, true);
                else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) TurnGuide(1, true);
                return;
            }
            var cam = view.Camera.transform; var to = AquariumLockerRoom.ScreenCentre - cam.position;
            guideNear = to.x * to.x + to.z * to.z < GuideReach * GuideReach && Vector3.Dot(cam.forward, to.normalized) > .7f;
            if (guideNear && Input.GetKeyDown(KeyCode.E)) OpenGuide();
        }

        private void OpenGuide()
        {
            if (guidePages == null || guidePages.Length == 0) return;
            guideOpen = true; guideNear = false; guideTurn = 0; guideOpenedAt = guideTurnedAt = Time.unscaledTime;
            guidePage = Mathf.Clamp(guidePage, 0, guidePages.Length - 1);
            UiClick();
            if (guideComputer != null) guideComputer.Show(guidePage);
        }
        private void CloseGuide(bool sound)
        {
            if (!guideOpen) return;
            guideOpen = false;
            if (sound) UiClick();
            if (guideComputer != null) guideComputer.Show(-1);
        }
        private void TurnGuide(int direction, bool sound)
        {
            int n = guidePages.Length; if (n < 2) return;
            guidePage = ((guidePage + direction) % n + n) % n; guideTurn = direction; guideTurnedAt = Time.unscaledTime;
            if (sound) UiClick();
            if (guideComputer != null) guideComputer.Show(guidePage);
        }

        private void GuideStyles()
        {
            if (guideHead != null) return;
            guideHead = Line1(22, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
            guideName = Line1(44, Color.white, FontStyle.Bold, TextAnchor.UpperLeft);
            guideTagInk = Line1(17, new Color(.02f, .13f, .17f), FontStyle.Bold, TextAnchor.MiddleCenter);
            guideTrait = Text(24, new Color(.88f, .95f, .97f)); guideTrait.alignment = TextAnchor.UpperLeft;
            guideFeed = Line1(19, cyan, FontStyle.Normal, TextAnchor.MiddleLeft);
            guideFeedWarm = Line1(19, Warm, FontStyle.Normal, TextAnchor.MiddleLeft);
            guidePageStyle = Line1(18, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            guideKeyHint = Line1(15, new Color(.78f, .9f, .94f), FontStyle.Normal, TextAnchor.MiddleLeft);
            guideCount = Line1(15, new Color(1f, .82f, .45f), FontStyle.Bold, TextAnchor.MiddleCenter);
            // the trait sentences wrap: their height is measured once for the column's width
            guideTraitHeight = new float[guidePages.Length];
            guideTraitText = new string[guidePages.Length];
            for (int i = 0; i < guidePages.Length; i++)
            {
                guideTraitText[i] = WrapWords(guidePages[i].trait, guideTrait, GuideTextW - 6);   // lines break between words, not inside them
                measureContent.text = guideTraitText[i]; guideTraitHeight[i] = guideTrait.CalcHeight(measureContent, GuideTextW + 8);
            }
        }
        private string WrapWords(string text, GUIStyle style, float width)
        {
            var sb = new System.Text.StringBuilder(); float line = 0, space = Measure(style, "가 가").x - Measure(style, "가가").x;
            foreach (var word in (text ?? "").Split(' '))
            {
                float w = Measure(style, word).x - style.padding.horizontal;
                if (line > 0) { if (line + space + w > width) { sb.Append('\n'); line = 0; } else { sb.Append(' '); line += space; } }
                sb.Append(word); line += w;
            }
            return sb.ToString();
        }
        private float GuideKeyWidth(string key) { return Mathf.Max(24, 12 + Measure(keyInk, key).x); }

        // OnGUI, after the play HUD (which is drawn disabled under the open guide): the prompt, or the monitor.
        private void GuideComputerGUI(float width, float height)
        {
            GUI.enabled = !(showRules || roomDialog != RoomDialog.None);
            if (pause) return;
            if (!guideOpen)
            {
                if (guideNear) KeyPrompt(width / 2, height / 2 + 30, "E", GuidePromptText);
                return;
            }
            var state = session.State; var local = Local();
            if (state == null || local == null || guidePages == null) return;
            GuideStyles();
            float now = Time.unscaledTime, open = now - guideOpenedAt;
            Panel(new Rect(0, 0, width, height), new Color(0, .025f, .045f, .8f * Mathf.Clamp01(open / .15f)));
            // the monitor: its 16:9 screen as large as the view allows, a cream frame with a chin, a dark bezel
            float sw = Mathf.Min(width - 150, GuideW), sh = sw * GuideH / GuideW;
            if (sh > height - 110) { sh = height - 110; sw = sh * GuideW / GuideH; }
            float k = sw / GuideW, sx = (width - sw) / 2, sy = (height - (sh + 66 * k)) / 2 + 22 * k;
            var keep = GUI.matrix;
            if (open < .8f) R77PushJelly(new Vector2(width / 2, height / 2), open, .88f, .045f, true);
            var shell = new Rect(sx - 22 * k, sy - 22 * k, sw + 44 * k, sh + 66 * k);
            Shape(new Rect(shell.x + 3, shell.y + 9, shell.width, shell.height), new Color(0, 0, 0, .4f), 30 * k);
            Shape(shell, new Color(.94f, .93f, .89f), 28 * k);
            Shape(new Rect(shell.x, shell.yMax - 34 * k, shell.width, 34 * k), new Color(.86f, .85f, .8f, .55f), 28 * k);
            Shape(new Rect(sx - 8 * k, sy - 8 * k, sw + 16 * k, sh + 16 * k), new Color(.07f, .1f, .12f), 14 * k);
            float cy = sy + sh + 26 * k;
            Shape(new Rect(width / 2 - 60 * k, cy - 9 * k, 120 * k, 18 * k), new Color(.16f, .62f, .69f), 9 * k);
            Shape(new Rect(sx + sw - 22 * k, cy - 5 * k, 10 * k, 10 * k), new Color(.49f, 1f, .69f), 5 * k);
            for (int i = 0; i < 3; i++) Shape(new Rect(sx + sw - 62 * k - i * 22 * k, cy - 6 * k, 12 * k, 12 * k), new Color(.72f, .76f, .77f), 6 * k);
            var popped = GUI.matrix;
            GUI.matrix = popped * Matrix4x4.TRS(new Vector3(sx, sy, 0), Quaternion.identity, new Vector3(k, k, 1));
            GuideScreen(state, local, now);
            GUI.matrix = keep;
        }

        // The screen's own 1060 x 596 canvas.
        private void GuideScreen(AquariumSnapshot state, Swimmer local, float now)
        {
            int n = guidePages.Length, index = Mathf.Clamp(guidePage, 0, n - 1); var page = guidePages[index];
            bool repaint = Event.current.type == EventType.Repaint;
            Shape(new Rect(0, 0, GuideW, GuideH), GuideBack, 8);
            Shape(new Rect(0, 0, GuideW, 56), GuideBar, 8); Shape(new Rect(0, 28, GuideW, 28), GuideBar, 0);
            Shape(new Rect(0, 56, GuideW, 2), new Color(cyan.r, cyan.g, cyan.b, .7f), 0);
            Icon(22, 16, HudIcon.Fish, cyan, 24);
            Label(56, 0, Measure(guideHead, GuideTitleText).x + 8, 56, GuideTitleText, guideHead);
            // top right: [E] [Esc] 닫기 (also a button), and the countdown to the dive beside it
            float closeTextW = Measure(guideKeyHint, GuideClose).x, closeW = 12 + GuideKeyWidth("E") + 6 + GuideKeyWidth("Esc") + 8 + closeTextW + 16;
            var close = new Rect(GuideW - 16 - closeW, 12, closeW, 32);
            bool closeHover = GUI.enabled && close.Contains(Event.current.mousePosition);
            Shape(close, closeHover ? new Color(.13f, .32f, .4f, .95f) : new Color(.04f, .15f, .2f, .9f), 16);
            Shape(close, closeHover ? new Color(.6f, .95f, 1f, .8f) : Rim, 16, 1.2f);
            float cx = close.x + 12; cx += Key(cx, close.y + 5, "E") + 6; cx += Key(cx, close.y + 5, "Esc") + 8;
            Label(cx, close.y, closeTextW + 6, 32, GuideClose, guideKeyHint);
            float left = Mathf.Max(0, local.entryAt - AquariumDiveEntry.Seconds - state.time);
            string countText = Cat("입수까지 ", Int(Mathf.CeilToInt(left)), "초");
            float countW = Measure(guideCount, countText).x + 30; var count = new Rect(close.x - 12 - countW, 12, countW, 32);
            Shape(count, new Color(Warm.r, Warm.g, Warm.b, left <= 5 ? .3f : .14f), 16); Shape(count, new Color(Warm.r, Warm.g, Warm.b, .7f), 16, 1.2f);
            Label(count.x, count.y, count.width, 32, countText, guideCount);

            // the page: it slides in from the side it was turned to, and the picture pops
            float turn = now - guideTurnedAt, ease = guideTurn == 0 ? 1 : 1 - Mathf.Pow(1 - Mathf.Clamp01(turn / .22f), 3);
            var keep = GUI.matrix; var oldColor = GUI.color;
            GUI.matrix = keep * Matrix4x4.Translate(new Vector3(guideTurn * 70 * (1 - ease), 0, 0));
            GUI.color = new Color(1, 1, 1, .1f + .9f * ease);
            var tint = page.tint; tint.a = 1;
            var card = new Rect(64, 80, 420, 400);
            Shape(card, Color.Lerp(GuideBar, tint, .3f), 20);
            Shape(card, Color.Lerp(tint, Color.white, .3f), 20, 2);
            if (page.image != null && repaint)
            {
                var slid = GUI.matrix;
                if (guideTurn != 0 && turn < .7f) R77PushJelly(card.center, turn, .84f, .09f, true);
                GUI.DrawTexture(new Rect(card.x + 18, card.y + 18, card.width - 36, card.height - 36), page.image, ScaleMode.ScaleToFit, true);
                GUI.matrix = slid;
            }
            float y = 84; var nameSize = Measure(guideName, page.title);
            Label(GuideTextX, y, nameSize.x + 8, nameSize.y, page.title, guideName); y += nameSize.y + 10;
            var tag = new Rect(GuideTextX, y, Measure(guideTagInk, page.tag).x + 30, 32);
            Shape(tag, page.warn ? Warm : cyan, 16); Label(tag.x, tag.y, tag.width, tag.height, page.tag, guideTagInk); y += 32 + 16;
            Shape(new Rect(GuideTextX, y, GuideTextW, 1.5f), new Color(.55f, .93f, 1f, .28f), 0); y += 16;
            float traitH = guideTraitHeight[index];
            Label(GuideTextX, y, GuideTextW + 8, traitH + 2, guideTraitText[index], guideTrait); y += traitH + 18;
            Icon(GuideTextX, y + 1, HudIcon.Feed, page.warn ? Warm : cyan, 24);
            Label(GuideTextX + 32, y, Measure(guideFeed, page.feed).x + 6, 26, page.feed, page.warn ? guideFeedWarm : guideFeed);
            GUI.matrix = keep; GUI.color = oldColor;

            if (GuideArrow(new Rect(10, 238, 44, 84), -1)) TurnGuide(-1, false);   // (the button itself clicks)
            if (GuideArrow(new Rect(GuideW - 54, 238, 44, 84), 1)) TurnGuide(1, false);

            // bottom: page dots and n / N in the middle, the keys on both sides
            float by = GuideH - 70;
            Shape(new Rect(0, by, GuideW, 1), Line, 0);
            const float dot = 9, current = 26, gap = 8;
            float dx = GuideW / 2 - ((n - 1) * (dot + gap) + current) / 2;
            for (int i = 0; i < n; i++)
            {
                float w = i == index ? current : dot;
                Shape(new Rect(dx, by + 14, w, dot), i == index ? cyan : new Color(.35f, .6f, .68f, .7f), dot / 2); dx += w + gap;
            }
            Label(GuideW / 2 - 70, by + 30, 140, 28, guidePageText[index], guidePageStyle);
            float kx = 22, ky = by + 24;
            kx += Key(kx, ky, "A") + 5; kx += Key(kx, ky, "←") + 9;
            Label(kx, ky - 1, Measure(guideKeyHint, GuidePrev).x + 6, 24, GuidePrev, guideKeyHint);
            float nextW = Measure(guideKeyHint, GuideNext).x;
            kx = GuideW - 22 - (nextW + 9 + GuideKeyWidth("D") + 5 + GuideKeyWidth("→"));
            Label(kx, ky - 1, nextW + 6, 24, GuideNext, guideKeyHint); kx += nextW + 9;
            kx += Key(kx, ky, "D") + 5; Key(kx, ky, "→");
            if (UiButton(close, GUIContent.none, GUIStyle.none)) CloseGuide(false);
        }

        // A page-turn button with a chevron drawn from two bars (direction -1 left, +1 right).
        private bool GuideArrow(Rect r, int direction)
        {
            bool hover = GUI.enabled && r.Contains(Event.current.mousePosition);
            Shape(r, hover ? new Color(.13f, .32f, .4f, .95f) : new Color(.06f, .19f, .26f, .9f), 14);
            Shape(r, hover ? new Color(.6f, .95f, 1f, .8f) : Rim, 14, 1.4f);
            if (Event.current.type == EventType.Repaint)
            {
                var keep = GUI.matrix; var tip = new Vector3(r.center.x + direction * 7, r.center.y, 0);
                var ink = hover ? Color.white : new Color(.8f, .96f, 1f);
                var bar = direction < 0 ? new Rect(tip.x - 2, tip.y - 2.5f, 21, 5) : new Rect(tip.x - 19, tip.y - 2.5f, 21, 5);
                for (int side = -1; side <= 1; side += 2)
                {
                    GUI.matrix = keep * Matrix4x4.TRS(tip, Quaternion.Euler(0, 0, side * 48), Vector3.one) * Matrix4x4.Translate(-tip);
                    Shape(bar, ink, 2.5f);
                }
                GUI.matrix = keep;
            }
            return UiButton(r, GUIContent.none, GUIStyle.none);
        }
    }
}
