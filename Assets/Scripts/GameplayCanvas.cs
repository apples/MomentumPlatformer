using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameplayCanvas : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button continueButton;
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
        continueButton.Select();
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
