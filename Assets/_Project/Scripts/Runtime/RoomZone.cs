using UnityEngine;

// Box that marks which room the player is in (1..3). Put one on each room prefab.
[RequireComponent(typeof(BoxCollider))]
public class RoomZone : MonoBehaviour
{
    public int roomIndex = 1;

    BoxCollider box;

    void Awake()
    {
        box = GetComponent<BoxCollider>();
        box.isTrigger = true;
    }

    public bool Contains(Vector3 worldPos)
    {
        if (box == null) box = GetComponent<BoxCollider>();
        // ignore height so a tall player or a low ceiling doesn't matter
        var b = box.bounds;
        return worldPos.x >= b.min.x && worldPos.x <= b.max.x && worldPos.z >= b.min.z && worldPos.z <= b.max.z;
    }

    // 0 = not inside any zone
    public static int Find(RoomZone[] zones, Vector3 worldPos)
    {
        foreach (var z in zones)
            if (z != null && z.Contains(worldPos)) return z.roomIndex;
        return 0;
    }
}
