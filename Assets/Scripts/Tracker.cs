using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.VFX;

public class Tracker : MonoBehaviour
{
    private GameObject holder;

    private float next = 5f;

    private int i = 1;

    private void Start()
    {
        holder = new GameObject("TrackerHolder");
    }

    private void Update()
    {
        next -= Time.deltaTime;
        if (next <= 0)
        {
            next = 5f;
            var go = new GameObject("Tracker " + i.ToString());
            go.transform.parent = holder.transform;
            go.transform.position = transform.position;
            ++i;
        }
    }
}
