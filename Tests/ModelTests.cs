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
        // Most rule scenarios isolate one human per role; roster fill has its own checks below.
        m.Players.RemoveAll(p => p.filled);
        foreach (var fish in m.Fish) { fish.position = At(-12, 7); fish.facing = new Point(); fish.turnAt = float.MaxValue; }
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
            Test("living fish count toward food victory", () => {
                var m = Match(); m.Fish[0].position = At(1, 0); Act(m, 1);
                Check(m.Fish[0].eaten && m.Snapshot().remaining == 31, "fish eaten");
                for (int i = 1; i < m.Fish.Count; i++) m.Fish[i].eaten = true;
                m.Tick(1f / 30); Check(m.Winner == (int)Team.Hider && m.WinReason == WinReason.FoodGoalReached, "all eaten victory");
            });
            Test("three-second friendly swallow and six-second morph lock", () => {
                var m = Match(true); var p = m.Player(3); p.position = At(1, 0);
                Act(m, 1); Check(p.swallowedBy == 1, "swallowed"); Advance(m, 2.9f); Check(p.swallowedBy == 1, "early release");
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
                Check(m.Snapshot().target == 31 && m.Snapshot().remaining == 31 && m.Snapshot().killed == 1 && m.Snapshot().eaten == 0, "death reduces goal without counting as eaten");
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
                var m = Match(); m.Player(1).lastMealAt = m.EndsAt; Advance(m, 186); Check(m.Winner == (int)Team.Seeker && m.WinReason == WinReason.TimeExpired, "timeout");
                Advance(m, AquariumModel.ReviewSeconds); m.ReturnToLobby();
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
                Act(m, 2); Advance(m, 3.1f); Check(m.Player(1).alive, "horizontal shot must miss above");
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
            Test("harpoon enforces three-second host cooldown", () => {
                var m = Match(); Advance(m, 5.1f); m.Player(2).position = At(0, 6);
                Act(m, 2); float ready = m.Player(2).nextAction;
                Check(Math.Abs(ready - m.Time - 3) < 0.001f, "3s cooldown");
                Advance(m, 1); Act(m, 2); Check(m.Player(2).nextAction == ready, "early shot rejected");
                Advance(m, 2.1f); Act(m, 2); Check(m.Player(2).nextAction > ready && m.Harpoons.Count == 1, "ready shot accepted");
            });
            Test("feed attracts only nearby live fish for five seconds and cools down for twenty", () => {
                var m = Match(); Advance(m, 5.1f); m.Player(2).position = At(0, 6);
                var near = m.Fish[0]; near.position = At(4, 6);
                var far = m.Fish[1]; far.position = At(-10, 6);
                var dead = m.Fish[2]; dead.position = At(4, 6); dead.dead = true;
                m.SetInput(2, new SwimInput { aimX = 1, feed = true }); m.Tick(1f / 30);
                var food = m.Effects.Find(e => e.kind == "feed"); Check(food != null && food.duration == 5, "feed spawned");
                float ready = m.Player(2).nextFeed, before = (near.position - food.position).Length;
                Check(Math.Abs(ready - m.Time - 20) < 0.001f, "20s cooldown");
                Advance(m, 0.5f);
                Check((near.position - food.position).Length < before && far.position.x == -10 && dead.position.x == 4, "only nearby living fish converge");
                m.SetInput(2, new SwimInput { feed = true }); m.Tick(1f / 30);
                Check(m.Player(2).nextFeed == ready && m.Effects.FindAll(e => e.kind == "feed").Count == 1, "cannot spam food");
                Advance(m, 5); Check(!m.Effects.Exists(e => e.kind == "feed"), "food expires");
                Advance(m, 15); m.SetInput(2, new SwimInput { feed = true }); m.Tick(1f / 30);
                Check(m.Player(2).nextFeed > ready, "feed becomes ready again");
            });
            Test("feed is restricted to divers after hiding countdown", () => {
                var m = Match(); m.SetInput(1, new SwimInput { feed = true }); m.SetInput(2, new SwimInput { feed = true }); m.Tick(0.1f);
                Check(m.Effects.Count == 0 && m.Player(2).nextFeed == 0, "warmup and role restrictions");
                Advance(m, 5.1f); Check(!m.Effects.Exists(e => e.kind == "feed"), "warmup click is not queued");
            });
            Test("eating stuns movement and actions then resumes; red trace lasts five seconds", () => {
                var m = Match(); var p = m.Player(1); m.Fish[0].position = At(1, 0); m.Fish[1].position = At(1.2f, 0);
                Act(m, 1); var origin = p.position; float ateAt = m.Time;
                Check(Math.Abs(p.stunnedUntil - ateAt - AquariumModel.EatStun) < 0.001f, "eat stun");
                m.SetInput(1, new SwimInput { x = 1, action = true, morph = true }); m.Tick(0.1f);
                Check((p.position - origin).Length < 0.001f && p.species == -1 && !m.Fish[1].eaten, "no movement, morph or bite during stun");
                Advance(m, 2f); m.SetInput(1, new SwimInput { x = 1 }); m.Tick(0.1f);
                Check(p.position.x > origin.x, "movement resumes");
                Advance(m, 2f); Check(m.Effects.Exists(e => e.kind == "eat" && e.duration == 5), "trace persists after stun");
                Advance(m, 1); Check(!m.Effects.Exists(e => e.kind == "eat"), "trace expires");
            });
            Test("swallowing stuns carrier and yellow spit trace lasts five seconds", () => {
                var m = Match(true); m.Player(3).position = At(1, 0); var p = m.Player(1); Act(m, 1);
                var origin = p.position;
                m.SetInput(1, new SwimInput { x = 1 }); m.Tick(0.1f);
                Check((p.position - origin).Length < 0.001f && p.stunnedUntil > m.Time, "carrier is stunned");
                Advance(m, 3); var trace = m.Effects.Find(e => e.kind == "burp");
                Check(m.Player(3).swallowedBy == 0 && trace != null && trace.duration == 5, "automatic spit and yellow trace");
                Advance(m, 4); Check(m.Effects.Contains(trace), "spit trace persists");
                Advance(m, 1.1f); Check(!m.Effects.Contains(trace), "spit trace expires");
            });
            Test("dead fish lower goal once and cannot be eaten again", () => {
                var m = Match(); m.Fish[0].dead = true; m.Fish[0].position = At(1, 0); Act(m, 1);
                Check(!m.Fish[0].eaten && m.Snapshot().remaining == 31 && m.Snapshot().target == 31 && m.Snapshot().eaten == 0, "corpse cannot count twice");
                for (int i = 1; i < m.Fish.Count; i++) m.Fish[i].eaten = true;
                m.Tick(1f / 30); Check(m.Winner == (int)Team.Hider && m.Snapshot().remaining == 0 && m.Snapshot().eaten == 31, "reduced goal victory");
            });
            Test("last fish killed by diver completes alien goal", () => {
                var m = Match(); Advance(m, 5.1f); for (int i = 1; i < m.Fish.Count; i++) m.Fish[i].eaten = true;
                m.Player(2).position = At(0, 6); m.Fish[0].position = At(1.3f, 6);
                Act(m, 2); Check(m.Fish[0].dead && m.Winner == (int)Team.Hider && m.WinReason == WinReason.FoodGoalReached, "last kill finishes goal");
            });
            Test("finished review freezes gameplay for twenty seconds before rematch", () => {
                var m = Match(true); m.Player(3).position = At(1, 0); Act(m, 1); m.RemovePlayer(2);
                Check(m.WinReason == WinReason.DiversLeft && m.Player(3).swallowedBy == 0, "finish releases swallowed alien");
                Check(Math.Abs(m.ReviewEndsAt - m.Time - 20) < 0.001f && m.Snapshot().reviewEndsAt == m.ReviewEndsAt, "20s synchronized timer");
                var origin = m.Player(1).position; m.SetInput(1, new SwimInput { x = 1, action = true });
                m.ReturnToLobby(); Advance(m, 19);
                Check(m.Phase == RoundPhase.Finished && (m.Player(1).position - origin).Length < 0.001f, "frozen and cannot skip review");
                Advance(m, 1.1f); m.ReturnToLobby();
                Check(m.Phase == RoundPhase.Lobby && m.WinReason == WinReason.None && m.ReviewEndsAt == 0 && m.Player(1).stunnedUntil == 0, "review reset");
            });
            Test("five-player roster fills four aliens and removes only fill bots for rematch", () => {
                var m = new AquariumModel(); m.AddPlayer(1, "Human", Team.Hider); m.AddPlayer(2, "Diver", Team.Seeker);
                m.AddPlayer(3, "Practice friend", Team.Hider, true); m.SetRole(2, Team.Seeker, true);
                Check(m.StartRound() && m.Players.Count == 5 && m.Players.FindAll(p => p.team == Team.Hider).Count == 4, "one diver four aliens");
                Check(m.Players.FindAll(p => p.filled && p.id >= 1000).Count == 2, "distinct fill IDs");
                m.RemovePlayer(2); Advance(m, 20.1f); m.ReturnToLobby();
                Check(m.Players.Count == 2 && m.Player(3).bot && !m.Player(3).filled && !m.Players.Exists(p => p.filled), "preserve practice bot only");
                var invalid = new AquariumModel(); invalid.AddPlayer(1, "A", Team.Hider); invalid.AddPlayer(2, "B", Team.Seeker, true); invalid.AddPlayer(3, "C", Team.Seeker, true);
                Check(!invalid.StartRound() && invalid.StartProblem().Length > 0, "reject two divers");
            });
            Test("hunger begins after countdown and starvation is fatal", () => {
                var m = Match(); var p = m.Player(1);
                Check(p.lastMealAt == m.HuntAt, "countdown does not spend hunger");
                Advance(m, 64.8f); Check(p.alive, "survive before 60 hungry seconds");
                Advance(m, 0.4f); Check(!p.alive && p.deathReason == "starved" && m.WinReason == WinReason.AllAliensCaught, "starvation and wipe");
            });
            Test("only normal meals refresh hunger and grant food points", () => {
                var m = Match(true); var p = m.Player(1); Advance(m, 8); m.Fish[0].position = At(1, 0);
                Act(m, 1); float meal = p.lastMealAt;
                Check(Math.Abs(meal - m.Time) < 0.001f && p.personalEaten == 1 && p.score == 100, "normal meal rewards");
                Advance(m, 2.1f); m.Player(3).position = At(1, 0); Act(m, 1);
                Check(p.lastMealAt == meal && p.personalEaten == 1 && p.score == -50 && m.Player(3).score == 0, "ally grants no hunger and only eater loses points");
            });
            Test("ally stun ends before morph lock while both can sprint after release", () => {
                var m = Match(true); var p = m.Player(1); var ally = m.Player(3); ally.position = At(1, 0); p.species = ally.species = 2;
                Act(m, 1); float biteAt = m.Time;
                Check(p.species == -1 && ally.species == -1 && Math.Abs(p.morphLockedUntil - biteAt - 6) < 0.001f && ally.morphLockedUntil == p.morphLockedUntil, "both unmorph for six seconds");
                Advance(m, 3.1f); var before = p.position; m.SetInput(1, new SwimInput { z = 1, sprint = true }); m.Tick(0.1f);
                Check((p.position - before).Length > 0.6f && p.stamina < 100 && ally.swallowedBy == 0, "sprint resumes after three seconds");
                p.position = At(0, 0); m.Fish[0].position = At(1, 0); Act(m, 1, true); Check(p.species == -1, "morph still blocked");
                Advance(m, 3); m.Fish[0].position = At(1, 0); Act(m, 1, true); Check(p.species == 0, "morph resumes after six seconds");
            });
            Test("aimed morph accepts NPCs and disguised allies without identifying the target", () => {
                var m = Match(true); var p = m.Player(1); var ally = m.Player(3); ally.position = At(1, 0); ally.species = 2;
                Act(m, 1, true); Check(p.species == 2 && ally.species == 2 && ally.revealUntil == 0, "ally copies without revealing target");
                Check(m.Effects.FindAll(e => e.kind == "morph").Count == 1 && (m.Effects[0].position - p.position).Length < 0.001f, "caster-only effect matches NPC morph");
                Advance(m, 0.7f); p.species = -1; ally.position = At(-1, 0); Act(m, 1, true); Check(p.species == -1, "cannot copy behind aim");
                Advance(m, 0.7f); ally.position = At(1, 0); ally.species = -1; Act(m, 1, true); Check(p.species == -1, "unmorphed alien not a fish target");
                Advance(m, 0.7f); p.position = At(-7.6f, -2, 1); m.Fish[0].position = At(-5, -2, 1); Act(m, 1, true); Check(p.species == -1, "rock blocks morph");
            });
            Test("sprint drains only while moving and restores when released for both roles", () => {
                var m = Match(); Advance(m, 5.1f); var p = m.Player(1); var diver = m.Player(2); diver.position = At(0, 6);
                m.SetInput(1, new SwimInput { x = 1, sprint = true }); m.SetInput(2, new SwimInput { x = 1, sprint = true }); m.Tick(0.1f);
                Check(Math.Abs(p.position.x - 0.64f) < 0.001f && Math.Abs(diver.position.x - 0.752f) < 0.001f, "boost speed for both roles");
                Check(Math.Abs(p.stamina - 97.5f) < 0.001f && p.stamina == diver.stamina, "25 stamina per second");
                m.SetInput(1, new SwimInput { sprint = true }); m.Tick(0.1f); Check(Math.Abs(p.stamina - 99.3f) < 0.001f, "stationary sprint does not drain");
                p.stamina = 0; var before = p.position; m.SetInput(1, new SwimInput { x = 1, sprint = true }); m.Tick(0.1f);
                Check(Math.Abs((p.position - before).Length - 0.4f) < 0.001f && p.stamina == 0, "empty stamina gives no pulse boost");
                m.SetInput(1, new SwimInput()); m.Tick(0.1f); Check(Math.Abs(p.stamina - 1.8f) < 0.001f, "18 stamina per second recovery");
                p.position = At(AquariumModel.HalfWidth - 0.4f, 0); p.stamina = 40;
                m.SetInput(1, new SwimInput { x = 1, sprint = true }); m.Tick(0.1f); Check(p.stamina >= 40, "blocked movement does not consume stamina");
            });
            Test("scan has role countdown and host cooldown without exposing identity", () => {
                var m = Match(); m.Player(1).species = 2; m.SetInput(1, new SwimInput { scan = true }); m.SetInput(2, new SwimInput { scan = true }); m.Tick(0.1f);
                Check(m.Player(1).scanUntil == 0 && m.Player(2).scanUntil == 0, "role and countdown restrictions");
                Advance(m, 5.1f); m.SetInput(2, new SwimInput { scan = true }); m.Tick(0.1f); var diver = m.Player(2);
                float ready = diver.nextScan; Check(Math.Abs(diver.scanUntil - m.Time - 5) < 0.001f && Math.Abs(ready - m.Time - 18) < 0.001f, "scan timers");
                Check(m.Player(1).species == 2 && m.Player(1).revealUntil == 0 && m.Player(1).score == 0, "scan alone does not reveal or score");
                Advance(m, 5.1f); m.SetInput(2, new SwimInput { scan = true }); m.Tick(0.1f); Check(diver.nextScan == ready && diver.scanUntil < m.Time, "scan cannot be spammed");
            });
            Test("exposure points require diver view range and line of sight with fifty per life cap", () => {
                var m = Match(); Advance(m, 5.1f); var p = m.Player(1); var diver = m.Player(2);
                p.position = At(2, 0, 4); p.species = 1; diver.position = At(0, 0, 4); diver.facing = new Point(1, 0);
                Advance(m, 2.1f); Check(p.score == 5 && p.exposureScore == 5, "visible disguised fish scores");
                diver.facing = new Point(-1, 0); Advance(m, 2.1f); Check(p.score == 5, "behind diver does not score");
                diver.facing = new Point(1, 0); p.position = At(13, 0, 4); Advance(m, 2.1f); Check(p.score == 5, "distant target does not score");
                diver.position = At(-10, -2, 1); p.position = At(-3, -2, 1); Advance(m, 2.1f); Check(p.score == 5, "rock blocks exposure");
                diver.position = At(0, 0, 4); p.position = At(2, 0, 4); Advance(m, 24); Check(p.score == 50 && p.exposureScore == 50, "fifty point life cap");
            });
            Test("eggs require three personal meals dead teammate and once-only use", () => {
                var m = Match(true); var p = m.Player(1); p.personalEaten = 3;
                m.SetInput(1, new SwimInput { layEgg = true }); m.Tick(0.1f); Check(m.Eggs.Count == 0 && !p.eggUsed, "cannot waste egg with no dead teammate");
                m.Player(3).alive = false; p.personalEaten = 2; m.SetInput(1, new SwimInput { layEgg = true }); m.Tick(0.1f);
                Check(m.Eggs.Count == 0 && !p.eggUsed, "three personal food required");
                p.personalEaten = 3; p.stunnedUntil = m.Time + 1; m.SetInput(1, new SwimInput { layEgg = true }); m.Tick(0.1f); Check(m.Eggs.Count == 0, "cannot lay while stunned");
                Advance(m, 1.1f); m.SetInput(1, new SwimInput { layEgg = true }); m.Tick(0.1f);
                Check(m.Eggs.Count == 1 && p.eggUsed && m.Eggs[0].revivePlayer == 3 && m.Snapshot().eggs.Length == 1, "reserved teammate and synchronized egg");
                m.SetInput(1, new SwimInput { layEgg = true }); m.Tick(0.1f); Check(m.Eggs.Count == 1, "once-only egg");
            });
            Test("egg hatches after twelve seconds restores hunger and stamina but preserves lifetime stats", () => {
                var m = Match(true); var owner = m.Player(1); var dead = m.Player(3); owner.personalEaten = 3; dead.alive = false;
                dead.stamina = 0; dead.lastMealAt = -100; dead.personalEaten = 4; dead.score = 225; dead.eggUsed = true; dead.exposureScore = 50;
                m.SetInput(1, new SwimInput { layEgg = true }); m.Tick(0.1f); var egg = m.Eggs[0];
                Advance(m, 11.8f); Check(!dead.alive && m.Eggs.Count == 1, "not hatched early");
                Advance(m, 0.3f); Check(dead.alive && m.Eggs.Count == 0 && (dead.position - egg.position).Length < 0.001f, "one teammate at egg position");
                Check(dead.stamina == 100 && m.Time - dead.lastMealAt < 0.2f && dead.personalEaten == 4 && dead.score == 225 && dead.eggUsed && dead.exposureScore == 0, "new life keeps round stats");
                Check(owner.score == 150, "owner receives hatch points");
            });
            Test("egg target reservations prevent duplicate revival and wipe waits for viable egg", () => {
                var m = Match(true); m.Player(1).personalEaten = 3; m.Player(3).alive = false;
                var owner2 = new Swimmer { id = 4, name = "Other", team = Team.Hider, ready = true, position = At(4, 5), personalEaten = 3, lastMealAt = m.HuntAt };
                m.Players.Add(owner2);
                m.SetInput(1, new SwimInput { layEgg = true }); m.SetInput(4, new SwimInput { layEgg = true }); m.Tick(0.1f);
                Check(m.Eggs.Count == 1 && !owner2.eggUsed, "dead teammate reserved once");
                m.Player(1).alive = owner2.alive = false; m.Tick(0.1f); Check(m.Phase == RoundPhase.Playing, "egg keeps wiped team in game");
                Advance(m, 12.1f); Check(m.Player(3).alive && m.Phase == RoundPhase.Playing, "hatch rescues wiped team");
            });
            Test("one harpoon destroys an egg and completes wipe before hatching", () => {
                var m = Match(true); Advance(m, 5.1f); var owner = m.Player(1); owner.personalEaten = 3; m.Player(3).alive = false;
                m.SetInput(1, new SwimInput { layEgg = true }); m.Tick(0.1f); m.Eggs[0].position = At(1.3f, 0); owner.alive = false;
                m.Player(2).position = At(0, 0); Act(m, 2);
                Check(m.Eggs.Count == 0 && m.WinReason == WinReason.AllAliensCaught && !m.Player(3).alive && m.Effects.Exists(e => e.kind == "eggBreak"), "egg destroyed in one hit");
            });
            Test("NPCs flee a nearby diver but bait takes precedence", () => {
                var m = Match(); Advance(m, 5.1f); var diver = m.Player(2); diver.position = At(0, 6); var fish = m.Fish[0]; fish.position = At(3, 6); fish.facing = new Point();
                m.Tick(0.1f); Check(fish.position.x > 3, "flee diver");
                m.SetInput(2, new SwimInput { aimX = 1, feed = true }); m.Tick(0.1f); float before = fish.position.x; m.Tick(0.1f);
                Check(fish.position.x < before, "bait overrides flight");
            });
            Test("wipe condition takes priority over simultaneous food completion", () => {
                var m = Match(); m.Player(1).alive = false; foreach (var fish in m.Fish) fish.eaten = true; m.Tick(0.1f);
                Check(m.Winner == (int)Team.Seeker && m.WinReason == WinReason.AllAliensCaught, "wipe wins deterministic tie");
            });
            Test("practice diver acts on visible evidence and visible eggs only", () => {
                var m = Match(true); Advance(m, 5.1f); var diver = m.Player(2); diver.bot = true;
                diver.position = At(0, 0, 4); diver.facing = new Point(-1, 0); m.Player(1).position = At(3, 0, 4); m.Player(3).position = At(12, 7, 4);
                m.Tick(0.1f); Check(diver.nextAction == 0, "does not target alien behind view");
                diver.position = At(-10, -2, 1); diver.facing = new Point(1, 0); m.Player(1).position = At(-3, -2, 1);
                m.Tick(0.1f); Check(diver.nextAction == 0, "does not track alien through rock");
                diver.position = At(0, 0, 4); diver.facing = new Point(1, 0); m.Player(1).position = At(-12, 7, 4); m.Player(3).alive = false;
                m.Eggs.Add(new RevivalEgg { id = 99, owner = 1, revivePlayer = 3, position = At(1.3f, 0, 4), hatchAt = m.Time + 12 });
                m.Tick(0.1f); Check(m.Eggs.Count == 0 && diver.nextAction > m.Time && m.Effects.Exists(e => e.kind == "eggBreak"), "visible egg counterplay");
            });
            Console.WriteLine("RESULT: " + passed + " gameplay tests passed."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL " + ex.Message); return 1; }
    }
}
