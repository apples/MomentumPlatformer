using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameplayCanvas : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject infoPanel;

    // Start is called before the first frame update
    void Start()
    {
        pausePanel.SetActive(false);
        infoPanel.SetActive(true);
        Time.timeScale = 0;
    }

    public void Pause()
    {
        pausePanel.SetActive(true);
        infoPanel.SetActive(false);
        Time.timeScale = 0;
    }

    public void Continue()
    {
        pausePanel.SetActive(false);
        infoPanel.SetActive(false);
        Time.timeScale = 1;
    }

    public void Exit()
    {
        Time.timeScale = 1;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}
