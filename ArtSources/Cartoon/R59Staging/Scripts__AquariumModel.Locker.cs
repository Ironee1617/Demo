using System;

namespace PrototypeLab.Aquarium
{
    public sealed partial class AquariumModel
    {
        // R59: a diver waiting in the changing room (AquariumLockerRoom) walks about on the floor: the swim input
        // flattened to the floor, walk / run (Shift) speed, no actions, no stamina use. Bots stand still.
        private void TickLocker(Swimmer p, float dt)
        {
            SwimInput input = null;
            if (!p.bot && inputs.TryGetValue(p.id, out input) && Time - inputTimes[p.id] > 0.4f) input = null;
            var move = input == null ? new Point() : new Point(input.x, 0, input.z);
            if (move.Length > 1) move = move.Normalized;
            float speed = input != null && input.sprint ? AquariumLockerRoom.RunSpeed : AquariumLockerRoom.WalkSpeed;
            var wanted = move * speed;
            var change = wanted - p.velocity; float step = 14f * dt;
            p.velocity = change.Length <= step ? wanted : p.velocity + change.Normalized * step;
            p.velocity.y = 0;
            var before = p.position;
            p.position = AquariumLockerRoom.Clamp(p.position + p.velocity * dt);
            if (dt > 0) { var real = (p.position - before) / dt; p.velocity = new Point(real.x, 0, real.z); }
            if (input != null)
            {
                var aim = new Point(input.aimX, 0, input.aimZ);
                if (aim.Length > .1f) p.facing = aim.Normalized;
                input.action = input.morph = input.feed = input.scan = input.layEgg = input.unmorph = input.interact = input.ability = false;
            }
            p.stamina = StaminaMax(p);
        }
    }
}
