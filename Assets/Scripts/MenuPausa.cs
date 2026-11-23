using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;
using InputDevice = UnityEngine.XR.InputDevice;
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
    private bool prevVRMenuPressed = false;

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
            var rt = pauseMenu.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(850f, 600f);
            }

            var panel = pauseMenu.transform.Find("Panel")?.GetComponent<RectTransform>();
            if (panel != null)
            {
                panel.anchorMin = Vector2.zero;
                panel.anchorMax = Vector2.one;
                panel.offsetMin = Vector2.zero;
                panel.offsetMax = Vector2.zero;
                panel.pivot = new Vector2(0.5f, 0.5f);
            }

            bool isVR = ArtSimulator.XRPlayerRigManager.Instance != null && ArtSimulator.XRPlayerRigManager.Instance.IsVRActive;
            if (!isVR)
            {
                pauseMenu.transform.localScale = Vector3.one;
            }
            else if (pauseMenu.transform.localScale.sqrMagnitude < 0.000001f)
            {
                pauseMenu.transform.localScale = Vector3.one * 0.0016f;
            }

            var canvas = pauseMenu.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = 1000;
            }

            // Raycaster para controles VR (Meta Quest 2 / OpenXR)
            var tdRay = pauseMenu.GetComponent<TrackedDeviceGraphicRaycaster>();
            if (tdRay == null) tdRay = pauseMenu.AddComponent<TrackedDeviceGraphicRaycaster>();
            if (tdRay != null) tdRay.ignoreReversedGraphics = false;

            // Raycaster estándar para clics de ratón en Editor y PC
            var gr = pauseMenu.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (gr != null)
            {
                gr.ignoreReversedGraphics = false;
                gr.blockingObjects = UnityEngine.UI.GraphicRaycaster.BlockingObjects.None;
            }

            // Desactivar raycastTarget en todos los textos y títulos para que nunca bloqueen clics en los botones
            var tp = pauseMenu.transform.Find("TextPausa");
            if (tp != null)
            {
                var tpRt = tp.GetComponent<RectTransform>();
                if (tpRt != null)
                {
                    tpRt.anchorMin = new Vector2(0.5f, 0.5f);
                    tpRt.anchorMax = new Vector2(0.5f, 0.5f);
                    tpRt.pivot = new Vector2(0.5f, 0.5f);
                    tpRt.anchoredPosition = new Vector2(0, 180f);
                    tpRt.sizeDelta = new Vector2(600f, 70f);
                }
                var tpGr = tp.GetComponent<UnityEngine.UI.Graphic>();
                if (tpGr != null) tpGr.raycastTarget = false;
                var tpTmp = tp.GetComponent<TMPro.TMP_Text>();
                if (tpTmp != null) tpTmp.raycastTarget = false;
            }

            var resumeBtn = pauseMenu.transform.Find("ResumeButton")?.GetComponent<UnityEngine.UI.Button>();
            if (resumeBtn != null)
            {
                resumeBtn.interactable = true;
                var rRt = resumeBtn.GetComponent<RectTransform>();
                if (rRt != null)
                {
                    rRt.anchorMin = new Vector2(0.5f, 0.5f);
                    rRt.anchorMax = new Vector2(0.5f, 0.5f);
                    rRt.pivot = new Vector2(0.5f, 0.5f);
                    rRt.anchoredPosition = new Vector2(0, 70f);
                    rRt.sizeDelta = new Vector2(360f, 60f);
                }
                var img = resumeBtn.GetComponent<UnityEngine.UI.Image>();
                if (img != null) img.raycastTarget = true;
                var tmp = resumeBtn.GetComponentInChildren<TMPro.TMP_Text>();
                if (tmp != null) tmp.raycastTarget = false;

                resumeBtn.onClick.RemoveListener(Resume);
                resumeBtn.onClick.AddListener(Resume);
                var trig = resumeBtn.GetComponent<ArtSimulator.TabletButtonTrigger>() ?? resumeBtn.gameObject.AddComponent<ArtSimulator.TabletButtonTrigger>();
                trig.SetAction("Resumir", Resume);
            }

            var homeBtn = pauseMenu.transform.Find("HomeButton")?.GetComponent<UnityEngine.UI.Button>();
            if (homeBtn != null)
            {
                homeBtn.interactable = true;
                var hRt = homeBtn.GetComponent<RectTransform>();
                if (hRt != null)
                {
                    hRt.anchorMin = new Vector2(0.5f, 0.5f);
                    hRt.anchorMax = new Vector2(0.5f, 0.5f);
                    hRt.pivot = new Vector2(0.5f, 0.5f);
                    hRt.anchoredPosition = new Vector2(0, -90f);
                    hRt.sizeDelta = new Vector2(360f, 60f);
                }
                var img = homeBtn.GetComponent<UnityEngine.UI.Image>();
                if (img != null) img.raycastTarget = true;
                var tmp = homeBtn.GetComponentInChildren<TMPro.TMP_Text>();
                if (tmp != null) tmp.raycastTarget = false;

                homeBtn.onClick.RemoveListener(Home);
                homeBtn.onClick.AddListener(Home);
                var trig = homeBtn.GetComponent<ArtSimulator.TabletButtonTrigger>() ?? homeBtn.gameObject.AddComponent<ArtSimulator.TabletButtonTrigger>();
                trig.SetAction("Salir", Home);
            }

            // Botón de alternar Realidad Virtual / Escritorio
            var vrBtnTransform = pauseMenu.transform.Find("VRButton");
            if (vrBtnTransform == null && resumeBtn != null)
            {
                GameObject newVrBtn = Instantiate(resumeBtn.gameObject, pauseMenu.transform);
                newVrBtn.name = "VRButton";
                vrBtnTransform = newVrBtn.transform;
            }

            if (vrBtnTransform != null)
            {
                var vrBtn = vrBtnTransform.GetComponent<UnityEngine.UI.Button>();
                var vrRt = vrBtnTransform.GetComponent<RectTransform>();
                if (vrRt != null)
                {
                    vrRt.anchorMin = new Vector2(0.5f, 0.5f);
                    vrRt.anchorMax = new Vector2(0.5f, 0.5f);
                    vrRt.pivot = new Vector2(0.5f, 0.5f);
                    vrRt.anchoredPosition = new Vector2(0, -10f);
                    vrRt.sizeDelta = new Vector2(360f, 60f);
                }

                if (vrBtn != null)
                {
                    vrBtn.interactable = true;
                    var img = vrBtn.GetComponent<UnityEngine.UI.Image>();
                    if (img != null) img.raycastTarget = true;

                    UpdateVRButtonLabel(vrBtnTransform);

                    vrBtn.onClick.RemoveListener(OnVRButtonClicked);
                    vrBtn.onClick.AddListener(OnVRButtonClicked);
                    var trig = vrBtn.GetComponent<ArtSimulator.TabletButtonTrigger>() ?? vrBtn.gameObject.AddComponent<ArtSimulator.TabletButtonTrigger>();
                    trig.SetAction("AlternarVR", OnVRButtonClicked);
                }
            }
        }
    }

    public void OnVRButtonClicked()
    {
        if (ArtSimulator.XRPlayerRigManager.Instance != null)
        {
            ArtSimulator.XRPlayerRigManager.Instance.ToggleVRMode();
        }
        var vrBtnTransform = pauseMenu != null ? pauseMenu.transform.Find("VRButton") : null;
        if (vrBtnTransform != null)
        {
            UpdateVRButtonLabel(vrBtnTransform);
        }
        if (isPaused)
        {
            Pause();
        }
    }

    private void UpdateVRButtonLabel(Transform vrBtnTransform)
    {
        var tmp = vrBtnTransform.GetComponentInChildren<TMPro.TMP_Text>();
        if (tmp != null)
        {
            tmp.raycastTarget = false;
            bool isVR = ArtSimulator.XRPlayerRigManager.Instance != null && ArtSimulator.XRPlayerRigManager.Instance.IsVRActive;
            tmp.text = isVR ? "Modo: VR Activado (F12)" : "Modo: PC / Teclado (F12)";
        }
    }

    public void Update()
    {
        bool escapePressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame) && Application.isFocused)
        {
            escapePressed = true;
        }

        // Detectar botón de menú en controladores XR reconocidos por Input System
        var xrControllers = InputSystem.devices;
        for (int i = 0; i < xrControllers.Count; i++)
        {
            var menuCtrl = xrControllers[i].TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("menuButton")
                        ?? xrControllers[i].TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("menu")
                        ?? xrControllers[i].TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("appMenu")
                        ?? xrControllers[i].TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("start");
            if (menuCtrl != null && menuCtrl.wasPressedThisFrame)
            {
                escapePressed = true;
                break;
            }
        }
