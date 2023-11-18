using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SurpriseRock : MonoBehaviour
{
    private bool isSprouting = false;
    private bool hasSprouted = false;
    private Vector3 startingPos;
    private Transform rock;

    public float distance;
    public float speed;

    // Start is called before the first frame update
    void Start()
    {
        rock = transform.GetChild(0);
        startingPos = rock.position;
    }

    // Update is called once per frame
    void Update()
    {
        if(isSprouting)
        {
            if (((rock.position - startingPos).magnitude) < distance)
            {
                rock.position += Time.deltaTime * speed * transform.up;
            }
            else
            {
                isSprouting = false;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!hasSprouted)
        {
            isSprouting = true;
            hasSprouted = true;
        }
    }
}
