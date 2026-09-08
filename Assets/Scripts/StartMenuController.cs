using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMenuController : MonoBehaviour
{
    public void OnStartClick()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene("Nivel");
    }

    public void OnQuitClick()
    {
        Time.timeScale = 1;
        Application.Quit();
    }

    public void OnHomeClick()
    {
        SceneManager.LoadScene("MenuInicio");
        Time.timeScale = 1;
    }
}
