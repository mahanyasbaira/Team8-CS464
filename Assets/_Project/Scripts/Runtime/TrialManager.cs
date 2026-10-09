using Team8.Core;
using Unity.XR.CoreUtils;
using UnityEngine;

// Runs the session: Lobby -> Practice -> Trial -> Lobby (break + SSQ) -> ... -> Done.
// One trial = find lever 1 (opens gate 1), lever 2 (opens gate 2), lever 3 (unlocks exit), walk out.
public class TrialManager : MonoBehaviour
{
    public enum State { Lobby, Practice, Running, Done }

    [Header("Study objects")]
    public SessionConfig session;
    public LocomotionSwitcher locomotion;
    public LayoutLoader layouts;
    public PositionLogger logger;
    public TeleportTrail trail;
    public XROrigin rig;

    [Header("Rooms")]
    public LeverController[] levers = new LeverController[3];
    public GateController[] gates = new GateController[2];
    public ExitDoor exitDoor;
    public LeverController practiceLever;

    [Header("Spawn points (blue arrow = facing)")]
    public Transform lobbySpawn;
    public Transform practiceSpawn;
    public Transform startSpawn;

    public float timeLimit = (float)TrialResult.TimeLimitSeconds;

    public State CurrentState { get; private set; } = State.Lobby;
    public int CurrentTrial { get; private set; } = 1;   // 1..3
    public TrialSpec CurrentSpec => session.Order[CurrentTrial - 1];
    public double Elapsed => CurrentState == State.Running ? Time.timeAsDouble - trialStart : 0;
    public string Message { get; private set; } = "";
    public string DataFolder => writer != null ? writer.Folder : "";
    public bool SessionStarted => writer != null;

    StudyDataWriter writer;
    TrialResult result;
    double trialStart;

    void OnEnable()
    {
        foreach (var l in levers) if (l != null) l.Flipped += OnLeverFlipped;
        if (practiceLever != null) practiceLever.Flipped += OnPracticeLever;
    }

    void OnDisable()
    {
        foreach (var l in levers) if (l != null) l.Flipped -= OnLeverFlipped;
        if (practiceLever != null) practiceLever.Flipped -= OnPracticeLever;
    }

    void Start()
    {
        ResetSession();
    }

    // Called by the operator panel when the participant ID or start trial changes.
    public void ResetSession()
    {
        if (CurrentState == State.Running) return;
        writer?.Dispose();
        writer = null;
        CurrentTrial = session.startTrial;
        CurrentState = State.Lobby;
        Place(lobbySpawn);
        Message = "Check participant and order, then press Practice.";
    }

    public void BeginPractice()
    {
        if (CurrentState != State.Lobby) return;
        locomotion.Apply(CurrentSpec.Condition);
        if (practiceLever != null) practiceLever.ResetLever();
        Place(practiceSpawn);
        trail.AddHere();
        CurrentState = State.Practice;
        Message = "Practice: " + ConditionNames.ToLabel(CurrentSpec.Condition) + ". Press Start when ready.";
    }

    public void StartTrial()
    {
        if (CurrentState != State.Practice) return;
        if (writer == null) writer = new StudyDataWriter(session.ParticipantId);

        var spec = CurrentSpec;
        foreach (var l in levers) l.ResetLever();
        foreach (var g in gates) g.ResetGate();
        exitDoor.ResetDoor();
        layouts.Apply(spec.Layout);
        locomotion.Apply(spec.Condition); // also clears the practice trail
        Place(startSpawn);
        trail.AddHere();

        result = new TrialResult
        {
            Participant = session.ParticipantId,
            LatinRow = session.LatinRow,
            Trial = spec.TrialNumber,
            Condition = spec.Condition,
            Layout = spec.Layout,
            AppVersion = session.AppVersion
        };

        trialStart = Time.timeAsDouble;
        logger.Begin(writer, session.ParticipantId, spec.TrialNumber, spec.Condition, spec.Layout, trialStart);
        logger.WriteEvent("trial_start");
        CurrentState = State.Running;
        Message = "Trial " + spec.TrialNumber + " running.";
    }

    void Update()
    {
        if (CurrentState != State.Running) return;
        if (exitDoor.PlayerIsInside(logger.HeadPosition)) Finish(false, false);
        else if (Elapsed >= timeLimit) Finish(true, false);
    }

    void OnLeverFlipped(int room)
    {
        if (CurrentState != State.Running || room < 1 || room > 3) return;
        double t = Elapsed;
        result.LeverSeconds[room - 1] = t;
        logger.WriteEvent("lever_flip", room.ToString());

        if (room < 3)
        {
            gates[room - 1].Open();
            logger.WriteEvent("gate_open", room.ToString());
        }
        else
        {
            exitDoor.Unlock();
            logger.WriteEvent("exit_unlock");
        }
    }

    void OnPracticeLever(int room)
    {
        if (CurrentState == State.Practice) Message = "Practice lever done. Press Start when ready.";
    }

    // operator button, e.g. the participant feels sick
    public void AbortTrial()
    {
        if (CurrentState == State.Running) Finish(false, true);
    }

    void Finish(bool timedOut, bool aborted)
    {
        double t = timedOut ? timeLimit : Elapsed;
        logger.WriteEvent(aborted ? "trial_abort" : timedOut ? "timeout" : "escaped");
        logger.End();

        result.CompletionSeconds = t;
        result.TimedOut = timedOut;
        result.Aborted = aborted;
        result.Backtracks = logger.Grid.Backtracks;
        result.CellsEntered = logger.Grid.CellsEntered;
        result.UniqueCells = logger.Grid.UniqueCells;
        result.Teleports = logger.Teleports;
        result.PathMetres = logger.PathMetres;
        writer.WriteTrial(result);
        writer.Flush();

        int finished = CurrentTrial;
        trail.SetActive(false);
        Place(lobbySpawn);

        if (finished >= LatinSquare.TrialsPerSession)
        {
            writer.Dispose();
            writer = null;
            CurrentState = State.Done;
            Message = "All 3 trials done. Headset off: last SSQ (after_3) + ranking.";
        }
        else
        {
            CurrentTrial = finished + 1;
            CurrentState = State.Lobby;
            Message = "Trial " + finished + (aborted ? " aborted" : timedOut ? " timed out" : " done") +
                      ". Headset off: SSQ (after_" + finished + "), then 5 min break.";
        }
        Debug.Log("[Trial] " + Message);
    }

    // Move the rig so the head ends up over the spawn point, facing its forward.
    void Place(Transform spawn)
    {
        if (spawn == null || rig == null) return;
        var cc = rig.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        rig.MatchOriginUpCameraForward(Vector3.up, spawn.forward);
        var camY = rig.Camera.transform.position.y;
        rig.MoveCameraToWorldLocation(new Vector3(spawn.position.x, camY, spawn.position.z));
        if (cc != null) cc.enabled = true;
    }

    // Quest pauses the app when the headset comes off, so save what we have
    void OnApplicationPause(bool paused)
    {
        if (paused) writer?.Flush();
    }

    void OnApplicationQuit()
    {
        writer?.Dispose();
    }
}
