using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArtSimulator
{
    public class FirstPersonPlayerController : MonoBehaviour
    {
        private static FirstPersonPlayerController instance;
        public static FirstPersonPlayerController Instance
        {
            get
            {
                if (instance == null) instance = FindAnyObjectByType<FirstPersonPlayerController>();
                return instance;
            }
            private set => instance = value;
        }

        [Header("Movimiento")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float sprintSpeed = 7.0f;
        [SerializeField] private float gravity = -18.0f;

        private CharacterController characterController;
        private float verticalVelocity = 0f;

        [Header("Cámara y Ratón")]
        [SerializeField] private float mouseSensitivity = 2.0f;
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        [Header("Agarre de Objetos")]
        [SerializeField] private float interactionRange = 3.2f;
        [SerializeField] private Vector3 holdOffset = new Vector3(0.3f, -0.2f, 0.7f);
        [SerializeField] private float carryLerpSpeed = 12f;

        private Camera playerCamera;
        private float yaw = 0f;
        private float pitch = 0f;
        private bool isCursorLocked = true;

        private Transform heldObject = null;
        private Rigidbody heldRigidbody = null;
        private Collider heldCollider = null;

        // Modo Dibujo interactivo sobre papel
        private bool isDrawingMode = false;
        private DrawingCanvas activeDrawingCanvas = null;
        public bool IsDrawingMode => isDrawingMode;

        // Zoom de cámara y dibujo
        private float normalPlayerFOV = 60f;
        private float drawingTargetFOV = 35f;
        private const float minDrawingFOV = 15f;
        private const float maxDrawingFOV = 65f;
        private const float drawingZoomSpeed = 12f;

        // UI Retícula y mensajes
        private GameObject crosshairCanvas;
        private Image reticleDot;
        private Text interactionPromptText;
        private Text cameraPromptText;

        private void Awake()
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                enabled = false;
                return;
            }

            if (Instance == null) Instance = this;

            playerCamera = GetComponent<Camera>();
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null) playerCamera = Camera.main;

            if (playerCamera != null)
            {
                yaw = playerCamera.transform.eulerAngles.y;
                pitch = playerCamera.transform.eulerAngles.x;
                normalPlayerFOV = playerCamera.fieldOfView;
            }

            // Configurar CharacterController para colisiones con paredes y suelo firme
            characterController = GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = gameObject.AddComponent<CharacterController>();
            }
            characterController.height = 1.85f;
            characterController.radius = 0.30f;
            characterController.center = new Vector3(0f, -0.925f, 0f);
            characterController.skinWidth = 0.04f;
            characterController.minMoveDistance = 0f;
            characterController.stepOffset = 0.35f;
            characterController.slopeLimit = 45f;

            // Eliminar CapsuleCollider trigger obsoleto si existe
            CapsuleCollider oldCol = GetComponent<CapsuleCollider>();
            if (oldCol != null) Destroy(oldCol);

            CreateCrosshairUI();
        }

        private void OnEnable()
        {
            if (crosshairCanvas != null)
            {
                crosshairCanvas.SetActive(true);
            }
            SetPromptText("");
            SetCameraPromptText("");
        }

        private void OnDisable()
        {
            SetPromptText("");
            SetCameraPromptText("");
            if (crosshairCanvas != null)
            {
                crosshairCanvas.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (crosshairCanvas != null)
            {
                Destroy(crosshairCanvas);
            }
        }

        public void SetCrosshairVisible(bool visible)
        {
            if (crosshairCanvas != null)
            {
                crosshairCanvas.SetActive(visible);
            }
            if (!visible)
            {
                SetPromptText("");
                SetCameraPromptText("");
            }
        }

        private void Start()
        {
            if (TabletManager.Instance != null)
            {
                TabletManager.Instance.CloseTablet();
            }

            SetCursorLock(true);
        }

        private void Update()
        {
            // Si el juego está pausado, congelar controles y liberar cursor para el menú de pausa
            if (MenuPausa.IsGamePaused)
            {
                if (isCursorLocked)
                {
                    SetCursorLock(false);
                }
                return;
            }

            // Si estamos en Modo Dibujo activo
            if (isDrawingMode)
            {
                HandleDrawingMode();
                return;
            }

            HandleTabletToggleInput();

            // Si la tableta está abierta en pantalla:
            bool isTabletOpen = TabletManager.Instance != null && TabletManager.Instance.IsTabletOpen;
            if (isTabletOpen)
            {
                TabletScreen currentScreen = TabletManager.Instance.CurrentScreen;

                // MODO CÁMARA: ¡Permitir moverse libremente por el cuarto y rotar la vista para encuadrar y tomar fotos!
                if (currentScreen == TabletScreen.Camera)
                {
                    HandleLook();
                    HandleMovement();

                    if (isCursorLocked)
                    {
                        SetCursorLock(false);
                    }

                    TabletCameraApp camApp = FindAnyObjectByType<TabletCameraApp>();
                    float zoomFactor = camApp != null ? camApp.CurrentZoomFactor : 1.0f;
                    SetCameraPromptText($"📷 <b>CÁMARA</b>: [WASD] Moverte  •  [Rueda / Q-E] Zoom ({zoomFactor:F1}x)  •  <b>[ESPACIO / F / Clic]</b> Tomar Foto  •  [🏠 / H] Volver");
                    HandleTabletShortcuts();
                    return;
                }

                // En otras pantallas (Home, Galería, Tienda): cursor libre para interactuar con la UI
                if (isCursorLocked)
                {
                    SetCursorLock(false);
                }
                SetPromptText("📱 <b>Tableta abierta</b> - Pulsa <b>[ESC]</b> para cerrar y caminar");
                HandleTabletShortcuts();
                return;
            }

            // Si la tableta está cerrada, asegurar que el cursor esté siempre bloqueado y oculto para jugar
            if (!isCursorLocked || Cursor.lockState != CursorLockMode.Locked || Cursor.visible)
            {
                SetCursorLock(true);
            }

            HandleLook();
            HandleMovement();
            HandleInspectionZoom();
            HandleInteractionRaycast();
            UpdateHeldObjectPosition();
        }

        private void HandleTabletToggleInput()
        {
            bool isEscPressed = false;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) isEscPressed = true;
#else
            if (Input.GetKeyDown(KeyCode.Escape)) isEscPressed = true;
#endif
            if (isEscPressed)
            {
                var tm = TabletManager.Instance;
                if (tm != null && tm.IsTabletOpen)
                {
                    tm.CloseTablet();
                    SetCursorLock(true);
                }
            }
        }

        private void HandleTabletShortcuts()
        {
            if (TabletManager.Instance == null) return;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
                    TabletManager.Instance.ShowImages();
                else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
                    TabletManager.Instance.ShowCamera();
                else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
                    TabletManager.Instance.ShowStore();
                else if (kb.hKey.wasPressedThisFrame)
                    TabletManager.Instance.ShowHome();
                else if (kb.escapeKey.wasPressedThisFrame)
                    TabletManager.Instance.CloseTablet();
            }
