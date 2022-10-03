using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.VFX;

// Mostly copied from https://github.com/nicholas-maltbie/OpenKCC

public class PlayerController : MonoBehaviour
{
    private const float EPSILON = 0.001f;
    private const float MAX_ANGLE_SHOVE_DEG = 90f;

    [Header("Camera")]
    [SerializeField] private Transform cameraFollowTarget;
    [SerializeField] private float lookSensitivity = 1f;
    [SerializeField] private float standingCameraDamping = 0f;
    [SerializeField] private float walkingCameraDamping = 0.5f;
    [SerializeField] private float slidingCameraDamping = 0.9f;
    [SerializeField] private float cameraNaturalTilt = 15f;

    [Header("Physics")]
    [SerializeField] private float groundAcceleration = 1f;
    [SerializeField] private float airAcceleration = 1f;
    [SerializeField] private float maxSpeed = 20f;
    [SerializeField] private float jumpForce = 100f;
    [SerializeField] private float gravity = 20f;
    [SerializeField] private float groundSenseDistance = 0.1f;
    [SerializeField] private int maxBounces;
    [SerializeField] private float angleCollisionPower;
    [SerializeField] private float groundedGravityFactor = 0f;
    [SerializeField] private float overlapCorrectionDistance = 0.1f;

    [Header("Sliding Physics")]
    [SerializeField] private float slideSteeringAngularSpeedDeg = 90f;
    [SerializeField] private float slideSkidAngle = 10f;
    [SerializeField] private float snowMassEquivalent = 0.1f;
    [SerializeField] private float carvingTurnFactor = 0.9f;

    [Header("Board")]
    [SerializeField] private GameObject board;
    [SerializeField] private VisualEffect skidEffect;
    [SerializeField] private float skidEffectSpawnFactor = 1f;
    [SerializeField] private VisualEffect boostEffect;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private float walkAnimSpeed = 1f;
    [SerializeField] private float runAnimSpeed = 10f;
    [SerializeField] private float animSpeedFactor = 10f;
    [SerializeField] private float maxAnimSpeed = 2f;

    [Header("Audio")]
    [SerializeField] private AudioSource boardSfx;
    [SerializeField] private float boardSfxVolumeSpeed = 10f;
    [SerializeField] private float carveSfxMinPitch = 0.1f;
    [SerializeField] private float carveSfxMaxPitch = 0.5f;
    [SerializeField] private float carveSfxMinSpeed = 50f;
    [SerializeField] private float carveSfxMaxSpeed = 200f;
    [SerializeField] private float skidSfxMinPitch = 0.9f;
    [SerializeField] private float skidSfxMaxPitch = 1.1f;
    [SerializeField] private float skidSfxMinSpeed = 1f;
    [SerializeField] private float skidSfxMaxSpeed = 100f;
    [SerializeField] private float skidSfxDebounceTime = 0.1f;

    [Header("Ragdoll")]
    [SerializeField] private SimpleRagdoll ragdoll;
    [SerializeField] private float ragdollTime = 5f;
    [SerializeField] private CinemachineVirtualCamera ragdollVCam;

    [Header("Misc")]
    [SerializeField] private SOUP.Event onPause;

    private new Rigidbody rigidbody;
    private CapsuleCollider capsuleCollider;

    private PlayerControls controls;

    private Vector3 velocity;
    private Quaternion rotation;

    private Quaternion cameraRotation;

    private bool isGrounded;
    private Vector3 surfacePlane;

    private bool isSliding = false;

    private bool jumpRequested = false;

    private float boardSfxTargetVolume = 0f;
    private float skidSfxRemainingDebounceTime = 0f;

    private float lastTouchedGround = 0f;

    private float backflipTimer = 0f;

    private float ragdollTimer = 0f;

