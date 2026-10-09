using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// A lever the player grabs (near only, set the interaction layer in the inspector).
// Flips once per trial and tells the TrialManager which room it was in.
[RequireComponent(typeof(XRSimpleInteractable))]
public class LeverController : MonoBehaviour
{
    [Tooltip("1..3 for the study rooms, 0 for the practice lever")]
    public int roomIndex = 1;
    public Transform handle;
    public Vector3 offEuler = new Vector3(-40, 0, 0);
    public Vector3 onEuler = new Vector3(40, 0, 0);
    public float flipTime = 0.3f;
    public AudioSource clickSound;

    public event Action<int> Flipped;
    public bool IsFlipped { get; private set; }

    XRSimpleInteractable interactable;

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        if (handle != null) handle.localEulerAngles = offEuler;
    }

    void OnEnable() => interactable.selectEntered.AddListener(OnSelected);
    void OnDisable() => interactable.selectEntered.RemoveListener(OnSelected);

    void OnSelected(SelectEnterEventArgs args)
    {
        if (IsFlipped) return;
        IsFlipped = true;
        if (clickSound != null) clickSound.Play();
        if (handle != null) StartCoroutine(Animate());
        Flipped?.Invoke(roomIndex);
    }

    IEnumerator Animate()
    {
        var from = Quaternion.Euler(offEuler);
        var to = Quaternion.Euler(onEuler);
        for (float t = 0; t < flipTime; t += Time.deltaTime)
        {
            handle.localRotation = Quaternion.Slerp(from, to, t / flipTime);
            yield return null;
        }
        handle.localRotation = to;
    }

    public void ResetLever()
    {
        StopAllCoroutines();
        IsFlipped = false;
        if (handle != null) handle.localEulerAngles = offEuler;
    }
}
