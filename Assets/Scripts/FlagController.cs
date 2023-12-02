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
    [SerializeField] private SOUP.Event onLose;
    [SerializeField] private SOUP.FloatValue gameMode;
    [SerializeField] private SOUP.FloatValue score;
    [SerializeField] private SOUP.FloatValue scoreGoal;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<PlayerController>() is PlayerController player)
        {
            if((gameMode.Value != (float)Globals.Gamemodes.Score && gameMode.Value != (float)Globals.Gamemodes.All) || score.Value > scoreGoal.Value)
            {
                onWin.Raise();
            }
            else
            {
                onLose.Raise();
            }
        }
    }
}
