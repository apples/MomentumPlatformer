using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.VFX;

public class Brazier : MonoBehaviour
{
    [SerializeField] private new Collider collider;
    [SerializeField] private VisualEffect flameEffect;

    [SerializeField] private bool isAlive = true;

    public bool IsAlive
    {
        get { return isAlive; }
        set
        {
            isAlive = value;
            flameEffect.enabled = isAlive;
            collider.enabled = isAlive;
        }
    }
}
