using Team8.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

// Puts the XRI Starter Assets rig into one of the three study conditions.
// Only call this between trials (never while a teleport is happening).
//
//   left stick  = joystick move (Joystick) or teleport (Teleport, Trail)
//   right stick = snap turn in every condition, right teleport ray always off
//
// Inspector setup on the rig (checked in issue #9):
//   - Snap Turn Provider: left hand turn input = Unused, so only the right stick turns
//   - Continuous Move Provider: right hand move input = Unused
//   - Tunneling Vignette: removed/disabled in all conditions
public class LocomotionSwitcher : MonoBehaviour
{
    [Tooltip("ControllerInputActionManager on the left controller (Starter Assets rig)")]
    public MonoBehaviour leftHand;
    [Tooltip("ControllerInputActionManager on the right controller (Starter Assets rig)")]
    public MonoBehaviour rightHand;
    public ContinuousMoveProvider moveProvider;
    public TeleportationProvider teleportProvider;
    public XRRayInteractor leftTeleportInteractor;
    public XRRayInteractor rightTeleportInteractor;
    public TeleportTrail trail;

    [Tooltip("Fixed walking speed for the joystick condition, m/s")]
    public float joystickSpeed = 1.5f;

    public Condition Current { get; private set; }

    public void Apply(Condition condition)
    {
        bool joystick = condition == Condition.Joystick;

        // the starter rig's manager swaps the stick's input actions between Move and Teleport Mode
        SetBool(leftHand, "smoothMotionEnabled", joystick);
        SetBool(rightHand, "smoothMotionEnabled", false);
        SetBool(rightHand, "smoothTurnEnabled", false);

        moveProvider.enabled = joystick;
        moveProvider.moveSpeed = joystickSpeed;
        teleportProvider.enabled = !joystick;

        // the manager toggles these GameObjects on/off itself, so we switch the component instead
        if (leftTeleportInteractor != null) leftTeleportInteractor.enabled = !joystick;
        if (rightTeleportInteractor != null) rightTeleportInteractor.enabled = false;

        if (trail != null) trail.SetActive(condition == Condition.Trail);

        Current = condition;
        Debug.Log("[Locomotion] " + ConditionNames.ToLabel(condition));
    }

    // The hand managers come from the XRI Starter Assets sample, which isn't always imported,
    // so we set their properties by name instead of referencing the sample's type directly.
    static void SetBool(MonoBehaviour target, string property, bool value)
    {
        if (target == null) return;
        var prop = target.GetType().GetProperty(property);
        if (prop == null || prop.PropertyType != typeof(bool))
        {
            Debug.LogError("[Locomotion] " + target.GetType().Name + " has no bool property " + property);
            return;
        }
        prop.SetValue(target, value);
    }
}
