using System;

namespace PrototypeLab.Aquarium
{
    // R27: what a swimmer is doing at a return drain.
    public enum DrainTask { None, Screw, Lid, Enter, Cover }

    // R27: escaping through a return drain (host rules).
    //  * An alien in its own form aims at a screw and presses F: one tentacle reaches out and backs the
    //    screw out over ScrewSeconds. Progress stays on the screw if the alien lets go, so several aliens
    //    can work on different screws and come back later.
    //  * With all six screws out, F on the lid drags it aside over LidSeconds (heavy).
    //  * F on the open hole starts the squeeze: the body and arms work into the pipe over SqueezeSeconds.
    //    A harpoon hit pops it straight back out; a second hit kills it and the spear reels it in as usual.
    //    Finishing the squeeze is an escape: that alien wins and leaves the tank.
    //  * The diver presses E at a moved lid and pushes it back over CoverSeconds (not while an alien is
    //    wedged in the hole). The screws stay loose, so the aliens only have to drag the lid again.
    public sealed partial class AquariumModel
    {
        public const float ScrewSeconds = 20f, LidSeconds = 10f, SqueezeSeconds = 10f, CoverSeconds = 2f;
        // R28: the diver sees where an alien got out (the "escape" effect) for this long, through walls.
        public const float EscapeMarkSeconds = 10f;
        public const float ScrewReach = 2.6f, LidReach = 2.7f, HoleReach = 2.6f, CoverReach = 3.0f, DrainSlack = .6f;
        public const int EscapeScore = 300;
        // Interaction parts: 0-5 screws, then the lid and the open hole.
        public const int DrainLidPart = 6, DrainHolePart = 7;
        // Where an alien hovers while it squeezes in: its rounded hull just touching the mouth.
        public const float SqueezeHover = .55f, SqueezeSettleSeconds = .6f;

        public AquariumDrainPlacement[] Drains => drains;
        public static Point DrainPartPoint(AquariumDrainPlacement d, int part)
        {
            if (part >= 0 && part < AquariumDrainLayout.ScrewCount) return AquariumDrainLayout.ScrewPoint(d, part);
            if (part == DrainLidPart) return AquariumDrainLayout.LidCenter(d);
            return AquariumDrainLayout.Mouth(d);
        }
        public static float DrainReach(int part, Team team)
        { return team == Team.Seeker ? CoverReach : part == DrainLidPart ? LidReach : part == DrainHolePart ? HoleReach : ScrewReach; }
        public static Point SqueezePoint(AquariumDrainPlacement d) { return AquariumDrainLayout.Mouth(d) + new Point(0, SqueezeHover, 0); }

        // Shared by the host and the local crosshair prompt: can this swimmer start on this part now?
        // reason is a short Korean hint for the HUD when it cannot (empty when it is only out of reach).
        public static bool CanUseDrainPart(Swimmer p, AquariumDrainPlacement[] all, int index, int part, Swimmer[] players, float time, float huntAt, out string reason)
        {
            reason = "";
            if (p == null || all == null || index < 0 || index >= all.Length || !p.alive || p.swallowedBy != 0) return false;
            var d = all[index];
            if (time < huntAt) { reason = "준비 시간"; return false; }
            if (p.team == Team.Seeker)
            {
                if (part != DrainLidPart || d.lid <= 0) return false;
                if (p.entryAt > time) return false;
            }
            else
            {
                if (p.species >= 0) { reason = "외계인 상태에서만"; return false; }
                if (time < p.stunnedUntil || p.layEndsAt > 0) return false;
                if (part >= 0 && part < AquariumDrainLayout.ScrewCount)
                {
                    if (d.lid > 0 || d.screws == null || d.screws[part] >= 1) return false;
                }
                else if (part == DrainLidPart)
                {
                    if (d.lid >= 1) return false;
                    if (!AquariumDrainLayout.AllScrewsOut(d)) { reason = "나사 " + AquariumDrainLayout.ScrewsOut(d) + " / " + AquariumDrainLayout.ScrewCount; return false; }
                }
                else if (part == DrainHolePart) { if (!d.isOpen) return false; }
                else return false;
            }
            var target = DrainPartPoint(d, part);
            // R29: too far away is simply not usable (no "come closer" hint on the crosshair).
            if ((target - p.position).Length > DrainReach(part, p.team)) return false;
            if (players != null) foreach (var other in players)
            {
                if (other == p || other.drainIndex != index || other.drainTask == DrainTask.None) continue;
                bool busy = part < AquariumDrainLayout.ScrewCount ? other.drainTask == DrainTask.Screw && other.drainScrew == part
                    : part == DrainHolePart ? other.drainTask == DrainTask.Enter
                    : p.team == Team.Seeker ? other.drainTask == DrainTask.Cover : other.drainTask == DrainTask.Lid;
                if (busy) { reason = "다른 외계인이 사용 중"; return false; }
            }
            return true;
        }

