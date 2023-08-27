using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MusicController : MonoBehaviour
{
    private PlayerControls controls;

    [SerializeField] private AudioSource introAudioSource;
    [SerializeField] private AudioSource audioSource;
    private bool inIntro = true;

    private void Awake()
    {
        controls = new PlayerControls();
        controls.Special.Enable();
        controls.Special.MuteMusic.performed += ToggleMute;
    }

    public void Update()
    {
        if(inIntro && !introAudioSource.isPlaying)
        {
            inIntro = false;
            audioSource.Play();
        }
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
