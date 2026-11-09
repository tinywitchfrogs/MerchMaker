using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ArtSimulator
{
    public class TabletCameraApp : MonoBehaviour
    {
        [Header("Referencias de Cámara y Render")]
        [SerializeField] private Camera tabletCamera;
        [SerializeField] private RawImage viewfinderDisplay;
        [SerializeField] private Image flashOverlay;

        [Header("Botones de Interfaz")]
        [SerializeField] private Button shutterButton;
        [SerializeField] private Button homeButton;

        [Header("Zoom de Cámara")]
        [SerializeField] private float minFOV = 15f;   // ~4.3x zoom
        [SerializeField] private float maxFOV = 70f;   // ~0.9x gran angular
        [SerializeField] private float defaultFOV = 65f;
        [SerializeField] private float currentTargetFOV = 65f;
        [SerializeField] private float zoomLerpSpeed = 12f;

        [Header("Referencias de Zoom UI")]
        [SerializeField] private Text zoomIndicatorText;
        [SerializeField] private Button zoomInButton;
        [SerializeField] private Button zoomOutButton;

        public float CurrentZoomFactor => defaultFOV / (tabletCamera != null ? tabletCamera.fieldOfView : defaultFOV);

        private RenderTexture cameraRenderTexture;
        private AudioSource audioSource;
        private bool prevVRShotPressed = false;
        private bool isCapturing = false;
        private float lastPhotoTime = -10f;
        private const float PHOTO_COOLDOWN = 0.8f;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            EnsureReferences();
            SetupCameraAndTexture();
            WireButtons();
        }

        private void Start()
        {
            EnsureReferences();
            WireButtons();
        }

        public void EnsureReferences()
        {
            if (viewfinderDisplay == null)
            {
                viewfinderDisplay = transform.Find("Viewfinder")?.GetComponent<RawImage>();
            }
            if (flashOverlay == null)
            {
                flashOverlay = transform.Find("Viewfinder/FlashOverlay")?.GetComponent<Image>();
            }
            if (homeButton == null)
            {
                homeButton = transform.Find("CameraControls/HomeButton")?.GetComponent<Button>();
            }
            if (shutterButton == null)
            {
                shutterButton = transform.Find("CameraControls/ShutterButton")?.GetComponent<Button>();
            }
            if (zoomInButton == null)
            {
                zoomInButton = transform.Find("CameraControls/ZoomControls/ZoomInBtn")?.GetComponent<Button>();
            }
            if (zoomOutButton == null)
            {
                zoomOutButton = transform.Find("CameraControls/ZoomControls/ZoomOutBtn")?.GetComponent<Button>();
            }
            if (zoomIndicatorText == null)
            {
                zoomIndicatorText = transform.Find("CameraControls/ZoomControls/ZoomText/ZoomValue")?.GetComponent<Text>();
            }
        }

        public void WireButtons()
        {
            if (shutterButton != null)
            {
                shutterButton.onClick.RemoveAllListeners();
                shutterButton.onClick.AddListener(TakePhoto);
                var trig = shutterButton.GetComponent<TabletButtonTrigger>() ?? shutterButton.gameObject.AddComponent<TabletButtonTrigger>();
                trig.SetAction("Disparar Foto", TakePhoto);
            }

            if (homeButton != null)
            {
                homeButton.onClick.RemoveAllListeners();
                homeButton.onClick.AddListener(() => TabletManager.Instance?.ShowHome());
                var trig = homeButton.GetComponent<TabletButtonTrigger>() ?? homeButton.gameObject.AddComponent<TabletButtonTrigger>();
                trig.SetAction("Inicio", () => TabletManager.Instance?.ShowHome());
            }

            if (zoomInButton != null)
            {
                zoomInButton.onClick.RemoveAllListeners();
                zoomInButton.onClick.AddListener(ZoomIn);
                var trig = zoomInButton.GetComponent<TabletButtonTrigger>() ?? zoomInButton.gameObject.AddComponent<TabletButtonTrigger>();
                trig.SetAction("Zoom +", ZoomIn);
            }

            if (zoomOutButton != null)
            {
                zoomOutButton.onClick.RemoveAllListeners();
                zoomOutButton.onClick.AddListener(ZoomOut);
                var trig = zoomOutButton.GetComponent<TabletButtonTrigger>() ?? zoomOutButton.gameObject.AddComponent<TabletButtonTrigger>();
                trig.SetAction("Zoom -", ZoomOut);
            }
        }

        private void SetupCameraAndTexture()
        {
            if (cameraRenderTexture == null)
            {
                // Resolución optimizada (640x480): instantánea sin congelar el juego (4x más rápida)
                cameraRenderTexture = new RenderTexture(640, 480, 16, RenderTextureFormat.ARGB32);
                cameraRenderTexture.name = "Tablet_Camera_RT";
            }

            if (tabletCamera == null)
            {
                GameObject camObj = GameObject.Find("TabletViewCamera");
                if (camObj == null)
                {
                    camObj = new GameObject("TabletViewCamera");
                }
                tabletCamera = camObj.GetComponent<Camera>();
                if (tabletCamera == null)
                {
                    tabletCamera = camObj.AddComponent<Camera>();
                }
                tabletCamera.clearFlags = CameraClearFlags.Skybox;
                tabletCamera.fieldOfView = defaultFOV;
                tabletCamera.nearClipPlane = 0.03f;
                tabletCamera.cullingMask = ~0; // Renderizar todo excepto UI
                int uiLayer = LayerMask.NameToLayer("UI");
                if (uiLayer >= 0)
                {
                    tabletCamera.cullingMask &= ~(1 << uiLayer);
                }
            }

            tabletCamera.targetTexture = cameraRenderTexture;

            if (viewfinderDisplay != null)
            {
                viewfinderDisplay.texture = cameraRenderTexture;
                // Escala horizontal invertida (-1) para contrarrestar la escala negativa del lienzo padre
                viewfinderDisplay.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            }
        }

        private void LateUpdate()
        {
            if (tabletCamera == null) return;

            // Sincronizar posición y orientación en tiempo real con el lente trasero de la tableta física
            GameObject tableta = GameObject.Find("Tableta");
            Transform pantalla = tableta != null ? (tableta.transform.Find("ModeloTableta/Pantalla") ?? tableta.transform.Find("Pantalla")) : null;
            Transform camLens = tableta != null ? (tableta.transform.Find("ModeloTableta/Camara") ?? tableta.transform.Find("Camara")) : null;

            if (tableta != null && pantalla != null)
            {
                // La cámara física se ubica en el lente trasero y apunta hacia donde apunta la parte posterior de la tableta
                Vector3 lensPos = camLens != null ? camLens.position : (pantalla.position - pantalla.forward * 0.05f);
                Vector3 cameraForward = -pantalla.forward; // Dirección hacia donde apunta la parte trasera de la tableta
                Vector3 cameraUp = -pantalla.up; // Orientado con el borde superior de la tableta

                tabletCamera.transform.position = lensPos;
                tabletCamera.transform.rotation = Quaternion.LookRotation(cameraForward, cameraUp);
            }
            else
            {
                // Fallback para modo PC clásico sin tableta física en escena
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    tabletCamera.transform.position = mainCam.transform.position;
                    tabletCamera.transform.rotation = mainCam.transform.rotation;
                }
            }
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy) return;

            HandleZoomInput();
            UpdateCameraZoom();

            bool shotTriggered = false;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            {
                shotTriggered = true;
            }
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                bool isOverUI = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
                if (!isOverUI)
                {
                    shotTriggered = true;
                }
            }
