using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FixedFollow : MonoBehaviour
{
    [SerializeField] private Transform positionParent;

    private Vector3 fixedPosition;

    // Start is called before the first frame update
    void Start()
    {
        fixedPosition = transform.position - positionParent.position;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        transform.position = positionParent.position + fixedPosition;
    }
}
