using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MusicController : MonoBehaviour
{
    private PlayerControls controls;

    [SerializeField] private AudioSource audioSource;

    private void Awake()
    {
        controls = new PlayerControls();
        controls.Special.Enable();
        controls.Special.MuteMusic.performed += ToggleMute;
    }

    private void ToggleMute(InputAction.CallbackContext obj)
    {
        audioSource.mute = !audioSource.mute;
    }

    private void OnEnable()
    {
        controls.Player.Enable();
    }

    private void OnDisable()
    {
        controls.Player.Disable();
    }
}
