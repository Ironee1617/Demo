using System;
using System.Collections.Generic;

namespace PrototypeLab.Roguelike
{
    public sealed class OathAugmentDefinition
    {
        public readonly string Id, OwnerId, Name, Description;
        internal OathAugmentDefinition(string id, string owner, string name, string description)
        { Id = id; OwnerId = owner; Name = name; Description = description; }
    }

    // A connected player swing is one charge event, regardless of how many enemies it hits.
    public sealed class OathState
    {
        public const string Echo = "oath.echo", Dodge = "oath.dodge", Overdrive = "oath.overdrive";
        public const string MichaelCircle = "oath.michael.circle", MichaelWave = "oath.michael.wave";
        public const string GabrielFan = "oath.gabriel.fan", GabrielChain = "oath.gabriel.chain";
        public const string RaphaelOverflow = "oath.raphael.overflow", RaphaelGarden = "oath.raphael.garden";
        public static readonly IReadOnlyList<OathAugmentDefinition> Augments = Array.AsReadOnly(new[] {
            new OathAugmentDefinition(Echo, "", "메아리 서약", "연계가 0.3초 뒤 같은 위치에서 60% 위력으로 한 번 더 발동합니다.\n대신 필요한 충전이 2 증가합니다."),
            new OathAugmentDefinition(Dodge, "", "찰나의 맹세", "정확한 회피에 성공하면 서약이 즉시 완충됩니다.\n완충 후 다음 검격 적중으로 발동합니다."),
            new OathAugmentDefinition(Overdrive, "", "무거운 맹세", "연계의 피해·회복·보호막이 2배가 됩니다.\n대신 필요한 충전이 2 증가합니다."),
            new OathAugmentDefinition(MichaelCircle, "michael", "심판의 원", "전방 합동 참격이 반경 4.2의 전방위 참격으로 바뀝니다.\n뒤쪽의 적도 함께 공격합니다."),
            new OathAugmentDefinition(MichaelWave, "michael", "검의 행진", "합동 참격에 전방 관통 검기를 추가합니다.\n검기는 참격의 55% 피해로 7만큼 전진합니다."),
            new OathAugmentDefinition(GabrielFan, "gabriel", "세 갈래 계시", "관통 광선이 세 방향으로 펼쳐집니다.\n각 광선은 원래 피해의 65%를 줍니다."),
            new OathAugmentDefinition(GabrielChain, "gabriel", "전하는 빛", "각 광선이 처음 맞힌 적에서 반경 3.5 안의 다른 적 둘에게 빛이 튑니다.\n전달 피해는 광선의 50%입니다."),
            new OathAugmentDefinition(RaphaelOverflow, "raphael", "넘치는 은총", "최대 체력을 넘어 낭비되는 즉시 회복량의 2배를 보호막으로 전환합니다.\n전체 보호막 한도는 최대 체력의 65%입니다."),
            new OathAugmentDefinition(RaphaelGarden, "raphael", "안식의 자리", "연계를 발동한 자리에 3초간 치유 영역을 남깁니다.\n반경 2.2 안에 머물면 0.5초마다 추가 회복합니다.")
        });
        private readonly List<OathAugmentDefinition> acquired = new List<OathAugmentDefinition>();
        public string CompanionId { get; private set; }
        public int Charge { get; private set; }
        public int Activations { get; private set; }
        public int RequiredCharge { get { return 4 + (Has(Echo) ? 2 : 0) + (Has(Overdrive) ? 2 : 0); } }
        public float Power { get { return Has(Overdrive) ? 2 : 1; } }
        public bool Ready { get { return Charge >= RequiredCharge; } }
        public IReadOnlyList<OathAugmentDefinition> Acquired { get; private set; }
        public string Name { get { return CompanionId == "michael" ? "심판의 참격" : CompanionId == "gabriel" ? "계시의 광선" : "구원의 서약"; } }
        public string Description { get { return CompanionId == "michael" ? "검격과 함께 전방의 적을 넓게 벱니다." :
            CompanionId == "gabriel" ? "검격이 맞힌 적을 향해\n관통 광선을 발사합니다." : "검격 적중 시 체력을 회복하고\n3초간 보호막을 얻습니다."; } }
        public OathState(string companionId)
        {
            if (!Supports(companionId)) throw new ArgumentException("Unknown oath companion", "companionId");
            CompanionId = companionId; Acquired = acquired.AsReadOnly();
        }
        public static bool Supports(string id) { return id == "michael" || id == "gabriel" || id == "raphael"; }
        public static OathAugmentDefinition Get(string id)
        { foreach (var a in Augments) if (a.Id == id) return a; return null; }
        public bool Has(string id) { return acquired.Exists(a => a.Id == id); }
        public bool CanAcquire(OathAugmentDefinition a)
        { return a != null && (a.OwnerId == "" || a.OwnerId == CompanionId) && !Has(a.Id); }
        internal bool Acquire(string id)
        {
            var a = Get(id); if (!CanAcquire(a)) return false;
            bool wasReady = Ready; acquired.Add(a);
            if (wasReady) Charge = RequiredCharge;
            return true;
        }
        public bool OnSwordHit()
        {
            if (Ready) { Charge = 0; Activations++; return true; }
            Charge = Math.Min(RequiredCharge, Charge + 1); return false;
        }
        public void OnPerfectDodge() { Charge = Has(Dodge) ? RequiredCharge : Math.Min(RequiredCharge, Charge + 2); }
    }
}
