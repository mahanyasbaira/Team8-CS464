using System.Globalization;
using System.Text;

namespace Team8.Core
{
    public static class CsvUtil
    {
        // Always "." as decimal point, even on a laptop/headset set to another locale
        public static string Num(double v, int decimals = 3)
        {
            return v.ToString("F" + decimals, CultureInfo.InvariantCulture);
        }

        public static string Num(double? v, int decimals = 3)
        {
            return v.HasValue ? Num(v.Value, decimals) : "";
        }

        public static string Int(long v)
        {
            return v.ToString(CultureInfo.InvariantCulture);
        }

        public static string Escape(string s)
        {
            if (s == null) return "";
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        public static string Row(params string[] fields)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Escape(fields[i]));
            }
            return sb.ToString();
        }
    }

    // Column names, shared with analysis/ (see docs/PLAN.md section 2)
    public static class CsvSchema
    {
        public const string PositionsFile = "positions.csv";
        public const string EventsFile = "events.csv";
        public const string TrialsFile = "trials.csv";

        public static readonly string[] PositionColumns =
        {
            "participant", "trial", "condition", "layout", "t", "unix_ms",
            "x", "z", "yaw", "room", "cell_x", "cell_z", "cell_id"
        };

        public static readonly string[] EventColumns =
        {
            "participant", "trial", "condition", "layout", "t", "unix_ms", "event", "detail"
        };

        public static readonly string[] TrialColumns =
        {
            "participant", "latin_row", "trial", "condition", "layout",
            "completion_s", "timed_out", "aborted", "lever1_s", "lever2_s", "lever3_s",
            "backtracks", "cells_entered", "unique_cells", "teleports", "path_m", "app_version"
        };
    }
}
