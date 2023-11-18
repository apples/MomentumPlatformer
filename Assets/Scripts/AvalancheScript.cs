using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class AvalancheScript : MonoBehaviour
{
    public float avalancheSpeed;

    private float sphereSpinSpeed = 8.0f;

    public GameObject curtain;
    public GameObject particles;
    public GameObject feet;
    private List<Transform> spheres;

    public Transform player;

    // Start is called before the first frame update
    void Start()
    {
        spheres = feet.GetComponentsInChildren<Transform>().ToList();
        spheres.RemoveAt(0);

        foreach (var sphere in spheres)
        {
            sphere.rotation = Quaternion.Euler(Random.value * 360, Random.value * 360, Random.value * 360); 
        }
    }

    // Update is called once per frame
    void Update()
    {
        this.transform.position += new Vector3(Time.deltaTime * -avalancheSpeed * (1 + ((curtain.transform.position.x - player.position.x) / 1000)), 0, 0);

        float rando;
        RaycastHit hit;
        foreach (var sphere in spheres)
        {
            //rather expensive, this can be cut down to 5 repeating randos if performance needs
            rando = 1250 + (Mathf.PerlinNoise1D((Time.time + sphere.localPosition.z) * 3) * 5000);
            sphere.localScale = new Vector3(rando, rando, rando);

            if(Physics.Raycast(sphere.position, Vector3.down, out hit, 100))
            {
                sphere.position -= new Vector3(0, hit.distance, 0);
            }
            else if (Physics.Raycast(sphere.position, Vector3.up, out hit, 100))
            {
                sphere.position += new Vector3(0, hit.distance, 0);
            }

            sphere.rotation *= Quaternion.Euler(0, 0, Time.deltaTime * avalancheSpeed * sphereSpinSpeed);
        }

        curtain.transform.position = new Vector3(curtain.transform.position.x, spheres.ElementAt(0).position.y, curtain.transform.position.z);
        particles.transform.position = new Vector3(particles.transform.position.x, spheres.ElementAt(0).position.y, particles.transform.position.z);
    }
}
