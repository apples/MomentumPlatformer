using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class MainMenuSystem : MonoBehaviour
{
    [SerializeField] private SOUP.FloatValue enableTimerFlag;
    [SerializeField] private Camera camera;
    public SOUP.FloatValue currentGamemode;
    public SOUP.FloatValue currentLevel;
    public GameObject trophyParent;

    public List<GameObject> forwardButtons;

    private UnityEngine.UI.Button button;

    private Vector3 velocity = Vector3.zero;
    private Quaternion qVelocity = Quaternion.identity;
    private Vector3 targetPos = new Vector3(2.9000001f, 1.17058289f, -0.389999986f);
    private Quaternion targetRot = new Quaternion(0.0126448469f, -0.47822839f, 0.00688646408f, 0.878117502f);

    private Vector3 buttonVelocity = Vector3.zero;
    private Vector3 buttonScaleVelocity = Vector3.zero;

    public void Awake()
    {
        AudioSource[] sources = GameObject.FindObjectsOfType<AudioSource>();
        if(sources.Length > 2)
        {
            Destroy(sources[2]);
            Destroy(sources[3]);
        }
        else
        {
            DontDestroyOnLoad(GameObject.Find("IntroMusic"));
            DontDestroyOnLoad(GameObject.Find("Music"));
        }

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
        }

        List<GameObject> trophies = trophyParent.transform.GetComponentsInChildren<Transform>(true).Select(x => x.gameObject).ToList();

        int temp = 1;
        int level = 0;
        foreach (List<bool> item in saveData)
        {
            int totalCleared = 0;
            foreach (bool isCleared in item)
            {
                trophies[temp].SetActive(isCleared);
                temp++;
                if (isCleared)
                {
                    totalCleared++;
                }
            }
            if (totalCleared >= 3)
            {
                forwardButtons[level].SetActive(true);
                if (totalCleared == 3)
                {
                    forwardButtons[level].transform.GetChild(1).gameObject.SetActive(true);
                }
                else
                {
                    forwardButtons[level].transform.GetChild(1).gameObject.SetActive(false);
                }
            }
            else
            {
                forwardButtons[level].SetActive(false);
            }
            level++;
        }
        

        switch (currentLevel.Value)
        {
            case (int)Globals.Levels.Snow:
                SeeFirstLevel();
                break;
            case (int)Globals.Levels.JamVersion:
                SeeSecondLevel();
                break;
            case (int)Globals.Levels.Endless:
                SeeThirdLevel();
                break;
            default:
                break;
        }
    }

    public void Update()
    {
        camera.transform.SetPositionAndRotation(
            Vector3.SmoothDamp(camera.transform.position, targetPos, ref velocity, .5f),
            SmoothDampQ(camera.transform.rotation, targetRot, ref qVelocity, .5f)
            );

        if (button)
        {
            button.transform.localScale = Vector3.SmoothDamp(button.transform.localScale, new Vector3(10, 4, 1), ref buttonScaleVelocity, 0.2f);
            button.transform.localPosition = Vector3.SmoothDamp(button.transform.localPosition, new Vector3(-80, -54, 0), ref buttonVelocity, 0.2f);
        }
    }

    //depreciated
    public void SeeLevels()
    {
        targetPos = new Vector3(8.31f, 1.073f, 4.36f);
        targetRot = new Quaternion(0.0f, 0.0f, 0.0f, 1.0f);
    }

    public void SeeMain()
    {
        targetPos = new Vector3(2.9000001f, 1.17058289f, -0.389999986f);
        targetRot = new Quaternion(0.0126448469f, -0.47822839f, 0.00688646408f, 0.878117502f);
    }

    public void SeeFirstLevel()
    {
        targetPos = new Vector3(184.5f, 198.300003f, 157.899994f);
        targetRot = new Quaternion(0.00837235432f, 0.282001048f, 0.0024609929f, 0.959374487f);
    }

    public void SeeSecondLevel()
    {
        targetPos = new Vector3(270f, 160f, 145f);
        targetRot = new Quaternion(0.0058444147f, -0.263087988f, -0.00648040092f, 0.964732468f);
    }

    public void SeeThirdLevel()
    {
        targetPos = new Vector3(215f, 323f, 187f);
        targetRot = new Quaternion(0.136023045f, 0.117969982f, -0.019201776f, 0.983469367f);
    }

    public void SeeFourthLevel()
    {
        targetPos = new Vector3(268f, 247f, 161f);
        targetRot = new Quaternion(0.0814888924f, -0.240383789f, 0.0134906787f, 0.967157304f);
    }

    public void SeeHelp()
    {
        targetPos = new Vector3(1.62f, 1.23000002f, -6.25f);
        targetRot = new Quaternion(0.0143677797f, -0.873593032f, -0.000940123573f, 0.486444116f);
    }

    //depreciated
    public void Expand(UnityEngine.UI.Button self)
    {
        button = self;
        GameObject.FindGameObjectsWithTag("LevelButton").Where(x => x.name != button.name).ToList().ForEach(x => x.SetActive(false));
    }


    public void PlayGameNoTimer()
    {
        enableTimerFlag.Value = 0;
        UnityEngine.SceneManagement.SceneManager.LoadScene("GameplayScene");
    }

    public void WinterSun()
    {
        currentGamemode.Value = (float)Globals.Gamemodes.SunSurival;
        currentLevel.Value = (float)Globals.Levels.Snow;
        UnityEngine.SceneManagement.SceneManager.LoadScene("SnowMap2");
    }

    public void WinterScore()
    {
        currentGamemode.Value = (float)Globals.Gamemodes.Score;
        currentLevel.Value = (float)Globals.Levels.Snow;
        UnityEngine.SceneManagement.SceneManager.LoadScene("SnowMap2");
    }

    public void WinterAvalanche()
    {
        currentGamemode.Value = (float)Globals.Gamemodes.Avalanche;
        currentLevel.Value = (float)Globals.Levels.Snow;
        UnityEngine.SceneManagement.SceneManager.LoadScene("SnowMap2");
    }

    public void WinterFull()
    {
        currentGamemode.Value = (float)Globals.Gamemodes.All;
        currentLevel.Value = (float)Globals.Levels.Snow;
        UnityEngine.SceneManagement.SceneManager.LoadScene("SnowMap2");
    }

    public void WinterZen()
    {
        currentGamemode.Value = (float)Globals.Gamemodes.None;
        currentLevel.Value = (float)Globals.Levels.Snow;
        UnityEngine.SceneManagement.SceneManager.LoadScene("SnowMap2");
    }

    public void JamSun()
    {
        currentGamemode.Value = (float)Globals.Gamemodes.SunSurival;
        currentLevel.Value = (float)Globals.Levels.JamVersion;
        UnityEngine.SceneManagement.SceneManager.LoadScene("GameplayScene");
    }

    public void JamScore()
    {
        currentGamemode.Value = (float)Globals.Gamemodes.Score;
        currentLevel.Value = (float)Globals.Levels.JamVersion;
        UnityEngine.SceneManagement.SceneManager.LoadScene("GameplayScene");
    }

    public void JamAvalanche()
    {
        currentGamemode.Value = (float)Globals.Gamemodes.Avalanche;
        currentLevel.Value = (float)Globals.Levels.JamVersion;
        UnityEngine.SceneManagement.SceneManager.LoadScene("GameplayScene");
    }

    public void JamFull()
    {
        currentGamemode.Value = (float)Globals.Gamemodes.All;
        currentLevel.Value = (float)Globals.Levels.JamVersion;
        UnityEngine.SceneManagement.SceneManager.LoadScene("GameplayScene");
    }

    public void JamZen()
    {
        currentGamemode.Value = (float)Globals.Gamemodes.None;
        currentLevel.Value = (float)Globals.Levels.JamVersion;
        UnityEngine.SceneManagement.SceneManager.LoadScene("GameplayScene");
    }

    public static Quaternion SmoothDampQ(Quaternion rot, Quaternion target, ref Quaternion deriv, float time)
    {
        if (Time.deltaTime < Mathf.Epsilon) return rot;
        // account for double-cover
        var Dot = Quaternion.Dot(rot, target);
        var Multi = Dot > 0f ? 1f : -1f;
        target.x *= Multi;
        target.y *= Multi;
        target.z *= Multi;
        target.w *= Multi;
        // smooth damp (nlerp approx)
        var Result = new Vector4(
            Mathf.SmoothDamp(rot.x, target.x, ref deriv.x, time),
            Mathf.SmoothDamp(rot.y, target.y, ref deriv.y, time),
            Mathf.SmoothDamp(rot.z, target.z, ref deriv.z, time),
            Mathf.SmoothDamp(rot.w, target.w, ref deriv.w, time)
        ).normalized;

        // ensure deriv is tangent
        var derivError = Vector4.Project(new Vector4(deriv.x, deriv.y, deriv.z, deriv.w), Result);
        deriv.x -= derivError.x;
        deriv.y -= derivError.y;
        deriv.z -= derivError.z;
        deriv.w -= derivError.w;

        return new Quaternion(Result.x, Result.y, Result.z, Result.w);
    }
}
