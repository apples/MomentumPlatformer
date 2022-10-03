using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.VFX;

public class SimpleRagdoll : MonoBehaviour
{
    private Rigidbody[] rigidbodies;
    private Collider[] colliders;
    private Animator animator;

    public bool IsRagdolling { get; private set; }

    private void Awake()
    {
        rigidbodies = GetComponentsInChildren<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();
        animator = GetComponent<Animator>();
    }

    public void EnableRagdoll(bool force = false)
    {
        if (IsRagdolling && !force)
            return;

        foreach (var rigidbody in rigidbodies)
        {
            rigidbody.isKinematic = false;
        }

        foreach (var collider in colliders)
        {
            collider.enabled = true;
        }

        animator.enabled = false;

        IsRagdolling = true;
    }

    public void DisableRagdoll(bool force = false)
    {
        if (!IsRagdolling && !force)
            return;

        foreach (var rigidbody in rigidbodies)
        {
            rigidbody.isKinematic = true;
        }

        foreach (var collider in colliders)
        {
            collider.enabled = false;
        }

        animator.enabled = true;
        animator.Rebind();

        IsRagdolling = false;
    }

    private void Start()
    {
        DisableRagdoll(true);
    }

    public void SetVelocity(Vector3 velocity)
    {
        foreach (var rigidbody in rigidbodies)
        {
            rigidbody.velocity = velocity;
        }
    }
}
