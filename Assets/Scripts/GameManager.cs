using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Mostly copied from https://github.com/nicholas-maltbie/OpenKCC

public class GameManager : MonoBehaviour
{
    [Header("Torch")]
    public SOUP.FloatValue torchTimer;

    private void Start()
    {
    }

    private void Update()
    {
        // Torch
        torchTimer.Value = Mathf.Max(torchTimer.Value - Time.deltaTime, 0);
    }
}
