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

    public ConfigurableJoint RightHandJoint;
    public ConfigurableJoint LeftHandJoint;
    public ConfigurableJoint Spine;

    [Header("Hexabody Drag")]
    public float angularDragOnMove;
    public float angularBreakDrag;

    [Header("Hexabody Movespeed")]

    public float moveForceWalk;
    public float moveForceSprint;

    [Header("Hexabody Crouch & Jump")]
    private bool jumping = false;  // Voor toekomstige jump implementatie

    private float additionalHeight;
    public float lowestCrouch = 0.05f;
    public float highestCrouch = 1.8f;
    Vector3 CrouchTarget;

    

    //-----------Input Values------------------------------------------------//

    private Vector3 controllerPosition;
    private Quaternion headYaw;
    private Vector3 moveDirection;
    private Vector3 MonoBallTorque;

    private Vector3 RightHandControllerPos;
    private Vector3 LeftHandControllerPos;

    private Quaternion RightHandControllerRotation;
    private Quaternion LeftHandControllerRotation;
    private Vector2 RightTrackpad;
    // private Vector2 LeftTrackpad; wordt nog niet gebruikt

    private float RightTrackpadPressedValue;
    // private float LeftTrackpadPressedValue; wordt nog niet gebruikt

    private float RightTrackpadTouched;
    // private float LeftTrackpadTouched; wordt nog niet gebruikt
    
    private Vector3 CameraControllerPos;

    private Quaternion currentChestRotation;
    private Rigidbody monoBallRb;

    void Start()
    {
        // Initiële waarden om Unity warnings te voorkomen
        // Deze worden in de eerste frame meteen overschreven door echte controller data
        RightHandControllerRotation = Quaternion.identity;
        LeftHandControllerRotation = Quaternion.identity;
        currentChestRotation = Chest.transform.rotation;

        // Bereken een offset die gebruikt wordt voor de crouch-logica
        additionalHeight = (0.5f * MonoBall.transform.lossyScale.y)
                         + (0.5f * Fender.transform.lossyScale.y)
                         + (Head.transform.position.y - Chest.transform.position.y);

        // Initialiseer de huidige rotatie van de Chest
        currentChestRotation = Chest.transform.rotation;

        // Initialiseer CameraControllerPos
        CameraControllerPos = XRRig.Camera.transform.position;
        CrouchTarget = Vector3.zero;  // Initialiseer CrouchTarget\

        // Cache de Rigidbody reference
        monoBallRb = MonoBall.GetComponent<Rigidbody>();
        if (monoBallRb == null)
        {
        Debug.LogError("MonoBall missing Rigidbody component!");
        }
    }

    void OnEnable()
    {
    EnableInputActions();  // Activeert input wanneer script/object actief wordt
    }

    void OnDisable()
    {
    DisableInputActions();  // Cleanup wanneer script/object inactief wordt
    }

    private void EnableInputActions()
    {
    positionAction.action?.Enable();
    rightHandPositionAction.action?.Enable();
    rightHandRotationAction.action?.Enable();
    leftHandPositionAction.action?.Enable();
    leftHandRotationAction.action?.Enable();
    RightTrackpadTouch.action?.Enable();
    RightTrackpadPressed.action?.Enable();
    }

    private void DisableInputActions()
    {
    positionAction.action?.Disable();
    rightHandPositionAction.action?.Disable();
    rightHandRotationAction.action?.Disable();
    leftHandPositionAction.action?.Disable();
    leftHandRotationAction.action?.Disable();
    RightTrackpadTouch.action?.Disable();
    RightTrackpadPressed.action?.Disable();
    }

    void Update()
    {
        GetControllerInputValues();
        // Nieuw: Update de positie van het player-model zodat het hoofd (avatar) de camera volgt.
        UpdatePlayerBodyPosition();
        // Update camera positie
        CameraControllerPos = XRRig.Camera.transform.position;
    }

    void FixedUpdate()
    {
        movePlayerViaController();

        if (!jumping)
        {
            SpineContractionOnRealWorldCrouch();
        }
        RotatePlayer();
        MoveAndRotateHand();
    }

    // Leest de inputwaarden (hier de positie) en bepaalt de yaw rotatie van de camera
    private void GetControllerInputValues()
    {
        controllerPosition = positionAction.action.ReadValue<Vector3>();
        // Gebruik de rotatie van de XR Origin camera (die wordt getrackt door de HMD)
        headYaw = Quaternion.Euler(0, XRRig.Camera.transform.eulerAngles.y, 0); 
        moveDirection = headYaw * new Vector3(RightTrackpad.x, 0, RightTrackpad.y);
        MonoBallTorque = new Vector3(moveDirection.z, 0, moveDirection.x);
        // Right Controller
        // Position & Rotation
            // Right Controller
        RightHandControllerPos = rightHandPositionAction.action.ReadValue<Vector3>();
        RightHandControllerRotation = rightHandRotationAction.action.ReadValue<Quaternion>();

        // Trackpad
        RightTrackpad = RightTrackpadTouch.action.ReadValue<Vector2>();
        RightTrackpadPressedValue = RightTrackpadPressed.action.ReadValue<float>();  
        RightTrackpadTouched = RightTrackpadTouch.action.ReadValue<float>();

        // Left Controller
        LeftHandControllerPos = leftHandPositionAction.action.ReadValue<Vector3>();
        LeftHandControllerRotation = leftHandRotationAction.action.ReadValue<Quaternion>();
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
        monoBallRb.angularDamping = angularDragOnMove;
        monoBallRb.AddTorque(MonoBallTorque.normalized * force, ForceMode.Force);
    }

    private void stopMonoball()
    {
        Rigidbody rb = MonoBall.GetComponent<Rigidbody>();
        rb.angularDamping = angularBreakDrag;
    
        if (rb.velocity.magnitude < 0.01f)  // Betere check voor stilstand
        {
        rb.freezeRotation = true;
        }
    }

    // Past de spine aan op basis van de ingelezen controller-positie (voor crouch-effect)
    private void SpineContractionOnRealWorldCrouch()
    {
        // Berekenen van crouch waarde relatief aan start positie
        float headHeight = XRRig.Camera.transform.localPosition.y;
        float crouchAmount = Mathf.Clamp(headHeight - additionalHeight, lowestCrouch, highestCrouch);
    
        // Update CrouchTarget
        CrouchTarget = new Vector3(0, crouchAmount, 0);
        Spine.targetPosition = CrouchTarget;
    }

    private void MoveAndRotateHand()
    {
        RightHandJoint.targetPosition = RightHandControllerPos - CameraControllerPos;
        LeftHandJoint.targetPosition = LeftHandControllerPos - CameraControllerPos;

        RightHandJoint.targetRotation = RightHandControllerRotation;
        LeftHandJoint.targetRotation = LeftHandControllerRotation;
    }
}
