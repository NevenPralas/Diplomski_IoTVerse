using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuSceneLoader : MonoBehaviour
{
    public void LoadSceneByName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("Scene name is empty. Provjeri OnClick postavke na buttonu.");
            return;
        }

        Debug.Log("Loading scene: " + sceneName);
        SceneManager.LoadScene(sceneName);
    }

    public void LoadTutorial()
    {
        SceneManager.LoadScene("GridSystem");
    }

    public void LoadPhase1()
    {
        SceneManager.LoadScene("1ST_PHASE");
    }

    public void LoadPhase2()
    {
        SceneManager.LoadScene("2ND_PHASE");
    }

    public void LoadPhase3()
    {
        SceneManager.LoadScene("3RD_PHASE");
    }

    public void LoadPhase4()
    {
        SceneManager.LoadScene("4TH_PHASE");
    }

    public void LoadMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}