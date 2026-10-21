using System;
using System.Collections.Generic;
using System.Globalization;

namespace PrototypeLab.Roguelike
{
    // Unity-independent metaprogression. Save() can be stored directly in PlayerPrefs.
    public sealed class TowerProgress
    {
        private readonly HashSet<string> unlocked = new HashSet<string> { TowerMythCatalog.StarterMythId };
        private readonly Dictionary<string, int> creditedFloors = new Dictionary<string, int>();
        public int Points { get; private set; }
        public string SelectedMythId { get; private set; }
        public bool HasSelectedMyth { get { return !String.IsNullOrEmpty(SelectedMythId) && IsUnlocked(SelectedMythId); } }
        public TowerProgress() { SelectedMythId = ""; }
        public bool IsUnlocked(string id) { return id != null && unlocked.Contains(id); }
        public bool TryPurchase(string id)
        {
            var myth = TowerMythCatalog.Get(id);
            if (myth == null || IsUnlocked(id) || Points < myth.Price) return false;
            Points -= myth.Price; unlocked.Add(id); return true;
        }
        public bool TrySelect(string id)
        { if (!IsUnlocked(id)) return false; SelectedMythId = id; return true; }
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
            var myths = new List<string>(unlocked); myths.Sort(StringComparer.Ordinal);
            var runs = new List<string>();
            foreach (var entry in creditedFloors) runs.Add(entry.Key + ":" + entry.Value.ToString(CultureInfo.InvariantCulture));
            runs.Sort(StringComparer.Ordinal);
            return "1|" + Points.ToString(CultureInfo.InvariantCulture) + "|" + SelectedMythId + "|" + String.Join(",", myths.ToArray()) + "|" + String.Join(",", runs.ToArray());
        }
        public static TowerProgress Load(string saved)
        {
            var progress = new TowerProgress();
            if (String.IsNullOrEmpty(saved)) return progress;
            string[] fields = saved.Split('|'); int points;
            if (fields.Length != 5 || fields[0] != "1" || !Int32.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out points) || points < 0) return progress;
            progress.Points = points;
            foreach (string id in fields[3].Split(',')) if (TowerMythCatalog.Get(id) != null) progress.unlocked.Add(id);
            if (progress.IsUnlocked(fields[2])) progress.SelectedMythId = fields[2];
            foreach (string entry in fields[4].Split(','))
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
