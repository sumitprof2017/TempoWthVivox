using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EnableMenu : MonoBehaviour
{
    // Start is called before the first frame update
    public void OnEnable()
    {
        SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);

    }
}
