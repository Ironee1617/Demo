using System;

namespace PrototypeLab.Aquarium
{
    // R76: the drone's tranquiliser dart and the new tunables (all editable in Aquarium/밸런스 조정 툴).
    public sealed partial class AquariumModel
    {
        public static float DartCooldown = 60f, DartStun = 3f, DartRange = 16f;
        // Harpoon hit volumes on aliens (1.1 = 10% bigger than R73).
        public static float HarpoonHitScale = 1.1f;
        // The purple alien's egg (it was 40 s against 60 s; the egg now counts down only once an ally is down).
        public static float PurpleHatchSeconds = 20f;

        public static bool DartReady(Swimmer p, float time) { return time >= p.nextDart; }

        // LMB while flying the drone: an instant dart along the aim from the drone. An alien hit is paralysed for
        // DartStun seconds; one squeezing into a drain is pulled back out (the escape is cancelled) and paralysed.
        // The cooldown belongs to the diver, so switching back to the diver and to the drone again keeps it.
        private void FireDart(Swimmer p, Point aim)
        {
            p.nextDart = Time + DartCooldown;
            var dir = aim.Length > .1f ? aim.Normalized : p.droneFacing.Normalized;
            var start = p.dronePosition; var end = start + dir * DartRange;
            float first = 2f; Swimmer hit = null;
            foreach (var o in Players)
            {
                if (o.team != Team.Hider || !o.alive || o.swallowedBy != 0 || o.escaped) continue;
                var t = PlayerHitTime(start, end, o, Time, drains);
                if (t < first) { first = t; hit = o; }
            }
            var solid = SolidHit(start, end);
            if (solid < first) { first = solid; hit = null; }
            var at = start + (end - start) * Math.Min(1f, first);
            if (hit != null)
            {
                bool escaping = hit.drainTask == DrainTask.Enter;
                if (hit.drainTask != DrainTask.None) CancelDrainTask(hit, false);
                hit.stunnedUntil = Math.Max(hit.stunnedUntil, Time + DartStun * StunScale(hit));
                hit.velocity = new Point();   // aliens have no stun clip: the view shows the dart hit effect
                Effect("dartHit", at, dir, 1f, hit.id);
                if (escaping) Effect("dartEscape", at, dir, 1f, hit.id);
            }
            Effect("dart", start, at - start, .35f, p.id);
        }
    }

    // R76: the balance values saved by the editor tool (Resources/AquariumBalance.txt, "Name=Value" lines) are put
    // into AquariumModel's public static fields when the game starts, so a build plays with the saved numbers.
    public static class AquariumBalance
    {
        public const string ResourceName = "AquariumBalance";
        private static bool applied;

        public static void ApplySaved()
        {
            if (applied) return;
            applied = true;
            var text = UnityEngine.Resources.Load<UnityEngine.TextAsset>(ResourceName);
            if (text != null) Apply(text.text);
        }

        public static int Apply(string text)
        {
            int n = 0;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('='); if (eq <= 0) continue;
                var field = typeof(AquariumModel).GetField(line.Substring(0, eq).Trim(), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (field == null || field.IsLiteral || field.IsInitOnly) continue;
                float v;
                if (!float.TryParse(line.Substring(eq + 1).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) continue;
                if (field.FieldType == typeof(float)) { field.SetValue(null, v); n++; }
                else if (field.FieldType == typeof(int)) { field.SetValue(null, (int)Math.Round(v)); n++; }
            }
            return n;
        }
    }
}