#else
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                TabletManager.Instance.ShowImages();
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                TabletManager.Instance.ShowCamera();
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
                TabletManager.Instance.ShowStore();
            else if (Input.GetKeyDown(KeyCode.H))
                TabletManager.Instance.ShowHome();
            else if (Input.GetKeyDown(KeyCode.Escape))
                TabletManager.Instance.CloseTablet();
#endif
        }

        public void SetCursorLock(bool locked)
        {
            isCursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;

            if (reticleDot != null)
            {
                reticleDot.gameObject.SetActive(locked);
            }
        }

        private void HandleLook()
        {
            bool isCameraMode = TabletManager.Instance != null && TabletManager.Instance.IsTabletOpen && TabletManager.Instance.CurrentScreen == TabletScreen.Camera;
            if (!isCursorLocked && !isCameraMode) return;

            float mouseX = 0f;
            float mouseY = 0f;

#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                mouseX = delta.x * 0.1f * mouseSensitivity;
                mouseY = delta.y * 0.1f * mouseSensitivity;
            }
#else
            mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
#endif

            yaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            if (playerCamera != null)
            {
                playerCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
            else
            {
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
        }

        private void HandleMovement()
        {
            Vector3 inputDir = Vector3.zero;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed) inputDir.z += 1f;
                if (kb.sKey.isPressed) inputDir.z -= 1f;
                if (kb.aKey.isPressed) inputDir.x -= 1f;
                if (kb.dKey.isPressed) inputDir.x += 1f;
            }
            bool isSprinting = kb != null && kb.leftShiftKey.isPressed;
#else
            if (Input.GetKey(KeyCode.W)) inputDir.z += 1f;
            if (Input.GetKey(KeyCode.S)) inputDir.z -= 1f;
            if (Input.GetKey(KeyCode.A)) inputDir.x -= 1f;
            if (Input.GetKey(KeyCode.D)) inputDir.x += 1f;
            bool isSprinting = Input.GetKey(KeyCode.LeftShift);
#endif

            float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

            // Orientación horizontal basada únicamente en el giro de vista (yaw), para no inclinarse hacia el suelo ni volar al mirar arriba
            Quaternion horizontalRot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 forward = horizontalRot * Vector3.forward;
            Vector3 right = horizontalRot * Vector3.right;

            Vector3 moveDir = (forward * inputDir.z + right * inputDir.x).normalized;
            Vector3 horizontalVelocity = moveDir * currentSpeed;

            if (characterController != null && characterController.enabled)
            {
                if (characterController.isGrounded)
                {
                    verticalVelocity = -2f; // Mantener los pies pegados al suelo
                }
                else
                {
                    verticalVelocity += gravity * Time.deltaTime;
                }

                Vector3 totalVelocity = horizontalVelocity + Vector3.up * verticalVelocity;
                characterController.Move(totalVelocity * Time.deltaTime);
            }
            else
            {
                transform.position += horizontalVelocity * Time.deltaTime;
            }
        }

        private void HandleInteractionRaycast()
        {
            if (playerCamera == null) return;

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

            bool isInteractPressed = false;
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) isInteractPressed = true;
            if (kb != null && kb.eKey.wasPressedThisFrame) isInteractPressed = true;
