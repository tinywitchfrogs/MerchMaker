using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class StartMenuController : MonoBehaviour
{
    private RectTransform playButtonRect;
    private RectTransform quitButtonRect;

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name != "MenuInicio") return;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetupButtons();
    }

    private void Start()
    {
        if (SceneManager.GetActiveScene().name != "MenuInicio") return;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetupButtons();
    }

    private void SetupButtons()
    {
        // 1. PlayButton
        var playBtn = transform.Find("PlayButton")?.GetComponent<UnityEngine.UI.Button>();
        if (playBtn != null)
        {
            playButtonRect = playBtn.GetComponent<RectTransform>();
            playBtn.interactable = true;
            var img = playBtn.GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.raycastTarget = true;
            var tmp = playBtn.GetComponentInChildren<TMPro.TMP_Text>();
            if (tmp != null) tmp.raycastTarget = false;

            playBtn.onClick.RemoveListener(OnStartClick);
            playBtn.onClick.AddListener(OnStartClick);

            var trig = playBtn.GetComponent<ArtSimulator.TabletButtonTrigger>() ?? playBtn.gameObject.AddComponent<ArtSimulator.TabletButtonTrigger>();
            trig.SetAction("Iniciar", OnStartClick);
        }

        // 2. QuitButton
        var quitBtn = transform.Find("QuitButton")?.GetComponent<UnityEngine.UI.Button>();
        if (quitBtn != null)
        {
            quitButtonRect = quitBtn.GetComponent<RectTransform>();
            quitBtn.interactable = true;
            var img = quitBtn.GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.raycastTarget = true;
            var tmp = quitBtn.GetComponentInChildren<TMPro.TMP_Text>();
            if (tmp != null) tmp.raycastTarget = false;

            quitBtn.onClick.RemoveListener(OnQuitClick);
            quitBtn.onClick.AddListener(OnQuitClick);

            var trig = quitBtn.GetComponent<ArtSimulator.TabletButtonTrigger>() ?? quitBtn.gameObject.AddComponent<ArtSimulator.TabletButtonTrigger>();
            trig.SetAction("Salir", OnQuitClick);
        }

        // 3. Titulo
        var titleTmp = transform.Find("Titulo")?.GetComponent<TMPro.TMP_Text>();
        if (titleTmp != null) titleTmp.raycastTarget = false;
    }

    private void Update()
    {
        if (SceneManager.GetActiveScene().name != "MenuInicio") return;

        // Soporte teclado: Enter para Iniciar, Escape para Salir
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                OnStartClick();
                return;
            }
            if (kb.escapeKey.wasPressedThisFrame)
            {
                OnQuitClick();
                return;
            }
        }
#else
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            OnStartClick();
            return;
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnQuitClick();
            return;
        }
#endif

        // Respaldo infalible de clic de ratón por coordenadas de pantalla
        bool clickPressed = false;
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) clickPressed = true;
#else
        if (Input.GetMouseButtonDown(0)) clickPressed = true;
#endif
        if (clickPressed)
        {
            Vector2 mousePos = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (mouse != null) mousePos = mouse.position.ReadValue();
#else
            mousePos = Input.mousePosition;
#endif
            if (playButtonRect != null && RectTransformUtility.RectangleContainsScreenPoint(playButtonRect, mousePos, null))
            {
                OnStartClick();
                return;
            }
            if (quitButtonRect != null && RectTransformUtility.RectangleContainsScreenPoint(quitButtonRect, mousePos, null))
            {
                OnQuitClick();
                return;
            }
        }
    }

    public void OnStartClick()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SceneManager.LoadScene("Nivel");
    }

    public void OnQuitClick()
    {
        Time.timeScale = 1f;
        Application.Quit();
    }

    public void OnHomeClick()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("MenuInicio");
    }
}
