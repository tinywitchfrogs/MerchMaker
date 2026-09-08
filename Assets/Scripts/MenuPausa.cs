using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class MenuPausa : MonoBehaviour
{
    [SerializeField] GameObject pauseMenu;
    public bool isPaused;

     private void Start()
    {
        isPaused = false;

    if (pauseMenu != null)
        {
            pauseMenu.SetActive(false);
        }
    }

    public void Update()
    {  
         isPaused = false;

         if  (Keyboard.current[Key.P].wasPressedThisFrame && Application.isFocused)
        {
        isPaused = true;
        }

        if(isPaused == true)
        {
            pauseMenu.SetActive(true);
            Time.timeScale = 0;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        
    }

    public void Resume()
    {
        isPaused = false;
        pauseMenu.SetActive(false);
        Time.timeScale = 1;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
