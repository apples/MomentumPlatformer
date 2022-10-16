using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.VFX;

public class WorldOrigin : MonoBehaviour
{
    [SerializeField] private Transform track;
    [SerializeField] private float maxDist = 100;

    private new Rigidbody rigidbody;

    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        transform.position = -track.position;
        rigidbody.position = transform.position;
    }

    private void LateUpdate()
    {
        if (track.position.magnitude > maxDist)
        {
            transform.position = -track.position;
            rigidbody.position = transform.position;
        }
    }
}