        private void DrainInteract(Swimmer p, SwimInput input)
        {
            if (drains.Length == 0) return;
            bool same = p.drainTask != DrainTask.None && input.interactDrain == p.drainIndex
                && (p.drainTask == DrainTask.Screw ? input.interactPart == p.drainScrew : p.drainTask == DrainTask.Enter ? input.interactPart == DrainHolePart : input.interactPart == DrainLidPart);
            // Pressing again lets go (or backs out of the hole); a different target switches over.
            if (p.drainTask != DrainTask.None) { CancelDrainTask(p, true); if (same) return; }
            int index = input.interactDrain, part = input.interactPart; string reason;
            if (!CanUseDrainPart(p, drains, index, part, Players.ToArray(), Time, HuntAt, out reason)) return;
            p.drainIndex = index; p.drainAt = Time; p.drainProgress = 0;
            if (p.team == Team.Seeker) { p.drainTask = DrainTask.Cover; p.velocity = new Point(); return; }
            if (part < AquariumDrainLayout.ScrewCount) { p.drainTask = DrainTask.Screw; p.drainScrew = part; Effect("screwGrip", DrainPartPoint(drains[index], part), p.facing, .4f, p.id); }
            else if (part == DrainLidPart) { p.drainTask = DrainTask.Lid; Effect("lidGrip", DrainPartPoint(drains[index], part), p.facing, .4f, p.id); }
            else
            {
                p.drainTask = DrainTask.Enter; p.drainFrom = p.position; p.velocity = new Point();
                var flat = new Point(p.facing.x, 0, p.facing.z); p.facing = flat.Length > .01f ? flat.Normalized : new Point(0, 0, 1);
                Effect("squeeze", AquariumDrainLayout.Mouth(drains[index]), new Point(0, -1, 0), 1f, p.id);
            }
        }

        private static void ClearDrainTask(Swimmer p)
        { p.drainTask = DrainTask.None; p.drainIndex = -1; p.drainScrew = -1; p.drainProgress = 0; }

        // Letting go of a screw or the lid keeps their progress; leaving the hole puts the alien back above it.
        // R29: a harpoon hit only pops it out where it hovers (no lift, no kick): a first hit never moves it away.
        private void CancelDrainTask(Swimmer p, bool voluntary)
        {
            if (p.drainTask == DrainTask.None) return;
            if (p.drainTask == DrainTask.Enter && p.drainIndex >= 0 && p.drainIndex < drains.Length && p.alive)
            {
                var up = SqueezePoint(drains[p.drainIndex]) + new Point(0, voluntary ? .55f : .12f, 0);
                p.position = Constrain(up, .4f); p.velocity = voluntary ? new Point(0, 1.2f, 0) : new Point();
                Effect(voluntary ? "squeezeOut" : "squeezePop", AquariumDrainLayout.Mouth(drains[p.drainIndex]), new Point(0, 1, 0), .8f, p.id);
            }
            ClearDrainTask(p);
        }

        private bool DrainTaskValid(Swimmer p)
        {
            if (Phase != RoundPhase.Playing || Time >= EndsAt || !p.alive || p.swallowedBy != 0 || p.drainIndex < 0 || p.drainIndex >= drains.Length) return false;
            if (IsReleaseKick(p.visualAction) && Time < p.visualAt + ReleaseKickSeconds) return false;
            if (p.team == Team.Seeker) return p.drainTask == DrainTask.Cover;
            return p.drainTask != DrainTask.Cover && p.species < 0 && Time >= p.stunnedUntil && p.layEndsAt <= 0;
        }

