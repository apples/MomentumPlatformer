using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GotoScene : MonoBehaviour
{
    [SerializeField] private string sceneName;

    public void Goto()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }
}
