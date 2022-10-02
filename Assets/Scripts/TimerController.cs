using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimerController : MonoBehaviour
{
    public SOUP.FloatValue torchTimer;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.GetComponent<TMPro.TextMeshProUGUI>().text = torchTimer.Value.ToString();
    }
}