        // The squeeze holds the alien over the mouth; only F (back out) is read.
        private void TickSqueeze(Swimmer p, SwimInput input, float dt)
        {
            if (input != null && input.interact) { CancelDrainTask(p, true); return; }
            var d = drains[p.drainIndex];
            float settle = Math.Min(1, (Time - p.drainAt) / SqueezeSettleSeconds); settle = settle * settle * (3 - 2 * settle);
            var hold = SqueezePoint(d);
            p.position = p.drainFrom + (hold - p.drainFrom) * settle; p.velocity = new Point();
            p.stamina = Math.Min(MaxStamina, p.stamina + 18f * dt);
            p.drainProgress = Math.Min(1, p.drainProgress + dt / SqueezeSeconds);
            if (p.drainProgress >= 1) Escape(p);
        }

        private void TickDrains(float dt)
        {
            if (drains.Length == 0) return;
            foreach (var p in Players)
            {
                if (p.drainTask == DrainTask.None) continue;
                if (!DrainTaskValid(p)) { CancelDrainTask(p, false); continue; }
                int i = p.drainIndex;
                if (p.drainTask == DrainTask.Enter) { if (!drains[i].isOpen) CancelDrainTask(p, false); continue; }
                int part = p.drainTask == DrainTask.Screw ? p.drainScrew : DrainLidPart;
                if ((DrainPartPoint(drains[i], part) - p.position).Length > DrainReach(part, p.team) + DrainSlack) { CancelDrainTask(p, false); continue; }
                if (p.drainTask == DrainTask.Screw)
                {
                    if (drains[i].lid > 0 || drains[i].screws[p.drainScrew] >= 1) { ClearDrainTask(p); continue; }
                    drains[i].screws[p.drainScrew] = Math.Min(1, drains[i].screws[p.drainScrew] + dt / ScrewSeconds);
                    if (drains[i].screws[p.drainScrew] >= 1)
                    { Effect("screwOut", DrainPartPoint(drains[i], p.drainScrew), new Point(0, 1, 0), .6f, p.id); ClearDrainTask(p); }
                }
                else if (p.drainTask == DrainTask.Lid)
                {
                    if (!AquariumDrainLayout.AllScrewsOut(drains[i])) { ClearDrainTask(p); continue; }
                    drains[i].lid = Math.Min(1, drains[i].lid + dt / LidSeconds);
                    if (drains[i].lid >= 1)
                    { drains[i].isOpen = true; Effect("lidOpen", AquariumDrainLayout.Mouth(drains[i]), new Point(0, 1, 0), 1f, p.id); ClearDrainTask(p); }
                }
                else
                {
                    // Covering waits while an alien is wedged in the hole: it has to be speared out first.
                    p.velocity = new Point();
                    bool wedged = false;
                    foreach (var a in Players) if (a.drainTask == DrainTask.Enter && a.drainIndex == i) { wedged = true; break; }
                    p.drainProgress = wedged ? -1 : 0;
                    if (wedged || drains[i].lid <= 0) continue;
                    drains[i].isOpen = false;
                    drains[i].lid = Math.Max(0, drains[i].lid - dt / CoverSeconds);
                    if (drains[i].lid <= 0) { Effect("lidClosed", AquariumDrainLayout.Mouth(drains[i]), new Point(0, 1, 0), 1f, p.id); ClearDrainTask(p); }
                }
            }
        }

        // The alien is through: a personal win. It leaves the tank (no body, no revival).
        private void Escape(Swimmer p)
        {
            var mouth = AquariumDrainLayout.Mouth(drains[p.drainIndex]);
            ClearDrainTask(p);
            p.escaped = true; p.escapedAt = Time; p.score += EscapeScore;
            p.alive = false; p.collected = true; p.deathReason = "escaped"; p.deathSpecies = -1;
            p.diedAt = Time; p.deathAt = Time; p.visualAction = "Escaped"; p.visualAt = Time;
            p.velocity = new Point(); p.layEndsAt = 0; p.bloodTrailUntil = 0; p.revealAt = 0;
            inputs.Remove(p.id); inputTimes.Remove(p.id); ReleaseCarried(p.id);
            Effect("escape", mouth, new Point(0, -1, 0), EscapeMarkSeconds, p.id);
        }
    }
}
