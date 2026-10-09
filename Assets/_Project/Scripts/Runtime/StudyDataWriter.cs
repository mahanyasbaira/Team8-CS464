using System;
using System.IO;
using Team8.Core;
using UnityEngine;

// Writes positions.csv, events.csv and trials.csv for one session into
// <persistentDataPath>/StudyData/<P05_20261118-143012>/
// A new folder every time, so nothing is ever overwritten (a resumed session gets its own folder).
public class StudyDataWriter : IDisposable
{
    public string Folder { get; }

    StreamWriter positions;
    StreamWriter events;
    StreamWriter trials;

    public StudyDataWriter(string participantId)
    {
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Folder = Path.Combine(Application.persistentDataPath, "StudyData", participantId + "_" + stamp);
        Directory.CreateDirectory(Folder);

        positions = Open(CsvSchema.PositionsFile, CsvSchema.PositionColumns);
        events = Open(CsvSchema.EventsFile, CsvSchema.EventColumns);
        trials = Open(CsvSchema.TrialsFile, CsvSchema.TrialColumns);
        Debug.Log("[StudyData] writing to " + Folder);
    }

    StreamWriter Open(string file, string[] header)
    {
        var w = new StreamWriter(Path.Combine(Folder, file), false, new System.Text.UTF8Encoding(false));
        w.NewLine = "\n";
        w.WriteLine(CsvUtil.Row(header));
        return w;
    }

    public void WritePosition(params string[] fields) => positions?.WriteLine(CsvUtil.Row(fields));
    public void WriteEvent(params string[] fields) => events?.WriteLine(CsvUtil.Row(fields));
    public void WriteTrial(TrialResult r) => trials?.WriteLine(CsvUtil.Row(r.ToCsvFields()));

    public void Flush()
    {
        positions?.Flush();
        events?.Flush();
        trials?.Flush();
    }

    public void Dispose()
    {
        Flush();
        positions?.Dispose();
        events?.Dispose();
        trials?.Dispose();
        positions = events = trials = null;
    }

    public static long UnixMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
