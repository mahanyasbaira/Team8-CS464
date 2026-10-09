namespace Team8.Core
{
    // One row of trials.csv
    public class TrialResult
    {
        public const double TimeLimitSeconds = 300.0;

        public string Participant;
        public int LatinRow;
        public int Trial;
        public Condition Condition;
        public char Layout;

        public double CompletionSeconds;
        public bool TimedOut;
        public bool Aborted;
        public double?[] LeverSeconds = new double?[3];

        public int Backtracks;
        public int CellsEntered;
        public int UniqueCells;
        public int Teleports;
        public double PathMetres;
        public string AppVersion;

        public string[] ToCsvFields()
        {
            return new[]
            {
                Participant,
                CsvUtil.Int(LatinRow),
                CsvUtil.Int(Trial),
                ConditionNames.ToCsv(Condition),
                Layout.ToString(),
                CsvUtil.Num(CompletionSeconds),
                TimedOut ? "1" : "0",
                Aborted ? "1" : "0",
                CsvUtil.Num(LeverSeconds[0]),
                CsvUtil.Num(LeverSeconds[1]),
                CsvUtil.Num(LeverSeconds[2]),
                CsvUtil.Int(Backtracks),
                CsvUtil.Int(CellsEntered),
                CsvUtil.Int(UniqueCells),
                CsvUtil.Int(Teleports),
                CsvUtil.Num(PathMetres),
                AppVersion ?? ""
            };
        }
    }
}
