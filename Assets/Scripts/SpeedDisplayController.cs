using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpeedDisplayController : MonoBehaviour
{
    public SOUP.FloatValue currentSpeed;

    private List<GameObject> healthbarSegments = new List<GameObject>();

    // Start is called before the first frame update
    void Start()
    {
        foreach (Transform segment in transform)
        {
            healthbarSegments.Add(segment.gameObject);
        }
        healthbarSegments.Sort((a, b) => a.name.CompareTo(b.name));
    }

    // Update is called once per frame
    void Update()
    {
        transform.GetComponent<TMPro.TextMeshProUGUI>().text = currentSpeed.Value.ToString() + " MPH";

        for(int i = 0; i < Mathf.Min((int)currentSpeed.Value / 10, 17); i++)
        {
            //if(!healthbarSegments[i].activeSelf)
                healthbarSegments[i].SetActive(true);
        }
        for (int i = 16; i > (int)currentSpeed.Value / 10; i--)
        {
            //if (healthbarSegments[i].activeSelf)
                healthbarSegments[i].SetActive(false);
        }
    }
}
