using UnityEngine;

// Slow constant rotation for background props (ceiling fan, wall vent).
// Kept slow and away from lever spots so it doesn't distract or help people search.
public class AmbientSpin : MonoBehaviour
{
    public Vector3 axis = Vector3.up;
    public float degreesPerSecond = 60f;

    void Update()
    {
        transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
