using UnityEngine;

// Marks a room prefab as dressed (by RoomDresser or by hand).
// RoomDresser never touches a room that has this, so hand edits are safe.
public class DressedRoom : MonoBehaviour
{
    public string theme;
}
