using SOUP;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using System.Linq;

public class ScoreController : MonoBehaviour
{
    public FloatValue currentScore;
    public TextMeshProUGUI baseText;
    public GameObject subScoreObject;

    public float timePenaltyInterval = 1;
    public float timePenaltyRampInterval = 10;

    public SOUP.FloatValue scoreTimeValue;

    private float penaltyTimer;
    private List<SubScore> subScores = new List<SubScore>();

    // Start is called before the first frame update
    void Start()
    {
        currentScore.Value = 100; //for testing
        penaltyTimer = 0;
    }

    // Update is called once per frame
    void Update()
    {
        if(currentScore.Value > 0)
        {
            penaltyTimer += Time.deltaTime;
        }

        if (penaltyTimer > timePenaltyInterval)
        {
            penaltyTimer -= timePenaltyInterval;
            currentScore.Value -= 5 * (Mathf.FloorToInt(scoreTimeValue.Value / timePenaltyRampInterval) + 1);
        }

        baseText.text = "Score: " + currentScore.Value.ToString("00000");

        foreach (SubScore subScore in subScores)
        {
            subScore.Update();
        }
        subScores.FindAll(x => x.isDeletable).ForEach(y => Destroy(y.baseObject));
        subScores.RemoveAll(x => x.isDeletable);
    }


    public void AddScore(string reason, int score)
    {
        currentScore.Value += score;

        GameObject newSubScore = Instantiate(subScoreObject, transform);
        SubScore newSubScoreStruct = new SubScore();
        newSubScoreStruct.baseObject = newSubScore;
        newSubScoreStruct.text = newSubScore.GetComponentsInChildren<TextMeshProUGUI>()[0];
        newSubScoreStruct.text.text = reason;
        newSubScoreStruct.value = newSubScore.GetComponentsInChildren<TextMeshProUGUI>()[1];
        newSubScoreStruct.value.text = "+" + score;
        newSubScoreStruct.baseObject.transform.localPosition = new Vector3(210, -40, 0);
        newSubScoreStruct.lerpFrom = new Vector3(210, -40, 0);
        newSubScoreStruct.lerpTo = new Vector3(-155, -40, 0);

        foreach (SubScore subScore in subScores)
        {
            subScore.MoveDown();
        }

        subScores.Add(newSubScoreStruct);
    }
}

internal class SubScore
{
    public GameObject baseObject;
    public TextMeshProUGUI text;
    public TextMeshProUGUI value;

    public Vector3 lerpFrom;
    public Vector3 lerpTo;
    public float lerpT = 0;

    public bool isDeletable = false;

    public void Update()
    {
        lerpT += Time.deltaTime * 2;
        baseObject.transform.localPosition = Vector3.Lerp(lerpFrom, lerpTo, lerpT);

        text.color = new Color(text.color.r, text.color.g, text.color.b, text.color.a - (Time.deltaTime * .15f));
        value.color = new Color(value.color.r, value.color.g, value.color.b, value.color.a - (Time.deltaTime * .15f));

        if (text.color.a <= 0)
        {
            isDeletable = true;
        }
    }

    public void MoveDown()
    {
        lerpFrom = baseObject.transform.localPosition;
        lerpTo = new Vector3(lerpTo.x, lerpTo.y - 40, lerpTo.z);
        lerpT = 0;
    }
}