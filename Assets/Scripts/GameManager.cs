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

    public SOUP.FloatValue currentGamemode;

    public Light sun;
    public GameObject braziers;
    public GameObject avalanche;

    private bool stopTimer = false;

    //public bool withCountdown = true;

    private void Start()
    {
        torchTimer.Value = 10;
        scoreTimeValue.Value = 0;
        //enableTimerFlag.Value = withCountdown ? 1 : 0;

        switch (currentGamemode.Value)
        {
            case (float)Globals.Gamemodes.SunSurival:
                torchTimer.Value = 10;
                scoreTimeValue.Value = 0;
                braziers.SetActive(true);
                avalanche.SetActive(false);
                break;
            case (float)Globals.Gamemodes.Avalanche:
                avalanche.SetActive(true);
                braziers.SetActive(false);
                break;
            case (float)Globals.Gamemodes.Score:
                braziers.SetActive(false);
                avalanche.SetActive(false);
                break;
            case (float)Globals.Gamemodes.All:
                torchTimer.Value = 10;
                scoreTimeValue.Value = 0;
                braziers.SetActive(true);
                avalanche.SetActive(true);
                break;
        }
    }

    private void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Keypad7))
        {
            stopTimer = !stopTimer;
        }

        if (!stopTimer)
        {
            // Torch
            if (currentGamemode.Value == (float)Globals.Gamemodes.SunSurival)
            {
                torchTimer.Value = Mathf.Max(torchTimer.Value - Time.deltaTime, 0);

                if (torchTimer.Value == 0)
                {
                    Lose();
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
        scoreTimeValue.Value = 0;
        StartCoroutine(WinCoroutine());
    }

    public void Lose()
    {
        stopTimer = true;
        scoreTimeValue.Value = 0;
        StartCoroutine(LoseCoroutine());
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