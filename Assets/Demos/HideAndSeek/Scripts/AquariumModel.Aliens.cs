namespace PrototypeLab.Aquarium
{
    // 2026-10-28: which alien each player plays (AquariumAlienKinds). Picked in the lobby and kept between
    // rounds; the host owns it like the team. Per-kind abilities will be ruled here once they are designed.
    public sealed partial class AquariumModel
    {
        public void SetAlien(int id, int kind)
        {
            var p = Player(id);
            if (p == null || Phase != RoundPhase.Lobby) return;
            p.alienKind = AquariumAlienKinds.Clamp(kind);
        }
    }
}
