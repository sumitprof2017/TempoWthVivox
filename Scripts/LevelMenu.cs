using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelMenu : MonoBehaviour
{
    public GameObject loadingScreen;
    public GameObject levelMenu;
    public Slider loadingSlider;
    
    // Start is called before the first frame update
    public void OpenLevel(int levelId)
    {
        levelMenu.SetActive(false);
        loadingScreen.SetActive(true);
        StartCoroutine(LoadSceneAsync(levelId));
        //string levelName = "Level" + levelId;
        //SceneManager.LoadScene(levelId);
    }

    IEnumerator LoadSceneAsync(int levelId)
    {
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(levelId);
        

        //loadingScreen.SetActive(true);

        while (!loadOperation.isDone)
        {
            float progressValue = Mathf.Clamp01(loadOperation.progress / 0.9f);
            loadingSlider.value = progressValue;

            yield return null;
        }
    
    }
}
