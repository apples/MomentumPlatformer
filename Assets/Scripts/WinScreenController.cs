using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WinScreenController : MonoBehaviour
{
    public TextMeshProUGUI label;
    public TextMeshProUGUI value;

    public SOUP.FloatValue gameMode;
    public SOUP.FloatValue elapsedTime;
    public SOUP.FloatValue score;

    // Start is called before the first frame update
    void Start()
    {
        if(gameMode.Value == (float)Globals.Gamemodes.Score)
        {
            label.text = "  Final Score:";
            value.text = score.Value.ToString();
        }
        else
        {
            label.text = "Elapsed time:";
            value.text = elapsedTime.Value.ToString("0.00");
        }
    }
}
