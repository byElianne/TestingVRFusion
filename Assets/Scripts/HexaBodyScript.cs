using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;

public class HexaBodyScript : MonoBehaviour
{
    [Header("XR Toolkit Parts")]
    public XROrigin XRRig;
    // De main camera blijft onderdeel van de XR Origin en wordt automatisch getrackt.

    [Header("Action-based Controller")]

    [Header("Action-based Controller")]
    public InputActionProperty positionAction;
    public InputActionProperty rightHandPositionAction;
    public InputActionProperty rightHandRotationAction;
    public InputActionProperty leftHandPositionAction;
    public InputActionProperty leftHandRotationAction;

    public InputActionReference RightTrackpadTouch;
    public InputActionReference RightTrackpadPressed;
    // rotationAction laten we voorlopig weg als we de headtracking van XR Origin gebruiken

    [Header("Hexabody Parts")]
    public GameObject Head;    // Dit is de hoofd van je speler (avatar) die je wilt koppelen
    public GameObject Chest;
    public GameObject Fender;
    public GameObject MonoBall;

    public ConfigurableJoint Spine;

    [Header("Hexabody Drag")]
    public float angularDampingOnMove;
    public float angularBreakDrag;

    [Header("Hexabody Movespeed")]

    public float moveForceWalk;
    public float moveForceSprint;

    [Header("Hexabody Crouch & Jump")]
    bool jumping = false;

    private float additionalHeight;
    public float lowestCrouch = 0.05f;
    public float highestCrouch = 1.8f;
    Vector3 CrouchTarget;

    private Quaternion headYaw;
    private Vector3 controllerPosition;

    private Quaternion currentChestRotation;

    void Start()
    {
        // Activeer de input-acties
        positionAction.action.Enable();

        // Bereken een offset die gebruikt wordt voor de crouch-logica
        additionalHeight = (0.5f * MonoBall.transform.lossyScale.y)
                         + (0.5f * Fender.transform.lossyScale.y)
                         + (Head.transform.position.y - Chest.transform.position.y);

        // Initialiseer de huidige rotatie van de Chest
        currentChestRotation = Chest.transform.rotation;
    }

    void Update()
    {
        GetControllerInputValues();
        // Nieuw: Update de positie van het player-model zodat het hoofd (avatar) de camera volgt.
        UpdatePlayerBodyPosition();
    }

    void FixedUpdate()
    {
        if (!jumping)
        {
            SpineContractionOnRealWorldCrouch();
        }
        RotatePlayer();
    }

    // Leest de inputwaarden (hier de positie) en bepaalt de yaw rotatie van de camera
    private void GetControllerInputValues()
    {
        controllerPosition = positionAction.action.ReadValue<Vector3>();
        // Gebruik de rotatie van de XR Origin camera (die wordt getrackt door de HMD)
        headYaw = Quaternion.Euler(0, XRRig.Camera.transform.eulerAngles.y, 0);
    }

    // Synchroniseert het player-model (Head) met de positie van de XR Origin camera
    private void UpdatePlayerBodyPosition()
    {
        // Door de positie van de camera toe te wijzen aan het Head-object,
        // zorg je ervoor dat je player-model mee beweegt met de HMD.
        Head.transform.position = XRRig.Camera.transform.position;
    }

    // Zorgt voor een vloeiende rotatie van de Chest richting de head yaw
    private void RotatePlayer()
    {
        Quaternion targetRotation = headYaw;
        currentChestRotation = Quaternion.Slerp(currentChestRotation, targetRotation, Time.fixedDeltaTime * 5f);
        Chest.transform.rotation = currentChestRotation;
    }

    private void movePlayerViaController()
    {
        if (RightTrackpadTouched == 0)
        {
            stopMonoball();
        }
        else if (RightTrackpadPressedValue == 0 && RightTrackpadTouched == 1)
        {
            moveMonoball(moveForceWalk);
        }
        else if (RightTrackpadPressedValue == 1)
        {
            moveMonoball(moveForceSprint);
        }
    }
    private void moveMonoball(float force)
    {
        monoBallRb.freezeRotation = false;
        monoBallRb.angularDamping = angularDampingOnMove;
        monoBallRb.AddTorque(MonoBallTorque.normalized * force, ForceMode.Force);
    }

    private void stopMonoball()
    {
        monoBallRb.angularDamping = angularBreakDrag;
    
        if (monoBallRb.linearVelocity.magnitude < 0.01f)  // Betere check voor stilstand
        {
        monoBallRb.freezeRotation = true;
        }
    }

    // Past de spine aan op basis van de ingelezen controller-positie (voor crouch-effect)
    private void SpineContractionOnRealWorldCrouch()
    {
        CrouchTarget.y = Mathf.Clamp(controllerPosition.y - additionalHeight, lowestCrouch, highestCrouch - additionalHeight);
        Spine.targetPosition = new Vector3(0, CrouchTarget.y, 0);
    }
}
