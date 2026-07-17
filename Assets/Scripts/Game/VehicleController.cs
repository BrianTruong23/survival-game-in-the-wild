using UnityEngine;
using StarterAssets;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// A simple drivable vehicle. Walk up to it and press I to get in; while
// driving, WASD/arrows steer the vehicle directly (faster than the player's
// own sprint speed) and I gets back out. Mirrors NpcDialogue's proximity
// "press key to interact" pattern, but on enter it also hands off movement
// control from the player's ThirdPersonController to this component.
[RequireComponent(typeof(Rigidbody))]
public sealed class VehicleController : MonoBehaviour
{
    private const string EnterPrompt = "Press I to enter vehicle";
    private const string ExitPrompt = "Press I to exit vehicle";

    // ThirdPersonController.SprintSpeed is 5.335 - keep this comfortably above
    // it so driving always reads faster than sprinting on foot.
    [SerializeField] private float driveSpeed = 9f;
    [SerializeField] private float reverseSpeedFactor = 0.5f;
    [SerializeField] private float turnSpeed = 90f;
    [SerializeField] private Vector3 exitOffset = new Vector3(2.6f, 0f, 0f);
    // Lifts the vehicle a touch off the sampled ground so wheels rest on the
    // surface instead of clipping slightly into it. Tune up if wheels still
    // sink, down if the car floats.
    [SerializeField] private float groundOffset = 0f;
    // How fast the vehicle tilts to match the ground slope (higher = snappier).
    [SerializeField] private float groundAlignSpeed = 8f;

    // World-space height above the vehicle where the follow camera aims while
    // driving. The camera then sits CameraDistance behind that point (see
    // GameScoreManager.DrivingCameraDistance).
    [SerializeField] private float cameraTargetHeight = 1.8f;
    // Mouse-look while driving, mirroring the on-foot camera: move the mouse to
    // orbit the view around the car. Yaw is relative to the car's heading, so
    // with no input the camera simply trails behind.
    [SerializeField] private float cameraLookSensitivity = 1f;
    [SerializeField] private float cameraDefaultPitch = 12f;
    [SerializeField] private float cameraMinPitch = -20f;
    [SerializeField] private float cameraMaxPitch = 65f;
    // Must clear the tallest vehicle collider top used in GameScoreManager
    // (RedCar's BoxCollider: center.y 1.6 + size.y 3.2 / 2 = 3.2) with margin,
    // so the ray never starts at/under the vehicle's own roof.
    [SerializeField] private float groundRayHeight = 4.5f;
    [SerializeField] private LayerMask groundMask = ~0;
    // Matches the old enterTrigger's radius. Proximity is now checked by
    // distance every frame instead of relying solely on OnTriggerEnter/Exit -
    // physics trigger events can be missed (e.g. if the fast-moving player or
    // the every-frame SnapToGround repositioning causes the collider to skip
    // past the trigger boundary between physics steps without ever reporting
    // "inside"), which is what caused "sometimes pressing I does not get the
    // player inside the vehicle".
    [SerializeField] private float enterRange = 3f;

    private GameScoreManager game;
    private bool playerInRange;
    private bool isDriving;
    private Transform trackedPlayerTransform;
    private Collider vehicleCollider;
    private float lastDistanceToVehicle;

    private ThirdPersonController playerController;
    private CharacterController playerCharController;
    private StarterAssetsInputs playerInputs;
    private Transform playerTransform;
    private Renderer[] hiddenPlayerRenderers;

    // The vehicle's steering heading (world yaw, degrees). Movement and the
    // ground-aligned rotation are both rebuilt from this, so the car can tilt
    // to the terrain without the tilt corrupting its heading.
    private float headingYaw;

    // Camera-look state while driving (yaw is relative to the car's heading).
    private float cameraYaw;
    private float cameraPitch;

