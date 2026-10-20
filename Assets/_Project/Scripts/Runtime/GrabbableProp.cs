using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// A decoy prop the participant can pick up. Grabs are logged by TrialManager,
// and every prop goes back to where it started at the beginning of each trial.
[RequireComponent(typeof(XRGrabInteractable))]
public class GrabbableProp : MonoBehaviour
{
    public string propName;

    public static event Action<GrabbableProp> Grabbed;

    XRGrabInteractable grab;
    Rigidbody body;
    Vector3 startPos;
    Quaternion startRot;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        body = GetComponent<Rigidbody>();
        startPos = transform.position;
        startRot = transform.rotation;
        if (string.IsNullOrEmpty(propName)) propName = name;
    }

    void OnEnable() => grab.selectEntered.AddListener(OnGrab);
    void OnDisable() => grab.selectEntered.RemoveListener(OnGrab);

    void OnGrab(SelectEnterEventArgs args) => Grabbed?.Invoke(this);

    public void ResetPose()
    {
        if (grab.isSelected && grab.interactionManager != null)
            grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        transform.SetPositionAndRotation(startPos, startRot);
    }

    public static void ResetAll()
    {
        foreach (var p in FindObjectsByType<GrabbableProp>(FindObjectsSortMode.None)) p.ResetPose();
    }
}
