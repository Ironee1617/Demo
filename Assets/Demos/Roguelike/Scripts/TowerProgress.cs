using System;
using System.Collections.Generic;
using System.Globalization;

namespace PrototypeLab.Roguelike
{
    // Unity-independent floor points. Save() can be stored directly in PlayerPrefs.
    // Every mythology is open from the start; points are spent in shop floors.
    public sealed class TowerProgress
    {
        private readonly Dictionary<string, int> creditedFloors = new Dictionary<string, int>();
        public int Points { get; private set; }
        public bool TrySpend(int amount)
        {
            if (amount < 0 || Points < amount) return false;
            Points -= amount; return true;
        }
        public int RecordFloorClear(TowerRun run)
        {
            if (run == null || run.IsOathTrial) return 0;
            int previous; creditedFloors.TryGetValue(run.RunId, out previous);
            int earned = Math.Max(0, run.ClearedFloors - previous);
            if (earned == 0) return 0;
            creditedFloors[run.RunId] = run.ClearedFloors;
            Points = (int)Math.Min(Int32.MaxValue, (long)Points + earned);
            return earned;
        }
        public string Save()
        {
            var runs = new List<string>();
            foreach (var entry in creditedFloors) runs.Add(entry.Key + ":" + entry.Value.ToString(CultureInfo.InvariantCulture));
            runs.Sort(StringComparer.Ordinal);
            return "2|" + Points.ToString(CultureInfo.InvariantCulture) + "|" + String.Join(",", runs.ToArray());
        }
        public static TowerProgress Load(string saved)
        {
            var progress = new TowerProgress();
            if (String.IsNullOrEmpty(saved)) return progress;
            string[] fields = saved.Split('|'); int points;
            // Version 1 also stored mythology purchases; only its points and credited runs carry over.
            bool current = fields.Length == 3 && fields[0] == "2", legacy = fields.Length == 5 && fields[0] == "1";
            if (!current && !legacy || !Int32.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out points) || points < 0) return progress;
            progress.Points = points;
            foreach (string entry in fields[fields.Length - 1].Split(','))
            {
                string[] pair = entry.Split(':'); Guid runId; int floors;
                if (pair.Length != 2 || !Guid.TryParseExact(pair[0], "N", out runId) || !Int32.TryParse(pair[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out floors) || floors < 0) continue;
                int existing; progress.creditedFloors.TryGetValue(pair[0], out existing);
                progress.creditedFloors[pair[0]] = Math.Max(existing, floors);
            }
            return progress;
        }
    }
}
