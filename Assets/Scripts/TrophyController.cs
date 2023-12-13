using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrophyController : MonoBehaviour
{
    Vector3 direction;

    // Start is called before the first frame update
    void Start()
    {
        direction = new Vector3(Random.value, Random.value, Random.value).normalized;
    }

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(direction * Time.deltaTime * 100);
    }
}
