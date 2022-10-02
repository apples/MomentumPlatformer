using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
