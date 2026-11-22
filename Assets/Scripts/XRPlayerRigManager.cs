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
            SetupTableColliders();
            SetupVRInteractables();
            SetupBasurero();
            SetupColoredPens();

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

        private System.Collections.IEnumerator AlignPlayerToDeskRoutine()
        {
            // Esperar 1-2 frames para recibir la orientación real del visor físico en OpenXR
            yield return null;
            yield return new WaitForEndOfFrame();

            if (spawnedXROrigin != null)
            {
                Camera vrCam = spawnedXROrigin.GetComponentInChildren<Camera>();
                // Orientar hacia la mesa principal de trabajo y dibujo (Mesa Dibujo / Mesa1.0 con las hojas y lápices)
                GameObject mesaObj = GameObject.Find("Mesa Dibujo") ?? GameObject.Find("Mesa1.0");
                Vector3 deskCenter = mesaObj != null ? mesaObj.transform.position : new Vector3(5.62f, -0.07f, -0.15f);
                deskCenter.y = -0.07f;

                Vector3 camPos = vrCam != null ? vrCam.transform.position : spawnedXROrigin.transform.position;
                Vector3 toDesk = deskCenter - camPos;
                toDesk.y = 0f;

                if (toDesk.sqrMagnitude > 0.01f)
                {
                    Quaternion desiredHeadRot = Quaternion.LookRotation(toDesk.normalized, Vector3.up);
                    float headYaw = vrCam != null ? vrCam.transform.localEulerAngles.y : 0f;
                    Quaternion headYawRot = Quaternion.Euler(0f, headYaw, 0f);

                    spawnedXROrigin.transform.rotation = desiredHeadRot * Quaternion.Inverse(headYawRot);
                    Debug.Log($"<color=green>[XR Manager]</color> Vista inicial alineada de frente al escritorio de dibujo. HeadYaw={headYaw:F1}°, RigYaw={spawnedXROrigin.transform.eulerAngles.y:F1}°");
                }
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
                    // Ubicar el XR Origin de pie frente a la mesa principal de trabajo y dibujo (Mesa1.0)
                    Vector3 spawnPos = new Vector3(5.62f, -1.17f, -1.05f);
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

                    // Configurar altura promedio de persona adulta y modo de seguimiento de suelo en VR
                    var originComp = spawnedXROrigin.GetComponent<Unity.XR.CoreUtils.XROrigin>();
                    if (originComp != null)
                    {
                        originComp.RequestedTrackingOriginMode = Unity.XR.CoreUtils.XROrigin.TrackingOriginMode.Floor;
                        originComp.CameraYOffset = 1.75f;
                    }
                    Transform camOffset = spawnedXROrigin.transform.Find("Camera Offset");
                    if (camOffset != null)
                    {
                        camOffset.localPosition = new Vector3(0f, 1.75f, 0f);
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
                // Asegurar que el XR Origin esté ubicado justo frente a la mesa principal de trabajo
                spawnedXROrigin.transform.position = new Vector3(5.62f, -1.17f, -1.05f);
                spawnedXROrigin.transform.rotation = Quaternion.identity;
                StartCoroutine(AlignPlayerToDeskRoutine());

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

                // Configurar límites de alcance y desactivar manipulación infinita de distancia
                ConfigureInteractorsAndGrabLimits(spawnedXROrigin);
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

                    // Desactivar siempre el GameObject de DesktopPlayer en modo VR para eliminar su CharacterController
                    c.gameObject.SetActive(false);
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
                vrCam.stereoTargetEye = StereoTargetEyeMask.Both;
                vrCam.allowHDR = false;
                vrCam.allowMSAA = false;

                var uacd = vrCam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (uacd == null)
                {
                    uacd = vrCam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                }
                if (uacd != null)
                {
                    uacd.allowXRRendering = true;
                    uacd.allowHDROutput = false;
                    uacd.renderPostProcessing = false;
                    uacd.renderShadows = true;
                }

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
                // Ubicar al jugador de escritorio de frente a la mesa de dibujo
                currentDesktopPlayer.transform.position = new Vector3(5.62f, 0.82f, -1.05f);
                currentDesktopPlayer.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
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

        public void ConfigureInteractorsAndGrabLimits(GameObject rig)
        {
            if (rig == null) return;

            // 1. Desactivar manipulación infinita de distancia con la palanca del mando (useManipulationInput = false)
            // Esto evita que los objetos sostenidos sean empujados hacia el cielo o al infinito
            var attachControllers = rig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Attachment.InteractionAttachController>(true);
            for (int i = 0; i < attachControllers.Length; i++)
            {
                attachControllers[i].useManipulationInput = false;
            }

            // 2. Limitar distancia máxima del rayo de interacción a 3.5 metros (CurveInteractionCaster)
            var curveCasters = rig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.Casters.CurveInteractionCaster>(true);
            for (int i = 0; i < curveCasters.Length; i++)
            {
                curveCasters[i].castDistance = 3.5f;
            }

            // 3. Limitar distancia máxima de cualquier XRRayInteractor
            var rayInteractors = rig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(true);
            for (int i = 0; i < rayInteractors.Length; i++)
            {
                rayInteractors[i].maxRaycastDistance = 3.5f;
            }

            Debug.Log("<color=green>[XR Manager]</color> Límites de agarre configurados: manipulación infinita desactivada, alcance máximo = 3.5m.");
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

            // 4. Dispensador de sobres
            if (EnvelopeDispenser.Instance != null)
            {
                var envDispInt = EnvelopeDispenser.Instance.GetComponent<XRSimpleInteractable>();
                if (envDispInt == null) envDispInt = EnvelopeDispenser.Instance.gameObject.AddComponent<XRSimpleInteractable>();

                envDispInt.selectEntered.RemoveListener(OnEnvelopeDispenserSelected);
                envDispInt.selectEntered.AddListener(OnEnvelopeDispenserSelected);
            }

            // 5. Sobres de envíos
            var envelopes = FindObjectsByType<EnvelopeController>(FindObjectsInactive.Include);
            for (int i = 0; i < envelopes.Length; i++)
            {
                var grab = envelopes[i].GetComponent<XRGrabInteractable>();
                if (grab == null) grab = envelopes[i].gameObject.AddComponent<XRGrabInteractable>();
                grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
                grab.throwOnDetach = true;
            }

            // 6. Atril de dibujo
            GameObject atrilObj = GameObject.Find("Atril");
            if (atrilObj != null && atrilObj.GetComponent<AtrilController>() == null)
            {
                atrilObj.AddComponent<AtrilController>();
            }

            // 7. Configurar interactores para que las capturas a distancia atraigan los objetos a la mano
            var nearFarInteractors = FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(FindObjectsInactive.Include);
            for (int i = 0; i < nearFarInteractors.Length; i++)
            {
                nearFarInteractors[i].farAttachMode = UnityEngine.XR.Interaction.Toolkit.Attachment.InteractorFarAttachMode.Near;
            }
        }

        public void ConfigureTabletForVR(GameObject tabletObj)
        {
            if (tabletObj == null) return;

            // Escala ergonómica y proporcionada para Realidad Virtual (equivalente a 11-12 pulgadas)
            tabletObj.transform.localScale = new Vector3(0.040f, 0.040f, 0.040f);

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

            // 4. Configurar TabletAmbidextrousGrab para sostener con CUALQUIERA de las dos manos (ambidiestro)
            TabletAmbidextrousGrab grab = tabletObj.GetComponent<TabletAmbidextrousGrab>();
            if (grab == null)
            {
                var oldGrabs = tabletObj.GetComponents<XRGrabInteractable>();
                for (int i = 0; i < oldGrabs.Length; i++)
                {
                    if (!(oldGrabs[i] is TabletAmbidextrousGrab))
                    {
                        if (Application.isPlaying) Destroy(oldGrabs[i]);
                        else DestroyImmediate(oldGrabs[i]);
                    }
                }
                grab = tabletObj.AddComponent<TabletAmbidextrousGrab>();
            }
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = true;
            grab.useDynamicAttach = false;
            grab.matchAttachPosition = false;
            grab.matchAttachRotation = false;
            grab.snapToColliderVolume = false;
            grab.attachEaseInTime = 0.15f;
            grab.selectMode = UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode.Multiple; // Permite pasar la tableta de una mano a otra con total fluidez

            // Registrar explícitamente el BoxCollider en los colisionadores de agarre del interactuable
            if (!grab.colliders.Contains(bc))
            {
                grab.colliders.Clear();
                grab.colliders.Add(bc);
            }

            // Punto de agarre ergonómico MANO IZQUIERDA (marco lateral izquierdo / bezel izquierdo)
            Transform leftAttach = tabletObj.transform.Find("VRTabletLeftAttachPoint");
            if (leftAttach == null)
            {
                GameObject lap = new GameObject("VRTabletLeftAttachPoint");
                lap.transform.SetParent(tabletObj.transform, false);
                leftAttach = lap.transform;
            }
            leftAttach.localPosition = new Vector3(0.08f, 9.90f, -0.43f);
            leftAttach.localRotation = Quaternion.Euler(0f, 20f, -90f);
            grab.LeftHandAttachPoint = leftAttach;

            // Punto de agarre ergonómico MANO DERECHA (marco lateral derecho / bezel derecho)
            Transform rightAttach = tabletObj.transform.Find("VRTabletRightAttachPoint");
            if (rightAttach == null)
            {
                GameObject rap = new GameObject("VRTabletRightAttachPoint");
                rap.transform.SetParent(tabletObj.transform, false);
                rightAttach = rap.transform;
            }
            rightAttach.localPosition = new Vector3(0.08f, 1.70f, -0.43f);
            rightAttach.localRotation = Quaternion.Euler(0f, 20f, -90f);
            grab.RightHandAttachPoint = rightAttach;

            // Anclaje por defecto (mano izquierda sosteniendo el marco para interactuar con la derecha)
            grab.attachTransform = leftAttach;

            // Ignorar colisiones físicas entre la tableta y los dibujos/copias para que no choquen al tomar fotos
            var allDrawings = FindObjectsByType<ScannableDrawing>(FindObjectsInactive.Include);
            for (int i = 0; i < allDrawings.Length; i++)
            {
                var dCol = allDrawings[i].GetComponent<Collider>();
                if (dCol != null && bc != null)
                {
                    Physics.IgnoreCollision(bc, dCol, true);
                }
            }

            grab.selectEntered.RemoveListener(OnTabletGrabbed);
            grab.selectEntered.AddListener(OnTabletGrabbed);
            grab.selectExited.RemoveListener(OnTabletReleased);
            grab.selectExited.AddListener(OnTabletReleased);

            // 5. Montar el lienzo UI directamente sobre la pantalla física 3D de la tableta
            MountTabletCanvasOnScreen(tabletObj);
            Debug.Log("<color=green>[XR Manager]</color> Tableta configurada como objeto físico ambidiestro (sostenible con mano izquierda o derecha).");
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
            var rb = args.interactableObject.transform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
            Debug.Log("<color=cyan>[XR Manager]</color> Tableta soltada de la mano VR y cae con gravedad.");
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

            // Evitar re-montaje redundante si el lienzo ya está anclado a la pantalla de la tableta
            if (canvasObj.transform.parent != pantalla)
            {
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
            }

            canvasObj.SetActive(true);
        }

        private void OnDispenserSelected(SelectEnterEventArgs args)
        {
            if (PaperDispenser.Instance != null)
            {
                ScannableDrawing newPaper = PaperDispenser.Instance.DispenseNewPaper();
                if (newPaper != null)
                {
                    ConfigureDrawingForVR(newPaper.gameObject);

                    // Si el jugador pulsó con su mano o rayo sobre la pila, poner la hoja directamente en su mano
                    var grab = newPaper.GetComponent<XRGrabInteractable>();
                    if (grab != null && args != null && args.manager != null && args.interactorObject != null)
                    {
                        var interactor = args.interactorObject;
                        args.manager.SelectExit(interactor, args.interactableObject);
                        args.manager.SelectEnter(interactor, grab);
                        Debug.Log("<color=green>[XR Manager]</color> Hoja limpia dispensada directamente a la mano VR del jugador.");
                    }
                }
            }
        }

        private void OnEnvelopeDispenserSelected(SelectEnterEventArgs args)
        {
            if (EnvelopeDispenser.Instance != null)
            {
                EnvelopeController newEnv = EnvelopeDispenser.Instance.DispenseNewEnvelope();
                if (newEnv != null)
                {
                    var grab = newEnv.GetComponent<XRGrabInteractable>();
                    if (grab != null && args != null && args.manager != null && args.interactorObject != null)
                    {
                        var interactor = args.interactorObject;
                        args.manager.SelectExit(interactor, args.interactableObject);
                        args.manager.SelectEnter(interactor, grab);
                        Debug.Log("<color=green>[XR Manager]</color> Sobre nuevo dispensado directamente a la mano VR del jugador.");
                    }
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
                col = bc;
            }

            // Ignorar colisión física con la Tableta para que no choquen al acercar la hoja a la cámara
            GameObject tabletaObj = GameObject.Find("Tableta");
            if (tabletaObj != null && col != null)
            {
                var tabCol = tabletaObj.GetComponent<Collider>();
                if (tabCol != null)
                {
                    Physics.IgnoreCollision(col, tabCol, true);
                }
            }

            var grab = drawingObj.GetComponent<XRGrabInteractable>();
            if (grab == null)
            {
                grab = drawingObj.AddComponent<XRGrabInteractable>();
            }
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = true;
            grab.useDynamicAttach = false;
            grab.matchAttachPosition = false;
            grab.matchAttachRotation = false;
            grab.snapToColliderVolume = false;
            grab.attachEaseInTime = 0.15f;

            // Punto de agarre ergonómico en la mano: sujeta la hoja por el borde inferior
            // e inclinada 55° mirando de frente a la vista del jugador y la cámara de la tableta
            Transform attachPoint = drawingObj.transform.Find("DrawingVRHandAttach");
            if (attachPoint == null)
            {
                GameObject ap = new GameObject("DrawingVRHandAttach");
                ap.transform.SetParent(drawingObj.transform, false);
                attachPoint = ap.transform;
            }
            attachPoint.localPosition = new Vector3(0f, 0f, -4.5f);
            attachPoint.localRotation = Quaternion.Euler(-55f, 0f, 0f);
            grab.attachTransform = attachPoint;

            grab.selectExited.RemoveListener(OnDrawingReleased);
            grab.selectExited.AddListener(OnDrawingReleased);
            grab.selectEntered.RemoveListener(OnDrawingGrabbed);
            grab.selectEntered.AddListener(OnDrawingGrabbed);
        }

        private void OnDrawingReleased(SelectExitEventArgs args)
        {
            if (args != null && args.interactableObject != null)
            {
                var rb = args.interactableObject.transform.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                }
            }
        }

        private void OnDrawingGrabbed(SelectEnterEventArgs args)
        {
            if (args != null && args.interactableObject != null)
            {
                var rb = args.interactableObject.transform.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                }
                if (PrinterController.Instance != null && PrinterController.Instance.CurrentInsertedDrawing != null)
                {
                    var scannable = args.interactableObject.transform.GetComponent<ScannableDrawing>();
                    if (scannable != null && PrinterController.Instance.CurrentInsertedDrawing == scannable)
                    {
                        PrinterController.Instance.RetrieveDrawing();
                    }
                }
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

            // En Realidad Virtual, la pantalla física 3D nunca se apaga (siempre encendida en la oficina)
            canvasObj.SetActive(isVRActive ? true : isOpen);
        }

        private void SetupColoredPens()
        {
            // Eliminar el lápiz de prueba anterior si existía
            var oldPen = GameObject.Find("VRPen_Stylus");
            if (oldPen != null) Destroy(oldPen);

            // Asegurar que el ColoredPenManager esté presente en la escena
            if (ColoredPenManager.Instance == null)
            {
                var existing = FindAnyObjectByType<ColoredPenManager>();
                if (existing == null)
                {
                    gameObject.AddComponent<ColoredPenManager>();
                }
            }
        }

        private void SetupTableColliders()
        {
            // 1. Mesa principal de trabajo y dibujo (Mesa Dibujo / Mesa1.0)
            GameObject mesa = GameObject.Find("Mesa Dibujo") ?? GameObject.Find("Mesa1.0");
            if (mesa != null)
            {
                var bc = mesa.GetComponent<BoxCollider>();
                if (bc == null) bc = mesa.AddComponent<BoxCollider>();
                bc.center = new Vector3(0f, 4.30f, 0f);
                bc.size = new Vector3(4.85f, 0.28f, 8.35f);
            }

            // 2. Mesa secundaria de impresora (Mesa Impresora / Mesa1.0 (1))
            GameObject mesa2 = GameObject.Find("Mesa Impresora") ?? GameObject.Find("Mesa1.0 (1)");
            if (mesa2 != null)
            {
                var bc2 = mesa2.GetComponent<BoxCollider>();
                if (bc2 == null) bc2 = mesa2.AddComponent<BoxCollider>();
                bc2.center = new Vector3(-0.01f, 4.30f, 1.97f);
                bc2.size = new Vector3(4.85f, 0.28f, 8.35f);
            }

            // 3. Mesa de envíos y logística (Mesa Envios)
            GameObject mesa3 = GameObject.Find("Mesa Envios");
            if (mesa3 != null)
            {
                var bc3 = mesa3.GetComponent<BoxCollider>();
                if (bc3 == null) bc3 = mesa3.AddComponent<BoxCollider>();
                bc3.center = new Vector3(0f, 4.30f, 0f);
                bc3.size = new Vector3(4.85f, 0.28f, 8.35f);
            }

            // 4. Caja de lapices sobre la mesa
            GameObject caja = GameObject.Find("caja_lapices");
            if (caja != null)
            {
                Transform cube = caja.transform.Find("Cube");
                if (cube != null)
                {
                    var bcCube = cube.GetComponent<BoxCollider>();
                    if (bcCube == null) bcCube = cube.gameObject.AddComponent<BoxCollider>();
                }
            }

            // 5. Ignorar colisión entre el jugador, elementos del taller y la caja de envíos
            SendProduct sendBox = FindAnyObjectByType<SendProduct>();
            if (sendBox != null)
            {
                Collider boxCol = sendBox.GetComponent<Collider>();
                if (boxCol != null)
                {
                    var allCols = FindObjectsByType<Collider>(FindObjectsInactive.Include);
                    for (int i = 0; i < allCols.Length; i++)
                    {
                        string cName = allCols[i].name;
                        if (cName.Contains("Player") || cName.Contains("XR") || cName.Contains("Origin") || cName.ToLower().Contains("impresora") || cName.ToLower().Contains("tijera") || cName.ToLower().Contains("sobre"))
                        {
                            Physics.IgnoreCollision(allCols[i], boxCol, true);
                        }
                    }
                }
            }
        }

        private void SetupBasurero()
        {
            // Configurar el Basurero con su controlador de descarte de artículos no deseados
            GameObject basurero = GameObject.Find("Basurero");
            if (basurero != null)
            {
                var trashCtrl = basurero.GetComponent<TrashCanController>();
                if (trashCtrl == null)
                {
                    trashCtrl = basurero.AddComponent<TrashCanController>();
                }
                trashCtrl.SetupTrashTrigger();
            }
        }
    }
}
