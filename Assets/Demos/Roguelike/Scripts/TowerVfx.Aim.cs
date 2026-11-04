using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // The hero's mouse-aimed strikes: the facing slashes of TowerVfx.cs turned toward any direction, either sweeping down
    // from above the aim or rising from below it, and speed lines along a dash in any direction.
    // Aimed straight ahead on a downward sweep they match BasicSlash, HeavySlash and SpeedLines exactly.
    public sealed partial class TowerVfx
    {
        // turn is +1 when the sweep runs clockwise on screen (a downward cut facing right), -1 when it runs the other way.
        private static float SweepTurn(int facing, bool rising) { return (facing < 0 ? -1 : 1) * (rising ? -1 : 1); }
        private static Vector2 AimUnit(Vector2 aim, int facing) { return aim.sqrMagnitude > .0001f ? aim.normalized : new Vector2(facing < 0 ? -1 : 1, 0); }
        // A point ahead along the aim and across it toward the side the sweep starts from.
        private static Vector2 Toward(Vector2 pos, Vector2 aim, Vector2 across, float ahead, float side) { return pos + aim * ahead + across * side; }
        // The painted arc opens toward +x and sweeps top to bottom; this points it along the aim and mirrors a counter-clockwise sweep.
        private static void Orient(Clip clip, Vector2 aim, float turn, float offset = 0)
        {
            clip.angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg + offset * turn;
            if (turn < 0) clip.scale.y = -clip.scale.y;
        }

        public void AimedSlash(Vector2 pos, Vector2 aim, int facing, bool rising, Color tint)
        {
            EnsureTextures();
            aim = AimUnit(aim, facing); float turn = SweepTurn(facing, rising), angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            Vector2 across = new Vector2(-aim.y, aim.x) * turn;
            Color light = Color.Lerp(tint, Color.white, .1f);
            var body = Sheet("Sword arc body", slash, Toward(pos, aim, across, -.1f, 0), 5.6f, Alpha(tint, .7f), .24f, 129, 0, false); Orient(body, aim, turn); body.scale.y *= 1.08f; body.startScale = .88f; body.endScale = 1.08f; body.fadePower = .9f;
            var arc = Sheet("Sword arc", slash, pos, 5.1f, Alpha(light, 1), .22f, 130, 0, false); Orient(arc, aim, turn); arc.startScale = .9f; arc.endScale = 1.06f; arc.fadePower = .9f;
            var edge = Sheet("Sword arc edge", slash, Toward(pos, aim, across, .08f, .03f), 4.5f, new Color(1, 1, 1, .9f), .17f, 131, 0, false); Orient(edge, aim, turn); edge.scale.y *= .9f; edge.startScale = .92f; edge.endScale = 1.04f; edge.fadePower = 1.2f;
            var bloom = Spawn("Sword arc glow", glow, Toward(pos, aim, across, 1.5f, .2f), 3.4f, Alpha(tint, .45f), .2f, true, 128); bloom.scale.x *= .55f; bloom.angle = angle; bloom.endScale = 1.2f; bloom.ease = 1; bloom.fadePower = 1.3f;
            var wind = Puff(Toward(pos, aim, across, .9f, -.15f), 2.1f, new Color(1, 1, 1, .12f), aim * 2.6f - across * .7f, .3f, 129); wind.spin = -turn * 70; wind.endScale = 1.9f;
            SparkFlip(Toward(pos, aim, across, 1.4f, .15f), .9f, Alpha(Color.Lerp(Color.white, tint, .3f), .95f), .1f, 131);
            for (int i = 0; i < 5; i++) Spark(Toward(pos, aim, across, Range(1.1f, 1.6f), Range(-.3f, .5f)), aim * Range(3, 6.5f) + across * Range(-1.5f, 2.5f), i % 2 == 0 ? Color.white : light, Range(.3f, .5f), Range(.22f, .38f), 7, 2.5f);
        }

        // Combo finishers and dash slashes along the aim: a double arc with a shock ring, rays, sparks and ground dust.
        public void AimedHeavySlash(Vector2 pos, Vector2 aim, int facing, bool rising, Color tint)
        {
            EnsureTextures();
            aim = AimUnit(aim, facing); float turn = SweepTurn(facing, rising);
            Vector2 across = new Vector2(-aim.y, aim.x) * turn;
            int side = facing < 0 ? -1 : 1;
            Color light = Color.Lerp(tint, Color.white, .3f);
            var arc = Sheet("Finisher arc", slash, pos, 6.2f, Alpha(light, .95f), .3f, 130, 0, false); Orient(arc, aim, turn); arc.scale.y *= 1.12f; arc.startScale = .85f; arc.endScale = 1.12f; arc.fadePower = 1.3f;
            var edge = Sheet("Finisher edge", slash, pos, 5.6f, new Color(1, 1, 1, .7f), .26f, 131, 0, false); Orient(edge, aim, turn); edge.scale.y *= 1.1f; edge.startScale = .88f; edge.endScale = 1.08f;
            var second = Sheet("Finisher second arc", slash, Toward(pos, aim, across, .3f, .1f), 5.2f, Alpha(light, .7f), .3f, 129, 0, false); Orient(second, aim, turn, -26); second.age = -.05f; second.startScale = .8f; second.endScale = 1.1f;
            Vector2 tip = Toward(pos, aim, across, 1.4f, .1f);
            var flash = Flash(tip, 2.4f, Alpha(Warm, .95f), .14f); flash.renderer.sortingOrder = 132;
            ImpactRing(tip, .9f, tint);
            Rays(tip, 6, 1.3f, 2.3f, light, .22f);
            for (int i = 0; i < 12; i++) Spark(tip + Inside(.3f), aim * Range(2.5f, 8) + across * Range(-2, 4), i % 3 == 0 ? Color.white : light, Range(.3f, .55f), Range(.25f, .45f), 8, 2.2f);
            // Footwork dust stays on the ground whichever way the blade goes.
            for (int i = 0; i < 4; i++) Puff(pos - aim * 1.1f + new Vector2(side * (1.1f + Range(.2f, 1.3f)), -.85f), Range(.4f, .7f), DustTone, new Vector2(side * Range(1.5f, 3.5f), Range(.4f, 1.1f)), Range(.4f, .6f), 118);
            for (int i = 0; i < 3; i++) { var ink = Puff(Toward(tip, aim, across, Range(.1f, .6f), Range(-.4f, .3f)), Range(.5f, .8f), Soot, aim * Range(1.2f, 2.6f) + across * Range(-.2f, .8f), Range(.35f, .5f), 126); ink.endScale = 1.5f; }
        }

        public void SpeedLines(Vector2 center, Vector2 direction, Color tint)
        {
            EnsureTextures();
            Vector2 d = AimUnit(direction, 1), n = new Vector2(-d.y, d.x);
            for (int i = 0; i < 7; i++)
            {
                float offset = Range(-.85f, .85f), length = Range(1.1f, 2.7f), thickness = Range(.05f, .13f);
                Vector2 start = center + n * offset - d * Range(.2f, .8f);
                var line = Line(start, start - d * length, thickness, i % 2 == 0 ? new Color(1, 1, 1, .75f) : Alpha(tint, .85f), Range(.14f, .26f), 128);
                line.velocity = -d * Range(2, 5); line.fadePower = 1.5f;
                if (i < 2) { var corel = Line(start, start - d * length * .8f, thickness * .35f, Color.white, .16f, 129); corel.velocity = line.velocity; }
            }
        }
    }
}
