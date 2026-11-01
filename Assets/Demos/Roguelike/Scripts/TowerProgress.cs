using System;
using System.Globalization;

namespace PrototypeLab.Roguelike
{
    // Unity-independent account wallet. Save() can be stored directly in PlayerPrefs.
    // Gold is picked up from the breakable caches on battle floors, spent at shop floors and kept after a climb ends.
    public sealed class TowerProgress
    {
        // Floor points were the shop currency before gold; an old balance keeps its buying power.
        public const int LegacyPointGold = 10;
        public int Gold { get; private set; }
        /// <summary>Adds picked-up gold, saturating at Int32.MaxValue; returns the amount actually added.</summary>
        public int AddGold(int amount)
        {
            if (amount <= 0) return 0;
            int before = Gold;
            Gold = (int)Math.Min(Int32.MaxValue, (long)Gold + amount);
            return Gold - before;
        }
        public bool TrySpend(int amount)
        {
            if (amount < 0 || Gold < amount) return false;
            Gold -= amount; return true;
        }
        public string Save() { return "3|" + Gold.ToString(CultureInfo.InvariantCulture); }
        public static TowerProgress Load(string saved)
        {
            var progress = new TowerProgress();
            if (String.IsNullOrEmpty(saved)) return progress;
            string[] fields = saved.Split('|'); int amount;
            // Version 2 held floor points and per-run receipts, version 1 also mythology purchases; only the balance carries over.
            bool current = fields.Length == 2 && fields[0] == "3", points = fields.Length == 3 && fields[0] == "2" || fields.Length == 5 && fields[0] == "1";
            if (!current && !points || !Int32.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out amount) || amount < 0) return progress;
            progress.Gold = current ? amount : (int)Math.Min(Int32.MaxValue, (long)amount * LegacyPointGold);
            return progress;
        }
    }
}
