using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GotoScene : MonoBehaviour
{
    [SerializeField] private string sceneName;
    public SOUP.FloatValue currentLevel;

    public void Goto()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }

    public void GoBack()
    {
        switch (currentLevel.Value)
        {
            case (float)Globals.Levels.Snow:
                UnityEngine.SceneManagement.SceneManager.LoadScene("SnowMap2");
                break;
            case (float)Globals.Levels.JamVersion:
                UnityEngine.SceneManagement.SceneManager.LoadScene("GameplayScene");
                break;
            case (float)Globals.Levels.Endless:
                UnityEngine.SceneManagement.SceneManager.LoadScene("InfiniteTerrainTest");
                break;
            default:
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
                break;
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }
}
