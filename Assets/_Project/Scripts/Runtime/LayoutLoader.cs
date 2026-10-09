using System;
using UnityEngine;

// Moves each room's lever to the spot for layout A, B or C.
public class LayoutLoader : MonoBehaviour
{
    [Serializable]
    public class RoomLayout
    {
        public string name = "Room 1";
        public Transform lever;
        public Transform spotA;
        public Transform spotB;
        public Transform spotC;
    }

    public RoomLayout[] rooms = new RoomLayout[3];

    public void Apply(char layout)
    {
        foreach (var r in rooms)
        {
            var spot = layout == 'A' ? r.spotA : layout == 'B' ? r.spotB : r.spotC;
            if (r.lever == null || spot == null)
            {
                Debug.LogError("[LayoutLoader] missing lever or spot for " + r.name + " layout " + layout);
                continue;
            }
            r.lever.SetPositionAndRotation(spot.position, spot.rotation);
        }
        Debug.Log("[LayoutLoader] layout " + layout);
    }
}
