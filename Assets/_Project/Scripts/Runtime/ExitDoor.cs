using UnityEngine;

// Last door. Lever 3 unlocks it; walking or teleporting into exitZone ends the trial.
// We check the head position against the zone every frame instead of OnTriggerEnter,
// because a teleport can skip the trigger.
public class ExitDoor : MonoBehaviour
{
    public GateController gate;
    public BoxCollider exitZone;

    public bool IsUnlocked { get; private set; }

    void Awake()
    {
        if (exitZone != null) exitZone.isTrigger = true;
    }

    public void Unlock()
    {
        IsUnlocked = true;
        if (gate != null) gate.Open();
    }

    public void ResetDoor()
    {
        IsUnlocked = false;
        if (gate != null) gate.ResetGate();
    }

    public bool PlayerIsInside(Vector3 headPos)
    {
        if (!IsUnlocked || exitZone == null) return false;
        var b = exitZone.bounds;
        return headPos.x >= b.min.x && headPos.x <= b.max.x && headPos.z >= b.min.z && headPos.z <= b.max.z;
    }
}
