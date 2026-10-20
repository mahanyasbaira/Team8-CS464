using System.Collections.Generic;

namespace Team8.Core
{
    // Which decoy props were grabbed during one trial (unique names, in first-grab order).
    public class GrabLog
    {
        readonly List<string> names = new List<string>();
        readonly Dictionary<string, double> firstTimes = new Dictionary<string, double>();

        public int Count => names.Count;
        public IReadOnlyList<string> Names => names;
        public int TotalGrabs { get; private set; }

        // Returns true the first time a prop is grabbed in this trial.
        public bool Add(string prop, double time)
        {
            if (string.IsNullOrEmpty(prop)) return false;
            TotalGrabs++;
            if (firstTimes.ContainsKey(prop)) return false;
            firstTimes[prop] = time;
            names.Add(prop);
            return true;
        }

        public double? FirstTime(string prop)
        {
            double t;
            return firstTimes.TryGetValue(prop, out t) ? t : (double?)null;
        }

        public void Clear()
        {
            names.Clear();
            firstTimes.Clear();
            TotalGrabs = 0;
        }

        // Short text for the operator panel, e.g. "mug, book, crate (+2)"
        public string Summary(int max = 5)
        {
            if (names.Count == 0) return "none";
            int shown = names.Count < max ? names.Count : max;
            string s = string.Join(", ", names.GetRange(0, shown));
            return names.Count > max ? s + " (+" + (names.Count - max) + ")" : s;
        }
    }
}
