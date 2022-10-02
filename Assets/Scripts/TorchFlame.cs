using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.VFX;

// Mostly copied from https://github.com/nicholas-maltbie/OpenKCC

public class TorchFlame : MonoBehaviour
{
    [SerializeField] private VisualEffect flameEffect;

    public void SetFlameBasedOnTime(float time)
    {
        flameEffect.enabled = time > 0;
    }
}
