using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Guardian : MonoBehaviour
{
    public Transform player;

    private Material mat;

    // Start is called before the first frame update
    void Start()
    {
        mat = GetComponent<Renderer>().material;
    }

    // Update is called once per frame
    void Update()
    {
        mat.SetVector("_SpherePosition", player.position);
        transform.position = new Vector3(player.position.x, player.position.y, transform.position.z);
    }
}