#else
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E)) isInteractPressed = true;
#endif

            // 1. Si llevamos un dibujo o sobre en manos
            if (heldObject != null)
            {
                // Prioridad A: Colocar en el Atril de dibujo
                AtrilController atrilTarget = GetTargetedAtril(ray);
                if (atrilTarget != null)
                {
                    ScannableDrawing drawingHeld = heldObject.GetComponentInChildren<ScannableDrawing>() 
                        ?? heldObject.GetComponentInParent<ScannableDrawing>();
                    if (drawingHeld != null)
                    {
                        string drawName = drawingHeld.GetArtworkData().title;
                        SetPromptText($"🎨 <b>Atril</b> - <b>[Clic Izquierdo]</b> o <b>[E]</b> para colocar <b>'{drawName}'</b> en el atril y pintar");
                        SetReticleColor(Color.cyan);

                        if (isInteractPressed)
                        {
                            if (atrilTarget.MountDrawing(drawingHeld))
                            {
                                if (heldCollider != null) heldCollider.enabled = true;
                                heldObject = null;
                                heldRigidbody = null;
                                heldCollider = null;
                            }
                        }
                        return;
                    }
                }

                // Prioridad B: Insertar copia impresa en un sobre de envíos
                EnvelopeController envelopeTarget = GetTargetedEnvelope(ray);
                if (envelopeTarget != null && !envelopeTarget.IsSealed)
                {
                    ScannableDrawing drawingHeld = heldObject.GetComponentInChildren<ScannableDrawing>() 
                        ?? heldObject.GetComponentInParent<ScannableDrawing>();
                    if (drawingHeld != null && !drawingHeld.IsOriginal)
                    {
                        if (envelopeTarget.IsSealed || (envelopeTarget.TargetOrderCopies > 0 && envelopeTarget.CurrentCopiesCount >= envelopeTarget.TargetOrderCopies))
                        {
                            SetPromptText($"✉️ <b>Sobre Completo</b> ({envelopeTarget.CurrentCopiesCount}/{envelopeTarget.TargetOrderCopies}) - ¡No caben más copias!");
                            SetReticleColor(Color.yellow);
                            return;
                        }

                        string drawName = drawingHeld.GetArtworkData().title;
                        SetPromptText($"✉️ <b>Sobre</b> - <b>[Clic Izquierdo]</b> o <b>[E]</b> para meter copia de <b>'{drawName}'</b> en el sobre");
                        SetReticleColor(Color.green);

                        if (isInteractPressed)
                        {
                            if (envelopeTarget.TryInsertCopy(drawingHeld))
                            {
                                if (heldCollider != null) heldCollider.enabled = true;
                                heldObject = null;
                                heldRigidbody = null;
                                heldCollider = null;
                            }
                        }
                        return;
                    }
                }

                PrinterController targetedPrinter = GetTargetedPrinter(ray);
                if (targetedPrinter != null)
                {
                    if (targetedPrinter.HasDrawingInside)
                    {
                        SetPromptText("🖨️ <b>Impresora ocupada</b> - Retira primero el dibujo en el escáner para insertar otro");
                        SetReticleColor(new Color(1f, 0.6f, 0.1f));
                        return;
                    }

                    ScannableDrawing drawingToInsert = heldObject.GetComponentInChildren<ScannableDrawing>() 
                        ?? heldObject.GetComponentInParent<ScannableDrawing>();
                    string drawName = drawingToInsert != null ? drawingToInsert.GetArtworkData().title : "dibujo";

                    SetPromptText($"🖨️ <b>Impresora</b> - <b>[Clic Izquierdo]</b> o <b>[E]</b> para insertar <b>'{drawName}'</b> y escanear");
                    SetReticleColor(Color.cyan);

                    if (isInteractPressed)
                    {
                        if (drawingToInsert != null && targetedPrinter.InsertDrawing(drawingToInsert))
                        {
                            if (heldCollider != null) heldCollider.enabled = true;
                            heldObject = null;
                            heldRigidbody = null;
                            heldCollider = null;
                        }
                    }
                    return;
                }

                SendProduct targetedSendBox = GetTargetedSendZone(ray);
                if (targetedSendBox != null)
                {
                    ScannableDrawing drawingHeld = heldObject.GetComponentInChildren<ScannableDrawing>()
                        ?? heldObject.GetComponentInParent<ScannableDrawing>();

                    if (drawingHeld != null && drawingHeld.IsOriginal)
                    {
                        SetPromptText("⚠️ <b>Caja de Envíos</b> - Es un dibujo ORIGINAL. ¡Solo debes enviar copias impresas en sobres!");
                        SetReticleColor(new Color(1f, 0.5f, 0f));
                        return;
                    }

                    SetPromptText("📦 <b>Caja de Envíos</b> - <b>[Clic Izquierdo]</b> o pulsa <b>[E]</b> para enviar pedido");
                    SetReticleColor(Color.green);

                    if (isInteractPressed)
                    {
                        GameObject objToDeliver = heldObject.gameObject;
                        bool delivered = targetedSendBox.ProcessDeliveredObject(objToDeliver);
                        if (delivered)
                        {
                            if (heldCollider != null) heldCollider.enabled = true;
                            heldObject = null;
                            heldRigidbody = null;
                            heldCollider = null;
                        }
                    }
                    return;
                }

                // Si llevamos un Sobre de envíos en la mano, permitir sellarlo con [F] directamente
                EnvelopeController envHeld = heldObject.GetComponentInChildren<EnvelopeController>()
                    ?? heldObject.GetComponentInParent<EnvelopeController>();
                if (envHeld != null)
                {
                    bool isSealActionPressed = false;
#if ENABLE_INPUT_SYSTEM
                    if (kb != null && (kb.fKey.wasPressedThisFrame || (mouse != null && mouse.rightButton.wasPressedThisFrame))) isSealActionPressed = true;
#else
                    if (Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(1)) isSealActionPressed = true;
#endif
                    if (!envHeld.IsSealed)
                    {
                        if (envHeld.CurrentCopiesCount > 0)
                        {
                            SetPromptText($"✉️ <b>Sobre ({envHeld.CurrentCopiesCount} copias)</b> - <b>[F / Clic Der]</b> Sellar sobre  •  <b>[Clic / E]</b> Soltar en mesa");
                            SetReticleColor(Color.green);

                            if (isSealActionPressed)
                            {
                                envHeld.SealEnvelope();
                                SetPromptText("✓ ¡Sobre cerrado y sellado con éxito! Llévalo a la caja de envíos.");
                                return;
                            }
                        }
                        else
                        {
                            SetPromptText("✉️ <b>Sobre vacío</b> - <b>[Clic / E]</b> Colocar sobre la mesa e insertar copias impresas");
                            SetReticleColor(Color.yellow);
                        }
                    }
                    else
                    {
                        SetPromptText("✉️ <b>Sobre SELLADO</b> - Llévalo a la caja de envíos  •  <b>[Clic / E]</b> Soltar en mesa");
                        SetReticleColor(Color.green);
                    }

                    if (isInteractPressed)
                    {
                        DropHeldObject();
                    }
                    return;
                }

                SetPromptText("🖐️ Llevando objeto - <b>[Clic Izquierdo]</b> o <b>[E]</b> para soltarlo sobre una superficie");
                SetReticleColor(Color.yellow);

                if (isInteractPressed)
                {
                    DropHeldObject();
                }
                return;
            }

            // 2. Detección de dibujos (ScannableDrawing) en la mira (directo o por aproximación)
            ScannableDrawing targetedDrawing = null;

            // Primero: Raycast directo y preciso al centro de la mira
            RaycastHit directHit;
            if (Physics.Raycast(ray, out directHit, interactionRange, ~0, QueryTriggerInteraction.Collide))
            {
                targetedDrawing = directHit.collider.GetComponentInParent<ScannableDrawing>();
            }

            // Segundo: Si el rayo directo no dio de lleno, usar un SphereCast estrecho
            if (targetedDrawing == null)
            {
                RaycastHit[] hits = Physics.SphereCastAll(ray, 0.15f, interactionRange, ~0, QueryTriggerInteraction.Collide);
                float closestDist = float.MaxValue;
                for (int i = 0; i < hits.Length; i++)
                {
                    ScannableDrawing d = hits[i].collider.GetComponentInParent<ScannableDrawing>();
                    if (d != null && hits[i].distance < closestDist)
                    {
                        closestDist = hits[i].distance;
                        targetedDrawing = d;
                    }
                }
            }

            PrinterController printer = GetTargetedPrinter(ray);

            // Si estamos apuntando directamente a un dibujo (copia u original)
            if (targetedDrawing != null)
            {
                // CASO A: Es el dibujo original que está insertado en el escáner de la impresora
                if (printer != null && printer.HasDrawingInside && targetedDrawing == printer.CurrentInsertedDrawing)
                {
                    string artTitle = targetedDrawing.GetArtworkData().title;
                    SetPromptText($"🖨️ <b>[Clic / E]</b> Retirar '{artTitle}' del escáner");
                    SetReticleColor(Color.cyan);

                    if (isInteractPressed)
                    {
                        ScannableDrawing retrieved = printer.RetrieveDrawing();
                        if (retrieved != null)
                        {
                            PickupObject(retrieved.transform);
                        }
                    }
                    return;
                }

                // CASO B: Es una copia impresa (por ejemplo reposando en la bandeja de salida)
                if (!targetedDrawing.IsOriginal)
                {
                    SetPromptText($"🖐️ <b>[Clic / E]</b> Tomar copia de <b>'{targetedDrawing.GetArtworkData().title}'</b>");
                    SetReticleColor(Color.yellow);

                    if (isInteractPressed)
                    {
                        PickupObject(targetedDrawing.transform);
                    }
                    return;
                }

                // CASO C: Es un dibujo original sobre la mesa (fuera del escáner)
                DrawingCanvas canvas = targetedDrawing.GetComponent<DrawingCanvas>();
                bool isDrawPressed = false;
#if ENABLE_INPUT_SYSTEM
                if (kb != null && kb.fKey.wasPressedThisFrame) isDrawPressed = true;
                if (mouse != null && mouse.rightButton.wasPressedThisFrame) isDrawPressed = true;
#else
                if (Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(1)) isDrawPressed = true;
#endif

                if (canvas != null)
                {
                    SetPromptText($"🖐️ <b>[Clic / E]</b> Tomar  •  🖌️ <b>[F / Clic Der]</b> Dibujar en {targetedDrawing.GetArtworkData().title} <color=#60A5FA>(Original)</color>");
                    SetReticleColor(Color.cyan);

                    if (isDrawPressed)
                    {
                        EnterDrawingMode(canvas);
                        return;
                    }
                }
                else
                {
                    SetPromptText($"🖐️ <b>[Clic / E]</b> Tomar {targetedDrawing.GetArtworkData().title} <color=#60A5FA>(Original)</color>");
                    SetReticleColor(Color.cyan);
                }

                if (isInteractPressed)
                {
                    PickupObject(targetedDrawing.transform);
                }
                return;
            }

            // 3. Si no apuntamos directamente a un dibujo, pero apuntamos a la impresora con un dibujo adentro
            if (printer != null && printer.HasDrawingInside)
            {
                ScannableDrawing insideDrawing = printer.CurrentInsertedDrawing;
                string artTitle = insideDrawing != null ? insideDrawing.GetArtworkData().title : "dibujo";
                SetPromptText($"🖨️ <b>[Clic / E]</b> Retirar '{artTitle}' del escáner");
                SetReticleColor(Color.green);

                if (isInteractPressed)
                {
                    ScannableDrawing retrieved = printer.RetrieveDrawing();
                    if (retrieved != null)
                    {
                        PickupObject(retrieved.transform);
                    }
                }
                return;
            }

            // 3. Si no hay dibujo en la mira, comprobar si apuntamos a la tableta física en la mesa
            RaycastHit hit;
            bool hitTablet = false;
            if (Physics.Raycast(ray, out hit, interactionRange))
            {
                if (hit.collider.GetComponentInParent<TabletManager>() != null ||
                    hit.collider.GetComponentInParent<TabletInteractiveScreen>() != null ||
                    hit.collider.name.ToLower().Contains("tableta") ||
                    hit.collider.name.ToLower().Contains("pantalla"))
                {
                    hitTablet = true;
                }
            }

            if (!hitTablet)
            {
                RaycastHit[] tHits = Physics.SphereCastAll(ray, 0.25f, interactionRange);
                for (int i = 0; i < tHits.Length; i++)
                {
                    var col = tHits[i].collider;
                    if (col.GetComponentInParent<TabletManager>() != null ||
                        col.GetComponentInParent<TabletInteractiveScreen>() != null ||
                        col.name.ToLower().Contains("tableta") ||
                        col.name.ToLower().Contains("pantalla"))
                    {
                        hitTablet = true;
                        break;
                    }
                }
            }

            if (hitTablet)
            {
                SetPromptText("📱 <b>Tableta</b> - <b>[Clic Izquierdo]</b> o pulsa <b>[E]</b> para usarla");
                SetReticleColor(Color.cyan);

                if (isInteractPressed)
                {
                    if (TabletManager.Instance != null)
                        TabletManager.Instance.OpenTablet();
                }
                return;
            }

            // 4. Comprobar si apuntamos a la pila de hojas de papel para sacar un nuevo dibujo original
            PaperDispenser dispenser = GetTargetedDispenser(ray);
            if (dispenser != null)
            {
                SetPromptText("📄 <b>Pila de Hojas</b> - <b>[Clic Izquierdo]</b> o <b>[E]</b> para tomar una hoja limpia");
                SetReticleColor(Color.cyan);

                if (isInteractPressed)
                {
                    ScannableDrawing newPaper = dispenser.DispenseNewPaper();
                    if (newPaper != null && heldObject == null)
                    {
                        PickupObject(newPaper.transform);
                        SetPromptText($"🖐️ Llevando {newPaper.GetArtworkData().title} - <b>[Clic / E]</b> para colocar sobre la mesa");
                    }
                }
                return;
            }

            // 4b. Comprobar si apuntamos al dispensador de sobres
            EnvelopeDispenser envDispenser = GetTargetedEnvelopeDispenser(ray);
            if (envDispenser != null)
            {
                SetPromptText("✉️ <b>Pila de Sobres</b> - <b>[Clic Izquierdo]</b> o <b>[E]</b> para tomar un sobre");
                SetReticleColor(Color.cyan);

                if (isInteractPressed)
                {
                    EnvelopeController newEnv = envDispenser.DispenseNewEnvelope();
                    if (newEnv != null && heldObject == null)
                    {
                        PickupObject(newEnv.transform);
                        SetPromptText("🖐️ Llevando Sobre - <b>[Clic / E]</b> para colocar sobre la mesa");
                    }
                }
                return;
            }

            // 5. Comprobar si apuntamos al Atril de dibujo
            AtrilController targetedAtril = GetTargetedAtril(ray);
            if (targetedAtril != null && targetedAtril.HasDrawingMounted)
            {
                bool isDrawPressed = false;
#if ENABLE_INPUT_SYSTEM
                if (kb != null && (kb.fKey.wasPressedThisFrame || (mouse != null && mouse.rightButton.wasPressedThisFrame))) isDrawPressed = true;
#else
                if (Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(1)) isDrawPressed = true;
#endif
                var mountedDrawing = targetedAtril.CurrentMountedDrawing;
                string title = mountedDrawing != null ? mountedDrawing.GetArtworkData().title : "Dibujo";
                var canvas = mountedDrawing != null ? mountedDrawing.GetComponent<DrawingCanvas>() : null;

                if (canvas != null)
                {
                    SetPromptText($"🎨 <b>[F / Clic Der]</b> Dibujar en el atril  •  🖐️ <b>[Clic / E]</b> Retirar '{title}'");
                    SetReticleColor(Color.cyan);

                    if (isDrawPressed)
                    {
                        EnterDrawingMode(canvas);
                        return;
                    }
                }
                else
                {
                    SetPromptText($"🖐️ <b>[Clic / E]</b> Retirar '{title}' del atril");
                    SetReticleColor(Color.yellow);
                }

                if (isInteractPressed)
                {
                    var detached = targetedAtril.DetachDrawing();
                    if (detached != null)
                    {
                        PickupObject(detached.transform);
                    }
                    return;
                }
                return;
            }

            // 6. Comprobar si apuntamos a un Sobre de envío
            EnvelopeController targetedEnv = GetTargetedEnvelope(ray);
            if (targetedEnv != null)
            {
                if (!targetedEnv.IsSealed)
                {
                    bool isSealPressed = false;
#if ENABLE_INPUT_SYSTEM
                    if (kb != null && (kb.fKey.wasPressedThisFrame || (mouse != null && mouse.rightButton.wasPressedThisFrame))) isSealPressed = true;
#else
                    if (Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(1)) isSealPressed = true;
#endif
                    if (targetedEnv.CurrentCopiesCount > 0)
                    {
                        SetPromptText($"✉️ <b>[Clic / E]</b> Tomar sobre ({targetedEnv.CurrentCopiesCount} copias)  •  📦 <b>[F / Clic Der]</b> Sellar sobre");
                        SetReticleColor(Color.yellow);

                        if (isSealPressed)
                        {
                            targetedEnv.SealEnvelope();
                            return;
                        }
                    }
                    else
                    {
                        SetPromptText($"✉️ <b>[Clic / E]</b> Tomar sobre vacío (inserta copias impresas antes de sellar)");
                        SetReticleColor(Color.yellow);
                    }

                    if (isInteractPressed)
                    {
                        PickupObject(targetedEnv.transform);
                        return;
                    }
                    return;
                }
                else
                {
                    SetPromptText($"✉️ <b>[Clic / E]</b> Tomar sobre SELLADO ({targetedEnv.CurrentCopiesCount} copias) listo para envío");
                    SetReticleColor(Color.green);

                    if (isInteractPressed)
                    {
                        PickupObject(targetedEnv.transform);
                        return;
                    }
                    return;
                }
            }

            // Sin interacción en la mira
            SetPromptText("");
            SetReticleColor(Color.white);
        }

        private PaperDispenser GetTargetedDispenser(Ray ray)
        {
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, interactionRange, ~0, QueryTriggerInteraction.Collide))
            {
                var pd = hit.collider.GetComponentInParent<PaperDispenser>();
                if (pd != null) return pd;
                if (hit.collider.name.ToLower().Contains("pila") || hit.collider.name.ToLower().Contains("dispenser") || hit.collider.name.ToLower().Contains("hojas"))
                    return PaperDispenser.Instance;
            }

            RaycastHit[] hits = Physics.SphereCastAll(ray, 0.25f, interactionRange, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++)
            {
                var pd = hits[i].collider.GetComponentInParent<PaperDispenser>();
                if (pd != null) return pd;
                if (hits[i].collider.name.ToLower().Contains("pila") || hits[i].collider.name.ToLower().Contains("dispenser") || hits[i].collider.name.ToLower().Contains("hojas"))
                    return PaperDispenser.Instance;
            }

            return null;
        }

        private EnvelopeDispenser GetTargetedEnvelopeDispenser(Ray ray)
        {
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, interactionRange, ~0, QueryTriggerInteraction.Collide))
            {
                var ed = hit.collider.GetComponentInParent<EnvelopeDispenser>();
                if (ed != null) return ed;
            }

            RaycastHit[] hits = Physics.SphereCastAll(ray, 0.25f, interactionRange, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++)
            {
                var ed = hits[i].collider.GetComponentInParent<EnvelopeDispenser>();
                if (ed != null) return ed;
            }

            return null;
        }

        private PrinterController GetTargetedPrinter(Ray ray)
        {
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, interactionRange, ~0, QueryTriggerInteraction.Collide))
            {
                var pc = hit.collider.GetComponentInParent<PrinterController>() ?? hit.collider.GetComponentInChildren<PrinterController>();
                if (pc != null) return pc;
                if (hit.collider.name.ToLower().Contains("impresora") || hit.collider.name.ToLower().Contains("printer"))
                    return PrinterController.Instance;
            }

            RaycastHit[] hits = Physics.SphereCastAll(ray, 0.5f, interactionRange, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++)
            {
                var pc = hits[i].collider.GetComponentInParent<PrinterController>() ?? hits[i].collider.GetComponentInChildren<PrinterController>();
                if (pc != null) return pc;
                if (hits[i].collider.name.ToLower().Contains("impresora") || hits[i].collider.name.ToLower().Contains("printer"))
                    return PrinterController.Instance;
            }

            return null;
        }

        private SendProduct GetTargetedSendZone(Ray ray)
        {
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, interactionRange, ~0, QueryTriggerInteraction.Collide))
            {
                var sp = hit.collider.GetComponentInParent<SendProduct>();
                if (sp != null) return sp;
                if (hit.collider.name.ToLower().Contains("envio") || hit.collider.name.ToLower().Contains("caja"))
                    return SendProduct.Instance;
            }

            RaycastHit[] hits = Physics.SphereCastAll(ray, 0.35f, interactionRange, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++)
            {
                var sp = hits[i].collider.GetComponentInParent<SendProduct>();
                if (sp != null) return sp;
                if (hits[i].collider.name.ToLower().Contains("envio") || hits[i].collider.name.ToLower().Contains("caja"))
                    return SendProduct.Instance;
            }

            return null;
        }

        private AtrilController GetTargetedAtril(Ray ray)
        {
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, interactionRange, ~0, QueryTriggerInteraction.Collide))
            {
                var atril = hit.collider.GetComponentInParent<AtrilController>() ?? hit.collider.GetComponentInChildren<AtrilController>();
                if (atril != null) return atril;
                if (hit.collider.name.ToLower().Contains("atril") || hit.collider.name.ToLower().Contains("madera"))
                    return AtrilController.Instance;
            }

            RaycastHit[] hits = Physics.SphereCastAll(ray, 0.45f, interactionRange, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++)
            {
                var atril = hits[i].collider.GetComponentInParent<AtrilController>() ?? hits[i].collider.GetComponentInChildren<AtrilController>();
                if (atril != null) return atril;
                if (hits[i].collider.name.ToLower().Contains("atril"))
                    return AtrilController.Instance;
            }

            return null;
        }

        private EnvelopeController GetTargetedEnvelope(Ray ray)
        {
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, interactionRange, ~0, QueryTriggerInteraction.Collide))
            {
                var env = hit.collider.GetComponentInParent<EnvelopeController>() ?? hit.collider.GetComponentInChildren<EnvelopeController>();
                if (env != null) return env;
            }

            RaycastHit[] hits = Physics.SphereCastAll(ray, 0.35f, interactionRange, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++)
            {
                var env = hits[i].collider.GetComponentInParent<EnvelopeController>() ?? hits[i].collider.GetComponentInChildren<EnvelopeController>();
                if (env != null) return env;
            }

            return null;
        }

        #region Modo Dibujo Interactivo

        public void EnterDrawingMode(DrawingCanvas canvas)
        {
            if (canvas == null) return;
            isDrawingMode = true;
            activeDrawingCanvas = canvas;
            if (playerCamera != null)
            {
                normalPlayerFOV = playerCamera.fieldOfView;
            }
            drawingTargetFOV = 35f; // Enfoque cercano por defecto al papel
            SetCursorLock(false);
            UpdateDrawingPrompt();
            DrawingUIOverlay.Instance.Show(canvas);
        }

        public void ExitDrawingMode()
        {
            if (activeDrawingCanvas != null)
            {
                activeDrawingCanvas.StopStroke();
            }
            isDrawingMode = false;
            activeDrawingCanvas = null;
            DrawingUIOverlay.Instance.Hide();
            SetCursorLock(true);
            SetPromptText("");
            StartCoroutine(RestoreCameraFOV());
        }

        public void AdjustDrawingZoom(float deltaFOV)
        {
            drawingTargetFOV = Mathf.Clamp(drawingTargetFOV + deltaFOV, minDrawingFOV, maxDrawingFOV);
            UpdateDrawingPrompt();
        }

        public void ResetDrawingZoom()
        {
            drawingTargetFOV = 35f;
            UpdateDrawingPrompt();
        }

        private void UpdateDrawingPrompt()
        {
            float zoom = 60f / drawingTargetFOV;
            SetPromptText($"🖌️ <b>Mesa de Dibujo</b>: Clic y arrastra para pintar  •  [Rueda / Q-E] Zoom ({zoom:F1}x)  •  <b>[F / ESC]</b> Salir");
        }

        private System.Collections.IEnumerator RestoreCameraFOV()
        {
            if (playerCamera == null) yield break;
            while (Mathf.Abs(playerCamera.fieldOfView - normalPlayerFOV) > 0.1f && !isDrawingMode)
            {
                playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, normalPlayerFOV, Time.deltaTime * 12f);
                yield return null;
            }
            if (!isDrawingMode)
            {
                playerCamera.fieldOfView = normalPlayerFOV;
            }
        }

        private void HandleDrawingMode()
        {
            // Salir con tecla F o Escape
            bool exitPressed = false;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.fKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) exitPressed = true;
