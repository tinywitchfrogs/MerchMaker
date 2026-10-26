using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MenuPausa : MonoBehaviour
{
    private static MenuPausa instance;
    public static MenuPausa Instance => instance;
    public static bool IsGamePaused => instance != null && instance.isPaused;

    [SerializeField] private GameObject pauseMenu;
    public bool isPaused = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        EnsurePauseMenuReference();
    }

    private void Start()
    {
        isPaused = false;
        Time.timeScale = 1f;
        EnsurePauseMenuReference();

        if (pauseMenu != null)
        {
            pauseMenu.SetActive(false);
        }
    }

    private void EnsurePauseMenuReference()
    {
        if (pauseMenu == null)
        {
            pauseMenu = GameObject.Find("MenuPausa");
            if (pauseMenu == null)
            {
                var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
                foreach (var c in canvases)
                {
                    if (c.gameObject.name == "MenuPausa" && c.gameObject.scene.name == "Nivel")
                    {
                        pauseMenu = c.gameObject;
                        break;
                    }
                }
            }
        }

        if (pauseMenu != null)
        {
            pauseMenu.transform.localScale = Vector3.one;
            var canvas = pauseMenu.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = 1000;
            }

            // Desactivar raycastTarget en todos los textos y títulos para que nunca bloqueen clics en los botones
            var tp = pauseMenu.transform.Find("TextPausa");
            if (tp != null)
            {
                var tpGr = tp.GetComponent<UnityEngine.UI.Graphic>();
                if (tpGr != null) tpGr.raycastTarget = false;
                var tpTmp = tp.GetComponent<TMPro.TMP_Text>();
                if (tpTmp != null) tpTmp.raycastTarget = false;
            }

            var resumeBtn = pauseMenu.transform.Find("ResumeButton")?.GetComponent<UnityEngine.UI.Button>();
            if (resumeBtn != null)
            {
                resumeBtn.interactable = true;
                var img = resumeBtn.GetComponent<UnityEngine.UI.Image>();
                if (img != null) img.raycastTarget = true;
                var tmp = resumeBtn.GetComponentInChildren<TMPro.TMP_Text>();
                if (tmp != null) tmp.raycastTarget = false;

                resumeBtn.onClick.RemoveListener(Resume);
                resumeBtn.onClick.AddListener(Resume);
                var trig = resumeBtn.GetComponent<ArtSimulator.TabletButtonTrigger>() ?? resumeBtn.gameObject.AddComponent<ArtSimulator.TabletButtonTrigger>();
                trig.SetAction("Resumir", Resume);
                resumeBtn.transform.SetAsLastSibling();
            }

            var homeBtn = pauseMenu.transform.Find("HomeButton")?.GetComponent<UnityEngine.UI.Button>();
            if (homeBtn != null)
            {
                homeBtn.interactable = true;
                var img = homeBtn.GetComponent<UnityEngine.UI.Image>();
                if (img != null) img.raycastTarget = true;
                var tmp = homeBtn.GetComponentInChildren<TMPro.TMP_Text>();
                if (tmp != null) tmp.raycastTarget = false;

                homeBtn.onClick.RemoveListener(Home);
                homeBtn.onClick.AddListener(Home);
                var trig = homeBtn.GetComponent<ArtSimulator.TabletButtonTrigger>() ?? homeBtn.gameObject.AddComponent<ArtSimulator.TabletButtonTrigger>();
                trig.SetAction("Salir", Home);
            }
        }
    }

    public void Update()
    {
        bool escapePressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && Application.isFocused)
        {
            escapePressed = true;
        }
#else
        if (Input.GetKeyDown(KeyCode.Escape) && Application.isFocused)
        {
            escapePressed = true;
        }
#endif

        if (isPaused)
        {
            // Detección directa y a prueba de fallos de clic de ratón sobre los botones de pausa
            bool clickPressed = false;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) clickPressed = true;
#else
            if (Input.GetMouseButtonDown(0)) clickPressed = true;
#endif
            if (clickPressed && pauseMenu != null && pauseMenu.activeInHierarchy)
            {
                Vector2 mousePos = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
                if (mouse != null) mousePos = mouse.position.ReadValue();
#else
                mousePos = Input.mousePosition;
#endif
                var rBtn = pauseMenu.transform.Find("ResumeButton")?.GetComponent<RectTransform>();
                if (rBtn != null && RectTransformUtility.RectangleContainsScreenPoint(rBtn, mousePos, null))
                {
                    Debug.Log("<color=green>[MenuPausa]</color> Clic directo detectado en Resumir");
                    Resume();
                    return;
                }

                var hBtn = pauseMenu.transform.Find("HomeButton")?.GetComponent<RectTransform>();
                if (hBtn != null && RectTransformUtility.RectangleContainsScreenPoint(hBtn, mousePos, null))
                {
                    Debug.Log("<color=green>[MenuPausa]</color> Clic directo detectado en Salir");
                    Home();
                    return;
                }
            }
        }

        if (escapePressed)
        {
            if (isPaused)
            {
                Resume();
            }
            else
            {
                // Si la tableta o el modo dibujo están activos, ellos consumen el primer Escape para cerrarse
                var tm = ArtSimulator.TabletManager.Instance;
                if (tm != null && tm.IsTabletOpen)
                {
                    return;
                }

                var player = ArtSimulator.FirstPersonPlayerController.Instance;
                if (player != null && player.IsDrawingMode)
                {
                    return;
                }

                Pause();
            }
        }
    }

    public void Pause()
    {
        isPaused = true;
        EnsurePauseMenuReference();
        if (pauseMenu != null)
        {
            pauseMenu.transform.localScale = Vector3.one;
            var canvas = pauseMenu.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = 1000;
            }
            pauseMenu.SetActive(true);
        }
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (ArtSimulator.FirstPersonPlayerController.Instance != null)
        {
            ArtSimulator.FirstPersonPlayerController.Instance.SetCursorLock(false);
        }
    }

    public void Resume()
    {
        isPaused = false;
        if (pauseMenu != null)
        {
            pauseMenu.SetActive(false);
        }
        Time.timeScale = 1f;

        if (ArtSimulator.FirstPersonPlayerController.Instance != null)
        {
            ArtSimulator.FirstPersonPlayerController.Instance.SetCursorLock(true);
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void Home()
    {
        isPaused = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("MenuInicio");
    }

    public void OnHomeClick()
    {
        Home();
    }

    public void TogglePause()
    {
        if (isPaused) Resume();
        else Pause();
    }
}
