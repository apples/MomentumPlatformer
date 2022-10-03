using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.VFX;

public class FlagController : MonoBehaviour
{
    [SerializeField] private SOUP.Event onWin;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<PlayerController>() is PlayerController player)
        {
            onWin.Raise();
        }
    }
}
