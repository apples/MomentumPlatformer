using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainMenuSystem : MonoBehaviour
{
    [SerializeField] private SOUP.FloatValue enableTimerFlag;

    public void PlayGame()
    {
        enableTimerFlag.Value = 1;
        UnityEngine.SceneManagement.SceneManager.LoadScene("GameplayScene");
    }

    public void PlayGameNoTimer()
    {
        enableTimerFlag.Value = 0;
        UnityEngine.SceneManagement.SceneManager.LoadScene("GameplayScene");
    }
}
