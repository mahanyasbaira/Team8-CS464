using UnityEngine;

// Marks a room prefab as dressed. RoomDresser never touches a room that has this.
// generated = true means the dresser made it, so a CI rebuild (-team8Rebuild) may regenerate it.
// Untick "generated" after dressing a room by hand so nothing ever overwrites it.
public class DressedRoom : MonoBehaviour
{
    public string theme;
    public bool generated = true;
}
