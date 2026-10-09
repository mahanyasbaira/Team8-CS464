using Team8.Core;
using UnityEngine;

// Who is being tested and in what order. Set from the operator panel.
public class SessionConfig : MonoBehaviour
{
    [Range(1, 99)] public int participantNumber = 1;
    [Tooltip("1 normally. Set higher to resume after a crash.")]
    [Range(1, 3)] public int startTrial = 1;

    public string ParticipantId => LatinSquare.FormatId(participantNumber);
    public int LatinRow => LatinSquare.RowFor(participantNumber);
    public TrialSpec[] Order => LatinSquare.OrderFor(participantNumber);
    public string AppVersion => Application.version;

    public string OrderText()
    {
        var order = Order;
        var parts = new string[order.Length];
        for (int i = 0; i < order.Length; i++) parts[i] = (i + 1) + ". " + order[i];
        return string.Join("   ", parts);
    }
}
