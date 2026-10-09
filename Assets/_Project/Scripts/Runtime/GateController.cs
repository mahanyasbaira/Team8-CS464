using System.Collections;
using UnityEngine;

// Gate between rooms. Closed: its collider blocks walking and the teleport ray.
// Open: slides the door up and turns the colliders off.
public class GateController : MonoBehaviour
{
    public Transform door;
    public Collider[] blockers;
    public float openHeight = 3f;
    public float openTime = 1f;
    public AudioSource openSound;

    public bool IsOpen { get; private set; }

    Vector3 closedPos;

    void Awake()
    {
        if (door != null) closedPos = door.localPosition;
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        foreach (var c in blockers) if (c != null) c.enabled = false;
        if (openSound != null) openSound.Play();
        if (door != null) StartCoroutine(Slide());
    }

    IEnumerator Slide()
    {
        var to = closedPos + Vector3.up * openHeight;
        for (float t = 0; t < openTime; t += Time.deltaTime)
        {
            door.localPosition = Vector3.Lerp(closedPos, to, t / openTime);
            yield return null;
        }
        door.localPosition = to;
    }

    public void ResetGate()
    {
        StopAllCoroutines();
        IsOpen = false;
        if (door != null) door.localPosition = closedPos;
        foreach (var c in blockers) if (c != null) c.enabled = true;
    }
}