    // While driving we take direct control of the Main Camera instead of
    // reusing the player's Cinemachine rig. Cinemachine's 3rd-person-follow has
    // its own collision avoidance that yanks the camera hard against the car
    // (it treats the vehicle as an obstacle), and its follow distance lives
    // deep in version-specific internals - both fought every attempt to set a
    // clean, Inspector-driven distance. So on enter we simply switch its brain
    // off and position the camera ourselves; on exit we switch the brain back
    // on and Cinemachine resumes following the player. The follow distance is
    // GameScoreManager.DrivingCameraDistance, read live every frame so it can
    // be tuned in the Inspector even mid-drive.
    private Camera drivingCamera;
    private Behaviour cinemachineBrain;
    private Vector3 cameraVelocity;
    [SerializeField] private float cameraSmoothTime = 0.12f;

    private void Start()
    {
        if (game == null)
        {
            game = FindAnyObjectByType<GameScoreManager>();
        }

        vehicleCollider = GetComponent<Collider>();
        headingYaw = transform.eulerAngles.y;
    }

    private void Update()
    {
        if (isDriving)
        {
            DriveVehicle();            // steers, moves, then re-grounds the car

            if (WasInteractKeyPressed())
            {
                ExitVehicle();
            }

            return;
        }

        // Keep the parked car glued to (and tilted to) the ground every frame -
        // it may have spawned a hair below the terrain, or on a slope.
        SnapToGround();

        UpdateProximity();

        if (WasInteractKeyPressed())
        {
            if (playerInRange)
            {
                EnterVehicle();
            }
            else
            {
                // Diagnostic only - entering was reported as flaky and none
                // of the geometry/input theories checked out by reading the
                // code alone, so log exactly why "I" did nothing this time
                // instead of guessing further. Remove once the real cause is
                // confirmed from these logs.
                Debug.Log(trackedPlayerTransform == null
                    ? "[VehicleController] Pressed I but no player was found in the scene."
                    : $"[VehicleController] Pressed I but out of range: distance={lastDistanceToVehicle:F2}, enterRange={enterRange:F2}.");
            }
        }
    }

    // Distance-based replacement for the old OnTriggerEnter/Exit-only check -
    // see the enterRange field comment for why. Falls back to
    // FindAnyObjectByType if the player transform hasn't been found yet (or
    // was lost), so this keeps working even before the first OnTriggerEnter.
    private void UpdateProximity()
    {
        if (trackedPlayerTransform == null)
        {
            ThirdPersonController controller = FindAnyObjectByType<ThirdPersonController>();
            trackedPlayerTransform = controller != null ? controller.transform : null;
        }

        if (trackedPlayerTransform == null)
        {
            return;
        }

        // Measure from the vehicle's collider surface rather than its pivot -
        // a pivot-to-player distance made "in range" depend on where the
        // player stood relative to the model (e.g. a corner of the car sits
        // noticeably farther from the pivot than a side), which combined with
        // the CharacterController's own collision radius could put the
        // closest reachable standing spot only a hair inside enterRange -
        // easy to tip just outside it from ordinary physics jitter. Surface
        // distance is angle- and model-size-independent.
        Vector3 playerPosition = trackedPlayerTransform.position;
        float distanceToVehicle = vehicleCollider != null
            ? Vector3.Distance(vehicleCollider.ClosestPoint(playerPosition), playerPosition)
            : Vector3.Distance(transform.position, playerPosition);
        lastDistanceToVehicle = distanceToVehicle;
        bool inRange = distanceToVehicle <= enterRange;
        if (inRange == playerInRange)
        {
            return;
        }

        playerInRange = inRange;
        if (game == null)
        {
            return;
        }

        if (playerInRange)
        {
            game.SetPrompt(EnterPrompt);
        }
        else
        {
            game.ClearPrompt(EnterPrompt);
        }
    }

    private void DriveVehicle()
    {
        float throttle = Input.GetAxis("Vertical");
        float steer = Input.GetAxis("Horizontal");

        // Steering updates the heading (reversed when backing up, like a real
        // car). Movement is along the car's current forward, which SnapToGround
        // keeps lying on the ground plane, so driving up a slope climbs it.
        headingYaw += steer * turnSpeed * Time.deltaTime * Mathf.Sign(throttle == 0f ? 1f : throttle);

        float speed = throttle >= 0f ? driveSpeed : driveSpeed * reverseSpeedFactor;
        transform.position += transform.forward * (throttle * speed * Time.deltaTime);

        SnapToGround();
    }

