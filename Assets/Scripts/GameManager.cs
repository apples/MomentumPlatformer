using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public SOUP.FloatValue enableTimerFlag;
    public SOUP.FloatValue torchTimer;
    public SOUP.FloatValue scoreTimeValue;

    public SOUP.FloatValue currentGamemode;
    public SOUP.FloatValue currentLevel;

    public Light sun;
    public GameObject braziers;
    public GameObject avalanche;
    public GameObject scoreGoal;

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
                scoreGoal.SetActive(false);
                break;
            case (float)Globals.Gamemodes.Avalanche:
                avalanche.SetActive(true);
                braziers.SetActive(false);
                scoreGoal.SetActive(false);
                break;
            case (float)Globals.Gamemodes.Score:
                scoreGoal.SetActive(true);
                braziers.SetActive(false);
                avalanche.SetActive(false);
                break;
            case (float)Globals.Gamemodes.All:
                torchTimer.Value = 10;
                scoreTimeValue.Value = 0;
                braziers.SetActive(true);
                avalanche.SetActive(true);
                scoreGoal.SetActive(true);
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
        SaveGame();
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

    private void SaveGame()
    {
        string path = Application.persistentDataPath + "\\SaveData.txt";
        List<List<bool>> saveData = new List<List<bool>>();

        if (File.Exists(path))
        {
            string fileString = "";
            using (StreamReader sr = File.OpenText(path))
            {
                fileString = sr.ReadToEnd();
            }

            saveData = JsonConvert.DeserializeObject<List<List<bool>>>(fileString);
            saveData[(int)currentLevel.Value][(int)currentGamemode.Value] = true;
        }
        else
        {
            for(int i = 0; i < Enum.GetNames(typeof(Globals.Levels)).Length; i++)
            {
                saveData.Add(new List<bool>());
                for (int j = 0; j < Enum.GetNames(typeof(Globals.Gamemodes)).Length; j++)
                {
                    if(i == currentLevel.Value && j == currentGamemode.Value)
                    {
                        saveData[i].Add(true);
                    }
                    else
                    {
                        saveData[i].Add(false);
                    }
                }
            }
        }

        using (StreamWriter sw = File.CreateText(path))
        {
            sw.Write(Newtonsoft.Json.JsonConvert.SerializeObject(saveData));
        }
    }
}