using UnityEngine;

// Slow on/off blink for a small status light. Period is long on purpose (no flicker).
public class AmbientBlink : MonoBehaviour
{
    public Renderer target;
    public float period = 2f;

    void Update()
    {
        if (target != null) target.enabled = Mathf.Repeat(Time.time, period) < period / 2;
    }
}
