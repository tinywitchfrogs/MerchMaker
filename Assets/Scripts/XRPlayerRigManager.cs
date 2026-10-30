using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace ArtSimulator
{
    /// <summary>
    /// Administrador de Realidad Virtual e integración con el ecosistema XR Interaction Toolkit (XRI 3.6.1).
    /// Detecta visores VR y habilita el XR Origin (XR Rig) con mandos, manos, agarres físicos y simulador.
    /// Si no se usa VR, mantiene la compatibilidad fluida con el modo PC en primera persona.
    /// </summary>
    public class XRPlayerRigManager : MonoBehaviour
    {
        public static XRPlayerRigManager Instance { get; private set; }

        [Header("Configuración de Realidad Virtual")]
        [Tooltip("Activa el modo VR y el XR Interaction Simulator al probar en el Editor de Unity")]
        [SerializeField] private bool enableVRInEditor = true;

        [Tooltip("Fuerza el uso exclusivo de teclado y ratón en primera persona (ignora VR)")]
        [SerializeField] private bool forceDesktopMode = false;

        [Header("Prefabs de XR Interaction Toolkit")]
        [SerializeField] private GameObject xrOriginPrefab;
        [SerializeField] private GameObject xrSimulatorPrefab;
        [SerializeField] private InputActionAsset defaultInputActions;

        private GameObject spawnedXROrigin;
        private GameObject spawnedSimulator;
        private bool isVRActive = false;
        public bool IsVRActive => isVRActive;

        private const string XR_ORIGIN_PREFAB_PATH = "Assets/Samples/XR Interaction Toolkit/3.6.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        private const string XR_SIMULATOR_PREFAB_PATH = "Assets/Samples/XR Interaction Toolkit/3.6.1/XR Interaction Simulator/XR Interaction Simulator.prefab";
        private const string XR_INPUT_ACTIONS_PATH = "Assets/Samples/XR Interaction Toolkit/3.6.1/Starter Assets/XRI Default Input Actions.inputactions";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // Cargar prefabs de Starter Assets si no están asignados en el Inspector
            if (xrOriginPrefab == null)
            {
#if UNITY_EDITOR
                xrOriginPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(XR_ORIGIN_PREFAB_PATH);
#endif
            }

            if (xrSimulatorPrefab == null)
            {
#if UNITY_EDITOR
                xrSimulatorPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(XR_SIMULATOR_PREFAB_PATH);
#endif
            }

            if (defaultInputActions == null)
            {
#if UNITY_EDITOR
                defaultInputActions = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(XR_INPUT_ACTIONS_PATH);
#endif
            }

            DetermineAndSetupVR();
        }

        private void OnEnable()
        {
            UnityEngine.XR.InputDevices.deviceConnected += OnDeviceConnected;
            UnityEngine.XR.InputDevices.deviceDisconnected += OnDeviceDisconnected;
        }

        private void OnDisable()
        {
            UnityEngine.XR.InputDevices.deviceConnected -= OnDeviceConnected;
            UnityEngine.XR.InputDevices.deviceDisconnected -= OnDeviceDisconnected;
        }

        private void OnDeviceConnected(UnityEngine.XR.InputDevice device)
        {
            if ((device.characteristics & InputDeviceCharacteristics.HeadMounted) != 0)
            {
                Debug.Log($"<color=green>[XR Manager]</color> Visor VR conectado: {device.name}. Activando modo VR...");
                if (!forceDesktopMode && !isVRActive)
                {
                    EnableVRRig();
                }
            }
        }

        private void OnDeviceDisconnected(UnityEngine.XR.InputDevice device)
        {
            if ((device.characteristics & InputDeviceCharacteristics.HeadMounted) != 0)
            {
                Debug.Log($"<color=yellow>[XR Manager]</color> Visor VR desconectado: {device.name}.");
            }
        }

        private void Start()
        {
            SetupVRInteractables();
            SpawnVRPenOnDesk();

            if (TabletManager.Instance != null)
            {
                TabletManager.Instance.OnTabletOpenStateChanged -= OnTabletVisibilityChanged;
                TabletManager.Instance.OnTabletOpenStateChanged += OnTabletVisibilityChanged;
            }

            StartCoroutine(DelayedHMDCheck());
        }

        private IEnumerator DelayedHMDCheck()
        {
            // Margen para que OpenXR termine el handshake con visores conectados por cable/Link/SteamVR
            yield return new WaitForSeconds(1.2f);
            if (!forceDesktopMode && !isVRActive && CheckPhysicalVRDevice())
            {
                Debug.Log("<color=green>[XR Manager]</color> Visor VR físico verificado tras arranque. Activando modo VR...");
                EnableVRRig();
            }
        }

        private void OnDestroy()
        {
            if (TabletManager.Instance != null)
            {
                TabletManager.Instance.OnTabletOpenStateChanged -= OnTabletVisibilityChanged;
            }
        }

        private void Update()
        {
            // Tecla de acceso rápido F12 para alternar entre modo VR (Simulador) y modo Desktop
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f12Key.wasPressedThisFrame)
            {
                ToggleVRMode();
            }
#else
            if (Input.GetKeyDown(KeyCode.F12))
            {
                ToggleVRMode();
            }
#endif
        }

        public void ToggleVRMode()
        {
            if (isVRActive)
            {
                forceDesktopMode = true;
                EnableDesktopRig();
            }
            else
            {
                forceDesktopMode = false;
                enableVRInEditor = true;
                EnableVRRig();
            }
            Debug.Log($"<color=yellow>[XR Manager]</color> Modo alternado. VR Activo: {isVRActive}, Forzar Desktop: {forceDesktopMode}");
        }

        public bool CheckPhysicalVRDevice()
        {
            // En Android (Meta Quest), siempre es un visor VR físico
            if (Application.platform == RuntimePlatform.Android) return true;

            // 1. Verificar si hay un visor físico conectado (HMD) detectado por InputDevices
            var inputDevices = new List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, inputDevices);
            if (inputDevices.Count > 0)
            {
                for (int i = 0; i < inputDevices.Count; i++)
                {
                    if (inputDevices[i].isValid) return true;
                }
            }

            // 2. Verificar subsistemas XR activos en ejecución
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            for (int i = 0; i < displays.Count; i++)
            {
                if (displays[i].running) return true;
            }

            // 3. Fallback legado
            return XRSettings.isDeviceActive;
        }

        private void DetermineAndSetupVR()
        {
            bool isMobileVR = Application.platform == RuntimePlatform.Android;
            bool hasPhysicalHMD = CheckPhysicalVRDevice();
            bool shouldEnableVR = !forceDesktopMode && (isMobileVR || hasPhysicalHMD || (Application.isEditor && enableVRInEditor));

            if (shouldEnableVR)
            {
                EnableVRRig();
            }
            else
            {
                EnableDesktopRig();
            }
        }

        public void EnableVRRig()
        {
            isVRActive = true;

            // 1. Instanciar o activar XR Origin (XR Rig)
            if (spawnedXROrigin == null)
            {
                var existingOrigins = FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include);
                if (existingOrigins.Length > 0)
                {
                    spawnedXROrigin = existingOrigins[0].gameObject;
                    for (int i = 1; i < existingOrigins.Length; i++)
                    {
                        Destroy(existingOrigins[i].gameObject);
                    }
                }
                else
                {
                    var foundByName = GameObject.Find("XR Origin (VR Rig)");
                    if (foundByName != null) spawnedXROrigin = foundByName;
                }
            }

            if (spawnedXROrigin == null)
            {
                if (xrOriginPrefab == null)
                {
#if UNITY_EDITOR
                    xrOriginPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(XR_ORIGIN_PREFAB_PATH);
#endif
                }

                if (xrOriginPrefab != null)
                {
                    var playerCtrl = FirstPersonPlayerController.Instance != null ? FirstPersonPlayerController.Instance : FindAnyObjectByType<FirstPersonPlayerController>();
                    Vector3 spawnPos = new Vector3(5.20f, 0.57f, -8.00f);
                    if (playerCtrl != null)
                    {
                        spawnPos = playerCtrl.transform.position;
                    }

                    // Alinear la base del XR Origin exactamente con la superficie física del piso
                    if (Physics.Raycast(new Vector3(spawnPos.x, spawnPos.y + 1.5f, spawnPos.z), Vector3.down, out RaycastHit floorHit, 15f))
                    {
                        spawnPos = floorHit.point;
                    }
                    else
                    {
                        spawnPos.y = -1.15f;
                    }

                    Quaternion spawnRot = Quaternion.identity;
                    spawnedXROrigin = Instantiate(xrOriginPrefab, spawnPos, spawnRot);
                    spawnedXROrigin.name = "XR Origin (VR Rig)";

                    // Configurar CharacterController para movimiento ergonómico y colisiones
                    var cc = spawnedXROrigin.GetComponent<CharacterController>();
                    if (cc != null)
                    {
                        cc.height = 1.75f;
                        cc.radius = 0.30f;
                        cc.center = new Vector3(0f, 0.875f, 0f);
                        cc.skinWidth = 0.04f;
                        cc.stepOffset = 0.35f;
                        cc.slopeLimit = 45f;
                    }

                    // Desactivar JumpProvider en VR (según requerimiento de diseño: no saltar en el juego)
                    Transform jumpTransform = spawnedXROrigin.transform.Find("Locomotion/Jump");
                    if (jumpTransform != null)
                    {
                        jumpTransform.gameObject.SetActive(false);
                    }
                }
                else
                {
                    Debug.LogWarning("[XR Manager] No se encontró el prefab de XR Origin.");
                }
            }

            if (spawnedXROrigin != null)
            {
                spawnedXROrigin.SetActive(true);

                // Integrar XREditorLocomotionHelper para movimiento fluido con WASD + ratón en Editor y VR
                if (spawnedXROrigin.GetComponent<XREditorLocomotionHelper>() == null)
                {
                    spawnedXROrigin.AddComponent<XREditorLocomotionHelper>();
                }

                // Configurar InputActionManager para activar automáticamente todos los mapas de acción de VR
                var iam = spawnedXROrigin.GetComponent<InputActionManager>();
                if (iam == null)
                {
                    iam = spawnedXROrigin.AddComponent<InputActionManager>();
                }
                if (defaultInputActions == null)
                {
#if UNITY_EDITOR
                    defaultInputActions = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(XR_INPUT_ACTIONS_PATH);
#endif
                }
                if (defaultInputActions != null)
                {
                    iam.actionAssets = new List<InputActionAsset> { defaultInputActions };
                    iam.EnableInput();
                }
            }

            // 2. Instanciar Simulador SOLO en el Editor cuando no hay visor fisico conectado
            if (Application.isEditor && !CheckPhysicalVRDevice())
            {
                if (spawnedSimulator == null)
                {
                    var existingSims = FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRInteractionSimulator>(FindObjectsInactive.Include);
                    if (existingSims.Length > 0)
                    {
                        spawnedSimulator = existingSims[0].gameObject;
                        for (int i = 1; i < existingSims.Length; i++)
                        {
                            Destroy(existingSims[i].gameObject);
                        }
                    }
                    else
                    {
                        var foundSimByName = GameObject.Find("XR Interaction Simulator");
                        if (foundSimByName != null) spawnedSimulator = foundSimByName;
                    }
                }

                if (spawnedSimulator == null && xrSimulatorPrefab == null)
                {
#if UNITY_EDITOR
                    xrSimulatorPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(XR_SIMULATOR_PREFAB_PATH);
#endif
                }

                if (spawnedSimulator == null && xrSimulatorPrefab != null)
                {
                    spawnedSimulator = Instantiate(xrSimulatorPrefab);
                    spawnedSimulator.name = "XR Interaction Simulator";
                }

                if (spawnedSimulator != null)
                {
                    spawnedSimulator.SetActive(true);
                }
            }
            else
            {
                // En visor real (Meta Quest) o Android, destruir cualquier simulador de editor remanente
                var existingSims = FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRInteractionSimulator>(FindObjectsInactive.Include);
                for (int i = 0; i < existingSims.Length; i++)
                {
                    if (existingSims[i] != null)
                    {
                        existingSims[i].gameObject.SetActive(false);
                        Destroy(existingSims[i].gameObject);
                    }
                }
            }

            // 3. Identificar la camara de Realidad Virtual del XR Origin
            Camera vrCam = null;
            if (spawnedXROrigin != null)
            {
                vrCam = spawnedXROrigin.GetComponentInChildren<Camera>(true);
            }

            // 4. Desactivar de forma exhaustiva TODAS las demas camaras y AudioListeners
            var allCameras = FindObjectsByType<Camera>(FindObjectsInactive.Include);
            for (int i = 0; i < allCameras.Length; i++)
            {
                Camera c = allCameras[i];
                if (c != null && c != vrCam && (spawnedXROrigin == null || !c.transform.IsChildOf(spawnedXROrigin.transform)))
                {
                    c.enabled = false;
                    c.tag = "Untagged";
                    var al = c.GetComponent<AudioListener>();
                    if (al != null) al.enabled = false;

                    var fpc = c.GetComponent<FirstPersonPlayerController>();
                    if (fpc != null) fpc.enabled = false;

                    if (Application.platform == RuntimePlatform.Android)
                    {
                        c.gameObject.SetActive(false);
                    }
                }
            }

            // Ocultar cualquier HUD 2D residual de escritorio en VR
            var allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            for (int i = 0; i < allCanvases.Length; i++)
            {
                if (allCanvases[i] != null && allCanvases[i].gameObject.name == "PlayerCrosshairHUD")
                {
                    allCanvases[i].gameObject.SetActive(false);
                }
            }

            // Activar la camara VR como la unica MainCamera oficial del juego
            if (vrCam != null)
            {
                vrCam.tag = "MainCamera";
                vrCam.enabled = true;
                AudioListener vrAudio = vrCam.GetComponent<AudioListener>();
                if (vrAudio != null) vrAudio.enabled = true;
            }

            // 5. Adaptar EventSystem para Realidad Virtual con XRUIInputModule
            ConfigureEventSystemForVR();

            // 6. Configurar objetos interactivos
            SetupVRInteractables();

            Debug.Log("<color=green>[XR Manager]</color> <b>Modo Realidad Virtual (VR) ACTIVADO</b>. XR Origin configurado en la oficina.");
        }

        public void EnableDesktopRig()
        {
            isVRActive = false;

            if (spawnedXROrigin != null)
            {
                Camera vrCam = spawnedXROrigin.GetComponentInChildren<Camera>();
                if (vrCam != null)
                {
                    vrCam.tag = "Untagged";
                    vrCam.enabled = false;
                }
                spawnedXROrigin.SetActive(false);
            }
            var allOrigins = FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include);
            for (int i = 0; i < allOrigins.Length; i++)
            {
                allOrigins[i].gameObject.SetActive(false);
            }

            if (spawnedSimulator != null)
            {
                spawnedSimulator.SetActive(false);
            }
            var allSims = FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRInteractionSimulator>(FindObjectsInactive.Include);
            for (int i = 0; i < allSims.Length; i++)
            {
                allSims[i].gameObject.SetActive(false);
            }

            // Restaurar EventSystem para Desktop (activar InputSystemUIInputModule y desactivar XRUIInputModule)
            EventSystem es = FindAnyObjectByType<EventSystem>();
            if (es != null)
            {
                var xrModule = es.GetComponent<XRUIInputModule>();
                if (xrModule != null) xrModule.enabled = false;

#if ENABLE_INPUT_SYSTEM
                var inputSystemModule = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                if (inputSystemModule != null) inputSystemModule.enabled = true;
#endif
                var standalone = es.GetComponent<StandaloneInputModule>();
                if (standalone != null) standalone.enabled = true;
            }

            // Asegurar que la tableta no bloquee el movimiento de inicio en modo escritorio
            if (TabletManager.Instance != null)
            {
                TabletManager.Instance.CloseTablet();
            }

            // Reactivar jugador de escritorio
            var currentDesktopPlayer = FirstPersonPlayerController.Instance != null ? FirstPersonPlayerController.Instance : FindAnyObjectByType<FirstPersonPlayerController>();
            if (currentDesktopPlayer != null)
            {
                Camera desktopCam = currentDesktopPlayer.GetComponent<Camera>();
                if (desktopCam != null)
                {
                    desktopCam.tag = "MainCamera";
                    desktopCam.enabled = true;
                }
                AudioListener desktopAudio = currentDesktopPlayer.GetComponent<AudioListener>();
                if (desktopAudio != null)
                {
                    desktopAudio.enabled = true;
                }
                currentDesktopPlayer.enabled = true;
                currentDesktopPlayer.SetCursorLock(true);
                currentDesktopPlayer.SetCrosshairVisible(true);
            }
            else
            {
                var allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
                for (int i = 0; i < allCanvases.Length; i++)
                {
                    if (allCanvases[i] != null && allCanvases[i].gameObject.name == "PlayerCrosshairHUD")
                    {
                        allCanvases[i].gameObject.SetActive(true);
                    }
                }
            }

            Debug.Log("<color=cyan>[XR Manager]</color> <b>Modo Escritorio (Teclado/Ratón) ACTIVADO</b>.");
        }

        private void ConfigureEventSystemForVR()
        {
            EventSystem es = FindAnyObjectByType<EventSystem>();
            if (es != null)
            {
#if ENABLE_INPUT_SYSTEM
                var inputSystemModule = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                if (inputSystemModule != null) inputSystemModule.enabled = false;
#endif
                var standalone = es.GetComponent<StandaloneInputModule>();
                if (standalone != null) standalone.enabled = false;

                XRUIInputModule xrModule = es.GetComponent<XRUIInputModule>();
                if (xrModule == null)
                {
                    xrModule = es.gameObject.AddComponent<XRUIInputModule>();
                }
                xrModule.enabled = true;
            }
        }

        /// <summary>
        /// Configura los componentes XRGrabInteractable en todos los objetos de la oficina
        /// para que puedan ser agarrados directamente con las manos o el rayo en VR.
        /// </summary>
        public void SetupVRInteractables()
        {
            // 1. Dibujos y lienzos
            var drawings = FindObjectsByType<ScannableDrawing>();
            for (int i = 0; i < drawings.Length; i++)
            {
                ConfigureDrawingForVR(drawings[i].gameObject);
            }

            // 2. Tableta física interactiva para sostener con la mano en VR
            GameObject tabletaObj = GameObject.Find("Tableta");
            if (tabletaObj != null)
            {
                ConfigureTabletForVR(tabletaObj);
            }

            // 3. Dispensador de hojas
            if (PaperDispenser.Instance != null)
            {
                var dispInt = PaperDispenser.Instance.GetComponent<XRSimpleInteractable>();
                if (dispInt == null) dispInt = PaperDispenser.Instance.gameObject.AddComponent<XRSimpleInteractable>();

                dispInt.selectEntered.RemoveListener(OnDispenserSelected);
                dispInt.selectEntered.AddListener(OnDispenserSelected);
            }
        }

        public void ConfigureTabletForVR(GameObject tabletObj)
        {
            if (tabletObj == null) return;

            // 1. Remover interactuable simple previo
            var simpleInt = tabletObj.GetComponent<XRSimpleInteractable>();
            if (simpleInt != null) Destroy(simpleInt);

            // 2. Asegurar Rigidbody para física y agarre con la mano
            var rb = tabletObj.GetComponent<Rigidbody>();
            if (rb == null) rb = tabletObj.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.isKinematic = true; // Estable sobre la mesa al iniciar
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // 3. Asegurar BoxCollider perfectamente alineado a la malla física de la tableta
            var bc = tabletObj.GetComponent<BoxCollider>();
            if (bc == null) bc = tabletObj.AddComponent<BoxCollider>();
            bc.center = new Vector3(0.09f, 5.87f, -0.43f);
            bc.size = new Vector3(6.5f, 10.5f, 1.2f);
            bc.enabled = true;

            // 4. Configurar XRGrabInteractable para sostener con la mano y rayo
            var grab = tabletObj.GetComponent<XRGrabInteractable>();
            if (grab == null) grab = tabletObj.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = false;

            // Registrar explícitamente el BoxCollider en los colisionadores de agarre del interactuable
            if (!grab.colliders.Contains(bc))
            {
                grab.colliders.Clear();
                grab.colliders.Add(bc);
            }

            // Punto de agarre ergonómico centrado para sostener con naturalidad
            Transform attachPoint = tabletObj.transform.Find("VRTabletAttachPoint");
            if (attachPoint == null)
            {
                GameObject ap = new GameObject("VRTabletAttachPoint");
                ap.transform.SetParent(tabletObj.transform, false);
                attachPoint = ap.transform;
            }
            attachPoint.localPosition = new Vector3(0.09f, 5.87f, -0.43f);
            attachPoint.localRotation = Quaternion.Euler(0f, 0f, -90f); // Alinea el borde superior con el vector arriba del mando VR
            grab.attachTransform = attachPoint;

            grab.selectEntered.RemoveListener(OnTabletGrabbed);
            grab.selectEntered.AddListener(OnTabletGrabbed);
            grab.selectExited.RemoveListener(OnTabletReleased);
            grab.selectExited.AddListener(OnTabletReleased);

            // 5. Montar el lienzo UI directamente sobre la pantalla física 3D de la tableta
            MountTabletCanvasOnScreen(tabletObj);
            Debug.Log("<color=green>[XR Manager]</color> Tableta configurada como objeto físico para sostener con la mano.");
        }

        private void OnTabletGrabbed(SelectEnterEventArgs args)
        {
            var rb = args.interactableObject.transform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }

            if (TabletManager.Instance != null && !TabletManager.Instance.IsTabletOpen)
            {
                TabletManager.Instance.OpenTablet();
            }
            Debug.Log("<color=green>[XR Manager]</color> Tableta agarrada y sostenida en la mano VR.");
        }

        private void OnTabletReleased(SelectExitEventArgs args)
        {
            Debug.Log("<color=cyan>[XR Manager]</color> Tableta soltada de la mano VR.");
        }

        public void MountTabletCanvasOnScreen(GameObject tabletObj)
        {
            if (tabletObj == null) return;
            Transform pantalla = tabletObj.transform.Find("ModeloTableta/Pantalla");
            if (pantalla == null) return;

            GameObject canvasObj = GameObject.Find("TabletCanvas");
            if (canvasObj == null)
            {
                var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i].gameObject.name == "TabletCanvas")
                    {
                        canvasObj = canvases[i].gameObject;
                        break;
                    }
                }
            }
            if (canvasObj == null) return;

            Canvas canvas = canvasObj.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                if (spawnedXROrigin != null)
                {
                    Camera vrCam = spawnedXROrigin.GetComponentInChildren<Camera>();
                    if (vrCam != null) canvas.worldCamera = vrCam;
                }
                else
                {
                    canvas.worldCamera = Camera.main;
                }
            }

            // Anclar directamente al frente de la pantalla física 3D orientada al derecho
            canvasObj.transform.SetParent(pantalla, false);
            canvasObj.transform.localPosition = new Vector3(0, 0, 0.12f);
            canvasObj.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);

            RectTransform rt = canvasObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(1600, 1000);
                rt.localScale = new Vector3(-9.32f / 1600f, 5.53f / 1000f, 1f);
            }

            // En VR vemos el mundo real detrás de la tableta: ocultar Backdrop negro de PC
            Transform backdrop = canvasObj.transform.Find("Backdrop");
            if (backdrop != null) backdrop.gameObject.SetActive(false);

            // Ajustar el marco interactivo para que coincida exactamente con la pantalla
            RectTransform frame = canvasObj.transform.Find("TabletDeviceFrame")?.GetComponent<RectTransform>();
            if (frame != null)
            {
                frame.anchorMin = Vector2.zero;
                frame.anchorMax = Vector2.one;
                frame.offsetMin = Vector2.zero;
                frame.offsetMax = Vector2.zero;
            }

            // Asegurar raycaster de realidad virtual sin descarte de gráficos invertidos
            var raycaster = canvasObj.GetComponent<TrackedDeviceGraphicRaycaster>();
            if (raycaster == null) raycaster = canvasObj.AddComponent<TrackedDeviceGraphicRaycaster>();
            if (raycaster != null) raycaster.ignoreReversedGraphics = false;

            // Asegurar GraphicRaycaster estándar para clics con ratón en Editor
            var gr = canvasObj.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (gr != null)
            {
                gr.ignoreReversedGraphics = false;
                gr.blockingObjects = UnityEngine.UI.GraphicRaycaster.BlockingObjects.None;
            }

            canvasObj.SetActive(true);

            // En Realidad Virtual la tableta permanece siempre encendida mostrando la pantalla de inicio
            if (isVRActive && TabletManager.Instance != null)
            {
                TabletManager.Instance.OpenTablet();
                TabletManager.Instance.ShowHome();
            }
        }

        private void OnDispenserSelected(SelectEnterEventArgs args)
        {
            if (PaperDispenser.Instance != null)
            {
                ScannableDrawing newPaper = PaperDispenser.Instance.DispenseNewPaper();
                if (newPaper != null)
                {
                    ConfigureDrawingForVR(newPaper.gameObject);
                }
            }
        }

        public void ConfigureDrawingForVR(GameObject drawingObj)
        {
            if (drawingObj == null) return;

            var rb = drawingObj.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = drawingObj.AddComponent<Rigidbody>();
                rb.mass = 0.1f;
                rb.isKinematic = true; // Estable sobre la mesa al iniciar
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            var col = drawingObj.GetComponent<Collider>();
            if (col == null)
            {
                var bc = drawingObj.AddComponent<BoxCollider>();
                bc.size = new Vector3(10f, 0.5f, 10f);
            }

            var grab = drawingObj.GetComponent<XRGrabInteractable>();
            if (grab == null)
            {
                grab = drawingObj.AddComponent<XRGrabInteractable>();
                grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
                grab.throwOnDetach = true;
            }
        }

        private void ConfigureTabletCanvasForVR()
        {
            GameObject tabletaObj = GameObject.Find("Tableta");
            if (tabletaObj != null)
            {
                MountTabletCanvasOnScreen(tabletaObj);
            }
        }

        private void OnTabletVisibilityChanged(bool isOpen)
        {
            GameObject canvasObj = GameObject.Find("TabletCanvas");
            if (canvasObj == null)
            {
                var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i].gameObject.name == "TabletCanvas") { canvasObj = canvases[i].gameObject; break; }
                }
            }
            if (canvasObj == null) return;

            // En Realidad Virtual, la pantalla SIEMPRE permanece fijada a la tableta física en la mano
            GameObject tabletaObj = GameObject.Find("Tableta");
            if (tabletaObj != null)
            {
                MountTabletCanvasOnScreen(tabletaObj);
            }

            // En Realidad Virtual, la pantalla física 3D nunca se apaga (siempre encendida en la oficina)
            canvasObj.SetActive(isVRActive ? true : isOpen);
        }

        private void SpawnVRPenOnDesk()
        {
            if (GameObject.Find("VRPen_Stylus") != null) return;

            // Crear un lápiz 3D ergonómico en la mesa de dibujo
            GameObject penObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            penObj.name = "VRPen_Stylus";
            penObj.transform.position = new Vector3(8.55f, 0.61f, -6.85f);
            penObj.transform.rotation = Quaternion.Euler(0, 45f, 90f);
            penObj.transform.localScale = new Vector3(0.02f, 0.10f, 0.02f);

            var rb = penObj.GetComponent<Rigidbody>();
            if (rb == null) rb = penObj.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var grab = penObj.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;

            // Punta del lápiz interactiva
            GameObject tipObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tipObj.name = "PenTip";
            tipObj.transform.SetParent(penObj.transform, false);
            tipObj.transform.localPosition = new Vector3(0, 1.05f, 0);
            tipObj.transform.localScale = new Vector3(1.2f, 0.3f, 1.2f);

            var tipCol = tipObj.GetComponent<SphereCollider>();
            if (tipCol != null) tipCol.isTrigger = true;

            var penTip = tipObj.AddComponent<VRPenTip>();
            penTip.SetColor(Color.black);
            penTip.SetBrushSize(10);

            // Material del cuerpo del lápiz
            Renderer pr = penObj.GetComponent<Renderer>();
            if (pr != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material mat = new Material(s);
                mat.color = new Color(0.15f, 0.18f, 0.25f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.15f, 0.18f, 0.25f));
                pr.material = mat;
            }

            Debug.Log("<color=green>[XR Manager]</color> Lápiz físico 3D para dibujo en Realidad Virtual configurado en el escritorio.");
        }
    }
}
