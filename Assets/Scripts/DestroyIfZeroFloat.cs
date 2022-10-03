using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyIfZeroFloat : MonoBehaviour
{
    [SerializeField] private SOUP.FloatValue value;

    void Start()
    {
        if (value.Value == 0)
        {
            Destroy(gameObject);
        }
    }
}
