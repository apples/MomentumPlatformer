using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

// Mostly copied from https://github.com/nicholas-maltbie/OpenKCC

public class PlayerController : MonoBehaviour
{
    private const float EPSILON = 0.001f;
    private const float MAX_ANGLE_SHOVE_DEG = 90f;

    [Header("Camera")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float lookSensitivity = 1f;

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

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private float walkAnimSpeed = 1f;
    [SerializeField] private float runAnimSpeed = 10f;
    [SerializeField] private float animSpeedFactor = 10f;
    [SerializeField] private float maxAnimSpeed = 2f;

    private new Rigidbody rigidbody;
    private CapsuleCollider capsuleCollider;

    private PlayerControls controls;

    private Vector3 velocity;
    private Quaternion rotation;

    private Vector3 lookEuler;

    private bool isGrounded;
    private Vector3 surfacePlane;

    private bool isSliding = false;

    private bool jumpRequested = false;

    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        controls = new PlayerControls();
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
    }

    private void Update()
    {
        // inputs

        isSliding = controls.Player.Slide.IsPressed();

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

            lookEuler.x += -lookInput.y * lookSensitivity;
            lookEuler.y += lookInput.x * lookSensitivity;
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
                var boardRight = Vector3.Cross(surfacePlane, boardForward).normalized;

                var snowAngle = Vector3.SignedAngle(velocityDirection, boardForward, surfacePlane);

                // carving
                if (Mathf.Abs(snowAngle) < slideSkidAngle)
                {
                    // gently redirect the velocity to match the board's forward direction
                    velocity = Quaternion.AngleAxis(snowAngle * carvingTurnFactor, surfacePlane) * velocity;
                }
                // skidding
                else
                {
                    // model the skid as if the snow is a particle of a certain mass colliding with the side of the board
                    var snowForce = Vector3.Project(snowMassEquivalent * -currentPlanarVelocity, boardRight);
                    velocity += snowForce * Time.deltaTime;
                }
            }

            lookEuler.x += -lookInput.y * lookSensitivity;
            lookEuler.y = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
        }

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

        lookEuler.x = Mathf.Clamp(lookEuler.x, -90f, 90f);
        lookEuler.y = Mathf.Repeat(lookEuler.y, 360f);

        cameraPivot.rotation = Quaternion.Euler(lookEuler.x, lookEuler.y, 0f);

        // animation

        animator.SetFloat("Speed", Mathf.Min(Vector3.ProjectOnPlane(velocity, surfacePlane).magnitude / animSpeedFactor, maxAnimSpeed));
        animator.SetFloat("Walk-Run", Mathf.InverseLerp(walkAnimSpeed, runAnimSpeed, velocity.magnitude));
        animator.SetBool("Airborne", !isGrounded);
        animator.SetBool("Sliding", isSliding);
    }

    private void FixedUpdate()
    {
        var groundSensed = CastCollider(rigidbody.position, rigidbody.rotation, Vector3.down, groundSenseDistance, out var groundHit);

        Vector3? groundVelocity =
            groundSensed && groundHit.rigidbody != null ? groundHit.rigidbody.GetPointVelocity(groundHit.point) :
            groundSensed && groundHit.rigidbody == null ? Vector3.zero :
            null;

        var relativeGroundVelocity = groundVelocity - velocity;

        // avoid sensing new ground that is moving away from us
        if (!isGrounded && groundSensed && relativeGroundVelocity.Value.y < EPSILON)
        {
            groundSensed = false;
        }

        // flag
        isGrounded = groundSensed;

        // determine movement plane
        if (groundSensed)
        {
            surfacePlane = groundHit.normal;
        }
        else
        {
            surfacePlane = Vector3.up;
        }

        // jump
        if (jumpRequested && isGrounded)
        {
            velocity += surfacePlane * jumpForce;
            isGrounded = false;
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
                CastCollider(position + Vector3.up * overlapCorrectionDistance, rotation, Vector3.down, overlapCorrectionDistance, out var hit2);

                // if we're still overlapping something, just give up lol
                if (hit2.distance == 0 && hit2.point == Vector3.zero)
                {
                    break;
                }

                position += Vector3.up * (overlapCorrectionDistance - hit2.distance + EPSILON);
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
        var halfHeight = Vector3.up * (capsuleCollider.height * 0.5f - capsuleCollider.radius);
        var p1 = rotation * (capsuleCollider.center + halfHeight) + position;
        var p2 = rotation * (capsuleCollider.center - halfHeight) + position;

        var hits = Physics.CapsuleCastAll(p1, p2, capsuleCollider.radius, direction, distance, ~0, QueryTriggerInteraction.Ignore);

        hit = hits.Where(h => h.collider.transform != transform).OrderBy(h => h.distance).FirstOrDefault();

        return hits.Any(h => h.collider.transform != transform);
    }
}
