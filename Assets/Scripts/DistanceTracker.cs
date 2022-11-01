using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.VFX;

public class DistanceTracker : MonoBehaviour
{
    [SerializeField] private SOUP.FloatValue distance;

    private Vector3 startPosition;

    void Start()
    {
        distance.Value = 0;
        startPosition = transform.position;
    }

    void Update()
    {
        distance.Value = Vector3.Distance(startPosition, transform.position);
    }
}
