using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    // R81: one page of the fish guide on the changing room's computer (the texts come from AquariumGame).
    public struct GuidePage { public Texture2D image; public string title, tag, trait, feed; public Color tint; public bool warn; }

    public static partial class AquariumEnvironment
    {
        // R81: the fish-guide computer on the changing room's front wall (it replaced R60's wall posters): a long desk
        // with drawers, a tower with a lit side window, a big wall monitor on a chunky cream frame, keyboard, mouse,
        // a mug, books, a plant and a stool tucked under the desk. The screen is unlit geometry and lettering built
        // once: an idle title page and one page per species, switched by AquariumGuideComputer.Show.
        // Everything is in room space (AquariumLockerRoom.Desk / Screen*); the viewer looks toward -z, so the screen's
        // left edge is at +x.
        private static AquariumGuideComputer GuideComputer(Transform root, Geo paint, Geo metal, Geo glow, Func<string, float, float, Material> make, IList<GuidePage> pages)
        {
            var I = Quaternion.identity; var up = Vector3.up;
            var desk = AquariumLockerRoom.Desk;
            float wz = desk.yMin, fz = desk.yMax, dz = (wz + fz) / 2, dw = desk.width, top = AquariumLockerRoom.DeskTop;
            float sw = AquariumLockerRoom.ScreenWidth, sh = AquariumLockerRoom.ScreenHeight, sy = AquariumLockerRoom.ScreenY, sz = AquariumLockerRoom.ScreenZ;
            Color wood = C(0xc79a63), woodDark = C(0x94683c), cream = C(0xf0ede4), steel = C(0xb8c1c4), dark = C(0x26333a), teal = C(0x2a9db0), coral = C(0xeb6f6f), cable = C(0x1b2327);

            // ---- desk ------------------------------------------------------------------------------------------
            paint.Box(new Vector3(0, top - .025f, dz + .01f), new Vector3(dw, .05f, desk.height - .04f), wood, I);
            paint.Box(new Vector3(0, top - .075f, fz - .06f), new Vector3(dw - .08f, .05f, .03f), woodDark, I);
            paint.Box(new Vector3(0, top - .26f, wz + .05f), new Vector3(dw - .16f, .42f, .025f), C(0xe6ecec), I);
            foreach (float lx in new[] { -dw / 2 + .07f, dw / 2 - .07f })
            {
                foreach (float lz in new[] { wz + .09f, fz - .09f }) metal.Box(new Vector3(lx, (top - .05f) / 2, lz), new Vector3(.05f, top - .05f, .05f), steel, I);
                metal.Box(new Vector3(lx, .12f, dz), new Vector3(.04f, .04f, desk.height - .2f), steel, I);
            }
            // drawers (left as seen)
            float ux = dw / 2 - .42f;
            paint.Box(new Vector3(ux, .04f, dz + .02f), new Vector3(.5f, .08f, .5f), dark, I);
            paint.Box(new Vector3(ux, .4f, dz + .02f), new Vector3(.56f, .64f, .56f), cream, I);
            for (int i = 0; i < 3; i++)
            {
                float y = .61f - i * .2f;
                paint.Box(new Vector3(ux, y, dz + .303f), new Vector3(.5f, .17f, .012f), C(0xdde6e6), I);
                metal.Box(new Vector3(ux, y + .03f, dz + .316f), new Vector3(.16f, .022f, .022f), teal, I);
            }
            // tower (right as seen): dark case, front plate with a power ring, drive slot and vents, a lit side window
            float tx = -dw / 2 + .4f, tz = dz + .02f, tf = tz + .25f;
            metal.Box(new Vector3(tx, .32f, tz), new Vector3(.24f, .52f, .5f), dark, I);
            foreach (float s in new[] { -1f, 1f }) metal.Box(new Vector3(tx + s * .08f, .03f, tz), new Vector3(.04f, .06f, .44f), C(0x151c20), I);
            paint.Box(new Vector3(tx, .32f, tf + .004f), new Vector3(.2f, .48f, .008f), C(0x33434b), I);
            metal.Box(new Vector3(tx, .5f, tf + .01f), new Vector3(.15f, .022f, .006f), C(0x0f1518), I);
            glow.Cylinder(new Vector3(tx, .43f, tf + .008f), new Vector3(tx, .43f, tf + .013f), .024f, .024f, C(0x59e6dd), 14);
            for (int i = 0; i < 5; i++) metal.Box(new Vector3(tx, .14f + i * .035f, tf + .01f), new Vector3(.14f, .012f, .006f), C(0x10181c), I);
            glow.Box(new Vector3(tx + .121f, .32f, tz), new Vector3(.004f, .36f, .36f), C(0x1c6c7c), I);
            glow.Box(new Vector3(tx + .123f, .32f, tz - .06f), new Vector3(.003f, .2f, .03f), C(0x59e6dd), I);
            // cables: tower to the desk's back edge and up behind the monitor, keyboard and mouse leads
            metal.Cylinder(new Vector3(tx, .58f, wz + .12f), new Vector3(tx, top - .05f, wz + .06f), .012f, .012f, cable, 6);
            metal.Cylinder(new Vector3(tx, top, wz + .05f), new Vector3(-.55f, top + .2f, wz + .04f), .012f, .012f, cable, 6);
            metal.Cylinder(new Vector3(-.22f, top + .008f, wz + .3f), new Vector3(-.12f, top + .008f, fz - .3f), .006f, .006f, cable, 6);

            // ---- monitor ---------------------------------------------------------------------------------------
            float shellBottom = sy - sh / 2 - .125f, shellTop = sy + sh / 2 + .095f, shellY = (shellBottom + shellTop) / 2, shellH = shellTop - shellBottom;
            // a teal panel on the wall behind it, so the cream frame stands out from the cream wall
            paint.Box(new Vector3(0, 1.76f, wz + .012f), new Vector3(sw + .9f, 1.92f, .024f), C(0x1f7f8f), I);
            paint.Box(new Vector3(0, 2.735f, wz + .016f), new Vector3(sw + .96f, .03f, .032f), C(0x166472), I);
            metal.Box(new Vector3(0, sy, wz + .04f), new Vector3(.8f, .55f, .08f), C(0x4a5961), I);                     // wall bracket
            paint.Box(new Vector3(0, shellY, wz + .1f), new Vector3(sw - .3f, shellH - .3f, .1f), C(0xd9d6cc), I);     // back
            paint.Box(new Vector3(0, shellY, wz + .135f), new Vector3(sw + .2f, shellH, .13f), cream, I);              // frame
            paint.Box(new Vector3(0, sy, wz + .2f), new Vector3(sw + .07f, sh + .07f, .012f), C(0x12191d), I);         // bezel
            float chinY = shellBottom + .062f, cf = wz + .2f;
            paint.Box(new Vector3(0, chinY, cf + .003f), new Vector3(.36f, .062f, .006f), teal, I);                    // name plate with a little fish
            paint.Cylinder(new Vector3(.025f, chinY, cf + .006f), new Vector3(.025f, chinY, cf + .009f), .021f, .021f, cream, 12);
            paint.Triangle(new Vector3(0, chinY, cf + .0085f), new Vector3(-.04f, chinY + .021f, cf + .0085f), new Vector3(-.04f, chinY - .021f, cf + .0085f), cream);
            glow.Cylinder(new Vector3(-sw / 2 + .06f, chinY, cf), new Vector3(-sw / 2 + .06f, chinY, cf + .006f), .014f, .014f, C(0x7dffb0), 10);   // power light
            for (int i = 0; i < 3; i++) { float bx = -sw / 2 + .16f + i * .07f; metal.Cylinder(new Vector3(bx, chinY, cf), new Vector3(bx, chinY, cf + .008f), .016f, .016f, steel, 10); }
            for (int i = 0; i < 7; i++) metal.Box(new Vector3(sw / 2 - .08f - i * .035f, chinY, cf + .002f), new Vector3(.014f, .05f, .004f), C(0xb9b6ac), I);   // speaker slots
            metal.Box(new Vector3(0, shellTop + .025f, wz + .15f), new Vector3(.13f, .05f, .06f), dark, I);          // webcam
            glow.Cylinder(new Vector3(0, shellTop + .025f, wz + .18f), new Vector3(0, shellTop + .025f, wz + .183f), .013f, .013f, C(0x2c8fa6), 10);
            paint.Box(new Vector3(sw / 2 + .05f, sy + .38f, cf + .003f), new Vector3(.085f, .085f, .004f), C(0xf2c94c), Quaternion.Euler(0, 0, 6));    // sticky notes
            paint.Box(new Vector3(sw / 2 + .052f, sy + .25f, cf + .003f), new Vector3(.08f, .08f, .004f), C(0xf49ac1), Quaternion.Euler(0, 0, -5));
            metal.Box(new Vector3(0, (top + shellBottom) / 2, wz + .13f), new Vector3(.3f, shellBottom - top + .02f, .07f), C(0x9aa6aa), I);            // neck
            paint.Box(new Vector3(0, top + .012f, wz + .2f), new Vector3(.9f, .024f, .3f), C(0xdfe5e4), I);                                            // foot

            // ---- on the desk -----------------------------------------------------------------------------------
            var tilt = Quaternion.Euler(5, 0, 0); var kc = new Vector3(0, top + .022f, fz - .2f);
            paint.Box(kc, new Vector3(.68f, .024f, .23f), C(0xe4e9ea), tilt);
            for (int r = 0; r < 4; r++) for (int c = 0; c < 13; c++)
            {
                if (r == 3 && c >= 4 && c <= 8)
                {
                    if (c == 6) paint.Box(kc + tilt * new Vector3(0, .017f, .0735f), new Vector3(5 * .048f - .008f, .012f, .04f), C(0xfafcfc), tilt);   // space bar
                    continue;
                }
                var key = r == 0 && c == 0 ? coral : c == 12 && (r == 1 || r == 2) ? teal : (r * 3 + c) % 5 == 0 ? C(0xdcecee) : C(0xfafcfc);
                paint.Box(kc + tilt * new Vector3((6 - c) * .048f, .017f, (r - 1.5f) * .049f), new Vector3(.04f, .012f, .04f), key, tilt);
            }
            paint.Box(new Vector3(-.62f, top + .003f, fz - .2f), new Vector3(.3f, .006f, .25f), C(0x1f7f8f), I);       // mouse pad
            paint.Ellipsoid(new Vector3(-.62f, top + .024f, fz - .19f), new Vector3(.036f, .022f, .058f), C(0xf4f6f6), 10, 6, 0);
            paint.Box(new Vector3(-.62f, top + .045f, fz - .215f), new Vector3(.008f, .006f, .02f), coral, I);
            metal.Cylinder(new Vector3(-.62f, top + .012f, fz - .245f), new Vector3(-.5f, top + .008f, wz + .3f), .005f, .005f, cable, 6);
            paint.Cylinder(new Vector3(.72f, top, fz - .22f), new Vector3(.72f, top + .11f, fz - .22f), .042f, .048f, coral, 12, true);   // mug
            paint.Box(new Vector3(.78f, top + .058f, fz - .22f), new Vector3(.03f, .06f, .016f), coral, I);
            paint.Box(new Vector3(1.14f, top + .02f, fz - .27f), new Vector3(.3f, .04f, .2f), teal, Quaternion.Euler(0, 8, 0));         // books
            paint.Box(new Vector3(1.13f, top + .055f, fz - .27f), new Vector3(.27f, .03f, .19f), C(0xf2c94c), Quaternion.Euler(0, -6, 0));
            paint.Cylinder(new Vector3(-1.3f, top, wz + .3f), new Vector3(-1.3f, top + .11f, wz + .3f), .05f, .065f, C(0xd4593f), 10);   // plant
            paint.Ellipsoid(new Vector3(-1.3f, top + .21f, wz + .3f), new Vector3(.07f, .115f, .07f), C(0x4fae6e), 8, 5, 0);
            paint.Ellipsoid(new Vector3(-1.245f, top + .17f, wz + .33f), new Vector3(.05f, .08f, .05f), C(0x3f9a5e), 8, 5, 0);
            paint.Ellipsoid(new Vector3(-1.355f, top + .16f, wz + .28f), new Vector3(.05f, .075f, .05f), C(0x62c07f), 8, 5, 0);
            // stool, tucked under the desk
            var st = new Vector3(.12f, 0, fz - .1f);
            metal.Cylinder(st + up * .005f, st + up * .04f, .2f, .15f, C(0x7f8b90), 14);
            metal.Cylinder(st + up * .04f, st + up * .43f, .026f, .026f, steel, 8);
            paint.Cylinder(st + up * .43f, st + up * .5f, .17f, .19f, C(0x2a8a8f), 16);
            paint.Cylinder(st + up * .5f, st + up * .52f, .19f, .16f, C(0x34a0a3), 16);

            // ---- the screen's glow on the divers (the room's own surfaces get it from the shader) ----------------
            var lamp = new GameObject("Guide computer glow").AddComponent<Light>(); lamp.transform.SetParent(root, false); lamp.transform.localPosition = new Vector3(0, sy, sz + .7f);
            lamp.type = LightType.Point; lamp.color = new Color(.55f, .88f, 1f); lamp.intensity = .8f; lamp.range = 3.8f; lamp.shadows = LightShadows.None;

            // ---- screen ----------------------------------------------------------------------------------------
            var screen = new GameObject("Fish guide computer | screen"); screen.transform.SetParent(root, false);
            var computer = screen.AddComponent<AquariumGuideComputer>(); computer.glow = lamp; computer.glowIntensity = lamp.intensity;
            var screenMat = make("monitor screen", 0, 0); screenMat.SetFloat("_Unlit", 1);
            Color bg = C(0x0a2733), bar = C(0x0f3b4b), accent = C(0x61f5e6), white = Color.white, soft = C(0xdcecf0), muted = C(0x9fd8e0), ink = C(0x06222c), warm = C(0xffc24d);
            const int Px = 72;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Material lettering = null;
            int count = pages == null ? 0 : pages.Count;

            // u, v: 0..1 across and up the screen as the viewer sees it; layers stack toward the viewer.
            Func<float, float, int, Vector3> S = (u, v, layer) => new Vector3(sw / 2 - u * sw, sy - sh / 2 + v * sh, sz + layer * .0012f);
            Action<Geo, float, float, float, float, int, Color> Fill = (g, u0, v0, u1, v1, layer, c) =>
            {
                int n = g.Count;
                g.V(S(u0, v0, layer), c, new Vector2(0, 0)); g.V(S(u1, v0, layer), c, new Vector2(1, 0)); g.V(S(u1, v1, layer), c, new Vector2(1, 1)); g.V(S(u0, v1, layer), c, new Vector2(0, 1));
                g.Q(n, n + 1, n + 2, n + 3);
            };
            Func<Transform, string, float, float, float, Color, TextAnchor, FontStyle, TextMesh> Write = (parent, text, u, v, em, color, anchor, style) =>
            {
                var go = new GameObject("text " + (text.Length > 12 ? text.Substring(0, 12) : text)); go.transform.SetParent(parent, false);
                go.transform.localPosition = S(u, v, 6); go.transform.localRotation = Quaternion.Euler(0, 180, 0);
                var label = go.AddComponent<TextMesh>(); if (font != null) label.font = font;
                label.richText = false; label.text = text; label.fontSize = Px; label.characterSize = em * 10f / Px; label.anchor = anchor; label.alignment = TextAlignment.Left; label.color = color; label.fontStyle = style;
                var renderer = go.GetComponent<MeshRenderer>(); renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                if (lettering == null)
                {
                    var source = font != null ? font.material : renderer.sharedMaterial;
                    lettering = new Material(Shader.Find("Aquarium/World Label")) { name = "Guide computer lettering" };
                    if (source != null) lettering.mainTexture = source.mainTexture;
                }
                renderer.sharedMaterial = lettering;
                if (font != null) font.RequestCharactersInTexture(text, Px, style);
                return label;
            };
            Func<string, float, FontStyle, float> Width = (text, em, style) =>
            {
                if (font == null) return text.Length * em * .9f;
                font.RequestCharactersInTexture(text, Px, style);
                float sum = 0; CharacterInfo info;
                foreach (char ch in text) sum += font.GetCharacterInfo(ch, out info, Px, style) ? info.advance : Px;
                return sum * em / Px;
            };
            Func<string, float, float, string> Wrap = (text, em, max) =>
            {
                var sb = new StringBuilder(); float line = 0, space = Width(" ", em, FontStyle.Normal);
                foreach (var word in text.Split(' '))
                {
                    float w = Width(word, em, FontStyle.Normal);
                    if (line > 0) { if (line + space + w > max) { sb.Append('\n'); line = 0; } else { sb.Append(' '); line += space; } }
                    sb.Append(word); line += w;
                }
                return sb.ToString();
            };
            Action<Transform, Material, float, float, float, float, Color, float, string> Picture = (parent, material, u0, v0, u1, v1, back, aspect, name) =>
            {
                float bw = (u1 - u0) * sw, bh = (v1 - v0) * sh, w = Mathf.Min(bw, bh * aspect), h = w / aspect;
                float cu = (u0 + u1) / 2, cv = (v0 + v1) / 2, du = w / sw / 2, dv = h / sh / 2;
                var g = new Geo(); Fill(g, cu - du, cv - dv, cu + du, cv + dv, 4, back); g.Emit(name, parent, material, false);
            };
            var pictures = new Material[count]; var aspects = new float[count];
            for (int i = 0; i < count; i++)
            {
                var image = pages[i].image; if (image == null) continue;
                pictures[i] = make("guide picture " + pages[i].title, 0, 0); pictures[i].SetFloat("_Unlit", 1); pictures[i].SetFloat("_Tex", 1); pictures[i].mainTexture = image;
                aspects[i] = image.height > 0 ? image.width / (float)image.height : 1;
            }

            // idle page: the title, a line about the guide, the blinking key hint and a row of every species
            {
                var page = new GameObject("Idle page"); page.transform.SetParent(screen.transform, false); computer.attract = page;
                var g = new Geo(); Fill(g, 0, 0, 1, 1, 0, bg);
                Fill(g, 0, .925f, 1, 1, 1, bar); Fill(g, 0, 0, 1, .045f, 1, bar);
                float[] bubbles = { .08f, .62f, .05f, .9f, .74f, .035f, .17f, .84f, .025f, .82f, .5f, .02f, .93f, .3f, .045f, .05f, .33f, .03f };
                for (int i = 0; i + 2 < bubbles.Length; i += 3) { var at = S(bubbles[i], bubbles[i + 1], 1); g.Cylinder(at, at + new Vector3(0, 0, .0008f), bubbles[i + 2] * sw, bubbles[i + 2] * sw, C(0x0e3545), 20); }
                Fill(g, .43f, .575f, .57f, .583f, 2, accent);
                g.Emit("Idle page panels", page.transform, screenMat, false);
                Write(page.transform, "어종 도감", .5f, .72f, .27f, white, TextAnchor.MiddleCenter, FontStyle.Bold);
                Write(page.transform, "수조의 물고기를 미리 알아 두세요", .5f, .515f, .075f, muted, TextAnchor.MiddleCenter, FontStyle.Normal);
                var hint = new GameObject("Key hint"); hint.transform.SetParent(page.transform, false); computer.hint = hint;
                const string hintText = "눌러서 도감 열기"; float hintEm = .07f, keyU = .05f, textU = Width(hintText, hintEm, FontStyle.Bold) / sw, hintU = .5f - (keyU + .014f + textU) / 2;
                var hg = new Geo(); Fill(hg, hintU, .375f, hintU + keyU, .375f + keyU * sw / sh, 2, C(0xe6f8ff)); hg.Emit("Key hint cap", hint.transform, screenMat, false);
                Write(hint.transform, "E", hintU + keyU / 2, .375f + keyU * sw / sh / 2, hintEm, ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                Write(hint.transform, hintText, hintU + keyU + .014f, .375f + keyU * sw / sh / 2, hintEm, accent, TextAnchor.MiddleLeft, FontStyle.Bold);
                float thumbV = .2f, thumbU = thumbV * sh / sw, gapU = .012f, rowU = count * thumbU + Mathf.Max(0, count - 1) * gapU, startU = .5f - rowU / 2;
                var cards = new Geo();
                for (int i = 0; i < count; i++)
                {
                    float u0 = startU + i * (thumbU + gapU); var tint = pages[i].tint; tint.a = 1; var card = Color.Lerp(bar, tint, .3f);
                    Fill(cards, u0, .09f, u0 + thumbU, .09f + thumbV, 2, card);
                    if (pictures[i] != null) Picture(page.transform, pictures[i], u0 + .004f, .096f, u0 + thumbU - .004f, .084f + thumbV, card, aspects[i], "Idle picture " + i);
                }
                cards.Emit("Idle page cards", page.transform, screenMat, false);
            }

            // one page per species: picture card on the left; name, tag and traits on the right; page dots below
            computer.pages = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                var p = pages[i]; var page = new GameObject("Page " + (i + 1) + " " + p.title); page.transform.SetParent(screen.transform, false); computer.pages[i] = page;
                var tint = p.tint; tint.a = 1; var card = Color.Lerp(bar, tint, .3f);
                var g = new Geo(); Fill(g, 0, 0, 1, 1, 0, bg);
                Fill(g, 0, .885f, 1, 1, 1, bar); Fill(g, 0, .879f, 1, .885f, 1, accent);
                Fill(g, .035f, .13f, .47f, .84f, 1, Color.Lerp(tint, white, .3f)); Fill(g, .039f, .137f, .466f, .833f, 2, card);
                float tagEm = .062f, tagU = Width(p.tag ?? "", tagEm, FontStyle.Bold) / sw;
                Fill(g, .51f, .615f, .51f + tagU + .03f, .705f, 1, p.warn ? warm : accent);
                Fill(g, .51f, .578f, .965f, .582f, 1, C(0x1d5a6c));
                float dotU = .012f, dotV = dotU * sw / sh, currentU = .036f, gapU = .01f, rowU = (count - 1) * (dotU + gapU) + currentU, du = .5f - rowU / 2;
                for (int j = 0; j < count; j++) { float w = j == i ? currentU : dotU; Fill(g, du, .045f, du + w, .045f + dotV, 1, j == i ? accent : C(0x2b6a7c)); du += w + gapU; }
                g.Emit("Page panels", page.transform, screenMat, false);
                if (pictures[i] != null) Picture(page.transform, pictures[i], .05f, .15f, .455f, .82f, card, aspects[i], "Page picture");
                Write(page.transform, "어종 도감", .035f, .943f, .085f, white, TextAnchor.MiddleLeft, FontStyle.Bold);
                Write(page.transform, (i + 1) + " / " + count, .965f, .943f, .08f, accent, TextAnchor.MiddleRight, FontStyle.Bold);
                Write(page.transform, p.title ?? "", .51f, .775f, .15f, white, TextAnchor.MiddleLeft, FontStyle.Bold);
                Write(page.transform, p.tag ?? "", .525f, .66f, tagEm, ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                Write(page.transform, Wrap(p.trait ?? "", .07f, (.965f - .51f) * sw), .51f, .55f, .07f, soft, TextAnchor.UpperLeft, FontStyle.Normal);
                Write(page.transform, "A  이전", .035f, .058f, .055f, muted, TextAnchor.MiddleLeft, FontStyle.Normal);
                Write(page.transform, "다음  D", .965f, .058f, .055f, muted, TextAnchor.MiddleRight, FontStyle.Normal);
                page.SetActive(false);
            }
            return computer;
        }
    }
}