#else
            if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Escape)) exitPressed = true;
#endif

            if (exitPressed)
            {
                ExitDrawingMode();
                return;
            }

            if (activeDrawingCanvas == null)
            {
                ExitDrawingMode();
                return;
            }

            // Manejar entrada de zoom para la hoja de dibujo (Rueda del ratón o teclas Q / E)
            float scroll = 0f;
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null) scroll = mouse.scroll.ReadValue().y;
            if (kb != null)
            {
                if (kb.eKey.isPressed || kb.numpadPlusKey.isPressed) scroll += 120f * Time.deltaTime * 5f;
                if (kb.qKey.isPressed || kb.numpadMinusKey.isPressed) scroll -= 120f * Time.deltaTime * 5f;
            }
#else
            scroll = Input.GetAxis("Mouse ScrollWheel") * 120f;
            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.KeypadPlus)) scroll += 120f * Time.deltaTime * 5f;
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.KeypadMinus)) scroll -= 120f * Time.deltaTime * 5f;
#endif

            if (Mathf.Abs(scroll) > 0.05f)
            {
                float step = (scroll / 120f) * 6f;
                drawingTargetFOV = Mathf.Clamp(drawingTargetFOV - step, minDrawingFOV, maxDrawingFOV);
                UpdateDrawingPrompt();
            }

            if (playerCamera != null)
            {
                playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, drawingTargetFOV, Time.deltaTime * drawingZoomSpeed);
                float zoomRatio = 60f / playerCamera.fieldOfView;
                DrawingUIOverlay.Instance.UpdateZoomText($"{zoomRatio:F1}x");
            }

            Vector3 mousePos;
            bool isMouseDown = false;
            bool isMouseUp = false;

