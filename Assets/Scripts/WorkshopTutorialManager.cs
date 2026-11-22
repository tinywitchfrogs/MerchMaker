using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArtSimulator
{
    public enum TutorialStep
    {
        Step1_MountPaperOnEasel = 0,
        Step2_DrawWithPencil = 1,
        Step3_ScanDrawingOnPrinter = 2,
        Step4_TakePhotoWithTablet = 3,
        Step5_PublishInStore = 4,
        Step6_WaitForCustomerOrder = 5,
        Step7_PrintCopies = 6,
        Step8_PackInEnvelope = 7,
        Step9_DeliverInShippingBox = 8,
        Completed = 9
    }

    /// <summary>
    /// Sistema de Tutorial interactivo en el mundo 3D.
    /// Guía al jugador paso a paso en el taller flotando un cartel explicativo
    /// y una flecha indicadora encima de cada estación de trabajo.
    /// </summary>
    public class WorkshopTutorialManager : MonoBehaviour
    {
        private static WorkshopTutorialManager instance;
        public static WorkshopTutorialManager Instance
        {
            get
            {
                if (instance == null) instance = FindAnyObjectByType<WorkshopTutorialManager>();
                return instance;
            }
            private set => instance = value;
        }

        [Header("Estado del Tutorial")]
        [SerializeField] private TutorialStep currentStep = TutorialStep.Step1_MountPaperOnEasel;
        [SerializeField] private bool tutorialEnabled = true;

        [Header("Referencias de Estaciones")]
        [SerializeField] private Transform stationDrawingTable;
        [SerializeField] private Transform stationPencils;
        [SerializeField] private Transform stationEasel;
        [SerializeField] private Transform stationTablet;
        [SerializeField] private Transform stationPrinter;
        [SerializeField] private Transform stationEnvelopeDispenser;
        [SerializeField] private Transform stationShippingBox;

        // UI 3D Flotante
        private GameObject billboardObj;
        private RectTransform billboardRt;
        private Text stepBadgeText;
        private Text titleText;
        private Text instructionText;
        private Transform arrowObj;
        private AudioSource audioSource;

        private float stepTimer = 0f;
        private bool isPCMode = true;

        public TutorialStep CurrentStep => currentStep;
        public bool IsCompleted => currentStep == TutorialStep.Completed;

        private void Awake()
        {
            if (instance == null) instance = this;
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.5f;
            }

            DetectStations();
            CreateFloatingBillboard();
            UpdateStepDisplay();
        }

        private void Start()
        {
            DetectStations();
            UpdateStepDisplay();
        }

        private void DetectStations()
        {
            if (stationDrawingTable == null)
            {
                var d1 = GameObject.Find("Dibujo_1");
                if (d1 != null) stationDrawingTable = d1.transform;
                else
                {
                    var m = GameObject.Find("Mesa Dibujo");
                    if (m != null) stationDrawingTable = m.transform;
                }
            }

            if (stationEasel == null)
            {
                var atril = GameObject.Find("Atril");
                if (atril != null) stationEasel = atril.transform;
            }

            if (stationPencils == null)
            {
                var p = GameObject.Find("caja_lapices") ?? GameObject.Find("PencilsHolder");
                if (p != null) stationPencils = p.transform;
                else if (stationEasel != null) stationPencils = stationEasel;
            }

            if (stationTablet == null)
            {
                var tab = GameObject.Find("Tableta");
                if (tab != null) stationTablet = tab.transform;
            }

            if (stationPrinter == null)
            {
                var pr = GameObject.Find("impresora color (1)") ?? GameObject.Find("Mesa Impresora");
                if (pr != null) stationPrinter = pr.transform;
            }

            if (stationEnvelopeDispenser == null)
            {
                var disp = FindAnyObjectByType<EnvelopeDispenser>();
                if (disp != null) stationEnvelopeDispenser = disp.transform;
                else
                {
                    var s = GameObject.Find("Sobre");
                    if (s != null) stationEnvelopeDispenser = s.transform;
                    else
                    {
                        var m = GameObject.Find("Mesa Envios");
                        if (m != null) stationEnvelopeDispenser = m.transform;
                    }
                }
            }

            if (stationShippingBox == null)
            {
                var b = GameObject.Find("Caja");
                if (b != null) stationShippingBox = b.transform;
                else
                {
                    var sp = FindAnyObjectByType<SendProduct>();
                    if (sp != null) stationShippingBox = sp.transform;
                }
            }

            // Detectar modo PC vs VR
            if (FirstPersonPlayerController.Instance != null && FirstPersonPlayerController.Instance.enabled)
            {
                isPCMode = true;
            }
            else
            {
                isPCMode = !UnityEngine.XR.XRSettings.isDeviceActive;
            }
        }

        private void Update()
        {
            if (!tutorialEnabled || billboardObj == null) return;

            stepTimer += Time.deltaTime;

            // Animar flotación y orientación hacia la cámara del jugador
            AnimateBillboard();

            // Evaluar condiciones de avance automático del loop
            CheckAutoProgression();
        }

        private void AnimateBillboard()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var playerCam = FindAnyObjectByType<FirstPersonPlayerController>();
                if (playerCam != null) cam = playerCam.GetComponentInChildren<Camera>();
            }

            if (cam != null && billboardObj != null)
            {
                // Suave efecto bobbing vertical
                float bob = Mathf.Sin(Time.time * 2.4f) * 0.025f;

                Transform targetStation = GetCurrentStation();
                Vector3 basePos = targetStation != null ? targetStation.position : new Vector3(5.6f, 0.4f, -1.5f);
                Vector3 targetPos = basePos + Vector3.up * (0.45f + bob);

                billboardObj.transform.position = Vector3.Lerp(billboardObj.transform.position, targetPos, Time.deltaTime * 6f);

                // Mirar hacia la cámara del jugador (el frente del Canvas debe apuntar a la cámara)
                Vector3 lookDir = cam.transform.position - billboardObj.transform.position;
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    billboardObj.transform.rotation = Quaternion.LookRotation(lookDir);
                }
            }

            // Animar flecha pulsante
            if (arrowObj != null)
            {
                float arrowBob = Mathf.Sin(Time.time * 4f) * 6f;
                var aRt = arrowObj.GetComponent<RectTransform>();
                if (aRt != null) aRt.anchoredPosition = new Vector2(0f, -25f + arrowBob);
            }
        }

        private Transform GetCurrentStation()
        {
            switch (currentStep)
            {
                case TutorialStep.Step1_MountPaperOnEasel:
                    return stationDrawingTable != null ? stationDrawingTable : stationEasel;

                case TutorialStep.Step2_DrawWithPencil:
                    return stationPencils != null ? stationPencils : stationEasel;

                case TutorialStep.Step3_ScanDrawingOnPrinter:
                    return stationPrinter;

                case TutorialStep.Step4_TakePhotoWithTablet:
                case TutorialStep.Step5_PublishInStore:
                case TutorialStep.Step6_WaitForCustomerOrder:
                    return stationTablet != null ? stationTablet : stationDrawingTable;

                case TutorialStep.Step7_PrintCopies:
                    return stationPrinter;

                case TutorialStep.Step8_PackInEnvelope:
                    return stationEnvelopeDispenser;

                case TutorialStep.Step9_DeliverInShippingBox:
                    return stationShippingBox;

                case TutorialStep.Completed:
                    return stationShippingBox;

                default:
                    return stationDrawingTable;
            }
        }

        private void CheckAutoProgression()
        {
            switch (currentStep)
            {
                case TutorialStep.Step1_MountPaperOnEasel:
                    // Se avanza cuando el papel queda montado en el atril
                    if (AtrilController.Instance != null && AtrilController.Instance.HasDrawingMounted)
                    {
                        AdvanceToStep(TutorialStep.Step2_DrawWithPencil);
                    }
                    break;

                case TutorialStep.Step2_DrawWithPencil:
                    // Se avanza cuando se ha dibujado sobre el lienzo
                    bool drawn = false;
                    if (AtrilController.Instance != null && AtrilController.Instance.CurrentMountedDrawing != null)
                    {
                        var canvas = AtrilController.Instance.CurrentMountedDrawing.GetComponent<DrawingCanvas>();
                        if (canvas != null && canvas.HasBeenDrawnOn) drawn = true;
                    }
                    if (!drawn)
                    {
                        var canvases = FindObjectsByType<DrawingCanvas>(FindObjectsInactive.Exclude);
                        foreach (var c in canvases)
                        {
                            if (c != null && c.HasBeenDrawnOn) { drawn = true; break; }
                        }
                    }
                    if (drawn && stepTimer > 2.5f)
                    {
                        AdvanceToStep(TutorialStep.Step3_ScanDrawingOnPrinter);
                    }
                    break;

                case TutorialStep.Step3_ScanDrawingOnPrinter:
                    // Se avanza cuando el dibujo original es colocado en el escáner de la impresora y digitalizado
                    bool scanned = (PrinterController.Instance != null && PrinterController.Instance.HasDrawingInside) ||
                                   (TabletManager.Instance != null && TabletManager.Instance.ScannedArtworks != null && TabletManager.Instance.ScannedArtworks.Count > 0);
                    if (scanned && stepTimer > 1.5f)
                    {
                        AdvanceToStep(TutorialStep.Step4_TakePhotoWithTablet);
                    }
                    break;

                case TutorialStep.Step4_TakePhotoWithTablet:
                    // Se avanza cuando se toma una fotografía con la app de Cámara
                    if (TabletManager.Instance != null && TabletManager.Instance.PhotoArtworks != null && TabletManager.Instance.PhotoArtworks.Count > 0)
                    {
                        AdvanceToStep(TutorialStep.Step5_PublishInStore);
                    }
                    break;

                case TutorialStep.Step5_PublishInStore:
                    // Se avanza cuando se publica al menos 1 producto en la tienda
                    if (TabletManager.Instance != null && TabletManager.Instance.StoreProducts != null && TabletManager.Instance.StoreProducts.Count > 0)
                    {
                        AdvanceToStep(TutorialStep.Step6_WaitForCustomerOrder);
                    }
                    break;

                case TutorialStep.Step6_WaitForCustomerOrder:
                    // Se avanza cuando entra un pedido
                    if (TabletManager.Instance != null && TabletManager.Instance.CurrentOrders != null && TabletManager.Instance.CurrentOrders.Count > 0)
                    {
                        AdvanceToStep(TutorialStep.Step7_PrintCopies);
                    }
                    break;

                case TutorialStep.Step7_PrintCopies:
                    // Se avanza cuando hay al menos una copia física impresa en la escena
                    var drawings = FindObjectsByType<ScannableDrawing>(FindObjectsInactive.Exclude);
                    bool hasCopies = false;
                    foreach (var d in drawings)
                    {
                        if (d != null && !d.IsOriginal) { hasCopies = true; break; }
                    }
                    if (hasCopies)
                    {
                        AdvanceToStep(TutorialStep.Step8_PackInEnvelope);
                    }
                    break;

                case TutorialStep.Step8_PackInEnvelope:
                    // Se avanza cuando un sobre queda cerrado y sellado
                    var envelopes = FindObjectsByType<EnvelopeController>(FindObjectsInactive.Exclude);
                    bool anySealed = false;
                    foreach (var e in envelopes)
                    {
                        if (e != null && e.IsSealed) { anySealed = true; break; }
                    }
                    if (anySealed)
                    {
                        AdvanceToStep(TutorialStep.Step9_DeliverInShippingBox);
                    }
                    break;

                case TutorialStep.Step9_DeliverInShippingBox:
                    // Se avanza cuando se entrega y procesa el pedido en la caja de envíos
                    break;

                case TutorialStep.Completed:
                    if (stepTimer > 8f && billboardObj != null)
                    {
                        billboardObj.SetActive(false);
                    }
                    break;
            }
        }

        public void AdvanceToStep(TutorialStep nextStep)
        {
            if (currentStep == nextStep) return;

            currentStep = nextStep;
            stepTimer = 0f;

            PlayChimeSound();
            UpdateStepDisplay();

            Debug.Log($"<color=cyan>[Tutorial]</color> 🎯 Avanzando a: <b>{currentStep}</b>");
        }

        public void NotifyDeliveryCompleted()
        {
            if (currentStep == TutorialStep.Step9_DeliverInShippingBox)
            {
                AdvanceToStep(TutorialStep.Completed);
            }
        }

        public void NotifyEnvelopeSealed(EnvelopeController envelope)
        {
            if (currentStep == TutorialStep.Step8_PackInEnvelope)
            {
                AdvanceToStep(TutorialStep.Step9_DeliverInShippingBox);
            }
        }

        public void NotifyEnvelopeDispensed(EnvelopeController envelope)
        {
            // Opcional feedback
        }

        private void UpdateStepDisplay()
        {
            if (billboardObj == null) return;

            string badge = $"TUTORIAL  •  PASO {(int)currentStep + 1} / 9";
            string title = "";
            string desc = "";

            switch (currentStep)
            {
                case TutorialStep.Step1_MountPaperOnEasel:
                    title = "📄 1. PREPARAR EL LIENZO";
                    desc = isPCMode
                        ? "Toma la <b>hoja de papel</b> en la mesa (<b>Clic Izquierdo</b>) y colócala en el <b>Atril</b> para empezar a dibujar."
                        : "Toma la <b>hoja de papel</b> (<b>Botón Grip</b>) y acércala al <b>Atril</b> para que se enganche automáticamente.";
                    break;

                case TutorialStep.Step2_DrawWithPencil:
                    title = "✏️ 2. DIBUJAR TU OBRA";
                    desc = isPCMode
                        ? "Toma un lápiz (<b>Clic Izq</b>) y dibuja en el atril (o pulsa <b>[F / Clic Der]</b>). ¡Pinta el fondo y usa varios colores para máxima calidad y ventas!"
                        : "Toma un lápiz de la caja (<b>Grip</b>) y traza tu ilustración. ¡Pintar el fondo y usar colores aumenta el valor de tu arte!";
                    break;

                case TutorialStep.Step3_ScanDrawingOnPrinter:
                    title = "🖨️ 3. ESCANEAR EL DIBUJO";
                    desc = isPCMode
                        ? "Toma tu dibujo del atril (<b>Clic Izquierdo</b>) y colócalo sobre el <b>escáner de la impresora</b> para digitalizarlo."
                        : "Toma tu dibujo del atril (<b>Grip</b>) y apóyalo sobre el <b>cristal del escáner</b> de la impresora para registrarlo.";
                    break;

                case TutorialStep.Step4_TakePhotoWithTablet:
                    title = "📸 4. TOMAR FOTO CON LA CÁMARA";
                    desc = isPCMode
                        ? "Toma la Tableta (<b>Clic / E</b>), abre la app <b>Cámara</b>, apunta a tu arte y presiona el <b>Botón de Foto</b>."
                        : "Toma la Tableta (<b>Grip</b>), abre la app <b>Cámara</b> y pulsa el botón grande de disparo para fotografiar tu arte.";
                    break;

                case TutorialStep.Step5_PublishInStore:
                    title = "🏪 5. PUBLICAR EN LA TIENDA";
                    desc = "En la tableta, abre la app <b>Tienda</b> y presiona el botón azul <b>[＋ PUBLICAR]</b> para seleccionar tu foto, fijar el precio y publicarla.";
                    break;

                case TutorialStep.Step6_WaitForCustomerOrder:
                    title = "📦 6. REVISAR PEDIDO DE CLIENTE";
                    desc = "Ve a la pestaña <b>Pedidos</b> de la Tienda. ¡Un cliente acaba de comprar tu producto y te solicita copias físicas!";
                    break;

                case TutorialStep.Step7_PrintCopies:
                    title = "🖨️ 7. IMPRIMIR COPIAS FÍSICAS";
                    desc = isPCMode
                        ? "En la tableta ve a la <b>Galería (Imágenes)</b>, selecciona tu dibujo y presiona <b>'Imprimir'</b> para sacar las copias solicitadas."
                        : "En la tableta abre la <b>Galería</b>, selecciona tu dibujo y pulsa <b>'Imprimir'</b> para generar las copias en la impresora.";
                    break;

                case TutorialStep.Step8_PackInEnvelope:
                    title = "✉️ 8. EMPACAR COPIAS EN EL SOBRE";
                    desc = isPCMode
                        ? "Toma un sobre del <b>Dispensador</b> (<b>Clic</b>), introduce las copias impresas requeridas y presiona <b>'Cerrar Sobre'</b>."
                        : "Toma un sobre del dispensador (<b>Grip</b>), introduce las copias físicas impresas y pulsa <b>'Cerrar Sobre'</b>.";
                    break;

                case TutorialStep.Step9_DeliverInShippingBox:
                    title = "📬 9. DESPACHAR Y COBRAR DINERO";
                    desc = "Lleva el sobre cerrado y sellado a la <b>Caja de Envíos</b> para despacharlo. ¡Ganarás dinero por tu venta!";
                    break;

                case TutorialStep.Completed:
                    badge = "🏆 ¡TUTORIAL COMPLETADO!";
                    title = "🎉 ¡FELICITACIONES!";
                    desc = "Has dominado todo el ciclo de producción de <b>MerchMaker</b>. ¡Crea más dibujos, fotos y merchandising!";
                    break;
            }

            if (stepBadgeText != null) stepBadgeText.text = badge;
            if (titleText != null) titleText.text = title;
            if (instructionText != null) instructionText.text = desc;

            // Inmediatamente colocarlo sobre la estación correcta
            Transform targetStation = GetCurrentStation();
            if (targetStation != null && billboardObj != null)
            {
                billboardObj.transform.position = targetStation.position + Vector3.up * 0.45f;
            }
        }

        private void CreateFloatingBillboard()
        {
            if (billboardObj != null) return;

            billboardObj = new GameObject("TutorialFloatingBillboard");
            billboardObj.transform.position = new Vector3(5.6f, 0.5f, -1.5f);

            Canvas canvas = billboardObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            billboardRt = billboardObj.GetComponent<RectTransform>();
            billboardRt.sizeDelta = new Vector2(480, 220);
            billboardObj.transform.localScale = new Vector3(-0.0022f, 0.0022f, 0.0022f);

            // Fondo translúcido elegante
            GameObject bgObj = new GameObject("PanelBg");
            bgObj.transform.SetParent(billboardObj.transform, false);
            var bgRt = bgObj.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.10f, 0.14f, 0.22f, 0.94f);

            // Borde / Acento superior
            GameObject borderObj = new GameObject("AccentTop");
            borderObj.transform.SetParent(billboardObj.transform, false);
            var bRt = borderObj.AddComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0f, 0.96f);
            bRt.anchorMax = Vector2.one;
            bRt.offsetMin = Vector2.zero;
            bRt.offsetMax = Vector2.zero;
            var bImg = borderObj.AddComponent<Image>();
            bImg.color = new Color(0.25f, 0.65f, 0.95f);

            // 1. Badge Paso
            GameObject badgeObj = new GameObject("StepBadge");
            badgeObj.transform.SetParent(billboardObj.transform, false);
            var badgeRt = badgeObj.AddComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0.05f, 0.78f);
            badgeRt.anchorMax = new Vector2(0.95f, 0.93f);
            badgeRt.offsetMin = Vector2.zero;
            badgeRt.offsetMax = Vector2.zero;
            stepBadgeText = badgeObj.AddComponent<Text>();
            stepBadgeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            stepBadgeText.fontSize = 18;
            stepBadgeText.fontStyle = FontStyle.Bold;
            stepBadgeText.alignment = TextAnchor.MiddleLeft;
            stepBadgeText.color = new Color(0.35f, 0.75f, 1f);

            // 2. Título de la instrucción
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(billboardObj.transform, false);
            var tRt = titleObj.AddComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0.05f, 0.58f);
            tRt.anchorMax = new Vector2(0.95f, 0.78f);
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;
            titleText = titleObj.AddComponent<Text>();
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 24;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.color = Color.white;

            // 3. Descripción detallada con botones
            GameObject descObj = new GameObject("DescText");
            descObj.transform.SetParent(billboardObj.transform, false);
            var dRt = descObj.AddComponent<RectTransform>();
            dRt.anchorMin = new Vector2(0.05f, 0.08f);
            dRt.anchorMax = new Vector2(0.95f, 0.55f);
            dRt.offsetMin = Vector2.zero;
            dRt.offsetMax = Vector2.zero;
            instructionText = descObj.AddComponent<Text>();
            instructionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            instructionText.fontSize = 20;
            instructionText.alignment = TextAnchor.UpperLeft;
            instructionText.color = new Color(0.90f, 0.92f, 0.96f);

            // 4. Flecha indicadora apuntando hacia abajo dentro del mismo Canvas
            GameObject arrowObjGo = new GameObject("ArrowTxt");
            arrowObjGo.transform.SetParent(billboardObj.transform, false);
            var atRt = arrowObjGo.AddComponent<RectTransform>();
            atRt.anchorMin = new Vector2(0.40f, -0.22f);
            atRt.anchorMax = new Vector2(0.60f, 0.02f);
            atRt.offsetMin = Vector2.zero;
            atRt.offsetMax = Vector2.zero;
            var arrowTxt = arrowObjGo.AddComponent<Text>();
            arrowTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            arrowTxt.fontSize = 42;
            arrowTxt.alignment = TextAnchor.MiddleCenter;
            arrowTxt.color = new Color(0.35f, 0.75f, 1f);
            arrowTxt.text = "▼";
            arrowObj = arrowObjGo.transform;
        }

        private void PlayChimeSound()
        {
            if (audioSource == null) return;
            int sampleRate = 44100;
            float duration = 0.28f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            // Sonido de campana brillante de progreso (acorde Mayor)
            float freq1 = 587.33f; // D5
            float freq2 = 880.00f; // A5
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 8f);
                float wave = (Mathf.Sin(2f * Mathf.PI * freq1 * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * freq2 * t) * 0.5f);
                samples[i] = wave * env * 0.35f;
            }

            AudioClip clip = AudioClip.Create("TutorialChime", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            audioSource.PlayOneShot(clip);
        }
    }
}
