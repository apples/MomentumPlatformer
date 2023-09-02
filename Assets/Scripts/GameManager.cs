using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public SOUP.FloatValue enableTimerFlag;

    public SOUP.FloatValue torchTimer;

    public SOUP.FloatValue scoreTimeValue;

    public Light sun;

    private bool stopTimer = false;

    private void Start()
    {
        torchTimer.Value = 10;
        scoreTimeValue.Value = 0;
        enableTimerFlag.Value = 1;
    }

    private void LateUpdate()
    {
        if (!stopTimer)
        {
            // Torch
            if (enableTimerFlag.Value == 1)
            {
                torchTimer.Value = Mathf.Max(torchTimer.Value - Time.deltaTime, 0);

                if (torchTimer.Value == 0)
                {
                    stopTimer = true;
                    StartCoroutine(LoseCoroutine());
                }

                sun.transform.rotation = Quaternion.Euler(-10 + (torchTimer.Value * 5), 90, 0);
            }

            // Score
            scoreTimeValue.Value += Time.deltaTime;
        }
    }

    public void Win()
    {
        stopTimer = true;
        StartCoroutine(WinCoroutine());
    }

    private IEnumerator WinCoroutine()
    {
        Debug.Log("You win!");
        yield return new WaitForSeconds(3);
        UnityEngine.SceneManagement.SceneManager.LoadScene("Win");
    }

    private IEnumerator LoseCoroutine()
    {
        Debug.Log("You lose!");
        yield return new WaitForSeconds(3);
        UnityEngine.SceneManagement.SceneManager.LoadScene("Lose");
    }
}
