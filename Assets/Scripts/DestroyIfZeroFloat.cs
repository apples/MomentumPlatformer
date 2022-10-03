using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyIfZeroFloat : MonoBehaviour
{
    [SerializeField] private SOUP.FloatValue value;
    [SerializeField] private bool invert = false;

    void Start()
    {
        if (invert ? value.Value != 0 : value.Value == 0)
        {
            Destroy(gameObject);
        }
    }
}
