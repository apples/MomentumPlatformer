using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public SOUP.FloatValue enableTimerFlag;

    [Header("Torch")]
    public SOUP.FloatValue torchTimer;

    private void Start()
    {
        torchTimer.Value = 10;
    }

    private void Update()
    {
        // Torch
        if (enableTimerFlag.Value == 1)
        {
            torchTimer.Value = Mathf.Max(torchTimer.Value - Time.deltaTime, 0);
        }
    }
}
