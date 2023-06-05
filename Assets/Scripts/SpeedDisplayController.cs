using System.Collections;
using System.Collections.Generic;
using SOUP;
using TMPro;
using UnityEngine;

public class SpeedDisplayController : MonoBehaviour
{
    public FloatValue currentSpeed;

    public float capSpeed = 700f;

    private List<GameObject> healthbarSegments = new List<GameObject>();

    private TextMeshProUGUI tmpUgui;

    void Start()
    {
        foreach (Transform segment in transform)
        {
            healthbarSegments.Add(segment.gameObject);
        }
        healthbarSegments.Sort((a, b) => a.name.CompareTo(b.name));

        tmpUgui = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        var value = (int)(currentSpeed.Value * 3.6f); // km/h

        tmpUgui.text = value.ToString() + " km/h";

        var numChunks = Mathf.Min(value * 17 / capSpeed, 17);

        for(int i = 0; i < 17; i++)
        {
            healthbarSegments[i].SetActive(i < numChunks);
        }
    }
}