#if ENABLE_INPUT_SYSTEM
            mousePos = mouse != null ? (Vector3)mouse.position.ReadValue() : Input.mousePosition;
            if (mouse != null)
            {
                isMouseDown = mouse.leftButton.isPressed;
                isMouseUp = mouse.leftButton.wasReleasedThisFrame;
            }
#else
            mousePos = Input.mousePosition;
            isMouseDown = Input.GetMouseButton(0);
            isMouseUp = Input.GetMouseButtonUp(0);
#endif

            // Evitar dibujar si el cursor está sobre un elemento de la interfaz (los botones de colores o tamaños)
            bool isOverUI = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

            if (isMouseDown && !isOverUI && playerCamera != null)
            {
                Ray ray = playerCamera.ScreenPointToRay(mousePos);
                if (Physics.Raycast(ray, out RaycastHit hit, 10f))
                {
                    DrawingCanvas hitCanvas = hit.collider.GetComponentInParent<DrawingCanvas>();
                    if (hitCanvas == activeDrawingCanvas)
                    {
                        activeDrawingCanvas.DrawAtWorldPoint(hit.point);
                    }
                }
            }
            else if (isMouseUp)
            {
                activeDrawingCanvas.StopStroke();
            }
        }

        private void HandleInspectionZoom()
        {
            if (playerCamera == null || isDrawingMode) return;

            bool isHoldingRightClick = false;
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed) isHoldingRightClick = true;
#else
            if (Input.GetMouseButton(1)) isHoldingRightClick = true;
