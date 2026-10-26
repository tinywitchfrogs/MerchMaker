using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

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

        private GameObject spawnedXROrigin;
        private GameObject spawnedSimulator;
        private bool isVRActive = false;
        public bool IsVRActive => isVRActive;

        private const string XR_ORIGIN_PREFAB_PATH = "Assets/Samples/XR Interaction Toolkit/3.6.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        private const string XR_SIMULATOR_PREFAB_PATH = "Assets/Samples/XR Interaction Toolkit/3.6.1/XR Interaction Simulator/XR Interaction Simulator.prefab";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
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

            DetermineAndSetupVR();
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
            if (Input.GetKeyDown(KeyCode.F12))
            {
                ToggleVRMode();
            }
        }

        public void ToggleVRMode()
        {
            forceDesktopMode = !forceDesktopMode;
            Debug.Log($"<color=yellow>[XR Manager]</color> Alternando modo VR. Forzar Desktop: {forceDesktopMode}");
            DetermineAndSetupVR();
        }

        private void DetermineAndSetupVR()
        {
            bool hasPhysicalHMD = XRSettings.isDeviceActive;
            bool shouldEnableVR = !forceDesktopMode && (hasPhysicalHMD || (Application.isEditor && enableVRInEditor));

            if (shouldEnableVR)
            {
                EnableVRRig();
            }
            else
            {
                EnableDesktopRig();
            }
        }

        private void EnableVRRig()
        {
            isVRActive = true;

            // 1. Instanciar o activar XR Origin (XR Rig)
            if (spawnedXROrigin == null)
            {
                if (xrOriginPrefab != null)
                {
                    Vector3 spawnPos = new Vector3(5.20f, -0.85f, -8.00f);
                    Quaternion spawnRot = Quaternion.identity;
                    spawnedXROrigin = Instantiate(xrOriginPrefab, spawnPos, spawnRot);
                    spawnedXROrigin.name = "XR Origin (VR Rig)";

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
            }

            // 2. Instanciar Simulador si estamos en el Editor sin visor físico conectado
            if (Application.isEditor && !XRSettings.isDeviceActive)
            {
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

            // 3. Desactivar cámara y controlador de primera persona de escritorio
            Camera desktopCam = null;
            var playerCtrl = FirstPersonPlayerController.Instance;
            if (playerCtrl != null)
            {
                desktopCam = playerCtrl.GetComponent<Camera>();
                playerCtrl.enabled = false;
            }

            if (desktopCam != null && desktopCam != Camera.main)
            {
                desktopCam.enabled = false;
            }

            // Asegurar que la cámara del XR Origin sea la MainCamera activa
            if (spawnedXROrigin != null)
            {
                Camera vrCam = spawnedXROrigin.GetComponentInChildren<Camera>();
                if (vrCam != null)
                {
                    vrCam.tag = "MainCamera";
                    vrCam.enabled = true;
                }
            }

            // 4. Adaptar EventSystem para Realidad Virtual con XRUIInputModule
            ConfigureEventSystemForVR();

            Debug.Log("<color=green>[XR Manager]</color> <b>Modo Realidad Virtual (VR) ACTIVADO</b>. XR Origin configurado en la oficina.");
        }

        private void EnableDesktopRig()
        {
            isVRActive = false;

            if (spawnedXROrigin != null)
            {
                spawnedXROrigin.SetActive(false);
            }

            if (spawnedSimulator != null)
            {
                spawnedSimulator.SetActive(false);
            }

            // Reactivar jugador de escritorio
            var playerCtrl = FirstPersonPlayerController.Instance;
            if (playerCtrl != null)
            {
                playerCtrl.enabled = true;
                Camera desktopCam = playerCtrl.GetComponent<Camera>();
                if (desktopCam != null)
                {
                    desktopCam.tag = "MainCamera";
                    desktopCam.enabled = true;
                }
            }

            Debug.Log("<color=cyan>[XR Manager]</color> <b>Modo Escritorio (Teclado/Ratón) ACTIVADO</b>.");
        }

        private void ConfigureEventSystemForVR()
        {
            EventSystem es = FindFirstObjectByType<EventSystem>();
            if (es != null)
            {
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
            var drawings = FindObjectsByType<ScannableDrawing>(FindObjectsSortMode.None);
            for (int i = 0; i < drawings.Length; i++)
            {
                ConfigureDrawingForVR(drawings[i].gameObject);
            }

            // 2. Tableta física
            GameObject tabletaObj = GameObject.Find("Tableta");
            if (tabletaObj != null)
            {
                var simpleInt = tabletaObj.GetComponent<XRSimpleInteractable>();
                if (simpleInt == null) simpleInt = tabletaObj.AddComponent<XRSimpleInteractable>();

                simpleInt.selectEntered.RemoveListener(OnTabletaSelected);
                simpleInt.selectEntered.AddListener(OnTabletaSelected);
            }

            // 3. Dispensador de hojas
            if (PaperDispenser.Instance != null)
            {
                var dispInt = PaperDispenser.Instance.GetComponent<XRSimpleInteractable>();
                if (dispInt == null) dispInt = PaperDispenser.Instance.gameObject.AddComponent<XRSimpleInteractable>();

                dispInt.selectEntered.RemoveListener(OnDispenserSelected);
                dispInt.selectEntered.AddListener(OnDispenserSelected);
            }

            // 4. Asegurar TrackedDeviceGraphicRaycaster en el TabletCanvas
            ConfigureTabletCanvasForVR();
        }

        private void OnTabletaSelected(SelectEnterEventArgs args)
        {
            if (TabletManager.Instance != null)
            {
                TabletManager.Instance.ToggleTablet();
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
            GameObject canvasObj = GameObject.Find("TabletCanvas");
            if (canvasObj != null)
            {
                var raycaster = canvasObj.GetComponent<TrackedDeviceGraphicRaycaster>();
                if (raycaster == null)
                {
                    canvasObj.AddComponent<TrackedDeviceGraphicRaycaster>();
                }
            }
        }

        private void OnTabletVisibilityChanged(bool isOpen)
        {
            GameObject canvasObj = GameObject.Find("TabletCanvas");
            if (canvasObj == null) return;

            Canvas canvas = canvasObj.GetComponent<Canvas>();
            if (canvas == null) return;

            if (isVRActive)
            {
                // En Realidad Virtual, la tableta se posiciona en el espacio 3D flotando frente al jugador
                if (isOpen)
                {
                    canvas.renderMode = RenderMode.WorldSpace;
                    Camera vrCam = Camera.main;
                    if (vrCam != null)
                    {
                        canvasObj.transform.position = vrCam.transform.position + vrCam.transform.forward * 0.95f + Vector3.down * 0.05f;
                        canvasObj.transform.rotation = Quaternion.LookRotation(canvasObj.transform.position - vrCam.transform.position);
                        canvasObj.transform.localScale = Vector3.one * 0.00085f;
                    }
                    canvasObj.SetActive(true);
                }
                else
                {
                    canvasObj.SetActive(false);
                }
            }
            else
            {
                // En modo Desktop se mantiene la pantalla superpuesta original
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.SetActive(isOpen);
            }
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