    // Raycasts straight down from above the vehicle, then (1) clamps its height
    // to the surface and (2) tilts it to match the ground's slope, so the car
    // actually rests on the terrain - all four wheels down - instead of sitting
    // level and burying itself in any hill. Rotation is rebuilt from headingYaw
    // plus the ground normal so the slope tilt never corrupts the heading.
    //
    // RaycastNonAlloc + skipping the vehicle's own colliders: the car's solid
    // BoxCollider pokes up past groundRayHeight, so a naive ray could hit the
    // car's own roof and push it upward every frame (it once rocketed skyward
    // that way).
    private static readonly RaycastHit[] groundHitsBuffer = new RaycastHit[16];

    private void SnapToGround()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * groundRayHeight;
        int hitCount = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, groundHitsBuffer, groundRayHeight * 2f, groundMask, QueryTriggerInteraction.Ignore);

        float closestDistance = float.MaxValue;
        bool found = false;
        float groundY = transform.position.y;
        Vector3 groundNormal = Vector3.up;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHitsBuffer[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                groundY = hit.point.y;
                groundNormal = hit.normal;
                found = true;
            }
        }

        if (found)
        {
            Vector3 position = transform.position;
            position.y = groundY + groundOffset;
            transform.position = position;
        }

        // Rebuild the car's orientation: face headingYaw, but lay that heading
        // onto the ground plane and stand the car up along the ground normal.
        Vector3 headingForward = Quaternion.Euler(0f, headingYaw, 0f) * Vector3.forward;
        Vector3 forwardOnGround = Vector3.ProjectOnPlane(headingForward, groundNormal);
        if (forwardOnGround.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(forwardOnGround.normalized, groundNormal);
            // Smooth the tilt so bumps don't snap the car; on flat ground this
            // resolves to the same upright rotation immediately.
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, groundAlignSpeed * Time.deltaTime);
        }
    }

    private void EnterVehicle()
    {
        ThirdPersonController controller = FindAnyObjectByType<ThirdPersonController>();
        if (controller == null)
        {
            // Diagnostic only, see the matching comment in Update().
            Debug.Log("[VehicleController] EnterVehicle aborted: no ThirdPersonController found in scene.");
            return;
        }

        playerController = controller;
        playerCharController = controller.GetComponent<CharacterController>();
        playerInputs = controller.GetComponent<StarterAssetsInputs>();
        playerTransform = controller.transform;

        // Start the driving camera trailing directly behind the car, angled
        // slightly down. Mouse-look then orbits from here (see LateUpdate).
        cameraYaw = 0f;
        cameraPitch = cameraDefaultPitch;

        // Make sure the vehicle is sitting flush on the ground before we take
        // over, so the camera pivot starts from a correct base height.
        SnapToGround();

        // Freeze the player in place - just stop its control and physics and
        // hide its model. Deliberately NOT re-parented onto the vehicle: the
        // old code did that (plus set a driver-seat offset) and the constant
        // re-parenting/repositioning against the every-frame ground snap is
        // what made entering feel flaky. The player simply waits, invisible,
        // where it stood; the vehicle becomes what the camera follows.
        if (playerCharController != null)
        {
            playerCharController.enabled = false;
        }
        if (playerController != null)
        {
            playerController.enabled = false;
        }

        hiddenPlayerRenderers = playerTransform.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer playerRenderer in hiddenPlayerRenderers)
        {
            playerRenderer.enabled = false;
        }

        isDriving = true;

        // Hand the camera over to the vehicle: switch Cinemachine off and take
        // direct control of the Main Camera (positioned in LateUpdate).
        BeginVehicleCamera();

        if (game != null)
        {
            game.ClearPrompt(EnterPrompt);
            game.SetPrompt(ExitPrompt);
        }
    }

    private void ExitVehicle()
    {
        // Drop the player back into the world beside the vehicle. It was never
        // parented, so this is a plain teleport (done while its
        // CharacterController is still disabled, so the move isn't fought).
        if (playerTransform != null)
        {
            playerTransform.position = transform.TransformPoint(exitOffset);
            playerTransform.rotation = Quaternion.LookRotation(-transform.right, Vector3.up);
        }

        // Make the player model visible again now that they've stepped out.
        if (hiddenPlayerRenderers != null)
        {
            foreach (Renderer playerRenderer in hiddenPlayerRenderers)
            {
                if (playerRenderer != null)
                {
                    playerRenderer.enabled = true;
                }
            }
            hiddenPlayerRenderers = null;
        }

        // Re-enable CharacterController before ThirdPersonController - the
        // latter reads it in its own OnEnable/Start-driven state.
        if (playerCharController != null)
        {
            playerCharController.enabled = true;
        }
        if (playerController != null)
        {
            playerController.enabled = true;
        }

        isDriving = false;

        // Give the camera back to the player: switch Cinemachine back on.
        EndVehicleCamera();

        if (game != null)
        {
            game.ClearPrompt(ExitPrompt);
            if (playerInRange)
            {
                game.SetPrompt(EnterPrompt);
            }
        }

        playerController = null;
        playerCharController = null;
        playerInputs = null;
        playerTransform = null;
    }

    // Takes direct control of the Main Camera for driving: caches it and
    // switches its Cinemachine brain off so Cinemachine stops driving it. From
    // here LateUpdate positions the camera every frame.
    private void BeginVehicleCamera()
    {
        drivingCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        cinemachineBrain = FindCinemachineBrain(drivingCamera);
        if (cinemachineBrain != null)
        {
            cinemachineBrain.enabled = false;
        }
    }

    // Hands the Main Camera back to Cinemachine, which resumes following the
    // player.
    private void EndVehicleCamera()
    {
        if (cinemachineBrain != null)
        {
            cinemachineBrain.enabled = true;
            cinemachineBrain = null;
        }
        drivingCamera = null;
    }

    // Positions the driving camera after everything else has moved this frame.
    // The camera trails behind the car's heading at DrivingCameraDistance (read
    // live, so the Inspector value can be tuned mid-drive) and orbits with the
    // mouse - the same feel as the on-foot camera. It stays world-upright, so
    // the car's slope tilt never rolls the view.
    private void LateUpdate()
    {
        if (!isDriving || drivingCamera == null)
        {
            return;
        }

        Vector2 look = playerInputs != null ? playerInputs.look : Vector2.zero;
        if (look.sqrMagnitude >= 0.0001f)
        {
            cameraYaw += look.x * cameraLookSensitivity;
            cameraPitch += look.y * cameraLookSensitivity;
        }
        cameraPitch = Mathf.Clamp(cameraPitch, cameraMinPitch, cameraMaxPitch);

        // Car heading flattened onto the horizontal plane, so the camera trails
        // the direction of travel regardless of any slope tilt.
        Vector3 flatForward = transform.forward;
        flatForward.y = 0f;
        float carHeading = flatForward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(flatForward, Vector3.up).eulerAngles.y
            : headingYaw;

        Quaternion orbit = Quaternion.Euler(cameraPitch, carHeading + cameraYaw, 0f);
        Vector3 pivot = transform.position + Vector3.up * cameraTargetHeight;
        float distance = game != null ? game.DrivingCameraDistance : 9f;
        Vector3 desiredPosition = pivot - orbit * Vector3.forward * distance;

        drivingCamera.transform.position = Vector3.SmoothDamp(drivingCamera.transform.position, desiredPosition, ref cameraVelocity, cameraSmoothTime);
        drivingCamera.transform.rotation = Quaternion.LookRotation(pivot - drivingCamera.transform.position, Vector3.up);
    }

    // Finds the CinemachineBrain on the given camera by type name, so this
    // works regardless of the Cinemachine version's namespace.
    private static Behaviour FindCinemachineBrain(Camera camera)
    {
        if (camera == null)
        {
            return null;
        }

        foreach (Behaviour behaviour in camera.GetComponents<Behaviour>())
        {
            if (behaviour != null && behaviour.GetType().Name == "CinemachineBrain")
            {
                return behaviour;
            }
        }

        return null;
    }

    private static bool WasInteractKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
        return Input.GetKeyDown(KeyCode.I);
    }
}