#else
        if ((Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) && Application.isFocused)
        {
            escapePressed = true;
        }
#endif

        // Detección directa en mandos VR (Oculus Touch / Meta Quest 2 botón Menu en mano izquierda o derecha)
        bool vrMenuCurrent = false;
        var leftHandDevice = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
        if (leftHandDevice.isValid && leftHandDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool lMenuVal) && lMenuVal)
        {
            vrMenuCurrent = true;
        }
        var rightHandDevice = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
        if (rightHandDevice.isValid && rightHandDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool rMenuVal) && rMenuVal)
        {
            vrMenuCurrent = true;
        }
        if (vrMenuCurrent && !prevVRMenuPressed)
        {
            escapePressed = true;
        }
        prevVRMenuPressed = vrMenuCurrent;

        if (isPaused)
        {
            // Detección directa de clic de ratón (Editor / PC) sobre los botones de pausa
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
                var canvas = pauseMenu.GetComponent<Canvas>();
                Camera eventCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? (canvas.worldCamera ?? Camera.main) : null;

                var rBtn = pauseMenu.transform.Find("ResumeButton")?.GetComponent<RectTransform>();
                if (rBtn != null && RectTransformUtility.RectangleContainsScreenPoint(rBtn, mousePos, eventCam))
                {
                    Debug.Log("<color=green>[MenuPausa]</color> Clic directo detectado en Resumir");
                    Resume();
                    return;
                }

                var vrBtn = pauseMenu.transform.Find("VRButton")?.GetComponent<RectTransform>();
                if (vrBtn != null && RectTransformUtility.RectangleContainsScreenPoint(vrBtn, mousePos, eventCam))
                {
                    Debug.Log("<color=green>[MenuPausa]</color> Clic directo detectado en Alternar VR");
                    OnVRButtonClicked();
                    return;
                }

                var hBtn = pauseMenu.transform.Find("HomeButton")?.GetComponent<RectTransform>();
                if (hBtn != null && RectTransformUtility.RectangleContainsScreenPoint(hBtn, mousePos, eventCam))
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
                bool isVR = ArtSimulator.XRPlayerRigManager.Instance != null && ArtSimulator.XRPlayerRigManager.Instance.IsVRActive;
                if (!isVR)
                {
                    // Si la tableta o el modo dibujo están activos en PC clásico, ellos consumen el primer Escape para cerrarse
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
            bool isVR = ArtSimulator.XRPlayerRigManager.Instance != null && ArtSimulator.XRPlayerRigManager.Instance.IsVRActive;
            var canvas = pauseMenu.GetComponent<Canvas>();

            if (isVR)
            {
                // En Realidad Virtual, colocar el panel flotando ergonómicamente frente a la vista del jugador
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    Vector3 headPos = mainCam.transform.position;
                    Vector3 forward = mainCam.transform.forward;
                    forward.y = 0; // mantener nivelado con la vista horizontal
                    if (forward.sqrMagnitude < 0.01f) forward = mainCam.transform.forward;
                    forward.Normalize();

                    // Centrado exactamente a la altura de los ojos frente al jugador (1.15m de distancia)
                    pauseMenu.transform.position = headPos + forward * 1.15f - Vector3.up * 0.05f;
                    pauseMenu.transform.rotation = Quaternion.LookRotation(forward);
                }

                if (canvas != null)
                {
                    canvas.renderMode = RenderMode.WorldSpace;
                    canvas.worldCamera = mainCam;
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = 1000;
                }

                pauseMenu.transform.localScale = Vector3.one * 0.0016f;
                var rt = pauseMenu.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(850, 600);
                }

                var tdRay = pauseMenu.GetComponent<TrackedDeviceGraphicRaycaster>();
                if (tdRay == null) tdRay = pauseMenu.AddComponent<TrackedDeviceGraphicRaycaster>();
                if (tdRay != null) tdRay.ignoreReversedGraphics = false;

                var gr = pauseMenu.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                if (gr != null)
                {
                    gr.ignoreReversedGraphics = false;
                    gr.blockingObjects = UnityEngine.UI.GraphicRaycaster.BlockingObjects.None;
                }
            }
            else
            {
                if (canvas != null)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = 1000;
                }
                pauseMenu.transform.localScale = Vector3.one;
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

        bool isVR = ArtSimulator.XRPlayerRigManager.Instance != null && ArtSimulator.XRPlayerRigManager.Instance.IsVRActive;
        if (!isVR)
        {
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
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void Home()
    {
        isPaused = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (Application.CanStreamedLevelBeLoaded("MenuInicio"))
        {
            SceneManager.LoadScene("MenuInicio");
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
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
