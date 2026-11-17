using System;
using System.Collections.Generic;

namespace PrototypeLab.Aquarium
{
    // R78b: what the rebuilt tutorials need from the host rules (the practice host only; nothing here is networked).
    //  * Tutorial: no endings and no clock (CheckWinner keeps EndsAt ten minutes away), the hide phase and the diver's
    //    three-minute ability lock are already over when the lesson starts.
    //  * TutorialInput: every swimmer's input passes through it (scripted bots; the QA driver steers the player with it).
    //  * The lesson places its own drains, fish and swimmers in the tutorial tank (AquariumMaps.Tutorial).
    public sealed partial class AquariumModel
    {
        public bool Tutorial;
        public Func<Swimmer, SwimInput, SwimInput> TutorialInput;

        // Right after StartRound: the lesson's drains (sites on the sand, lid slide directions), an empty tank, the hunt already on.
        public void TutorialBegin(Point[] drainSites, float[] drainYaw, float[] slideYaw)
        {
            Tutorial = true;
            HuntAt = Time - 200f; EndsAt = Time + 599f;
            var list = new AquariumDrainPlacement[drainSites.Length];
            for (int i = 0; i < list.Length; i++)
            {
                var p = drainSites[i]; p.y = AquariumSeabed.Height(p) + .04f * AquariumDrainLayout.ModelScale;
                list[i] = new AquariumDrainPlacement { position = p, yaw = drainYaw[i], slideYaw = slideYaw[i], screws = new float[AquariumDrainLayout.ScrewCount] };
            }
            drains = list;
            Fish.Clear(); schools.Clear(); Eggs.Clear(); Harpoons.Clear();
            foreach (var p in Players) { p.entryAt = 0; p.velocity = new Point(); p.starveAt = StarveAtFull(Time); }   // R86: full (a lesson never starves)
        }

        public Prey TutorialFish(int species, Point at, Point facing)
        {
            species = Math.Max(0, Math.Min(SpeciesCount - 1, species));
            var fish = new Prey { id = 100 + Fish.Count, species = species, position = Constrain(at, AquariumFishTraits.WallRadius(species)),
                facing = facing.Length > .01f ? facing.Normalized : new Point(1, 0, 0), turnAt = Time + Range(1, 4) };
            Fish.Add(fish);
            return fish;
        }

        public void TutorialSchool(int species, Point centre, Point facing, int size)
        {
            species = Math.Max(0, Math.Min(SpeciesCount - 1, species));
            facing = facing.Length > .01f ? facing.Normalized : new Point(1, 0, 0);
            var members = new List<Prey>(); var slots = SchoolSlots(size, AquariumFishTraits.Scale(species));
            for (int m = 0; m < size; m++)
            {
                var fish = new Prey { id = 100 + Fish.Count, species = species, facing = facing, turnAt = Time + Range(1, 4), school = schools.Count, slot = slots[m], wobblePhase = Range(0, 100) };
                fish.position = Constrain(centre + SchoolOffset(facing, slots[m]), AquariumFishTraits.WallRadius(species));
                members.Add(fish); Fish.Add(fish);
            }
            schools.Add(members);
        }

        public void TutorialPlace(int id, Point at, Point facing)
        {
            var p = Player(id); if (p == null) return;
            p.position = Constrain(at, p.team == Team.Seeker ? .5f : .4f); p.velocity = new Point();
            if (facing.Length > .01f) p.facing = facing.Normalized;
            p.lastBloodPosition = p.position;
        }

        public void TutorialEffect(string kind, Point at, Point direction, float duration, int owner)
        { Effect(kind, at, direction, duration, owner); }

        // The lid of this drain slid aside as if an alien had just finished dragging it (the diver's alarm follows the effect).
        public void TutorialOpenLid(int index, int owner)
        {
            if (index < 0 || index >= drains.Length) return;
            for (int s = 0; s < drains[index].screws.Length; s++) drains[index].screws[s] = 1;
            drains[index].lid = 1; drains[index].isOpen = true;
            Effect("lidOpen", AquariumDrainLayout.Mouth(drains[index]), new Point(0, 1, 0), 1f, owner);
        }
    }
}
