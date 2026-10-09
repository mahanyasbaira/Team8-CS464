using System.Collections;
using Team8.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

// Logs head x/z/yaw at 10 Hz during a trial, plus trial events, and keeps
// the live backtrack count. The analysis script recomputes backtracks from positions.csv.
public class PositionLogger : MonoBehaviour
{
    public Transform head;
    public RoomZone[] zones;
    [Tooltip("Room 1's outer corner. Leave empty to use the world origin.")]
    public Transform gridOrigin;
    public TeleportationProvider teleportProvider;

    public float sampleHz = 10f;
    public float cellSize = 1f;
    public float hysteresis = 0.15f;

    public bool IsLogging { get; private set; }
    public int Teleports { get; private set; }
    public double PathMetres { get; private set; }
    public GridBacktrackCounter Grid { get; private set; }
    public Vector3 HeadPosition => head.position;

    StudyDataWriter writer;
    string participant;
    int trial;
    string condition;
    string layout;
    double startTime;
    double nextSample;
    Vector3 lastPos;
    bool hasLast;
    Vector3 teleportFrom;

    void OnEnable()
    {
        if (teleportProvider == null) return;
        teleportProvider.locomotionStarted += OnTeleportStarted;
        teleportProvider.locomotionEnded += OnTeleportEnded;
    }

    void OnDisable()
    {
        if (teleportProvider == null) return;
        teleportProvider.locomotionStarted -= OnTeleportStarted;
        teleportProvider.locomotionEnded -= OnTeleportEnded;
    }

    public void Begin(StudyDataWriter w, string participantId, int trialNumber, Condition c, char layoutLetter, double trialStartTime)
    {
        writer = w;
        participant = participantId;
        trial = trialNumber;
        condition = ConditionNames.ToCsv(c);
        layout = layoutLetter.ToString();
        startTime = trialStartTime;

        Grid = new GridBacktrackCounter(cellSize, hysteresis);
        Teleports = 0;
        PathMetres = 0;
        hasLast = false;
        IsLogging = true;
        Sample(startTime);
        nextSample = startTime + 1.0 / sampleHz;
    }

    public void End()
    {
        if (!IsLogging) return;
        Sample(Time.timeAsDouble);
        IsLogging = false;
    }

    void Update()
    {
        if (!IsLogging) return;
        double now = Time.timeAsDouble;
        if (now < nextSample) return;
        Sample(now);
        nextSample += 1.0 / sampleHz;
        if (nextSample < now) nextSample = now + 1.0 / sampleHz; // after a hitch don't try to catch up
    }

    void Sample(double now)
    {
        Vector3 p = head.position;
        Vector3 o = gridOrigin != null ? gridOrigin.position : Vector3.zero;
        double gx = p.x - o.x;
        double gz = p.z - o.z;

        Grid.AddSample(gx, gz);
        if (hasLast) PathMetres += Vector2.Distance(new Vector2(lastPos.x, lastPos.z), new Vector2(p.x, p.z));
        lastPos = p;
        hasLast = true;

        writer.WritePosition(
            participant, CsvUtil.Int(trial), condition, layout,
            CsvUtil.Num(now - startTime), CsvUtil.Int(StudyDataWriter.UnixMs()),
            CsvUtil.Num(gx), CsvUtil.Num(gz), CsvUtil.Num(head.eulerAngles.y, 1),
            CsvUtil.Int(RoomZone.Find(zones, p)),
            CsvUtil.Int(Grid.CellX), CsvUtil.Int(Grid.CellZ), GridBacktrackCounter.CellId(Grid.CellX, Grid.CellZ));
    }

    public void WriteEvent(string evt, string detail = "")
    {
        if (writer == null) return;
        writer.WriteEvent(
            participant, CsvUtil.Int(trial), condition, layout,
            CsvUtil.Num(Time.timeAsDouble - startTime), CsvUtil.Int(StudyDataWriter.UnixMs()),
            evt, detail);
    }

    void OnTeleportStarted(LocomotionProvider provider)
    {
        if (IsLogging) teleportFrom = head.position;
    }

    void OnTeleportEnded(LocomotionProvider provider)
    {
        if (IsLogging) StartCoroutine(LogTeleportNextFrame());
    }

    IEnumerator LogTeleportNextFrame()
    {
        yield return null; // rig has moved by now
        if (!IsLogging) yield break;
        Teleports++;
        Vector3 o = gridOrigin != null ? gridOrigin.position : Vector3.zero;
        Vector3 to = head.position;
        WriteEvent("teleport", string.Join(";",
            CsvUtil.Num(teleportFrom.x - o.x), CsvUtil.Num(teleportFrom.z - o.z),
            CsvUtil.Num(to.x - o.x), CsvUtil.Num(to.z - o.z)));
        Sample(Time.timeAsDouble); // log the landing straight away
    }
}
