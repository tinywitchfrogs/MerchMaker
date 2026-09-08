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

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            SetupCameraAndTexture();

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
        }

        private void SetupCameraAndTexture()
        {
            if (cameraRenderTexture == null)
            {
                cameraRenderTexture = new RenderTexture(1024, 768, 16, RenderTextureFormat.ARGB32);
                cameraRenderTexture.name = "Tablet_Camera_RT";
            }

            if (tabletCamera == null)
            {
                GameObject camObj = new GameObject("TabletViewCamera");
                tabletCamera = camObj.AddComponent<Camera>();
                tabletCamera.clearFlags = CameraClearFlags.Skybox;
                tabletCamera.fieldOfView = defaultFOV;
                tabletCamera.nearClipPlane = 0.05f;
                tabletCamera.cullingMask = ~0; // Todo excepto UI si es posible
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
            }
        }

        private void LateUpdate()
        {
            // Sincronizar posición y orientación en tiempo real con la cabeza / vista del jugador
            Camera mainCam = Camera.main;
            if (tabletCamera != null && mainCam != null)
            {
                tabletCamera.transform.position = mainCam.transform.position;
                tabletCamera.transform.rotation = mainCam.transform.rotation;
            }
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy) return;

            HandleZoomInput();
            UpdateCameraZoom();

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            {
                TakePhoto();
            }
#else
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                TakePhoto();
            }
#endif
        }

        private void HandleZoomInput()
        {
            float scrollDelta = 0f;

#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                scrollDelta = mouse.scroll.ReadValue().y;
            }
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.eKey.isPressed || kb.numpadPlusKey.isPressed) scrollDelta += 120f * Time.deltaTime * 5f;
                if (kb.qKey.isPressed || kb.numpadMinusKey.isPressed) scrollDelta -= 120f * Time.deltaTime * 5f;
            }
#else
            scrollDelta = Input.GetAxis("Mouse ScrollWheel") * 100f;
            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.KeypadPlus)) scrollDelta += 120f * Time.deltaTime * 5f;
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.KeypadMinus)) scrollDelta -= 120f * Time.deltaTime * 5f;
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

            if (viewfinderDisplay != null && cameraRenderTexture != null)
            {
                viewfinderDisplay.texture = cameraRenderTexture;
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

            // Ocultar auras de dibujos para que no aparezcan en el visor ni en las fotos
            ScannableDrawing.SetAllAurasVisible(false);
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
            if (cameraRenderTexture == null || tabletCamera == null) return;
            StartCoroutine(CapturePhotoRoutine());
        }

        private IEnumerator CapturePhotoRoutine()
        {
            // Garantizar que las auras no se capturen en la fotografía
            ScannableDrawing.SetAllAurasVisible(false);

            if (flashOverlay != null)
            {
                flashOverlay.gameObject.SetActive(true);
                flashOverlay.color = new Color(1, 1, 1, 0.9f);
            }

            PlayShutterClick();

            tabletCamera.Render();

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

            // Desvanecer el flash blanco
            if (flashOverlay != null)
            {
                float t = 0f;
                while (t < 0.25f)
                {
                    t += Time.deltaTime;
                    float alpha = Mathf.Lerp(0.9f, 0f, t / 0.25f);
                    flashOverlay.color = new Color(1, 1, 1, alpha);
                    yield return null;
                }
                flashOverlay.gameObject.SetActive(false);
            }

            Debug.Log("<color=green>[Cámara]</color> ¡Foto capturada con éxito y guardada en la galería!");
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
