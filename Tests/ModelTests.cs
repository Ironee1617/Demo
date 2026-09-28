using System;
using PrototypeLab.Aquarium;

public static class ModelTests
{
    private static int passed;
    private static Point At(float x, float z, float y = 1.6f) { return new Point(x, y, z); }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Test(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    private static AquariumModel Match(bool friend = false)
    {
        var m = new AquariumModel(); m.AddPlayer(1, "Hider", Team.Hider); m.AddPlayer(2, "Seeker", Team.Seeker); m.SetRole(2, Team.Seeker, true);
        if (friend) { m.AddPlayer(3, "Friend", Team.Hider); m.SetRole(3, Team.Hider, true); }
        Check(m.StartRound(), "start");
        foreach (var fish in m.Fish) { fish.position = At(-12, 7); fish.dead = true; }
        m.Player(1).position = At(0, 0); m.Player(1).facing = new Point(1, 0);
        m.Player(2).position = At(12, 7);
        return m;
    }
    private static void Advance(AquariumModel m, float seconds)
    { for (int i = 0; i < (int)Math.Ceiling(seconds * 30); i++) m.Tick(1f / 30); }
    private static void Act(AquariumModel m, int id, bool morph = false)
    { m.SetInput(id, new SwimInput { aimX = 1, action = !morph, morph = morph }); m.Tick(1f / 30); }
    public static int Main()
    {
        try
        {
            Test("lobby readiness and both roles", () => {
                var m = new AquariumModel(); m.AddPlayer(1, "One", Team.Hider); Check(!m.StartRound(), "solo must fail");
                m.AddPlayer(2, "Two", Team.Hider); m.SetRole(2, Team.Hider, true); Check(!m.StartRound(), "no seeker");
                m.SetRole(2, Team.Seeker, false); Check(!m.StartRound(), "unready"); m.SetRole(2, Team.Seeker, true); Check(m.StartRound(), "valid lobby");
                Check(m.AddPlayer(4, "Late", Team.Seeker) == null, "midround join");
            });
            Test("copy nearby living fish and brief reveal", () => {
                var m = Match(); m.Fish[0].dead = false; m.Fish[0].species = 2; m.Fish[0].position = At(1, 0);
                Act(m, 1, true); Check(m.Player(1).species == 2, "morph species"); Check(m.Player(1).revealUntil > m.Time, "reveal");
                Advance(m, 0.7f); Check(m.Player(1).revealUntil < m.Time, "reveal expires");
            });
            Test("dead fish remain edible and count toward victory", () => {
                var m = Match(); m.Fish[0].position = At(1, 0); Act(m, 1);
                Check(m.Fish[0].eaten && m.Snapshot().remaining == 31, "corpse eaten");
                for (int i = 1; i < m.Fish.Count; i++) m.Fish[i].eaten = true;
                m.Tick(1f / 30); Check(m.Winner == (int)Team.Hider, "all eaten victory");
            });
            Test("one-second friendly swallow and shared burp reveal", () => {
                var m = Match(true); var p = m.Player(3); p.position = At(1, 0);
                Act(m, 1); Check(p.swallowedBy == 1, "swallowed"); Advance(m, 0.9f); Check(p.swallowedBy == 1, "early release");
                Advance(m, 0.14f); Check(p.swallowedBy == 0 && p.position.x > 1, "released forward");
                Check(p.revealUntil > m.Time && m.Player(1).revealUntil > m.Time && m.Effects.Exists(e => e.kind == "burp"), "burp reveal");
            });
            Test("carrier disconnect safely releases friend", () => {
                var m = Match(true); m.Player(3).position = At(1, 0); Act(m, 1); m.RemovePlayer(1);
                Check(m.Player(3).swallowedBy == 0 && m.Player(3).alive && m.Phase == RoundPhase.Playing, "disconnect release");
            });
            Test("seeker warmup and speed advantage", () => {
                var m = Match(); var s = m.Player(2); s.position = At(0, 6);
                m.SetInput(2, new SwimInput { x = 1 }); m.Tick(0.1f); Check(s.position.x == 0, "head start freeze"); Advance(m, 5);
                m.Player(1).position = At(0, -7); m.SetInput(1, new SwimInput { x = 1 }); m.SetInput(2, new SwimInput { x = 1 }); m.Tick(0.1f);
                Check(Math.Abs(m.Player(1).position.x - 0.4f) < 0.001f && Math.Abs(s.position.x - 0.47f) < 0.001f, "speeds");
            });
            Test("harpoon kills hider and seekers win", () => {
                var m = Match(); Advance(m, 5.1f); m.Player(2).position = At(0, 0); m.Player(1).position = At(2, 0);
                Act(m, 2); Advance(m, 0.3f); Check(!m.Player(1).alive && m.Winner == (int)Team.Seeker, "kill");
            });
            Test("nearest fish intercepts harpoon before hider", () => {
                var m = Match(); Advance(m, 5.1f); m.Player(2).position = At(0, 0); m.Player(1).position = At(2, 0);
                m.Fish[0].position = At(1.3f, 0); m.Fish[0].dead = false; m.Fish[0].facing = new Point(1, 0);
                Act(m, 2); Advance(m, 0.25f); Check(m.Fish[0].dead && !m.Fish[0].eaten && m.Player(1).alive, "nearest collision");
            });
            Test("rock cover blocks harpoons", () => {
                var m = Match(); Advance(m, 5.1f); m.Player(2).position = At(-10, -2); m.Player(1).position = At(-3, -2);
                Act(m, 2); Advance(m, 0.8f); Check(m.Player(1).alive && m.Harpoons.Count == 0, "cover");
            });
            Test("carrier death releases swallowed player", () => {
                var m = Match(true); Advance(m, 5.1f); m.Player(1).position = At(2, 0); m.Player(3).position = At(3, 0); Act(m, 1);
                m.Player(2).position = At(0, 0); Act(m, 2); Advance(m, 0.2f);
                Check(!m.Player(1).alive && m.Player(3).alive && m.Player(3).swallowedBy == 0, "death release");
            });
            Test("timeout and rematch reset", () => {
                var m = Match(); Advance(m, 186); Check(m.Winner == (int)Team.Seeker, "timeout"); m.ReturnToLobby();
                Check(m.Phase == RoundPhase.Lobby && m.Fish.Count == 32 && m.Player(1).alive && !m.Player(2).ready, "lobby reset");
                m.SetRole(2, Team.Seeker, true); Check(m.StartRound() && m.Round == 2 && m.Winner == -1, "rematch");
            });
            Test("input expiry, diagonal clamp and malformed input", () => {
                var m = Match(); m.SetInput(1, new SwimInput { x = 1, z = 1 }); m.Tick(0.1f); Check(Math.Abs((m.Player(1).position - At(0, 0)).Length - 0.4f) < 0.001f, "diagonal clamp");
                Advance(m, 0.5f); var before = m.Player(1).position; Advance(m, 0.5f); Check((before - m.Player(1).position).Length < 0.001f, "stale input stopped");
                m.SetInput(1, new SwimInput { x = float.NaN }); m.SetInput(1, new SwimInput { x = float.MaxValue, aimX = float.PositiveInfinity });
                m.Tick(float.NaN); m.Tick(0.1f); Check(!float.IsNaN(m.Time) && (before - m.Player(1).position).Length < 0.001f, "invalid input");
            });
            Test("seeker leaves: surviving hiders win", () => { var m = Match(); m.RemovePlayer(2); Check(m.Winner == (int)Team.Hider, "forfeit"); });
            Test("3D movement normalizes all axes and bounds swimming height", () => {
                var m = Match(); var origin = m.Player(1).position;
                m.SetInput(1, new SwimInput { x = 1, y = 1, z = 1 }); m.Tick(0.1f);
                Check(Math.Abs((m.Player(1).position - origin).Length - 0.4f) < 0.001f, "3D diagonal speed");
                for (int i = 0; i < 100; i++) { m.SetInput(1, new SwimInput { y = 1 }); m.Tick(0.1f); }
                Check(Math.Abs(m.Player(1).position.y - (AquariumModel.WaterHeight - 0.4f)) < 0.001f, "water surface");
                for (int i = 0; i < 100; i++) { m.SetInput(1, new SwimInput { y = -1 }); m.Tick(0.1f); }
                Check(Math.Abs(m.Player(1).position.y - (AquariumModel.Floor + 0.4f)) < 0.001f, "floor");
                var before = m.Player(1).position; Advance(m, 0.5f);
                m.SetInput(1, new SwimInput { y = float.NaN, aimY = float.PositiveInfinity }); m.Tick(0.1f);
                Check(!float.IsNaN(m.Player(1).position.y), "invalid vertical input");
            });
            Test("bite uses depth and can eat directly above", () => {
                var m = Match(); m.Player(1).position = At(0, 0, 2); m.Fish[0].position = At(0, 0, 5);
                m.SetInput(1, new SwimInput { aimY = 1, action = true }); m.Tick(0.1f); Check(!m.Fish[0].eaten, "different height must miss");
                Advance(m, 0.7f); m.Fish[0].position = At(0, 0, 3);
                m.SetInput(1, new SwimInput { aimY = 1, action = true }); m.Tick(0.1f); Check(m.Fish[0].eaten, "vertical bite");
            });
            Test("3D harpoon misses wrong height and hits an elevated target", () => {
                var m = Match(); Advance(m, 5.1f); m.Player(2).position = At(0, 0, 2); m.Player(1).position = At(2, 0, 5);
                Act(m, 2); Advance(m, 1); Check(m.Player(1).alive, "horizontal shot must miss above");
                m.SetInput(2, new SwimInput { aimX = 2, aimY = 3, action = true }); Advance(m, 0.4f);
                Check(!m.Player(1).alive, "elevated shot");
            });
            Test("rock volume permits swimming and shooting over it", () => {
                var at = new Point(-6, 4, -2); Check((AquariumModel.Constrain(at, 0.4f) - at).Length < 0.001f, "above rock movement");
                Check(AquariumModel.ObstacleHit(new Point(-10, 4, -2), new Point(-3, 4, -2)) > 1, "above rock shot");
                Check(AquariumModel.ObstacleHit(new Point(-10, 1, -2), new Point(-3, 1, -2)) < 1, "rock blocks low shot");
                var grounded = AquariumModel.Constrain(new Point(-6, 0.2f, -2), 0.4f);
                Check(grounded.y >= AquariumModel.Floor + 0.4f && (grounded - new Point(-6, grounded.y, -2)).Length > 1, "cannot become trapped under rock");
            });
            Console.WriteLine("RESULT: " + passed + " gameplay tests passed."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL " + ex.Message); return 1; }
    }
}
