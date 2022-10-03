using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.VFX;

public class FixedCam : MonoBehaviour
{
    [SerializeField] private CinemachineVirtualCamera cam;

    private void Start()
    {
        cam.Follow = null;
        cam.LookAt = null;
        cam.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<PlayerController>() is PlayerController player)
        {
            cam.enabled = true;
            cam.LookAt = player.transform;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.GetComponent<PlayerController>() is PlayerController player)
        {
            cam.enabled = false;
            cam.LookAt = null;
        }
    }
}