#endif

            // Si llevamos un dibujo sostenido en manos, mantener clic derecho acerca la hoja a los ojos para inspeccionar el arte
            float targetFOV = (isHoldingRightClick && heldObject != null) ? 32f : normalPlayerFOV;
            if (Mathf.Abs(playerCamera.fieldOfView - targetFOV) > 0.05f)
            {
                playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, Time.deltaTime * 10f);
            }
        }

        #endregion

        private void PickupObject(Transform obj)
        {
            // La tableta y su pantalla son ESTÁTICAS, jamás se toman
            if (obj.GetComponentInParent<TabletManager>() != null ||
                obj.name.ToLower().Contains("tableta") ||
                obj.name.ToLower().Contains("pantalla"))
            {
                return;
            }

            heldObject = obj;
            heldRigidbody = obj.GetComponent<Rigidbody>();
            heldCollider = obj.GetComponent<Collider>();

            if (heldRigidbody != null)
            {
                heldRigidbody.isKinematic = true;
            }

            if (heldCollider != null)
            {
                heldCollider.enabled = false;
            }
        }

        public bool IsHoldingObject(Transform obj)
        {
            if (heldObject == null || obj == null) return false;
            return heldObject == obj || heldObject.IsChildOf(obj) || obj.IsChildOf(heldObject);
        }

        public void ReleaseDeliveredObject(Transform obj)
        {
            if (heldObject != null && (heldObject == obj || heldObject.IsChildOf(obj) || obj.IsChildOf(heldObject)))
            {
                heldObject = null;
                heldRigidbody = null;
                heldCollider = null;
                SetPromptText("");
            }
        }

        private void DropHeldObject()
        {
            if (heldObject == null) return;

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            ScannableDrawing sc = heldObject.GetComponentInChildren<ScannableDrawing>() 
                ?? heldObject.GetComponentInParent<ScannableDrawing>();

            // 1. Si soltamos el dibujo apuntando al atril, encajarlo en el atril
            AtrilController atrilUnderRay = GetTargetedAtril(ray);
            if (atrilUnderRay != null && sc != null)
            {
                if (heldCollider != null) heldCollider.enabled = true;
                heldObject = null;
                heldRigidbody = null;
                heldCollider = null;
                atrilUnderRay.MountDrawing(sc);
                SetPromptText("✓ Dibujo colocado en el atril. <b>[F / Clic Der]</b> para pintar.");
                return;
            }

            // 2. Si soltamos una copia apuntando a un sobre, insertarla
            EnvelopeController envUnderRay = GetTargetedEnvelope(ray);
            if (envUnderRay != null && sc != null && !sc.IsOriginal && !envUnderRay.IsSealed)
            {
                if (envUnderRay.TryInsertCopy(sc))
                {
                    if (heldCollider != null) heldCollider.enabled = true;
                    heldObject = null;
                    heldRigidbody = null;
                    heldCollider = null;
                    SetPromptText($"✓ Copia introducida en el sobre ({envUnderRay.CurrentCopiesCount}/{envUnderRay.TargetOrderCopies}).");
                    return;
                }
            }

            // 3. Si soltamos el dibujo apuntando a la impresora, insertarlo en el escáner
            PrinterController printerUnderRay = GetTargetedPrinter(ray);
            if (printerUnderRay != null && sc != null)
            {
                if (heldCollider != null) heldCollider.enabled = true;
                heldObject = null;
                heldRigidbody = null;
                heldCollider = null;
                printerUnderRay.InsertDrawing(sc);
                return;
            }

            // 4. Si soltamos apuntando a la caja de envíos
            SendProduct sendUnderRay = GetTargetedSendZone(ray);
            if (sendUnderRay != null)
            {
                GameObject delivered = heldObject.gameObject;
                if (heldCollider != null) heldCollider.enabled = true;
                heldObject = null;
                heldRigidbody = null;
                heldCollider = null;
                sendUnderRay.ProcessDeliveredObject(delivered);
                return;
            }

            // Detectar la superficie apuntada (mesa, atril, suelo) para apoyar el objeto
            RaycastHit hit;
            if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit, interactionRange, ~0, QueryTriggerInteraction.Ignore))
            {
                heldObject.position = hit.point + hit.normal * 0.025f;
                heldObject.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            }
            else
            {
                heldObject.position = playerCamera.transform.position + playerCamera.transform.forward * 1.5f;
            }

            // Asegurar que el dibujo tenga un BoxCollider adecuado para tomarlo sin fallar
            BoxCollider bc = heldObject.GetComponent<BoxCollider>();
            if (bc == null)
            {
                bc = heldObject.gameObject.AddComponent<BoxCollider>();
                bc.size = new Vector3(10f, 2f, 10f);
            }
            bc.enabled = true;

            if (heldCollider != null)
            {
                heldCollider.enabled = true;
            }

            if (heldRigidbody != null)
            {
                heldRigidbody.isKinematic = true; // Estable sobre la superficie, sin caer al vacío
            }

            heldObject = null;
            heldRigidbody = null;
            heldCollider = null;

            SetPromptText("✓ Objeto colocado en la superficie. Pulsa <b>[Clic]</b> o <b>[E]</b> para volver a tomarlo.");
        }

        private void UpdateHeldObjectPosition()
        {
            if (heldObject == null || playerCamera == null) return;

            Vector3 targetPosition = playerCamera.transform.position
                + playerCamera.transform.forward * holdOffset.z
                + playerCamera.transform.right * holdOffset.x
                + playerCamera.transform.up * holdOffset.y;

            // La cara ilustrada del dibujo (+Y local) mira directamente hacia la cámara del jugador
            Quaternion targetRotation = Quaternion.LookRotation(playerCamera.transform.forward, playerCamera.transform.up) * Quaternion.Euler(90, 180, 0);

            heldObject.position = Vector3.Lerp(heldObject.position, targetPosition, Time.deltaTime * carryLerpSpeed);
            heldObject.rotation = Quaternion.Slerp(heldObject.rotation, targetRotation, Time.deltaTime * carryLerpSpeed);
        }

        private void CreateCrosshairUI()
        {
            // Eliminar instancias duplicadas que hayan quedado en escena
            var existingCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            for (int i = 0; i < existingCanvases.Length; i++)
            {
                if (existingCanvases[i] != null && existingCanvases[i].gameObject.name == "PlayerCrosshairHUD")
                {
                    Destroy(existingCanvases[i].gameObject);
                }
            }

            crosshairCanvas = new GameObject("PlayerCrosshairHUD", typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = crosshairCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950;

            // Punto de mira central
            GameObject dotObj = new GameObject("ReticleDot", typeof(RectTransform), typeof(Image));
            dotObj.transform.SetParent(crosshairCanvas.transform, false);
            RectTransform dotRt = dotObj.GetComponent<RectTransform>();
            dotRt.sizeDelta = new Vector2(8, 8);
            dotRt.anchorMin = new Vector2(0.5f, 0.5f);
            dotRt.anchorMax = new Vector2(0.5f, 0.5f);

            reticleDot = dotObj.GetComponent<Image>();
            reticleDot.color = Color.white;

            // Texto de ayuda para interacción con objetos (UBICADO ARRIBA PARA NO TAPAR LA MIRA)
            GameObject promptObj = new GameObject("InteractionPromptText", typeof(RectTransform), typeof(Text));
            promptObj.transform.SetParent(crosshairCanvas.transform, false);
            RectTransform pRt = promptObj.GetComponent<RectTransform>();
            pRt.sizeDelta = new Vector2(850, 60);
            pRt.anchorMin = new Vector2(0.5f, 0.88f);
            pRt.anchorMax = new Vector2(0.5f, 0.88f);
            pRt.pivot = new Vector2(0.5f, 0.5f);
            pRt.anchoredPosition = Vector2.zero;

            interactionPromptText = promptObj.GetComponent<Text>();
            interactionPromptText.font = UIFontHelper.GetDefaultFont();
            interactionPromptText.fontSize = 16;
            interactionPromptText.fontStyle = FontStyle.Bold;
            interactionPromptText.alignment = TextAnchor.MiddleCenter;
            interactionPromptText.color = Color.white;

            Outline outline = promptObj.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // Texto de ayuda para modo cámara (UBICADO ABAJO PARA NO TAPAR EL VISOR)
            GameObject camPromptObj = new GameObject("CameraPromptText", typeof(RectTransform), typeof(Text));
            camPromptObj.transform.SetParent(crosshairCanvas.transform, false);
            RectTransform cpRt = camPromptObj.GetComponent<RectTransform>();
            cpRt.sizeDelta = new Vector2(850, 50);
            cpRt.anchorMin = new Vector2(0.5f, 0.07f);
            cpRt.anchorMax = new Vector2(0.5f, 0.07f);
            cpRt.pivot = new Vector2(0.5f, 0.5f);
            cpRt.anchoredPosition = Vector2.zero;

            cameraPromptText = camPromptObj.GetComponent<Text>();
            cameraPromptText.font = UIFontHelper.GetDefaultFont();
            cameraPromptText.fontSize = 14;
            cameraPromptText.fontStyle = FontStyle.Bold;
            cameraPromptText.alignment = TextAnchor.MiddleCenter;
            cameraPromptText.color = new Color(0.95f, 0.95f, 0.95f);

            Outline camOutline = camPromptObj.AddComponent<Outline>();
            camOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            camOutline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private void SetPromptText(string msg)
        {
            if (interactionPromptText != null)
                interactionPromptText.text = msg;
            if (cameraPromptText != null && !string.IsNullOrEmpty(msg))
                cameraPromptText.text = "";
        }

        private void SetCameraPromptText(string msg)
        {
            if (cameraPromptText != null)
                cameraPromptText.text = msg;
            if (interactionPromptText != null && !string.IsNullOrEmpty(msg))
                interactionPromptText.text = "";
        }

        private void SetReticleColor(Color c)
        {
            if (reticleDot != null)
                reticleDot.color = c;
        }
    }
}
