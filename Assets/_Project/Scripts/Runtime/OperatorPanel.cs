using System.Text;
using TMPro;
using Team8.Core;
using UnityEngine;

// World-space panel in the lobby. Wire the buttons' OnClick to these methods.
// Ashley sets the participant here before handing over the headset.
public class OperatorPanel : MonoBehaviour
{
    public SessionConfig session;
    public TrialManager trials;
    public TMP_Text info;

    float nextRefresh;

    public void NextParticipant() => ChangeParticipant(+1);
    public void PrevParticipant() => ChangeParticipant(-1);
    public void StartTrialUp() => ChangeStartTrial(+1);
    public void StartTrialDown() => ChangeStartTrial(-1);
    public void Practice() => trials.BeginPractice();
    public void StartTrial() => trials.StartTrial();
    public void Abort() => trials.AbortTrial();

    void ChangeParticipant(int delta)
    {
        if (!CanEdit()) return;
        session.participantNumber = Mathf.Clamp(session.participantNumber + delta, 1, 99);
        session.startTrial = 1;
        trials.ResetSession();
    }

    void ChangeStartTrial(int delta)
    {
        if (!CanEdit()) return;
        session.startTrial = Mathf.Clamp(session.startTrial + delta, 1, LatinSquare.TrialsPerSession);
        trials.ResetSession();
    }

    // only before the first trial of a session, or after it's done
    bool CanEdit()
    {
        var s = trials.CurrentState;
        return s == TrialManager.State.Done || (s == TrialManager.State.Lobby && !trials.SessionStarted);
    }

    void Update()
    {
        if (info == null || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.25f;

        var sb = new StringBuilder();
        sb.AppendLine("Team 8 locomotion study   v" + session.AppVersion);
        sb.AppendLine("Participant " + session.ParticipantId + "   (group " + session.LatinRow + ")");
        sb.AppendLine("Order: " + session.OrderText());
        if (trials.CurrentState != TrialManager.State.Done)
            sb.AppendLine("Next: trial " + trials.CurrentTrial + " of 3, " + trials.CurrentSpec);
        sb.AppendLine("State: " + trials.CurrentState);
        if (trials.CurrentState == TrialManager.State.Running)
            sb.AppendLine("Time: " + trials.Elapsed.ToString("0") + " s");
        sb.AppendLine(trials.Message);
        if (trials.SessionStarted) sb.AppendLine("Saving to: " + trials.DataFolder);
        info.text = sb.ToString();
    }
}
