using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

public class PlayerCollider : MonoBehaviour
{
    [SerializeField] private SOUP.FloatValue torchTimer;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<Brazier>() is Brazier brazier)
        {
            brazier.IsAlive = false;
            torchTimer.Value = 10;
        }
    }
}
