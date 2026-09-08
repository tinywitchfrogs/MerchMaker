using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ArtSimulator
{
    public class TabletUIBuilder : MonoBehaviour
    {
        [Header("Configuración del Canvas")]
        [SerializeField] private Vector2 canvasResolution = new Vector2(1920, 1080);

        public Canvas BuiltCanvas { get; private set; }

        public void BuildUIIfNeeded()
        {
            // Verificar si ya existe un Canvas de Tableta (incluyendo inactivos)
            GameObject canvasObj = null;
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].name == "TabletCanvas")
                {
                    canvasObj = canvases[i].gameObject;
                    break;
                }
            }

            if (canvasObj != null)
            {
                BuiltCanvas = canvasObj.GetComponent<Canvas>();
                canvasObj.transform.SetParent(null);
                HookEvents();
                var ctrl = GetComponent<TabletUIController>();
                if (ctrl != null) ctrl.WireAllButtons();
                return;
            }

            EnsureEventSystemExists();

            canvasObj = new GameObject("TabletCanvas");
            canvasObj.transform.SetParent(null);

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700;
            BuiltCanvas = canvas;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // 1. Fondo atenuado oscuro detrás de la tableta
            GameObject backdropObj = CreateUIElement("Backdrop", canvasObj.transform);
            SetFullStretch(backdropObj);
            Image bdImg = backdropObj.AddComponent<Image>();
            bdImg.color = new Color(0f, 0f, 0f, 0.55f);
            bdImg.raycastTarget = false;

            // 2. Marco físico exterior adaptable con proporción 16:10 que NUNCA desborda la pantalla
            GameObject deviceFrame = CreateUIElement("TabletDeviceFrame", canvasObj.transform);
            RectTransform frameRt = deviceFrame.GetComponent<RectTransform>();
            frameRt.anchorMin = new Vector2(0.10f, 0.06f);
            frameRt.anchorMax = new Vector2(0.90f, 0.94f);
            frameRt.offsetMin = Vector2.zero;
            frameRt.offsetMax = Vector2.zero;

            Image frameImg = deviceFrame.AddComponent<Image>();
            frameImg.color = new Color(0.08f, 0.09f, 0.12f); // Titanio oscuro
            frameImg.raycastTarget = false;

            // Barra superior del marco físico (Título y botón Guardar/Cerrar)
            GameObject topFrameBar = CreateUIElement("TopFrameBar", deviceFrame.transform);
            RectTransform tfbRt = topFrameBar.GetComponent<RectTransform>();
            tfbRt.anchorMin = new Vector2(0, 0.92f);
            tfbRt.anchorMax = new Vector2(1, 1f);
            tfbRt.offsetMin = Vector2.zero;
            tfbRt.offsetMax = Vector2.zero;

            Text titleText = CreateText("TabletModelTitle", topFrameBar.transform, "Suny", 16, FontStyle.Bold, new Color(0.85f, 0.88f, 0.95f), TextAnchor.MiddleLeft);
            RectTransform ttRt = titleText.GetComponent<RectTransform>();
            ttRt.anchorMin = new Vector2(0.03f, 0);
            ttRt.anchorMax = new Vector2(0.5f, 1);
            ttRt.offsetMin = Vector2.zero;
            ttRt.offsetMax = Vector2.zero;
            titleText.raycastTarget = false;

            // Botón Cerrar Tableta superior derecho
            GameObject closeBtnObj = CreateUIElement("CloseTabletButton", topFrameBar.transform);
            RectTransform cbtRt = closeBtnObj.GetComponent<RectTransform>();
            cbtRt.anchorMin = new Vector2(0.62f, 0.10f);
            cbtRt.anchorMax = new Vector2(0.98f, 0.90f);
            cbtRt.offsetMin = Vector2.zero;
            cbtRt.offsetMax = Vector2.zero;

            Image cbtImg = closeBtnObj.AddComponent<Image>();
            cbtImg.color = new Color(0.86f, 0.18f, 0.18f);
            cbtImg.raycastTarget = true;
            Button cbtBtn = closeBtnObj.AddComponent<Button>();
            cbtBtn.targetGraphic = cbtImg;

            TabletButtonTrigger cbtTrig = closeBtnObj.AddComponent<TabletButtonTrigger>();
            cbtTrig.SetAction("Cerrar Tableta", () => TabletManager.Instance?.CloseTablet());

            Text cbtTxt = CreateText("Lbl", closeBtnObj.transform, "✕ GUARDAR TABLETA [ESC]", 14, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetFullStretch(cbtTxt.gameObject);
            cbtTxt.raycastTarget = false;

            // 3. Área de pantalla activa (Display donde se ven las apps)
            GameObject screenArea = CreateUIElement("ScreenArea", deviceFrame.transform);
            RectTransform screenRt = screenArea.GetComponent<RectTransform>();
            screenRt.anchorMin = new Vector2(0.02f, 0.025f);
            screenRt.anchorMax = new Vector2(0.98f, 0.92f);
            screenRt.offsetMin = Vector2.zero;
            screenRt.offsetMax = Vector2.zero;

            Image screenBg = screenArea.AddComponent<Image>();
            screenBg.color = new Color(0.97f, 0.98f, 0.99f);
            screenBg.raycastTarget = false;

            // Crear los 4 paneles principales de la tableta
            GameObject homePanel = CreateHomeScreen(screenArea.transform);
            GameObject imagesPanel = CreateImagesScreen(screenArea.transform, out TabletGalleryApp galleryApp);
            GameObject cameraPanel = CreateCameraScreen(screenArea.transform, out TabletCameraApp cameraApp);
            GameObject storePanel = CreateStoreScreen(screenArea.transform, out TabletStoreApp storeApp);

            // Configurar TabletUIController
            TabletUIController uiCtrl = GetComponent<TabletUIController>();
            if (uiCtrl == null) uiCtrl = gameObject.AddComponent<TabletUIController>();

            uiCtrl.SetReferences(homePanel, imagesPanel, cameraPanel, storePanel, galleryApp, cameraApp, storeApp);
            uiCtrl.UpdateActiveScreen(TabletScreen.Home);

            HookEvents();
            uiCtrl.WireAllButtons();

            // TabletInteractiveScreen en el modelo 3D
            TabletInteractiveScreen interactiveScreen = GetComponent<TabletInteractiveScreen>();
            if (interactiveScreen == null) interactiveScreen = gameObject.AddComponent<TabletInteractiveScreen>();

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);

            Debug.Log("<color=green>[TabletUIBuilder]</color> ¡Interfaz de Tableta construida y configurada con éxito!");
        }

        private void EnsureEventSystemExists()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                esObj.AddComponent<StandaloneInputModule>();
