using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

// Breadcrumb trail for the Trail condition: a flat marker on every teleport landing,
// joined by one LineRenderer. Markers come from a pool made at startup (no Instantiate during trials).
// Marker prefab: flat disc, URP Unlit material with GPU instancing on, no collider, no shadows.
public class TeleportTrail : MonoBehaviour
{
    public TeleportationProvider teleportProvider;
    public Transform head;
    [Tooltip("XR Origin transform, used for the floor height")]
    public Transform rigOrigin;
    public GameObject markerPrefab;
    public LineRenderer line;

    public int maxPoints = 300;
    public float floorOffset = 0.02f;
    [Tooltip("skip a landing closer than this to the last one")]
    public float minSpacing = 0.25f;

    GameObject[] pool;
    Vector3[] points;
    int count;
    bool active;

    void Awake()
    {
        // markers live at the scene root so they don't move with the rig
        var root = new GameObject("TrailMarkers").transform;
        pool = new GameObject[maxPoints];
        points = new Vector3[maxPoints];
        for (int i = 0; i < maxPoints; i++)
        {
            pool[i] = Instantiate(markerPrefab, root);
            pool[i].SetActive(false);
        }
        line.useWorldSpace = true;
        line.positionCount = 0;
        line.enabled = false;
    }

    void OnEnable()
    {
        if (teleportProvider != null) teleportProvider.locomotionEnded += OnTeleportEnded;
    }

    void OnDisable()
    {
        if (teleportProvider != null) teleportProvider.locomotionEnded -= OnTeleportEnded;
    }

    public void SetActive(bool on)
    {
        Clear();
        active = on;
        line.enabled = on;
    }

    public void Clear()
    {
        for (int i = 0; i < count; i++) pool[i].SetActive(false);
        count = 0;
        line.positionCount = 0;
    }

    // also called by TrialManager at the start position
    public void AddHere()
    {
        AddPoint(head.position);
    }

    public void AddPoint(Vector3 pos)
    {
        if (!active || count >= maxPoints) return;
        pos.y = rigOrigin.position.y + floorOffset;
        if (count > 0 && Vector3.Distance(points[count - 1], pos) < minSpacing) return;

        points[count] = pos;
        pool[count].transform.position = pos;
        pool[count].SetActive(true);
        count++;

        line.positionCount = count;
        line.SetPosition(count - 1, pos);
    }

    void OnTeleportEnded(LocomotionProvider provider)
    {
        if (active) StartCoroutine(AddNextFrame());
    }

    // wait a frame so the rig has actually moved
    IEnumerator AddNextFrame()
    {
        yield return null;
        AddHere();
    }
}