    private class SavedState
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public Quaternion p_rotation;
        public Vector3 rb_position;
        public Quaternion rb_rotation;
        public Quaternion cameraRotation;
        public Vector3 camPos;
        public Quaternion camRot;
        public bool isGrounded;
        public Vector3 surfacePlane;
        public bool isSliding;
        public float lastTouchedGround;
        public float backflipTimer;
    }

    private SavedState savedState;

    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        controls = new PlayerControls();
        controls.Player.SaveState.performed += SaveState;
        controls.Player.LoadState.performed += LoadState;
        controls.Player.Pause.performed += Pause;
    }

    private void Pause(InputAction.CallbackContext obj)
    {
        onPause.Raise();
        boardSfx.volume = 0f;
        boardSfxTargetVolume = 0f;
    }

    private void LoadState(InputAction.CallbackContext obj)
    {
        if (savedState == null)
        {
            return;
        }

        var pdelta = savedState.position - transform.position;

        transform.position = savedState.position;
        transform.rotation = savedState.rotation;
        velocity = savedState.velocity;
        rotation = savedState.p_rotation;
        rigidbody.position = savedState.rb_position;
        rigidbody.rotation = savedState.rb_rotation;
        rigidbody.Move(savedState.rb_position, savedState.rb_rotation);
        cameraRotation = savedState.cameraRotation;
        // Camera.main.transform.position = savedState.camPos;
        // Camera.main.transform.rotation = savedState.camRot;
        isGrounded = savedState.isGrounded;
        surfacePlane = savedState.surfacePlane;
        isSliding = savedState.isSliding;
        lastTouchedGround = savedState.lastTouchedGround;
        backflipTimer = savedState.backflipTimer;

        if (Camera.main.GetComponent<CinemachineBrain>() is CinemachineBrain brain)
        {
            brain.ActiveVirtualCamera.OnTargetObjectWarped(transform, pdelta);
        }
    }

    private void SaveState(InputAction.CallbackContext obj)
    {
        savedState = new SavedState
        {
            position = transform.position,
            rotation = transform.rotation,
            velocity = velocity,
            p_rotation = rotation,
            rb_position = rigidbody.position,
            rb_rotation = rigidbody.rotation,
            cameraRotation = cameraRotation,
            camPos = Camera.main.transform.position,
            camRot = Camera.main.transform.rotation,
            isGrounded = isGrounded,
            surfacePlane = surfacePlane,
            isSliding = isSliding,
            lastTouchedGround = lastTouchedGround,
            backflipTimer = backflipTimer,
        };
    }

    private void OnEnable()
    {
        controls.Player.Enable();
    }

    private void OnDisable()
    {
        controls.Player.Disable();
    }

    private void Start()
    {
        velocity = Vector3.zero;
        rotation = rigidbody.rotation;
        cameraRotation = cameraFollowTarget.rotation;
        ragdollVCam.Priority = 0;
    }

    private void Update()
    {
        // ragdoll

        if (ragdollTimer > 0f)
        {
            ragdollTimer -= Time.deltaTime;
            if (ragdollTimer <= 0f)
            {
                ragdollTimer = 0f;
                StopRagdoll();
            }
        }

        if (controls.Player.ForceRagdoll.WasPressedThisFrame())
        {
            StartRagdoll();
        }

        if (ragdollTimer > 0f)
        {
            UpdateRagdoll();
            return;
        }

        // inputs

        if (controls.Player.Slide.WasPressedThisFrame())
        {
            isSliding = !isSliding;
        }

        board.SetActive(isSliding);

        var lookInput = controls.Player.Look.ReadValue<Vector2>();

        var moveInput = controls.Player.Move.ReadValue<Vector2>();

        if (controls.Player.Jump.WasPressedThisFrame())
        {
            jumpRequested = true;
        }

        // non-sliding input and acceleration

        if (!isSliding)
        {
            // movement plane basis
            var moveForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, surfacePlane).normalized;
            var moveRight = Vector3.Cross(Vector3.up, moveForward).normalized;

            // this is the velocity at which the player "wants" to go, based on their input
            var idealVelocity = (moveInput.x * moveRight + moveInput.y * moveForward) * maxSpeed;

            var currentPlanarVelocity = Vector3.ProjectOnPlane(velocity, surfacePlane);
            var accelerationCorrectionVector = idealVelocity - currentPlanarVelocity;
            var acceleration = isGrounded ? groundAcceleration : airAcceleration;

            // the final acceleration to apply
            var appliedAcceleration = Mathf.Min(accelerationCorrectionVector.magnitude, acceleration * Time.deltaTime) * (accelerationCorrectionVector != Vector3.zero ? accelerationCorrectionVector.normalized : Vector3.zero);

            velocity += appliedAcceleration;

            // rotation
            var velocityDirection = Vector3.ProjectOnPlane(velocity, surfacePlane).normalized;
            if (velocityDirection != Vector3.zero)
            {
                rotation = Quaternion.LookRotation(velocityDirection, surfacePlane);
            }

            // lookEuler.x += -lookInput.y * lookSensitivity;
            // lookEuler.y += lookInput.x * lookSensitivity;
            backflipTimer = 0f;
        }

        // sliding input and acceleration

        else
        {
            // steering
            var steeringAngularAcceleration = moveInput.x * slideSteeringAngularSpeedDeg * Time.deltaTime;
            rotation = Quaternion.AngleAxis(steeringAngularAcceleration, rotation * Vector3.up) * rotation;

            // board physics
            if (isGrounded)
            {
                rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(rotation * Vector3.forward, surfacePlane), surfacePlane);

                var currentPlanarVelocity = Vector3.ProjectOnPlane(velocity, surfacePlane);
                var velocityDirection = currentPlanarVelocity.normalized;

                var boardForward = Vector3.ProjectOnPlane(rotation * Vector3.forward, surfacePlane).normalized;

                if (Vector3.Dot(boardForward, currentPlanarVelocity) < -EPSILON)
                {
                    boardForward = -boardForward;
                }

                var boardRight = Vector3.Cross(surfacePlane, boardForward).normalized;

                var snowAngle = Vector3.SignedAngle(velocityDirection, boardForward, surfacePlane);

                // carving
                if (Mathf.Abs(snowAngle) < slideSkidAngle)
                {
                    // gently redirect the velocity to match the board's forward direction
                    velocity = Quaternion.AngleAxis(snowAngle * carvingTurnFactor, surfacePlane) * velocity;

                    skidEffect.SetFloat("SpawnRate", 5);
                    skidEffect.SetVector3("SpawnDirection", -currentPlanarVelocity.normalized);
                    skidEffect.SetFloat("SpawnVelocity", currentPlanarVelocity.magnitude);

                    skidSfxRemainingDebounceTime -= Time.deltaTime;

                    if (skidSfxRemainingDebounceTime <= 0f)
                    {
                        skidSfxRemainingDebounceTime = 0f;
                        boardSfxTargetVolume = Mathf.Clamp01(currentPlanarVelocity.magnitude / carveSfxMinSpeed);
                        boardSfx.pitch = Mathf.Lerp(carveSfxMinPitch, carveSfxMaxPitch, Mathf.InverseLerp(carveSfxMinSpeed, carveSfxMaxSpeed, currentPlanarVelocity.magnitude));
                    }
                }
                // skidding
                else
                {
                    // model the skid as if the snow is a particle of a certain mass colliding with the side of the board
                    var snowForce = Vector3.Project(snowMassEquivalent * -currentPlanarVelocity, boardRight);
                    velocity += snowForce * Time.deltaTime;

                    skidEffect.SetFloat("SpawnRate", snowForce.magnitude * skidEffectSpawnFactor);
                    skidEffect.SetVector3("SpawnDirection", Vector3.Reflect(-currentPlanarVelocity.normalized, boardRight));
                    skidEffect.SetFloat("SpawnVelocity", currentPlanarVelocity.magnitude);

                    var mag = Vector3.Project(currentPlanarVelocity, boardRight).magnitude;
                    boardSfxTargetVolume = Mathf.Clamp01(mag / skidSfxMinSpeed);
                    boardSfx.pitch = Mathf.Lerp(skidSfxMinPitch, skidSfxMaxPitch, Mathf.InverseLerp(skidSfxMinSpeed, skidSfxMaxSpeed, mag));
                    skidSfxRemainingDebounceTime = skidSfxDebounceTime;
                }

                if (backflipTimer != 0) Debug.Log(backflipTimer);

                if(backflipTimer > 1.0f)
                {
                    velocity += velocity.normalized * 50;
                    boostEffect.Play();
                    Debug.Log("Boost");
                }
                backflipTimer = 0f;
            }
            else
            {
                // air spin
                var spinAngularAcceleration = moveInput.y * slideSteeringAngularSpeedDeg * Time.deltaTime;
                rotation = Quaternion.AngleAxis(spinAngularAcceleration, Camera.main.transform.right) * rotation;

                skidEffect.SetFloat("SpawnRate", 0);
                boardSfxTargetVolume = 0;

                if(moveInput.y != 0)
                {
                    backflipTimer += Time.deltaTime;
                }
            }

            // lookEuler.x += -lookInput.y * lookSensitivity;
            // lookEuler.y = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
        }

        skidEffect.transform.rotation = Quaternion.identity;

        // gravity

        if (!isGrounded)
        {
            velocity += Vector3.down * gravity * Time.deltaTime;
        }
        else
        {
            var planarGravity = Vector3.ProjectOnPlane(Vector3.down * gravity, surfacePlane);

            if (!isSliding)
            {
                planarGravity *= groundedGravityFactor;
            }

            velocity += planarGravity * Time.deltaTime;
        }

        // camera

        {
            var damping =
                isSliding ? slidingCameraDamping :
                (moveInput != Vector2.zero) ? walkingCameraDamping
                : standingCameraDamping;

            if (damping != 0f)
            {
                var forward = Vector3.ProjectOnPlane(transform.forward, surfacePlane).normalized;

                var currentPlanarVelocity = Vector3.ProjectOnPlane(velocity, surfacePlane);
                if (isSliding && Vector3.Dot(forward, currentPlanarVelocity) < -EPSILON)
                {
                    forward = -forward;
                }

                var right = Vector3.Cross(Vector3.up, forward).normalized;

                var downAngle = Quaternion.AngleAxis(cameraNaturalTilt, right);
                var idealLook =
                    isGrounded ? Quaternion.LookRotation(downAngle * forward, Vector3.up) :
                    Quaternion.LookRotation(downAngle * velocity.normalized, Vector3.up);

                var dampFactor = 1f - Mathf.Pow(1f - Mathf.Clamp01(damping), Time.deltaTime);

                cameraRotation = Quaternion.Slerp(cameraRotation, idealLook, dampFactor);
            }
        }

        cameraFollowTarget.rotation = cameraRotation;

        // animation

        animator.SetFloat("Speed", Mathf.Min(Vector3.ProjectOnPlane(velocity, surfacePlane).magnitude / animSpeedFactor, maxAnimSpeed));
        animator.SetFloat("Walk-Run", Mathf.InverseLerp(walkAnimSpeed, runAnimSpeed, velocity.magnitude));
        animator.SetBool("Airborne", !isGrounded);
        animator.SetBool("Sliding", isSliding);

        // audio

        boardSfx.volume = Mathf.MoveTowards(boardSfx.volume, boardSfxTargetVolume, Time.deltaTime * boardSfxVolumeSpeed);
    }

    private void StopRagdoll()
    {
        if (!ragdoll.IsRagdolling) return;
        ragdoll.DisableRagdoll();
        animator.Rebind();
        ragdollVCam.Priority = 0;
        isSliding = false;
    }

    private void StartRagdoll(bool permanent = false)
    {
        if (ragdoll.IsRagdolling) return;
        ragdollTimer = permanent ? 9999 : ragdollTime;
        ragdoll.EnableRagdoll();
        ragdoll.SetVelocity(velocity);
        velocity = Vector3.zero;
        ragdollVCam.Priority = 20;
        boardSfx.volume = 0;
    }

    private void UpdateRagdoll()
    {
        skidEffect.SetFloat("SpawnRate", 0);
    }

    private void FixedUpdate()
    {
        if (ragdoll.IsRagdolling)
        {
            FixedUpdateRagdoll();
            return;
        }

        var groundSensed = CastCollider(rigidbody.position, rigidbody.rotation, rigidbody.rotation * Vector3.down, groundSenseDistance, out var groundHit);

        Vector3? groundVelocity =
            groundSensed && groundHit.rigidbody != null ? groundHit.rigidbody.GetPointVelocity(groundHit.point) :
            groundSensed && groundHit.rigidbody == null ? Vector3.zero :
            null;

        float? relativeGroundVelocity = groundVelocity != null ? Vector3.Dot(groundVelocity.Value - velocity, groundHit.normal) : null;

        // avoid sensing new ground that is moving away from us
        if (!isGrounded && groundSensed && relativeGroundVelocity.Value < -EPSILON)
        {
            groundSensed = false;
        }

        // flag
        isGrounded = groundSensed;
        if (isGrounded)
        {
            lastTouchedGround = 0f;
        }
        else
        {
            lastTouchedGround += Time.deltaTime;
        }

        // determine movement plane
        if (groundSensed)
        {
            if (groundHit.point != Vector3.zero)
            {
                surfacePlane = groundHit.normal;
            }
        }
        else
        {
            surfacePlane = Vector3.up;
        }

        // jump
        if (jumpRequested && lastTouchedGround < .2f)
        {
            velocity += surfacePlane * jumpForce;
            isGrounded = false;
            lastTouchedGround = .2f;
        }

        jumpRequested = false;

        // snap to surface
        if (isGrounded)
        {
            rigidbody.position += Vector3.down * groundHit.distance + Vector3.up * EPSILON;
        }

        // update rigidbody

        Debug.DrawLine(transform.position, transform.position + velocity, Color.red);
        rigidbody.MovePosition(ComputeMove(velocity * Time.fixedDeltaTime, out var lastDirection));
        rigidbody.MoveRotation(rotation);
        velocity = Vector3.Project(velocity, lastDirection);
        Debug.DrawLine(transform.position, transform.position + velocity, Color.blue);
    }

    private void FixedUpdateRagdoll()
    {
        
    }

    private Vector3 ComputeMove(Vector3 desiredMovement, out Vector3 lastDirection)
    {
        var position = rigidbody.position;
        var rotation = rigidbody.rotation;

        var remaining = desiredMovement;

        lastDirection = desiredMovement.normalized;

        for (var bounces = 0; bounces < maxBounces && remaining.magnitude > EPSILON; ++bounces)
        {
            var remainingDist = remaining.magnitude;

            // Do a cast of the collider to see if an object is hit during this movement bounce
            if (!CastCollider(position, rotation, remaining.normalized, remainingDist, out RaycastHit hit))
            {
                // If there is no hit, move to desired position and exit
                position += remaining;
                break;
            }

            // If we are overlapping with something, make a vague attempt to fix the problem
            if (hit.distance == 0 && hit.point == Vector3.zero)
            {
                if (Physics.ComputePenetration(
                    capsuleCollider,
                    position,
                    rotation,
                    hit.collider,
                    hit.collider.transform.position,
                    hit.collider.transform.rotation,
                    out var direction,
                    out var distance))
                {
                    position += direction * distance;
                }
                else
                {
                    CastCollider(position + Vector3.up * overlapCorrectionDistance, rotation, Vector3.down, overlapCorrectionDistance, out var hit2);

                    // if we're still overlapping something, just give up lol
                    if (hit2.distance == 0 && hit2.point == Vector3.zero)
                    {
                        position += -remaining.normalized * overlapCorrectionDistance;
                        break;
                    }

                    position += Vector3.up * (overlapCorrectionDistance - hit2.distance + EPSILON);
                }

                continue;
            }

            float percentTravelled = hit.distance / remainingDist;

            // Set the fraction of remaining movement (minus some small value)
            position += remaining * percentTravelled;
            // Push slightly along normal to stop from getting caught in walls
            position += hit.normal * EPSILON * 2;
            // Decrease remaining movement by fraction of movement remaining
            remaining *= 1f - percentTravelled;

            // Only apply angular change if hitting something
            // Get angle between surface normal and remaining movement
            var surfaceAngle = Vector3.Angle(hit.normal, remaining) - 90.0f;

            // Normalize angle between to be between 0 and 1
            // 0 means no angle, 1 means 90 degree angle
            surfaceAngle = Mathf.Min(MAX_ANGLE_SHOVE_DEG, Mathf.Abs(surfaceAngle));
            var normalizedSurfaceAngle = surfaceAngle / MAX_ANGLE_SHOVE_DEG;

            // Reduce the remaining movement by the remaining movement that ocurred
            remaining *= Mathf.Pow(1 - normalizedSurfaceAngle, angleCollisionPower) * 0.9f + 0.1f;

            // Rotate the remaining movement to be projected along the plane 
            // of the surface hit (emulate pushing against the object)
            var projectedRemaining = Vector3.ProjectOnPlane(remaining, hit.normal).normalized * remaining.magnitude;

            // If projected remaining movement is less than original remaining movement (so if the projection broke
            // due to float operations), then change this to just project along the vertical.
            if (projectedRemaining.magnitude + EPSILON < remaining.magnitude)
            {
                remaining = Vector3.ProjectOnPlane(remaining, Vector3.up).normalized * remaining.magnitude;
            }
            else
            {
                remaining = projectedRemaining;
            }

            // set the last direction to the direction we are moving in
            lastDirection = remaining.normalized;
        }

        // We're done, player was moved as part of loop
        return position;
    }

    private bool CastCollider(Vector3 position, Quaternion rotation, Vector3 direction, float distance, out RaycastHit hit)
    {
        var halfHeight = rotation * Vector3.up * (capsuleCollider.height * 0.5f - capsuleCollider.radius);
        var p1 = rotation * (capsuleCollider.center + halfHeight) + position;
        var p2 = rotation * (capsuleCollider.center - halfHeight) + position;

        var hits = Physics.CapsuleCastAll(p1, p2, capsuleCollider.radius, direction, distance, ~0, QueryTriggerInteraction.Ignore);

        hit = hits.Where(h => h.collider.transform != transform).OrderBy(h => h.distance).FirstOrDefault();

        return hits.Any(h => h.collider.transform != transform);
    }

    public void CheckTimer(float timer)
    {
        if (timer <= 0)
        {
            StartRagdoll(true);
        }
    }
}