#else
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                shotTriggered = true;
            }
            else if (Input.GetMouseButtonDown(0))
            {
                bool isOverUI = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
                if (!isOverUI)
                {
                    shotTriggered = true;
                }
            }
#endif

            // Detección directa en mandos VR (Botón primario A/X para disparar foto rápidamente)
            var rightHand = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
            bool vrBtn = false;
            if (rightHand.isValid && rightHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool aPressed))
            {
                vrBtn = aPressed;
            }
            if (vrBtn && !prevVRShotPressed)
            {
                shotTriggered = true;
            }
            prevVRShotPressed = vrBtn;

            if (shotTriggered)
            {
                TakePhoto();
            }
        }

        private void HandleZoomInput()
        {
            float scrollDelta = 0f;

#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                float my = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(my) > 0.01f)
                {
                    scrollDelta = Mathf.Sign(my) * 120f;
                }
            }
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.eKey.isPressed || kb.numpadPlusKey.isPressed || kb.equalsKey.isPressed) scrollDelta += 120f * Time.deltaTime * 5f;
                if (kb.qKey.isPressed || kb.numpadMinusKey.isPressed || kb.minusKey.isPressed) scrollDelta -= 120f * Time.deltaTime * 5f;
            }
#else
            float mouseScroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(mouseScroll) > 0.001f)
            {
                scrollDelta = Mathf.Sign(mouseScroll) * 120f;
            }
            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.KeypadPlus) || Input.GetKey(KeyCode.Equals)) scrollDelta += 120f * Time.deltaTime * 5f;
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.KeypadMinus) || Input.GetKey(KeyCode.Minus)) scrollDelta -= 120f * Time.deltaTime * 5f;
#endif

            if (Mathf.Abs(scrollDelta) > 0.01f)
            {
                // Rueda hacia arriba (>0) acerca (reduce FOV), rueda hacia abajo (<0) aleja (aumenta FOV)
                float fovStep = (scrollDelta / 120f) * 6f;
                currentTargetFOV = Mathf.Clamp(currentTargetFOV - fovStep, minFOV, maxFOV);
            }
        }

        private void UpdateCameraZoom()
        {
            if (tabletCamera != null)
            {
                tabletCamera.fieldOfView = Mathf.Lerp(tabletCamera.fieldOfView, currentTargetFOV, Time.deltaTime * zoomLerpSpeed);
                float zoom = defaultFOV / tabletCamera.fieldOfView;
                if (zoomIndicatorText != null)
                {
                    zoomIndicatorText.text = $"{zoom:F1}x";
                }
            }
        }

        public void ZoomIn()
        {
            currentTargetFOV = Mathf.Clamp(currentTargetFOV - 8f, minFOV, maxFOV);
        }

        public void ZoomOut()
        {
            currentTargetFOV = Mathf.Clamp(currentTargetFOV + 8f, minFOV, maxFOV);
        }

        public void ResetZoom()
        {
            currentTargetFOV = defaultFOV;
        }

        public void BindUI(RawImage viewfinder, Button shutter, Button home, Image flash, Button zoomIn = null, Button zoomOut = null, Text zoomText = null)
        {
            viewfinderDisplay = viewfinder;
            shutterButton = shutter;
            homeButton = home;
            flashOverlay = flash;
            zoomInButton = zoomIn;
            zoomOutButton = zoomOut;
            zoomIndicatorText = zoomText;

            if (viewfinderDisplay != null)
            {
                if (cameraRenderTexture != null) viewfinderDisplay.texture = cameraRenderTexture;
                viewfinderDisplay.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            }

            if (shutterButton != null)
            {
                shutterButton.onClick.RemoveAllListeners();
                shutterButton.onClick.AddListener(TakePhoto);
                var trig = shutterButton.GetComponent<TabletButtonTrigger>() ?? shutterButton.gameObject.AddComponent<TabletButtonTrigger>();
                trig.SetAction("Disparar Foto", TakePhoto);
            }

            if (homeButton != null)
            {
                homeButton.onClick.RemoveAllListeners();
                homeButton.onClick.AddListener(() => TabletManager.Instance?.ShowHome());
                var trig = homeButton.GetComponent<TabletButtonTrigger>() ?? homeButton.gameObject.AddComponent<TabletButtonTrigger>();
                trig.SetAction("Inicio", () => TabletManager.Instance?.ShowHome());
            }

            if (zoomInButton != null)
            {
                zoomInButton.onClick.RemoveAllListeners();
                zoomInButton.onClick.AddListener(ZoomIn);
                var trig = zoomInButton.GetComponent<TabletButtonTrigger>() ?? zoomInButton.gameObject.AddComponent<TabletButtonTrigger>();
                trig.SetAction("Zoom +", ZoomIn);
            }

            if (zoomOutButton != null)
            {
                zoomOutButton.onClick.RemoveAllListeners();
                zoomOutButton.onClick.AddListener(ZoomOut);
                var trig = zoomOutButton.GetComponent<TabletButtonTrigger>() ?? zoomOutButton.gameObject.AddComponent<TabletButtonTrigger>();
                trig.SetAction("Zoom -", ZoomOut);
            }

            if (zoomIndicatorText != null)
            {
                zoomIndicatorText.text = "1.0x";
            }
        }

        private void OnEnable()
        {
            EnsureReferences();
            WireButtons();

            if (tabletCamera != null)
            {
                tabletCamera.enabled = true;
                tabletCamera.fieldOfView = defaultFOV;
            }
            currentTargetFOV = defaultFOV;
            if (zoomIndicatorText != null)
            {
                zoomIndicatorText.text = "1.0x";
            }

            if (viewfinderDisplay != null)
            {
                viewfinderDisplay.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            }

            // En VR las auras se mantienen visibles en el mundo físico para ubicar las hojas fácilmente
        }

        private void OnDisable()
        {
            if (tabletCamera != null)
                tabletCamera.enabled = false;

            // Restaurar auras al salir del modo cámara
            ScannableDrawing.SetAllAurasVisible(true);
        }

        public void TakePhoto()
        {
            // Debounce y prevención de disparos múltiples simultáneos
            if (isCapturing || Time.time - lastPhotoTime < PHOTO_COOLDOWN) return;
            if (cameraRenderTexture == null || tabletCamera == null) return;
            StartCoroutine(CapturePhotoRoutine());
        }

        private IEnumerator CapturePhotoRoutine()
        {
            isCapturing = true;
            lastPhotoTime = Time.time;

            // Garantizar que las auras no se capturen en la fotografía
            ScannableDrawing.SetAllAurasVisible(false);

            if (flashOverlay != null)
            {
                flashOverlay.gameObject.SetActive(true);
                flashOverlay.color = new Color(1, 1, 1, 0.9f);
            }

            PlayShutterClick();

            yield return null; // Pausa mínima para permitir que la GPU termine el fotograma actual limpiamente

            if (tabletCamera != null && cameraRenderTexture != null)
            {
                tabletCamera.Render();
            }

            // Restaurar inmediatamente la visibilidad de las auras de color para el jugador
            ScannableDrawing.SetAllAurasVisible(true);

            if (cameraRenderTexture != null)
            {
                RenderTexture prevRT = RenderTexture.active;
                RenderTexture.active = cameraRenderTexture;

                Texture2D captured = new Texture2D(cameraRenderTexture.width, cameraRenderTexture.height, TextureFormat.RGBA32, false);
                captured.ReadPixels(new Rect(0, 0, cameraRenderTexture.width, cameraRenderTexture.height), 0, 0);
                captured.Apply();
                captured.name = $"Foto_{System.DateTime.Now:HHmmss}";

                RenderTexture.active = prevRT;

                if (TabletManager.Instance != null)
                {
                    TabletManager.Instance.AddCapturedPhoto(captured);
                }
            }

            // Desvanecer el flash blanco
            if (flashOverlay != null)
            {
                float t = 0f;
                while (t < 0.20f)
                {
                    t += Time.deltaTime;
                    float alpha = Mathf.Lerp(0.9f, 0f, t / 0.20f);
                    flashOverlay.color = new Color(1, 1, 1, alpha);
                    yield return null;
                }
                flashOverlay.gameObject.SetActive(false);
            }

            isCapturing = false;
            lastPhotoTime = Time.time;
            Debug.Log("<color=green>[Cámara]</color> ¡Foto única capturada con fluidez y guardada en la galería!");
        }

        private void PlayShutterClick()
        {
            if (audioSource == null) return;

            int sampleRate = 44100;
            float duration = 0.08f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = (Random.value * 2f - 1f) * (1f - (float)i / sampleCount) * 0.4f;
            }

            AudioClip clip = AudioClip.Create("ShutterClick", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            audioSource.PlayOneShot(clip);
        }
    }
}