#endif
            }
        }

        #region Pantalla Home / Menú Principal

        private GameObject CreateHomeScreen(Transform parent)
        {
            GameObject panel = CreateUIElement("HomeScreen", parent);
            SetFullStretch(panel);

            // Barra de estado superior
            GameObject topBar = CreateUIElement("TopBar", panel.transform);
            RectTransform tbRt = topBar.GetComponent<RectTransform>();
            tbRt.anchorMin = new Vector2(0, 0.91f);
            tbRt.anchorMax = new Vector2(1, 1);
            tbRt.offsetMin = Vector2.zero;
            tbRt.offsetMax = Vector2.zero;

            Text clockText = CreateText("Clock", topBar.transform, "12:45 PM", 22, FontStyle.Bold, new Color(0.12f, 0.16f, 0.24f), TextAnchor.MiddleLeft);
            RectTransform ctRt = clockText.GetComponent<RectTransform>();
            ctRt.anchorMin = new Vector2(0.04f, 0);
            ctRt.anchorMax = new Vector2(0.3f, 1);
            ctRt.offsetMin = Vector2.zero;
            ctRt.offsetMax = Vector2.zero;
            clockText.raycastTarget = false;

            Text statusInfo = CreateText("StatusInfo", topBar.transform, "📶 Wi-Fi Conectado   •   🔋 100%", 16, FontStyle.Bold, new Color(0.35f, 0.42f, 0.52f), TextAnchor.MiddleRight);
            RectTransform siRt = statusInfo.GetComponent<RectTransform>();
            siRt.anchorMin = new Vector2(0.6f, 0);
            siRt.anchorMax = new Vector2(0.96f, 1);
            siRt.offsetMin = Vector2.zero;
            siRt.offsetMax = Vector2.zero;
            statusInfo.raycastTarget = false;

            // Encabezado
            GameObject headerObj = CreateUIElement("HomeHeader", panel.transform);
            RectTransform hhRt = headerObj.GetComponent<RectTransform>();
            hhRt.anchorMin = new Vector2(0.05f, 0.72f);
            hhRt.anchorMax = new Vector2(0.95f, 0.89f);
            hhRt.offsetMin = Vector2.zero;
            hhRt.offsetMax = Vector2.zero;

            Text homeTitle = CreateText("Title", headerObj.transform, "APLICACIONES DISPONIBLES", 30, FontStyle.Bold, new Color(0.06f, 0.09f, 0.16f), TextAnchor.MiddleCenter);
            RectTransform htRt = homeTitle.GetComponent<RectTransform>();
            htRt.anchorMin = new Vector2(0, 0.45f);
            htRt.anchorMax = new Vector2(1, 1f);
            htRt.offsetMin = Vector2.zero;
            htRt.offsetMax = Vector2.zero;
            homeTitle.raycastTarget = false;

            Text homeSub = CreateText("Subtitle", headerObj.transform, "Haz clic en una aplicación para abrirla (o usa las teclas rápidas 1, 2, 3)", 16, FontStyle.Normal, new Color(0.35f, 0.42f, 0.52f), TextAnchor.MiddleCenter);
            RectTransform hsRt = homeSub.GetComponent<RectTransform>();
            hsRt.anchorMin = new Vector2(0, 0);
            hsRt.anchorMax = new Vector2(1, 0.45f);
            hsRt.offsetMin = Vector2.zero;
            hsRt.offsetMax = Vector2.zero;
            homeSub.raycastTarget = false;

            // Contenedor de aplicaciones
            GameObject appsContainer = CreateUIElement("AppsGrid", panel.transform);
            RectTransform gridRt = appsContainer.GetComponent<RectTransform>();
            gridRt.anchorMin = new Vector2(0.04f, 0.08f);
            gridRt.anchorMax = new Vector2(0.96f, 0.70f);
            gridRt.offsetMin = Vector2.zero;
            gridRt.offsetMax = Vector2.zero;

            HorizontalLayoutGroup hlg = appsContainer.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 40f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // 1. Galería de Arte
            CreateAppIcon(appsContainer.transform, "Galería de Arte", new Color(0.12f, 0.48f, 0.95f), "🖼️", "[1] Ver y Reimprimir", () =>
            {
                TabletManager.Instance?.ShowImages();
            });

            // 2. Cámara
            CreateAppIcon(appsContainer.transform, "Cámara", new Color(0.18f, 0.22f, 0.28f), "📷", "[2] Tomar Fotos", () =>
            {
                TabletManager.Instance?.ShowCamera();
            });

            // 3. Tienda de Arte
            CreateAppIcon(appsContainer.transform, "Tienda de Arte", new Color(0.92f, 0.50f, 0.08f), "🛍️", "[3] Vender y Pedidos", () =>
            {
                TabletManager.Instance?.ShowStore();
            });

            LayoutRebuilder.ForceRebuildLayoutImmediate(gridRt);

            return panel;
        }

        private GameObject CreateAppIcon(Transform parent, string title, Color iconColor, string emoji, string shortcutHelp, UnityEngine.Events.UnityAction onClick)
        {
            GameObject appObj = CreateUIElement($"App_{title}", parent);
            RectTransform appRt = appObj.GetComponent<RectTransform>();
            appRt.sizeDelta = new Vector2(220, 260);

            LayoutElement le = appObj.AddComponent<LayoutElement>();
            le.minWidth = 220;
            le.minHeight = 260;
            le.preferredWidth = 220;
            le.preferredHeight = 260;

            Image appBg = appObj.AddComponent<Image>();
            appBg.color = Color.white;
            appBg.raycastTarget = true;

            Button fullBtn = appObj.AddComponent<Button>();
            fullBtn.targetGraphic = appBg;
            ColorBlock cb = fullBtn.colors;
            cb.highlightedColor = new Color(0.94f, 0.97f, 1.0f);
            cb.pressedColor = new Color(0.88f, 0.92f, 0.98f);
            fullBtn.colors = cb;
            fullBtn.onClick.AddListener(onClick);

            TabletButtonTrigger trig = appObj.AddComponent<TabletButtonTrigger>();
            trig.SetAction($"Abrir {title}", () => onClick?.Invoke());

            // IconBox
            GameObject iconBox = CreateUIElement("IconBox", appObj.transform);
            RectTransform ibRt = iconBox.GetComponent<RectTransform>();
            ibRt.anchorMin = new Vector2(0.5f, 1f);
            ibRt.anchorMax = new Vector2(0.5f, 1f);
            ibRt.pivot = new Vector2(0.5f, 1f);
            ibRt.anchoredPosition = new Vector2(0, -14);
            ibRt.sizeDelta = new Vector2(130, 130);

            Image ibImg = iconBox.AddComponent<Image>();
            ibImg.color = iconColor;
            ibImg.raycastTarget = false;

            Text symbolText = CreateText("Symbol", iconBox.transform, emoji, 68, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter);
            SetFullStretch(symbolText.gameObject);
            symbolText.raycastTarget = false;

            // Labels
            GameObject labelObj = CreateUIElement("LabelContainer", appObj.transform);
            RectTransform lblContRt = labelObj.GetComponent<RectTransform>();
            lblContRt.anchorMin = new Vector2(0, 0);
            lblContRt.anchorMax = new Vector2(1, 0.40f);
            lblContRt.offsetMin = Vector2.zero;
            lblContRt.offsetMax = Vector2.zero;

            Text labelText = CreateText("Label", labelObj.transform, title, 20, FontStyle.Bold, new Color(0.08f, 0.12f, 0.20f), TextAnchor.MiddleCenter);
            RectTransform ltRt = labelText.GetComponent<RectTransform>();
            ltRt.anchorMin = new Vector2(0, 0.45f);
            ltRt.anchorMax = new Vector2(1, 1f);
            ltRt.offsetMin = Vector2.zero;
            ltRt.offsetMax = Vector2.zero;
            labelText.raycastTarget = false;

            GameObject pillObj = CreateUIElement("ShortcutPill", labelObj.transform);
            RectTransform pillRt = pillObj.GetComponent<RectTransform>();
            pillRt.anchorMin = new Vector2(0.08f, 0.08f);
            pillRt.anchorMax = new Vector2(0.92f, 0.44f);
            pillRt.offsetMin = Vector2.zero;
            pillRt.offsetMax = Vector2.zero;

            Image pillImg = pillObj.AddComponent<Image>();
            pillImg.color = new Color(0.93f, 0.95f, 0.98f);
            pillImg.raycastTarget = false;

            Text shortcutTxt = CreateText("Shortcut", pillObj.transform, shortcutHelp, 13, FontStyle.Bold, new Color(0.28f, 0.35f, 0.45f), TextAnchor.MiddleCenter);
            SetFullStretch(shortcutTxt.gameObject);
            shortcutTxt.raycastTarget = false;

            return appObj;
        }

        #endregion

        #region Pantalla Cámara (Boceto 1)

        private GameObject CreateCameraScreen(Transform parent, out TabletCameraApp cameraApp)
        {
            GameObject panel = CreateUIElement("CameraScreen", parent);
            SetFullStretch(panel);

            // Visor de Cámara (A la izquierda)
            GameObject vfObj = CreateUIElement("Viewfinder", panel.transform);
            RectTransform vfRt = vfObj.GetComponent<RectTransform>();
            vfRt.anchorMin = new Vector2(0.03f, 0.04f);
            vfRt.anchorMax = new Vector2(0.84f, 0.96f);
            vfRt.offsetMin = Vector2.zero;
            vfRt.offsetMax = Vector2.zero;

            RawImage vfImg = vfObj.AddComponent<RawImage>();
            vfImg.color = Color.white;

            // Flash overlay
            GameObject flashObj = CreateUIElement("FlashOverlay", vfObj.transform);
            SetFullStretch(flashObj);
            Image flashImg = flashObj.AddComponent<Image>();
            flashImg.color = new Color(1, 1, 1, 0);
            flashObj.SetActive(false);

            // Barra lateral derecha (Controles de cámara según boceto)
            GameObject rightBar = CreateUIElement("CameraControls", panel.transform);
            RectTransform rbRt = rightBar.GetComponent<RectTransform>();
            rbRt.anchorMin = new Vector2(0.85f, 0.04f);
            rbRt.anchorMax = new Vector2(0.98f, 0.96f);
            rbRt.offsetMin = Vector2.zero;
            rbRt.offsetMax = Vector2.zero;

            // BOTÓN DESTACADO PARA VOLVER AL MENÚ PRINCIPAL
            GameObject homeObj = CreateUIElement("HomeButton", rightBar.transform);
            RectTransform hRt = homeObj.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0.5f, 0.88f);
            hRt.anchorMax = new Vector2(0.5f, 0.88f);
            hRt.pivot = new Vector2(0.5f, 0.5f);
            hRt.anchoredPosition = Vector2.zero;
            hRt.sizeDelta = new Vector2(80, 58);

            Image hImg = homeObj.AddComponent<Image>();
            hImg.color = new Color(0.13f, 0.44f, 0.88f); // Azul zafiro destacado
            hImg.raycastTarget = true;

            Button homeBtn = homeObj.AddComponent<Button>();
            homeBtn.targetGraphic = hImg;
            ColorBlock hcb = homeBtn.colors;
            hcb.highlightedColor = new Color(0.20f, 0.55f, 1.0f);
            hcb.pressedColor = new Color(0.08f, 0.32f, 0.72f);
            homeBtn.colors = hcb;

            TabletButtonTrigger htTrig = homeObj.AddComponent<TabletButtonTrigger>();
            htTrig.SetAction("Volver al Inicio", () => TabletManager.Instance?.ShowHome());

            Text hText = CreateText("HouseSymbol", homeObj.transform, "← 🏠\nVOLVER", 12, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetFullStretch(hText.gameObject);
            hText.raycastTarget = false;

            // BOTÓN DISPARADOR NEGRO CIRCULAR (Centro derecha como en el boceto)
            GameObject shutterObj = CreateUIElement("ShutterButton", rightBar.transform);
            RectTransform shutRt = shutterObj.GetComponent<RectTransform>();
            shutRt.anchorMin = new Vector2(0.5f, 0.48f);
            shutRt.anchorMax = new Vector2(0.5f, 0.48f);
            shutRt.sizeDelta = new Vector2(74, 74);

            Image shutImg = shutterObj.AddComponent<Image>();
            shutImg.color = Color.black;
            shutImg.raycastTarget = true;

            Button shutterBtn = shutterObj.AddComponent<Button>();
            shutterBtn.targetGraphic = shutImg;

            // Anillo exterior blanco del disparador
            GameObject ringObj = CreateUIElement("ShutterRing", shutterObj.transform);
            SetFullStretch(ringObj);
            Image ringImg = ringObj.AddComponent<Image>();
            ringImg.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            ringImg.raycastTarget = false;

            Text shutLbl = CreateText("Lbl", shutterObj.transform, "FOTO\n[ESPACIO]", 11, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetFullStretch(shutLbl.gameObject);
            shutLbl.raycastTarget = false;

            // CONTROL DE ZOOM DE CÁMARA (Debajo del disparador)
            GameObject zoomGroup = CreateUIElement("ZoomControls", rightBar.transform);
            RectTransform zgRt = zoomGroup.GetComponent<RectTransform>();
            zgRt.anchorMin = new Vector2(0.5f, 0.18f);
            zgRt.anchorMax = new Vector2(0.5f, 0.18f);
            zgRt.sizeDelta = new Vector2(74, 94);

            // Botón Zoom In [+]
            GameObject zInObj = CreateUIElement("ZoomInBtn", zoomGroup.transform);
            RectTransform zinRt = zInObj.GetComponent<RectTransform>();
            zinRt.anchorMin = new Vector2(0.5f, 0.5f);
            zinRt.anchorMax = new Vector2(0.5f, 0.5f);
            zinRt.anchoredPosition = new Vector2(0, 32);
            zinRt.sizeDelta = new Vector2(56, 26);

            Image zinImg = zInObj.AddComponent<Image>();
            zinImg.color = new Color(0.18f, 0.22f, 0.30f, 0.95f);
            Button zInBtn = zInObj.AddComponent<Button>();
            zInBtn.targetGraphic = zinImg;

            Text zinLbl = CreateText("Lbl", zInObj.transform, "🔍 +", 11, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetFullStretch(zinLbl.gameObject);
            zinLbl.raycastTarget = false;

            // Indicador de Zoom (Ej: "1.0x")
            GameObject zTxtObj = CreateUIElement("ZoomText", zoomGroup.transform);
            RectTransform ztxtRt = zTxtObj.GetComponent<RectTransform>();
            ztxtRt.anchorMin = new Vector2(0.5f, 0.5f);
            ztxtRt.anchorMax = new Vector2(0.5f, 0.5f);
            ztxtRt.anchoredPosition = new Vector2(0, 4);
            ztxtRt.sizeDelta = new Vector2(64, 20);

            Text zoomTxt = CreateText("ZoomValue", zTxtObj.transform, "1.0x", 12, FontStyle.Bold, Color.cyan, TextAnchor.MiddleCenter);
            SetFullStretch(zoomTxt.gameObject);
            zoomTxt.raycastTarget = false;

            // Botón Zoom Out [-]
            GameObject zOutObj = CreateUIElement("ZoomOutBtn", zoomGroup.transform);
            RectTransform zoutRt = zOutObj.GetComponent<RectTransform>();
            zoutRt.anchorMin = new Vector2(0.5f, 0.5f);
            zoutRt.anchorMax = new Vector2(0.5f, 0.5f);
            zoutRt.anchoredPosition = new Vector2(0, -24);
            zoutRt.sizeDelta = new Vector2(56, 26);

            Image zoutImg = zOutObj.AddComponent<Image>();
            zoutImg.color = new Color(0.18f, 0.22f, 0.30f, 0.95f);
            Button zOutBtn = zOutObj.AddComponent<Button>();
            zOutBtn.targetGraphic = zoutImg;

            Text zoutLbl = CreateText("Lbl", zOutObj.transform, "🔍 -", 11, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetFullStretch(zoutLbl.gameObject);
            zoutLbl.raycastTarget = false;

            // Script de la cámara
            cameraApp = panel.AddComponent<TabletCameraApp>();
            cameraApp.BindUI(vfImg, shutterBtn, homeBtn, flashImg, zInBtn, zOutBtn, zoomTxt);

            TabletCameraApp localCam = cameraApp;
            TabletButtonTrigger stTrig = shutterObj.AddComponent<TabletButtonTrigger>();
            stTrig.SetAction("Disparar Foto", () => localCam?.TakePhoto());

            return panel;
        }

        #endregion

        #region Pantalla Imágenes (Galería de Dibujos e Imprimir)

        private GameObject CreateImagesScreen(Transform parent, out TabletGalleryApp galleryApp)
        {
            GameObject panel = CreateUIElement("ImagesScreen", parent);
            SetFullStretch(panel);

            // Barra Superior con Botón de Casa
            GameObject topBar = CreateTopHeaderWithHouse(panel.transform, "🖼️ ILUSTRACIONES ESCANEADAS", out Button homeBtn);

            // Mitad Izquierda: Vista Previa Ampliada y Botón Imprimir
            GameObject previewPanel = CreateUIElement("PreviewPanel", panel.transform);
            RectTransform prevRt = previewPanel.GetComponent<RectTransform>();
            prevRt.anchorMin = new Vector2(0.04f, 0.05f);
            prevRt.anchorMax = new Vector2(0.48f, 0.86f);
            prevRt.offsetMin = Vector2.zero;
            prevRt.offsetMax = Vector2.zero;

            Image prevBg = previewPanel.AddComponent<Image>();
            prevBg.color = new Color(0.92f, 0.93f, 0.96f);
            prevBg.raycastTarget = false;

            // Imagen seleccionada
            GameObject imgObj = CreateUIElement("ArtworkImage", previewPanel.transform);
            RectTransform imgRt = imgObj.GetComponent<RectTransform>();
            imgRt.anchorMin = new Vector2(0.06f, 0.38f);
            imgRt.anchorMax = new Vector2(0.94f, 0.95f);
            imgRt.offsetMin = Vector2.zero;
            imgRt.offsetMax = Vector2.zero;
            Image artImg = imgObj.AddComponent<Image>();
            artImg.raycastTarget = false;

            // Título de la ilustración
            Text titleTxt = CreateText("ArtworkTitle", previewPanel.transform, "Selecciona una ilustración", 20, FontStyle.Bold, new Color(0.08f, 0.12f, 0.20f), TextAnchor.MiddleCenter);
            RectTransform titRt = titleTxt.GetComponent<RectTransform>();
            titRt.anchorMin = new Vector2(0.05f, 0.27f);
            titRt.anchorMax = new Vector2(0.95f, 0.37f);
            titRt.offsetMin = Vector2.zero;
            titRt.offsetMax = Vector2.zero;
            titleTxt.raycastTarget = false;

            // Botón IMPRIMIR COPIA (Totalmente visible y accesible)
            GameObject printObj = CreateUIElement("PrintButton", previewPanel.transform);
            RectTransform prRt = printObj.GetComponent<RectTransform>();
            prRt.anchorMin = new Vector2(0.06f, 0.14f);
            prRt.anchorMax = new Vector2(0.94f, 0.25f);
            prRt.offsetMin = Vector2.zero;
            prRt.offsetMax = Vector2.zero;

            Image prImg = printObj.AddComponent<Image>();
            prImg.color = new Color(0.14f, 0.65f, 0.32f);
            prImg.raycastTarget = true;

            Button printBtn = printObj.AddComponent<Button>();
            printBtn.targetGraphic = prImg;

            TabletButtonTrigger ptTrig = printObj.AddComponent<TabletButtonTrigger>();

            Text printLbl = CreateText("PrintLabel", printObj.transform, "🖨️ IMPRIMIR COPIA FÍSICA", 16, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetFullStretch(printLbl.gameObject);
            printLbl.raycastTarget = false;

            // Selector de Cantidad de Copias (Mín 1, Máx 10) ABAJO del botón de impresión
            GameObject qtyBarObj = CreateUIElement("QuantitySelectorBar", previewPanel.transform);
            RectTransform qbRt = qtyBarObj.GetComponent<RectTransform>();
            qbRt.anchorMin = new Vector2(0.06f, 0.03f);
            qbRt.anchorMax = new Vector2(0.94f, 0.12f);
            qbRt.offsetMin = Vector2.zero;
            qbRt.offsetMax = Vector2.zero;

            Image qbBg = qtyBarObj.AddComponent<Image>();
            qbBg.color = new Color(0.85f, 0.88f, 0.93f);
            qbBg.raycastTarget = false;

            // Botón Disminuir [-]
            GameObject decBtnObj = CreateUIElement("DecQtyButton", qtyBarObj.transform);
            RectTransform decRt = decBtnObj.GetComponent<RectTransform>();
            decRt.anchorMin = new Vector2(0.02f, 0.10f);
            decRt.anchorMax = new Vector2(0.18f, 0.90f);
            decRt.offsetMin = Vector2.zero;
            decRt.offsetMax = Vector2.zero;

            Image decImg = decBtnObj.AddComponent<Image>();
            decImg.color = new Color(0.20f, 0.25f, 0.35f);
            decImg.raycastTarget = true;

            Button decBtn = decBtnObj.AddComponent<Button>();
            decBtn.targetGraphic = decImg;
            TabletButtonTrigger decTrig = decBtnObj.AddComponent<TabletButtonTrigger>();

            Text decTxt = CreateText("DecLabel", decBtnObj.transform, "－", 18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetFullStretch(decTxt.gameObject);
            decTxt.raycastTarget = false;

            // Etiqueta Central de Cantidad
            Text qtyLabel = CreateText("QuantityLabel", qtyBarObj.transform, "1 copia", 15, FontStyle.Bold, new Color(0.12f, 0.16f, 0.25f), TextAnchor.MiddleCenter);
            RectTransform qlRt = qtyLabel.GetComponent<RectTransform>();
            qlRt.anchorMin = new Vector2(0.20f, 0.10f);
            qlRt.anchorMax = new Vector2(0.80f, 0.90f);
            qlRt.offsetMin = Vector2.zero;
            qlRt.offsetMax = Vector2.zero;
            qtyLabel.raycastTarget = false;

            // Botón Aumentar [+]
            GameObject incBtnObj = CreateUIElement("IncQtyButton", qtyBarObj.transform);
            RectTransform incRt = incBtnObj.GetComponent<RectTransform>();
            incRt.anchorMin = new Vector2(0.82f, 0.10f);
            incRt.anchorMax = new Vector2(0.98f, 0.90f);
            incRt.offsetMin = Vector2.zero;
            incRt.offsetMax = Vector2.zero;

            Image incImg = incBtnObj.AddComponent<Image>();
            incImg.color = new Color(0.20f, 0.25f, 0.35f);
            incImg.raycastTarget = true;

            Button incBtn = incBtnObj.AddComponent<Button>();
            incBtn.targetGraphic = incImg;
            TabletButtonTrigger incTrig = incBtnObj.AddComponent<TabletButtonTrigger>();

            Text incTxt = CreateText("IncLabel", incBtnObj.transform, "＋", 18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetFullStretch(incTxt.gameObject);
            incTxt.raycastTarget = false;

            // Mitad Derecha: Lista horizontal de miniaturas
            GameObject scrollObj = CreateHorizontalScrollView("GalleryScroll", panel.transform, new Vector2(0.51f, 0.05f), new Vector2(0.96f, 0.86f), out Transform contentContainer);

            // Mensaje vacío
            Text emptyTxt = CreateText("EmptyState", scrollObj.transform, "Pasa un dibujo por la impresora\npara escanearlo y verlo aquí.", 18, FontStyle.Italic, new Color(0.4f, 0.45f, 0.55f), TextAnchor.MiddleCenter);
            SetFullStretch(emptyTxt.gameObject);
            emptyTxt.raycastTarget = false;

            // Texto de estado
            Text statusTxt = CreateText("StatusMsg", topBar.transform, "", 14, FontStyle.Bold, new Color(0.1f, 0.55f, 0.25f), TextAnchor.MiddleRight);
            RectTransform stRt = statusTxt.GetComponent<RectTransform>();
            stRt.anchorMin = new Vector2(0.4f, 0);
            stRt.anchorMax = new Vector2(0.85f, 1);
            stRt.offsetMin = Vector2.zero;
            stRt.offsetMax = Vector2.zero;
            statusTxt.raycastTarget = false;

            galleryApp = panel.AddComponent<TabletGalleryApp>();
            galleryApp.BindUI(contentContainer, artImg, titleTxt, printBtn, homeBtn, statusTxt, emptyTxt, qtyLabel, decBtn, incBtn);

            TabletGalleryApp localGallery = galleryApp;
            ptTrig.SetAction("Imprimir Copia", () => localGallery?.OnPrintClicked());
            decTrig.SetAction("Menos Copias", () => localGallery?.DecreaseQuantity());
            incTrig.SetAction("Más Copias", () => localGallery?.IncreaseQuantity());

            return panel;
        }

        #endregion

        #region Pantalla Tienda (Bocetos 2 y 3)

        private GameObject CreateStoreScreen(Transform parent, out TabletStoreApp storeApp)
        {
            GameObject panel = CreateUIElement("StoreScreen", parent);
            SetFullStretch(panel);

            // Barra Superior con pestañas y Botón de Casa (🏠) según bocetos
            GameObject storeHeader = CreateUIElement("StoreHeader", panel.transform);
            RectTransform shRt = storeHeader.GetComponent<RectTransform>();
            shRt.anchorMin = new Vector2(0, 0.87f);
            shRt.anchorMax = new Vector2(1, 1);
            shRt.offsetMin = Vector2.zero;
            shRt.offsetMax = Vector2.zero;

            Image shBg = storeHeader.AddComponent<Image>();
            shBg.color = new Color(0.90f, 0.92f, 0.95f);
            shBg.raycastTarget = false;

            // Pestaña TIENDA
            GameObject tabStoreObj = CreateUIElement("TabStore", storeHeader.transform);
            RectTransform tsRt = tabStoreObj.GetComponent<RectTransform>();
            tsRt.anchorMin = new Vector2(0.04f, 0.12f);
            tsRt.anchorMax = new Vector2(0.24f, 0.88f);
            tsRt.offsetMin = Vector2.zero;
            tsRt.offsetMax = Vector2.zero;

            Image tsImg = tabStoreObj.AddComponent<Image>();
            tsImg.color = Color.white;
            tsImg.raycastTarget = true;
            Button tabStoreBtn = tabStoreObj.AddComponent<Button>();
            tabStoreBtn.targetGraphic = tsImg;

            Text tabStoreLabel = CreateText("Lbl", tabStoreObj.transform, "TIENDA", 16, FontStyle.Bold, new Color(0.1f, 0.12f, 0.18f), TextAnchor.MiddleCenter);
            SetFullStretch(tabStoreLabel.gameObject);
            tabStoreLabel.raycastTarget = false;

            // Pestaña PEDIDOS
            GameObject tabOrdersObj = CreateUIElement("TabOrders", storeHeader.transform);
            RectTransform toRt = tabOrdersObj.GetComponent<RectTransform>();
            toRt.anchorMin = new Vector2(0.26f, 0.12f);
            toRt.anchorMax = new Vector2(0.46f, 0.88f);
            toRt.offsetMin = Vector2.zero;
            toRt.offsetMax = Vector2.zero;

            Image toImg = tabOrdersObj.AddComponent<Image>();
            toImg.color = new Color(0.85f, 0.86f, 0.90f);
            toImg.raycastTarget = true;
            Button tabOrdersBtn = tabOrdersObj.AddComponent<Button>();
            tabOrdersBtn.targetGraphic = toImg;

            Text tabOrdersLabel = CreateText("Lbl", tabOrdersObj.transform, "PEDIDOS", 16, FontStyle.Bold, new Color(0.4f, 0.45f, 0.52f), TextAnchor.MiddleCenter);
            SetFullStretch(tabOrdersLabel.gameObject);
            tabOrdersLabel.raycastTarget = false;

            // BOTÓN DESTACADO PARA VOLVER AL MENÚ PRINCIPAL (← 🏠 VOLVER)
            Button homeBtn = CreateReturnButton(storeHeader.transform, new Vector2(0.98f, 0.5f), new Vector2(0.98f, 0.5f), new Vector2(160, 44));

            // Contenedor Panel Tienda (Scroll horizontal con productos y botón MÁS)
            GameObject storePanel = CreateHorizontalScrollView("StoreScrollView", panel.transform, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.85f), out Transform storeContent);

            // Contenedor Panel Pedidos (Scroll horizontal con pedidos entrantes)
            GameObject ordersPanel = CreateHorizontalScrollView("OrdersScrollView", panel.transform, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.85f), out Transform ordersContent);
            ordersPanel.SetActive(false);

            // Modal Crear Producto (Pop-up al presionar MÁS)
            GameObject modalObj = CreateNewProductModal(panel.transform, out Transform pickerContent, out Image previewImg, out Text priceTxt, out Text nameTxt, out Button minusBtn, out Button plusBtn, out Button publishBtn, out Button closeBtn, out Text errorTxt);
            modalObj.SetActive(false);

            storeApp = panel.AddComponent<TabletStoreApp>();
            storeApp.BindUI(
                tabStoreBtn, tabOrdersBtn, tabStoreLabel, tabOrdersLabel, homeBtn,
                storePanel, ordersPanel, storeContent, ordersContent,
                modalObj, pickerContent, previewImg, priceTxt, nameTxt,
                minusBtn, plusBtn, publishBtn, closeBtn, errorTxt
            );

            TabletStoreApp localStore = storeApp;
            TabletButtonTrigger tst = tabStoreObj.AddComponent<TabletButtonTrigger>();
            tst.SetAction("Catálogo", () => localStore?.SelectStoreTab());

            TabletButtonTrigger tot = tabOrdersObj.AddComponent<TabletButtonTrigger>();
            tot.SetAction("Pedidos", () => localStore?.SelectOrdersTab());

            return panel;
        }

        private GameObject CreateNewProductModal(
            Transform parent, out Transform pickerContent, out Image previewImg, out Text priceTxt, out Text nameTxt,
            out Button minusBtn, out Button plusBtn, out Button publishBtn, out Button closeBtn, out Text errorTxt)
        {
            GameObject modal = CreateUIElement("NewProductModal", parent);
            SetFullStretch(modal);

            Image blocker = modal.AddComponent<Image>();
            blocker.color = new Color(0, 0, 0, 0.65f);

            GameObject box = CreateUIElement("DialogBox", modal.transform);
            RectTransform boxRt = box.GetComponent<RectTransform>();
            boxRt.anchorMin = new Vector2(0.12f, 0.08f);
            boxRt.anchorMax = new Vector2(0.88f, 0.92f);
            boxRt.offsetMin = Vector2.zero;
            boxRt.offsetMax = Vector2.zero;

            Image boxImg = box.AddComponent<Image>();
            boxImg.color = Color.white;

            Text titleModal = CreateText("Title", box.transform, "Publicar Producto en Tienda", 20, FontStyle.Bold, Color.black, TextAnchor.MiddleLeft);
            RectTransform tRt = titleModal.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0.05f, 0.88f);
            tRt.anchorMax = new Vector2(0.85f, 0.98f);
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;
            titleModal.raycastTarget = false;

            GameObject closeObj = CreateUIElement("CloseBtn", box.transform);
            RectTransform cRt = closeObj.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0.92f, 0.88f);
            cRt.anchorMax = new Vector2(0.98f, 0.98f);
            cRt.offsetMin = Vector2.zero;
            cRt.offsetMax = Vector2.zero;
            closeBtn = closeObj.AddComponent<Button>();
            Image cImg = closeObj.AddComponent<Image>();
            cImg.color = new Color(0.9f, 0.9f, 0.9f);
            closeBtn.targetGraphic = cImg;
            Text xTxt = CreateText("X", closeObj.transform, "✕", 18, FontStyle.Bold, Color.red, TextAnchor.MiddleCenter);
            SetFullStretch(xTxt.gameObject);
            xTxt.raycastTarget = false;

            Text photoSub = CreateText("PhotoSub", box.transform, "1. Selecciona la foto que tomaste con la cámara:", 15, FontStyle.Normal, new Color(0.3f, 0.3f, 0.3f), TextAnchor.MiddleLeft);
            RectTransform psRt = photoSub.GetComponent<RectTransform>();
            psRt.anchorMin = new Vector2(0.05f, 0.78f);
            psRt.anchorMax = new Vector2(0.95f, 0.86f);
            psRt.offsetMin = Vector2.zero;
            psRt.offsetMax = Vector2.zero;
            photoSub.raycastTarget = false;

            CreateHorizontalScrollView("PhotoPickerScroll", box.transform, new Vector2(0.05f, 0.44f), new Vector2(0.65f, 0.76f), out pickerContent);

            GameObject prevObj = CreateUIElement("SelectedPreview", box.transform);
            RectTransform prRt = prevObj.GetComponent<RectTransform>();
            prRt.anchorMin = new Vector2(0.7f, 0.44f);
            prRt.anchorMax = new Vector2(0.95f, 0.76f);
            prRt.offsetMin = Vector2.zero;
            prRt.offsetMax = Vector2.zero;
            previewImg = prevObj.AddComponent<Image>();
            previewImg.color = new Color(0.85f, 0.85f, 0.85f);
            previewImg.raycastTarget = false;

            nameTxt = CreateText("NameLabel", box.transform, "Nombre: Producto", 16, FontStyle.Bold, Color.black, TextAnchor.MiddleLeft);
            RectTransform nRt = nameTxt.GetComponent<RectTransform>();
            nRt.anchorMin = new Vector2(0.05f, 0.30f);
            nRt.anchorMax = new Vector2(0.45f, 0.40f);
            nRt.offsetMin = Vector2.zero;
            nRt.offsetMax = Vector2.zero;
            nameTxt.raycastTarget = false;

            Text priceTit = CreateText("PriceTitle", box.transform, "Precio de Venta:", 16, FontStyle.Bold, Color.black, TextAnchor.MiddleLeft);
            RectTransform ptRt = priceTit.GetComponent<RectTransform>();
            ptRt.anchorMin = new Vector2(0.5f, 0.30f);
            ptRt.anchorMax = new Vector2(0.7f, 0.40f);
            ptRt.offsetMin = Vector2.zero;
            ptRt.offsetMax = Vector2.zero;
            priceTit.raycastTarget = false;

            GameObject mObj = CreateUIElement("MinusBtn", box.transform);
            RectTransform mRt = mObj.GetComponent<RectTransform>();
            mRt.anchorMin = new Vector2(0.71f, 0.28f);
            mRt.anchorMax = new Vector2(0.77f, 0.42f);
            mRt.offsetMin = Vector2.zero;
            mRt.offsetMax = Vector2.zero;
            minusBtn = mObj.AddComponent<Button>();
            Image mImg = mObj.AddComponent<Image>();
            mImg.color = new Color(0.85f, 0.85f, 0.85f);
            minusBtn.targetGraphic = mImg;
            Text mTxt = CreateText("MinusLbl", mObj.transform, "-", 20, FontStyle.Bold, Color.black, TextAnchor.MiddleCenter);
            SetFullStretch(mTxt.gameObject);
            mTxt.raycastTarget = false;

            priceTxt = CreateText("PriceText", box.transform, "$5.000", 18, FontStyle.Bold, new Color(0.1f, 0.5f, 0.2f), TextAnchor.MiddleCenter);
            RectTransform ptxtRt = priceTxt.GetComponent<RectTransform>();
            ptxtRt.anchorMin = new Vector2(0.78f, 0.30f);
            ptxtRt.anchorMax = new Vector2(0.88f, 0.40f);
            ptxtRt.offsetMin = Vector2.zero;
            ptxtRt.offsetMax = Vector2.zero;
            priceTxt.raycastTarget = false;

            GameObject pObj = CreateUIElement("PlusBtn", box.transform);
            RectTransform pRt = pObj.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0.89f, 0.28f);
            pRt.anchorMax = new Vector2(0.95f, 0.42f);
            pRt.offsetMin = Vector2.zero;
            pRt.offsetMax = Vector2.zero;
            plusBtn = pObj.AddComponent<Button>();
            Image pImg = pObj.AddComponent<Image>();
            pImg.color = new Color(0.85f, 0.85f, 0.85f);
            plusBtn.targetGraphic = pImg;
            Text pTxt = CreateText("PlusLbl", pObj.transform, "+", 20, FontStyle.Bold, Color.black, TextAnchor.MiddleCenter);
            SetFullStretch(pTxt.gameObject);
            pTxt.raycastTarget = false;

            GameObject pubObj = CreateUIElement("PublishBtn", box.transform);
            RectTransform pubRt = pubObj.GetComponent<RectTransform>();
            pubRt.anchorMin = new Vector2(0.35f, 0.08f);
            pubRt.anchorMax = new Vector2(0.65f, 0.20f);
            pubRt.offsetMin = Vector2.zero;
            pubRt.offsetMax = Vector2.zero;
            publishBtn = pubObj.AddComponent<Button>();
            Image pubImg = pubObj.AddComponent<Image>();
            pubImg.color = new Color(0.2f, 0.6f, 0.3f);
            publishBtn.targetGraphic = pubImg;
            Text pubTxt = CreateText("PubLbl", pubObj.transform, "PUBLICAR", 16, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetFullStretch(pubTxt.gameObject);
            pubTxt.raycastTarget = false;

            errorTxt = CreateText("ErrorMsg", box.transform, "", 14, FontStyle.Bold, Color.red, TextAnchor.MiddleCenter);
            RectTransform errRt = errorTxt.GetComponent<RectTransform>();
            errRt.anchorMin = new Vector2(0.05f, 0.21f);
            errRt.anchorMax = new Vector2(0.95f, 0.27f);
            errRt.offsetMin = Vector2.zero;
            errRt.offsetMax = Vector2.zero;
            errorTxt.raycastTarget = false;

            return modal;
        }

        #endregion

        #region Utilidades de UI

        private GameObject CreateUIElement(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private void SetFullStretch(GameObject obj)
        {
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private Text CreateText(string name, Transform parent, string content, int fontSize, FontStyle style, Color color, TextAnchor anchor)
        {
            GameObject txtObj = CreateUIElement(name, parent);
            Text txt = txtObj.AddComponent<Text>();
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = anchor;
            txt.font = UIFontHelper.GetDefaultFont();
            txt.raycastTarget = false;
            return txt;
        }

        /// <summary>
        /// Crea un botón destacado de Volver / Menú Principal (← 🏠 VOLVER) con color llamativo y alto contraste.
        /// </summary>
        private Button CreateReturnButton(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
        {
            GameObject returnObj = CreateUIElement("ReturnHomeButton", parent);
            RectTransform rt = returnObj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = sizeDelta;

            Image img = returnObj.AddComponent<Image>();
            img.color = new Color(0.13f, 0.44f, 0.88f); // Azul zafiro vibrante
            img.raycastTarget = true;

            Button btn = returnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(0.20f, 0.55f, 1.0f);
            cb.pressedColor = new Color(0.08f, 0.32f, 0.72f);
            btn.colors = cb;

            TabletButtonTrigger trig = returnObj.AddComponent<TabletButtonTrigger>();
            trig.SetAction("Volver al Inicio", () => TabletManager.Instance?.ShowHome());

            Text lbl = CreateText("Lbl", returnObj.transform, "← 🏠 VOLVER", 15, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetFullStretch(lbl.gameObject);
            lbl.raycastTarget = false;

            return btn;
        }

        private GameObject CreateTopHeaderWithHouse(Transform parent, string title, out Button homeBtn)
        {
            GameObject topBar = CreateUIElement("TopHeader", parent);
            RectTransform tbRt = topBar.GetComponent<RectTransform>();
            tbRt.anchorMin = new Vector2(0, 0.87f);
            tbRt.anchorMax = new Vector2(1, 1);
            tbRt.offsetMin = Vector2.zero;
            tbRt.offsetMax = Vector2.zero;

            Text titleText = CreateText("HeaderTitle", topBar.transform, title, 22, FontStyle.Bold, new Color(0.08f, 0.12f, 0.20f), TextAnchor.MiddleLeft);
            RectTransform titRt = titleText.GetComponent<RectTransform>();
            titRt.anchorMin = new Vector2(0.04f, 0);
            titRt.anchorMax = new Vector2(0.70f, 1);
            titRt.offsetMin = Vector2.zero;
            titRt.offsetMax = Vector2.zero;
            titleText.raycastTarget = false;

            // Botón destacado de volver al menú principal (← 🏠 VOLVER)
            homeBtn = CreateReturnButton(topBar.transform, new Vector2(0.98f, 0.5f), new Vector2(0.98f, 0.5f), new Vector2(160, 44));
            return topBar;
        }

        private GameObject CreateHorizontalScrollView(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, out Transform contentTransform)
        {
            GameObject scrollObj = CreateUIElement(name, parent);
            RectTransform sRt = scrollObj.GetComponent<RectTransform>();
            sRt.anchorMin = anchorMin;
            sRt.anchorMax = anchorMax;
            sRt.offsetMin = Vector2.zero;
            sRt.offsetMax = Vector2.zero;

            Image sBg = scrollObj.AddComponent<Image>();
            sBg.color = new Color(0.95f, 0.95f, 0.97f, 0.8f);
            sBg.raycastTarget = false;

            ScrollRect sr = scrollObj.AddComponent<ScrollRect>();
            sr.horizontal = true;
            sr.vertical = false;

            GameObject viewportObj = CreateUIElement("Viewport", scrollObj.transform);
            SetFullStretch(viewportObj);
            viewportObj.AddComponent<RectMask2D>();

            GameObject contentObj = CreateUIElement("Content", viewportObj.transform);
            RectTransform cRt = contentObj.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0, 0);
            cRt.anchorMax = new Vector2(0, 1);
            cRt.pivot = new Vector2(0, 0.5f);
            cRt.sizeDelta = new Vector2(0, 0);

            HorizontalLayoutGroup hlg = contentObj.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20f;
            hlg.padding = new RectOffset(20, 20, 15, 15);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            ContentSizeFitter csf = contentObj.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            sr.viewport = viewportObj.GetComponent<RectTransform>();
            sr.content = cRt;

            contentTransform = contentObj.transform;
            return scrollObj;
        }

        #endregion

        private void Awake()
        {
            BuildUIIfNeeded();
        }

        private void Start()
        {
            HookEvents();
        }

        private void OnEnable()
        {
            HookEvents();
        }

        private void OnDestroy()
        {
            var tm = GetComponent<TabletManager>() ?? TabletManager.Instance;
            if (tm != null)
            {
                tm.OnTabletOpenStateChanged -= OnTabletOpenChanged;
            }
        }

        public void HookEvents()
        {
            var tm = GetComponent<TabletManager>() ?? TabletManager.Instance;
            if (tm != null && BuiltCanvas != null)
            {
                tm.OnTabletOpenStateChanged -= OnTabletOpenChanged;
                tm.OnTabletOpenStateChanged += OnTabletOpenChanged;
                BuiltCanvas.gameObject.SetActive(tm.IsTabletOpen);
            }
            else if (BuiltCanvas != null)
            {
                BuiltCanvas.gameObject.SetActive(false);
            }
        }

        private void OnTabletOpenChanged(bool isOpen)
        {
            if (BuiltCanvas != null)
            {
                BuiltCanvas.gameObject.SetActive(isOpen);
            }
        }
    }
}
