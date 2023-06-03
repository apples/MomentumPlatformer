using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using SOUP;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.VFX;

// Mostly copied from https://github.com/nicholas-maltbie/OpenKCC

public class PlayerController : MonoBehaviour
{
    private const float EPSILON = 0.001f;

    [Header("SOUP")]
    [SerializeField] private GameObjectValue playerGameObjectValue;

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
    [SerializeField] private bool snapToGround = false;
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
    [SerializeField] private Vector3 startVelocity = Vector3.zero;
    [SerializeField] private Quaternion startRotation = Quaternion.identity;

    [Header("Trick")]
    [SerializeField] private float trickBoostImpulse = 30f;
    [SerializeField] private float trickYawThreshold = 100f;
    [SerializeField] private float trickPitchThreshold = 100f;

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

    private float ragdollTimer = 0f;

    private bool hasTrick = false;
    private bool doingTrick = false;
    private float trickYaw;
    private float trickPitch;

    public SOUP.FloatValue currentSpeed;

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
        };
    }

    private void OnEnable()
    {
        controls.Player.Enable();
        playerGameObjectValue.Value = this.gameObject;
    }

    private void OnDisable()
    {
        if (playerGameObjectValue.Value == this.gameObject)
        {
            playerGameObjectValue.Value = null;
        }
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
            var moveRight = Vector3.Cross(surfacePlane, moveForward).normalized;

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

            hasTrick = false;
            doingTrick = false;
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

                // consume trick
                if (hasTrick)
                {
                    hasTrick = false;
                    velocity += boardForward * trickBoostImpulse;
                    boostEffect.Play();
                    Debug.Log("Boost");
                }

                doingTrick = false;
            }
            else
            {
                // air spin
                var spinAngularAcceleration = moveInput.y * slideSteeringAngularSpeedDeg * Time.deltaTime;
                rotation = Quaternion.AngleAxis(spinAngularAcceleration, Camera.main.transform.right) * rotation;

                skidEffect.SetFloat("SpawnRate", 0);
                boardSfxTargetVolume = 0;

                // trick
                if (!doingTrick)
                {
                    doingTrick = true;
                    trickYaw = 0;
                    trickPitch = 0;
                }

                trickYaw += steeringAngularAcceleration;
                trickPitch += spinAngularAcceleration;

                if (Mathf.Abs(trickYaw) > trickYawThreshold || Mathf.Abs(trickPitch) > trickPitchThreshold)
                {
                    hasTrick = true;
                }
            }
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

        currentSpeed.Value = (int)velocity.magnitude;

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

        if (isSliding)
        {
            animator.SetFloat("Lean", moveInput.x);
        }

        // audio

        boardSfx.volume = Mathf.MoveTowards(boardSfx.volume, boardSfxTargetVolume, Time.deltaTime * boardSfxVolumeSpeed);
    }

    private void StopRagdoll()
    {
        if (!ragdoll.IsRagdolling) return;
        ragdoll.DisableRagdoll();
        ragdollVCam.Priority = 0;
        isSliding = false;
        rotation = Quaternion.LookRotation(Camera.main.transform.forward, surfacePlane);
        rigidbody.rotation = rotation;
    }

    private void StartRagdoll(bool permanent = false)
    {
        if (ragdoll.IsRagdolling)
        {
            if (permanent)
            {
                ragdollTimer = 9999;
            }
            return;
        }

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
        board.SetActive(false);
    }

    private void FixedUpdate()
    {
        if (ragdoll.IsRagdolling)
        {
            FixedUpdateRagdoll();
            return;
        }

        // ground sensing
        var groundSensed = CastCollider(rigidbody.position, rigidbody.rotation, rigidbody.rotation * Vector3.down, groundSenseDistance, out var groundHit);

        var groundHitNormal = groundSensed ? GetTrueNormal(groundHit) : Vector3.up;

        Vector3? groundVelocity =
            groundSensed && groundHit.rigidbody != null ? groundHit.rigidbody.GetPointVelocity(groundHit.point) :
            groundSensed && groundHit.rigidbody == null ? Vector3.zero :
            null;
        
        float? relativeGroundVelocity = groundVelocity != null ? Vector3.Dot(groundVelocity.Value - velocity, groundHitNormal) : null;

        // avoid sensing new ground that is moving away from us
        if (!isGrounded && groundSensed && relativeGroundVelocity.Value < -EPSILON)
        {
            groundSensed = false;
        }

        // tumble
        if (isSliding && !groundSensed && CastCollider(rigidbody.position, rigidbody.rotation, rigidbody.rotation * Vector3.up, groundSenseDistance, out var headHit))
        {
            if (headHit.point != Vector3.zero)
            {
                StartRagdoll();
                return;
            }
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
                surfacePlane = groundHitNormal;
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
        if (snapToGround && isGrounded)
        {
            rigidbody.position += Vector3.down * (groundHit.distance - EPSILON) + Vector3.up * EPSILON;
        }

        // update rigidbody

        Debug.DrawLine(transform.position, transform.position + velocity, Color.red);
        MoveAndSlide(Time.fixedDeltaTime, 45f);
        rigidbody.MoveRotation(rotation);
        Debug.DrawLine(transform.position, transform.position + velocity, Color.blue);
    }

    private void FixedUpdateRagdoll()
    {
        
    }

    private void MoveAndSlide(float deltaTime, float minWallAngle)
    {
        var position = rigidbody.position;
        var rotation = rigidbody.rotation;

        var remainingMotion = velocity * deltaTime;
        var remainingTime = deltaTime;

        for (var bounces = 0; bounces < maxBounces && remainingMotion.magnitude > 0f; ++bounces)
        {
            var remainingDist = remainingMotion.magnitude;

            var direction = remainingMotion.normalized;

            // Do a cast of the collider to see if an object is hit during this movement bounce
            if (!CastCollider(position, rotation, direction, remainingDist, out var hit))
            {
                // If there is no hit, move to desired position and exit
                position += remainingMotion;

                // Apply gravity!
                var gravityDist = 0.5f * gravity * Mathf.Pow(remainingTime, 2);
                var didGravityHit = CastCollider(position, rotation, Vector3.down, gravityDist, out var gravityHit);
                var gravityAccel = gravityHit.distance > EPSILON ? Vector3.down * (gravityHit.distance - EPSILON) + Vector3.up * EPSILON : gravityDist * Vector3.down;

                position += gravityAccel;
                break;
            }

            // If we are overlapping with something, make a vague attempt to fix the problem
            if (hit.distance < EPSILON)
            {
                if (Physics.ComputePenetration(
                    capsuleCollider,
                    position,
                    rotation,
                    hit.collider,
                    hit.collider.transform.position,
                    hit.collider.transform.rotation,
                    out var resolveDirection,
                    out var distance))
                {
                    position += resolveDirection * distance;
                }
                else
                {
                    CastCollider(position + Vector3.up * overlapCorrectionDistance, rotation, Vector3.down, overlapCorrectionDistance, out var hit2);

                    // if we're still overlapping something, just give up lol
                    if (hit2.distance < EPSILON)
                    {
                        position += -remainingMotion.normalized * overlapCorrectionDistance;
                        break;
                    }

                    position += Vector3.up * (overlapCorrectionDistance - hit2.distance + EPSILON);
                }

                continue;
            }

            var trueNormal = GetTrueNormal(hit);


            var deltaPosition = direction * hit.distance + trueNormal * EPSILON;

            // Update position to where the hit occurred
            position += deltaPosition;

            float percentTravelled = deltaPosition.magnitude / remainingDist;

            // Decrease remaining movement by fraction of movement remaining
            remainingMotion *= 1f - percentTravelled;
            remainingTime *= 1f - percentTravelled;

            // Only apply angular change if hitting something
            // Get angle between surface normal and direction
            var surfaceAngle = Vector3.Angle(trueNormal, direction) - 90.0f;
            var isWall = surfaceAngle > minWallAngle;

            if (isWall)
            {
                remainingMotion = Vector3.ProjectOnPlane(remainingMotion, trueNormal);
                velocity = Vector3.ProjectOnPlane(velocity, trueNormal);
            }
            else
            {
                remainingMotion = Vector3.ProjectOnPlane(remainingMotion, trueNormal).normalized * remainingMotion.magnitude;
                velocity = Vector3.ProjectOnPlane(velocity, trueNormal).normalized * velocity.magnitude;
            }
        }

        // We're done, player was moved as part of loop
        rigidbody.MovePosition(position);
    }

    private Vector3 GetTrueNormal(RaycastHit hit)
    {
        var terrain = hit.collider.GetComponent<Terrain>();
        if (terrain != null)
        {
            var terrainData = terrain.terrainData;
            var terrainPos = terrain.transform.position;
            var angleFromUp = Vector3.Angle(Vector3.up, hit.normal);
            var terrainCoord = new Vector2((hit.point.x - terrainPos.x) / terrainData.size.x, (hit.point.z - terrainPos.z) / terrainData.size.z);
            var terrainHeight = terrainData.GetInterpolatedHeight(terrainCoord.x, terrainCoord.y);
            var heightAboveSurface = hit.point.y - terrainPos.y - terrainHeight;
            var isNotVertical = Vector3.Angle(Vector3.up, hit.normal) < 89f;
            var isOnSurface = heightAboveSurface < EPSILON;
            if (isNotVertical && isOnSurface)
            {
                return terrainData.GetInterpolatedNormal(terrainCoord.x, terrainCoord.y);
            }
        }

        return hit.normal;
    }

    private bool CastCollider(Vector3 position, Quaternion rotation, Vector3 direction, float distance, out RaycastHit hit)
    {
        var halfHeight = Vector3.up * (capsuleCollider.height * 0.5f - capsuleCollider.radius);
        var p1 = rotation * (capsuleCollider.center + halfHeight) + position;
        var p2 = rotation * (capsuleCollider.center - halfHeight) + position;

        var hits = Physics.CapsuleCastAll(p1, p2, capsuleCollider.radius - EPSILON, direction, distance, ~0, QueryTriggerInteraction.Ignore);

        hit = hits.Where(h => h.collider.transform != transform).OrderBy(h => h.distance).FirstOrDefault();

        return hits.Any(h => h.collider.transform != transform);
    }

    private bool OverlapCollider(Vector3 position, Quaternion rotation, out Collider[] results)
    {
        var halfHeight = Vector3.up * (capsuleCollider.height * 0.5f - capsuleCollider.radius);
        var p1 = rotation * (capsuleCollider.center + halfHeight) + position;
        var p2 = rotation * (capsuleCollider.center - halfHeight) + position;

        results = Physics.OverlapCapsule(p1, p2, capsuleCollider.radius, ~0, QueryTriggerInteraction.Ignore)
            .Where(c => c.transform != transform)
            .ToArray();

        return results.Length != 0;
    }

    public void CheckTimer(float timer)
    {
        if (timer <= 0)
        {
            StartRagdoll(true);
        }
    }

    public void Blastoff()
    {
        isSliding = true;
        velocity = startVelocity;
        rotation = startRotation;
    }
}
