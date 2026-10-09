using System;

namespace Team8.Core
{
    public struct TrialSpec
    {
        public int TrialNumber;     // 1..3, position in the session
        public Condition Condition;
        public char Layout;         // 'A', 'B' or 'C'

        public override string ToString()
        {
            return ConditionNames.ToLabel(Condition) + "/" + Layout;
        }
    }

    // 3x3 Latin square from the proposal (slide 9).
    // Layout is tied to position: 1st trial = A, 2nd = B, 3rd = C.
    public static class LatinSquare
    {
        public const int TrialsPerSession = 3;

        static readonly Condition[][] Rows =
        {
            new[] { Condition.Joystick, Condition.Teleport, Condition.Trail },   // group 1
            new[] { Condition.Teleport, Condition.Trail, Condition.Joystick },   // group 2
            new[] { Condition.Trail, Condition.Joystick, Condition.Teleport },   // group 3
        };

        static readonly char[] Layouts = { 'A', 'B', 'C' };

        // P01 -> row 1, P02 -> row 2, P03 -> row 3, P04 -> row 1, ...
        public static int RowFor(int participantNumber)
        {
            if (participantNumber < 1)
                throw new ArgumentOutOfRangeException(nameof(participantNumber), "participant numbers start at 1");
            return (participantNumber - 1) % Rows.Length + 1;
        }

        public static TrialSpec[] OrderFor(int participantNumber)
        {
            var row = Rows[RowFor(participantNumber) - 1];
            var order = new TrialSpec[TrialsPerSession];
            for (int i = 0; i < TrialsPerSession; i++)
            {
                order[i] = new TrialSpec { TrialNumber = i + 1, Condition = row[i], Layout = Layouts[i] };
            }
            return order;
        }

        public static string FormatId(int participantNumber)
        {
            return "P" + participantNumber.ToString("00");
        }

        // "P05" or "5" -> 5. Returns -1 if it can't be read.
        public static int ParseId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return -1;
            id = id.Trim();
            if (id[0] == 'P' || id[0] == 'p') id = id.Substring(1);
            int n;
            return int.TryParse(id, out n) && n > 0 ? n : -1;
        }
    }
}
