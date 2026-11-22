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
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
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

                if (TabletManager.Instance != null && !TabletManager.Instance.IsTabletOpen)
                {
                    canvasObj.SetActive(false);
                }
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

            Text titleText = CreateText("TabletModelTitle", topFrameBar.transform, "SUNY TAB - TALLER DE MERCHANDISING", 24, FontStyle.Bold, new Color(0.85f, 0.88f, 0.95f), TextAnchor.MiddleLeft);
            RectTransform ttRt = titleText.GetComponent<RectTransform>();
            ttRt.anchorMin = new Vector2(0.03f, 0);
            ttRt.anchorMax = new Vector2(0.5f, 1);
            ttRt.offsetMin = Vector2.zero;
            ttRt.offsetMax = Vector2.zero;
            titleText.raycastTarget = false;

            // El botón rojo de guardar tableta queda oculto por el formato interactivo 3D del taller
            GameObject closeBtnObj = CreateUIElement("CloseTabletButton", topFrameBar.transform);
            closeBtnObj.SetActive(false);

            // 3. Área de pantalla activa (Display donde se ven las apps)
            GameObject screenArea = CreateUIElement("ScreenArea", deviceFrame.transform);
            RectTransform screenRt = screenArea.GetComponent<RectTransform>();
            screenRt.anchorMin = new Vector2(0.02f, 0.025f);
            screenRt.anchorMax = new Vector2(0.98f, 0.92f);
            screenRt.offsetMin = Vector2.zero;
            screenRt.offsetMax = Vector2.zero;

            Image screenBg = screenArea.AddComponent<Image>();
            Sprite wallpaperSprite = Resources.Load<Sprite>("TabletUI/fondo tableta");
            if (wallpaperSprite != null)
            {
                screenBg.sprite = wallpaperSprite;
                screenBg.color = Color.white;
                screenBg.type = Image.Type.Simple;
            }
            else
            {
                screenBg.color = new Color(0.97f, 0.98f, 0.99f);
            }
            screenBg.raycastTarget = false;

            // Crear los 4 paneles principales de la tableta
            GameObject homePanel = CreateHomeScreen(screenArea.transform);
            GameObject imagesPanel = CreateImagesScreen(screenArea.transform, out TabletGalleryApp galleryApp);
            GameObject cameraPanel = CreateCameraScreen(screenArea.transform, out TabletCameraApp cameraApp);
            GameObject storePanel = CreateStoreScreen(screenArea.transform, out TabletStoreApp storeApp);

            // Barra inferior persistente con botón Home (estilo dock según imagen de referencia)
            GameObject bottomDock = CreateUIElement("BottomDockBar", screenArea.transform);
            RectTransform dockRt = bottomDock.GetComponent<RectTransform>();
            dockRt.anchorMin = new Vector2(0f, 0f);
            dockRt.anchorMax = new Vector2(1f, 0.10f); // 10% inferior sobre la franja gris del fondo
            dockRt.offsetMin = Vector2.zero;
            dockRt.offsetMax = Vector2.zero;

            Image dockBg = bottomDock.AddComponent<Image>();
            dockBg.color = new Color(0.49f, 0.47f, 0.45f, 0.95f); // Tono gris cálido de la barra inferior
            dockBg.raycastTarget = false;

            GameObject dockHomeBtn = CreateUIElement("DockHomeButton", bottomDock.transform);
            RectTransform dhbRt = dockHomeBtn.GetComponent<RectTransform>();
            dhbRt.anchorMin = new Vector2(0.5f, 0.5f);
            dhbRt.anchorMax = new Vector2(0.5f, 0.5f);
            dhbRt.pivot = new Vector2(0.5f, 0.5f);
            dhbRt.anchoredPosition = Vector2.zero;
            dhbRt.sizeDelta = new Vector2(65, 58);

            Image dhbImg = dockHomeBtn.AddComponent<Image>();
            Sprite homeSprite = Resources.Load<Sprite>("TabletUI/boton_hogarvolver");
            if (homeSprite != null)
            {
                dhbImg.sprite = homeSprite;
                dhbImg.preserveAspect = true;
                dhbImg.color = Color.white;
            }
            else
            {
                dhbImg.color = Color.white;
            }
            dhbImg.raycastTarget = true;

            Button homeBtn = dockHomeBtn.AddComponent<Button>();
            homeBtn.targetGraphic = dhbImg;
            ColorBlock hcb = homeBtn.colors;
            hcb.normalColor = Color.white;
            hcb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            hcb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            homeBtn.colors = hcb;

            TabletButtonTrigger dht = dockHomeBtn.AddComponent<TabletButtonTrigger>();
            dht.SetAction("Volver al Inicio", () => TabletManager.Instance?.ShowHome());

            bottomDock.transform.SetAsLastSibling();

            // Configurar TabletUIController
            TabletUIController uiCtrl = GetComponent<TabletUIController>();
            if (uiCtrl == null) uiCtrl = gameObject.AddComponent<TabletUIController>();

            uiCtrl.SetReferences(homePanel, imagesPanel, cameraPanel, storePanel, galleryApp, cameraApp, storeApp);
            uiCtrl.UpdateActiveScreen(TabletScreen.Home);

            HookEvents();
            uiCtrl.WireAllButtons();

            // TabletInteractiveScreen en el modelo 3D
            // Asegurar que el Rigidbody se mantenga cinemático al reposar sobre la mesa
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            // En Realidad Virtual, montar inmediatamente la interfaz sobre la pantalla física 3D
            if (XRPlayerRigManager.Instance != null && XRPlayerRigManager.Instance.IsVRActive)
            {
                XRPlayerRigManager.Instance.MountTabletCanvasOnScreen(gameObject);
            }
            else if (TabletManager.Instance != null && !TabletManager.Instance.IsTabletOpen)
            {
                canvasObj.SetActive(false);
            }

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
            RectTransform pRt = panel.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0, 0.10f); // Área utilizable sobre el dock inferior
            pRt.anchorMax = new Vector2(1, 1f);
            pRt.offsetMin = Vector2.zero;
            pRt.offsetMax = Vector2.zero;

            // Contenedor centrado para los 3 botones principales de apps (según diseño de referencia visual)
            GameObject appsContainer = CreateUIElement("AppsGrid", panel.transform);
            RectTransform gridRt = appsContainer.GetComponent<RectTransform>();
            gridRt.anchorMin = new Vector2(0.5f, 0.50f);
            gridRt.anchorMax = new Vector2(0.5f, 0.50f);
            gridRt.pivot = new Vector2(0.5f, 0.5f);
            gridRt.anchoredPosition = Vector2.zero;
            gridRt.sizeDelta = new Vector2(1000, 300);

            HorizontalLayoutGroup hlg = appsContainer.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 70f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // 1. Cámara (Izquierda según boceto de referencia)
            CreateStyledAppButton(appsContainer.transform, "Cámara", "TabletUI/boton_camara", () =>
            {
                TabletManager.Instance?.ShowCamera();
            });

            // 2. Galería de Fotos (Centro según boceto de referencia)
            CreateStyledAppButton(appsContainer.transform, "Galería de Fotos", "TabletUI/boton_galeria", () =>
            {
                TabletManager.Instance?.ShowImages();
            });

            // 3. Tienda de Arte (Derecha según boceto de referencia)
            CreateStyledAppButton(appsContainer.transform, "Tienda de Arte", "TabletUI/boton_tienda", () =>
            {
                TabletManager.Instance?.ShowStore();
            });

            LayoutRebuilder.ForceRebuildLayoutImmediate(gridRt);

            return panel;
        }

        private GameObject CreateStyledAppButton(Transform parent, string title, string resourcePath, UnityEngine.Events.UnityAction onClick)
        {
            GameObject appObj = CreateUIElement($"App_{title}", parent);
            RectTransform appRt = appObj.GetComponent<RectTransform>();
            appRt.sizeDelta = new Vector2(265, 260);

            LayoutElement le = appObj.AddComponent<LayoutElement>();
            le.minWidth = 265;
            le.minHeight = 260;
            le.preferredWidth = 265;
            le.preferredHeight = 260;

            Image appImg = appObj.AddComponent<Image>();
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
            {
                appImg.sprite = sprite;
                appImg.preserveAspect = true;
            }
            appImg.color = Color.white;
            appImg.raycastTarget = true;

            Button fullBtn = appObj.AddComponent<Button>();
            fullBtn.targetGraphic = appImg;
            ColorBlock cb = fullBtn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1.0f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1.0f);
            cb.selectedColor = Color.white;
            fullBtn.colors = cb;
            fullBtn.onClick.AddListener(onClick);

            TabletButtonTrigger trig = appObj.AddComponent<TabletButtonTrigger>();
            trig.SetAction($"Abrir {title}", () => onClick?.Invoke());

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
            vfRt.anchorMax = new Vector2(0.79f, 0.96f);
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

            // Barra lateral derecha (Controles de cámara amplios y ergonómicos)
            GameObject rightBar = CreateUIElement("CameraControls", panel.transform);
            RectTransform rbRt = rightBar.GetComponent<RectTransform>();
            rbRt.anchorMin = new Vector2(0.80f, 0.04f);
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
            hRt.sizeDelta = new Vector2(96, 85);

            Image hImg = homeObj.AddComponent<Image>();
            Sprite camHouseSp = Resources.Load<Sprite>("TabletUI/boton_hogarvolver");
            if (camHouseSp != null)
            {
                hImg.sprite = camHouseSp;
                hImg.preserveAspect = true;
                hImg.color = Color.white;
            }
            else
            {
                hImg.color = new Color(0.18f, 0.22f, 0.30f);
            }
            hImg.raycastTarget = true;

            Button homeBtn = homeObj.AddComponent<Button>();
            homeBtn.targetGraphic = hImg;
            ColorBlock hcb = homeBtn.colors;
            hcb.normalColor = Color.white;
            hcb.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            hcb.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            homeBtn.colors = hcb;

            TabletButtonTrigger htTrig = homeObj.AddComponent<TabletButtonTrigger>();
            htTrig.SetAction("Volver al Inicio", () => TabletManager.Instance?.ShowHome());

            // BOTÓN DISPARADOR NEGRO CIRCULAR AGRANDADO (150x150 para fácil pulsación en VR)
            GameObject shutterObj = CreateUIElement("ShutterButton", rightBar.transform);
            RectTransform shutRt = shutterObj.GetComponent<RectTransform>();
            shutRt.anchorMin = new Vector2(0.5f, 0.52f);
            shutRt.anchorMax = new Vector2(0.5f, 0.52f);
            shutRt.sizeDelta = new Vector2(150, 150);

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

            Text shutLbl = CreateText("Lbl", shutterObj.transform, "📸 FOTO", 26, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetFullStretch(shutLbl.gameObject);
            shutLbl.raycastTarget = false;

            // CONTROL DE ZOOM DE CÁMARA (Debajo del disparador)
            GameObject zoomGroup = CreateUIElement("ZoomControls", rightBar.transform);
            RectTransform zgRt = zoomGroup.GetComponent<RectTransform>();
            zgRt.anchorMin = new Vector2(0.5f, 0.18f);
            zgRt.anchorMax = new Vector2(0.5f, 0.18f);
            zgRt.sizeDelta = new Vector2(140, 140);

            // Botón Zoom In [+]
            GameObject zInObj = CreateUIElement("ZoomInBtn", zoomGroup.transform);
            RectTransform zinRt = zInObj.GetComponent<RectTransform>();
            zinRt.anchorMin = new Vector2(0.5f, 0.5f);
            zinRt.anchorMax = new Vector2(0.5f, 0.5f);
            zinRt.anchoredPosition = new Vector2(0, 46);
            zinRt.sizeDelta = new Vector2(110, 44);

            Image zinImg = zInObj.AddComponent<Image>();
            zinImg.color = new Color(0.18f, 0.22f, 0.30f, 0.95f);
            Button zInBtn = zInObj.AddComponent<Button>();
            zInBtn.targetGraphic = zinImg;

            Text zinLbl = CreateText("Lbl", zInObj.transform, "🔍 +", 24, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetFullStretch(zinLbl.gameObject);
            zinLbl.raycastTarget = false;

            // Indicador de Zoom (Ej: "1.0x")
            GameObject zTxtObj = CreateUIElement("ZoomText", zoomGroup.transform);
            RectTransform ztxtRt = zTxtObj.GetComponent<RectTransform>();
            ztxtRt.anchorMin = new Vector2(0.5f, 0.5f);
            ztxtRt.anchorMax = new Vector2(0.5f, 0.5f);
            ztxtRt.anchoredPosition = new Vector2(0, 4);
            ztxtRt.sizeDelta = new Vector2(110, 32);

            Text zoomTxt = CreateText("ZoomValue", zTxtObj.transform, "1.0x", 26, FontStyle.Bold, Color.cyan, TextAnchor.MiddleCenter);
            SetFullStretch(zoomTxt.gameObject);
            zoomTxt.raycastTarget = false;

            // Botón Zoom Out [-]
            GameObject zOutObj = CreateUIElement("ZoomOutBtn", zoomGroup.transform);
            RectTransform zoutRt = zOutObj.GetComponent<RectTransform>();
            zoutRt.anchorMin = new Vector2(0.5f, 0.5f);
            zoutRt.anchorMax = new Vector2(0.5f, 0.5f);
            zoutRt.anchoredPosition = new Vector2(0, -38);
            zoutRt.sizeDelta = new Vector2(110, 44);

            Image zoutImg = zOutObj.AddComponent<Image>();
            zoutImg.color = new Color(0.18f, 0.22f, 0.30f, 0.95f);
            Button zOutBtn = zOutObj.AddComponent<Button>();
            zOutBtn.targetGraphic = zoutImg;

            Text zoutLbl = CreateText("Lbl", zOutObj.transform, "🔍 -", 24, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetFullStretch(zoutLbl.gameObject);
            zoutLbl.raycastTarget = false;

            // Script de la cámara
            cameraApp = panel.AddComponent<TabletCameraApp>();
            cameraApp.BindUI(vfImg, shutterBtn, homeBtn, flashImg, zInBtn, zOutBtn, zoomTxt);

            TabletCameraApp localCam = cameraApp;
            TabletButtonTrigger stTrig = shutterObj.GetComponent<TabletButtonTrigger>() ?? shutterObj.AddComponent<TabletButtonTrigger>();
            stTrig.SetAction("Disparar Foto", () => localCam?.TakePhoto());

            TabletButtonTrigger zinTrig = zInObj.GetComponent<TabletButtonTrigger>() ?? zInObj.AddComponent<TabletButtonTrigger>();
            zinTrig.SetAction("Zoom +", () => localCam?.ZoomIn());

            TabletButtonTrigger zoutTrig = zOutObj.GetComponent<TabletButtonTrigger>() ?? zOutObj.AddComponent<TabletButtonTrigger>();
            zoutTrig.SetAction("Zoom -", () => localCam?.ZoomOut());

            return panel;
        }

        #endregion

        #region Pantalla Imágenes (Galería de Dibujos e Imprimir)

        private GameObject CreateImagesScreen(Transform parent, out TabletGalleryApp galleryApp)
        {
            GameObject panel = CreateUIElement("ImagesScreen", parent);
            SetFullStretch(panel);

            // Barra Superior con Botón de Casa
            GameObject topBar = CreateTopHeaderWithHouse(panel.transform, "📷 GALERÍA DE FOTOS", out Button homeBtn);

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
            imgRt.localScale = new Vector3(-1f, 1f, 1f);

            // Título de la ilustración
            Text titleTxt = CreateText("ArtworkTitle", previewPanel.transform, "Selecciona una ilustración", 30, FontStyle.Bold, new Color(0.08f, 0.12f, 0.20f), TextAnchor.MiddleCenter);
            RectTransform titRt = titleTxt.GetComponent<RectTransform>();
            titRt.anchorMin = new Vector2(0.05f, 0.27f);
            titRt.anchorMax = new Vector2(0.95f, 0.37f);
            titRt.offsetMin = Vector2.zero;
            titRt.offsetMax = Vector2.zero;
            titleTxt.raycastTarget = false;

            // Botón IMPRIMIR COPIA
            GameObject printObj = CreateUIElement("PrintButton", previewPanel.transform);
            RectTransform prRt = printObj.GetComponent<RectTransform>();
            prRt.anchorMin = new Vector2(0.06f, 0.14f);
            prRt.anchorMax = new Vector2(0.68f, 0.25f);
            prRt.offsetMin = Vector2.zero;
            prRt.offsetMax = Vector2.zero;

            Image prImg = printObj.AddComponent<Image>();
            prImg.color = new Color(0.14f, 0.65f, 0.32f);
            prImg.raycastTarget = true;

            Button printBtn = printObj.AddComponent<Button>();
            printBtn.targetGraphic = prImg;

            TabletButtonTrigger ptTrig = printObj.AddComponent<TabletButtonTrigger>();

            Text printLbl = CreateText("PrintLabel", printObj.transform, "🖨️ IMPRIMIR", 26, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetFullStretch(printLbl.gameObject);
            printLbl.raycastTarget = false;

            // Botón ELIMINAR FOTO
            GameObject delObj = CreateUIElement("DeletePhotoButton", previewPanel.transform);
            RectTransform delRt = delObj.GetComponent<RectTransform>();
            delRt.anchorMin = new Vector2(0.72f, 0.14f);
            delRt.anchorMax = new Vector2(0.94f, 0.25f);
            delRt.offsetMin = Vector2.zero;
            delRt.offsetMax = Vector2.zero;

            Image delImg = delObj.AddComponent<Image>();
            delImg.color = new Color(0.86f, 0.20f, 0.20f);
            delImg.raycastTarget = true;

            Button deleteBtn = delObj.AddComponent<Button>();
            deleteBtn.targetGraphic = delImg;

            TabletButtonTrigger delTrig = delObj.AddComponent<TabletButtonTrigger>();

            Text delLbl = CreateText("DeleteLabel", delObj.transform, "🗑️ BORRAR", 24, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetFullStretch(delLbl.gameObject);
            delLbl.raycastTarget = false;

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

            Text decTxt = CreateText("DecLabel", decBtnObj.transform, "－", 28, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetFullStretch(decTxt.gameObject);
            decTxt.raycastTarget = false;

            // Etiqueta Central de Cantidad
            Text qtyLabel = CreateText("QuantityLabel", qtyBarObj.transform, "1 copia", 26, FontStyle.Bold, new Color(0.12f, 0.16f, 0.25f), TextAnchor.MiddleCenter);
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

            Text incTxt = CreateText("IncLabel", incBtnObj.transform, "＋", 28, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetFullStretch(incTxt.gameObject);
            incTxt.raycastTarget = false;

            // Barra de Pestañas (🖨️ Escaneos de Impresora vs 📷 Fotos de Cámara)
            GameObject tabBarObj = CreateUIElement("GalleryTabBar", panel.transform);
            RectTransform tbRt = tabBarObj.GetComponent<RectTransform>();
            tbRt.anchorMin = new Vector2(0.51f, 0.79f);
            tbRt.anchorMax = new Vector2(0.96f, 0.86f);
            tbRt.offsetMin = Vector2.zero;
            tbRt.offsetMax = Vector2.zero;

            // Pestaña 1: Escaneos
            GameObject scansTabObj = CreateUIElement("ScansTab", tabBarObj.transform);
            RectTransform stTabRt = scansTabObj.GetComponent<RectTransform>();
            stTabRt.anchorMin = new Vector2(0f, 0f);
            stTabRt.anchorMax = new Vector2(0.48f, 1f);
            stTabRt.offsetMin = Vector2.zero;
            stTabRt.offsetMax = Vector2.zero;
            Image scansTabBg = scansTabObj.AddComponent<Image>();
            scansTabBg.color = new Color(0.12f, 0.48f, 0.95f);
            scansTabBg.raycastTarget = true;
            Button scansTabBtn = scansTabObj.AddComponent<Button>();
            scansTabBtn.targetGraphic = scansTabBg;
            Text scansTabTxt = CreateText("Label", scansTabObj.transform, "🖨️ ESCANEOS", 24, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetFullStretch(scansTabTxt.gameObject);
            scansTabTxt.raycastTarget = false;

            // Pestaña 2: Fotos
            GameObject photosTabObj = CreateUIElement("PhotosTab", tabBarObj.transform);
            RectTransform ptTabRt = photosTabObj.GetComponent<RectTransform>();
            ptTabRt.anchorMin = new Vector2(0.52f, 0f);
            ptTabRt.anchorMax = new Vector2(1f, 1f);
            ptTabRt.offsetMin = Vector2.zero;
            ptTabRt.offsetMax = Vector2.zero;
            Image photosTabBg = photosTabObj.AddComponent<Image>();
            photosTabBg.color = new Color(0.85f, 0.88f, 0.93f);
            photosTabBg.raycastTarget = true;
            Button photosTabBtn = photosTabObj.AddComponent<Button>();
            photosTabBtn.targetGraphic = photosTabBg;
            Text photosTabTxt = CreateText("Label", photosTabObj.transform, "📷 FOTOS", 24, FontStyle.Bold, new Color(0.20f, 0.25f, 0.35f), TextAnchor.MiddleCenter);
            SetFullStretch(photosTabTxt.gameObject);
            photosTabTxt.raycastTarget = false;

            // Mitad Derecha: Lista horizontal de miniaturas
            GameObject scrollObj = CreateHorizontalScrollView("GalleryScroll", panel.transform, new Vector2(0.51f, 0.05f), new Vector2(0.96f, 0.77f), out Transform contentContainer);

            // Mensaje vacío
            Text emptyTxt = CreateText("EmptyState", scrollObj.transform, "Pasa un dibujo por la impresora\npara escanearlo y verlo aquí.", 28, FontStyle.Italic, new Color(0.4f, 0.45f, 0.55f), TextAnchor.MiddleCenter);
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

            Text topTitleText = topBar.GetComponentInChildren<Text>();

            galleryApp = panel.AddComponent<TabletGalleryApp>();
            galleryApp.BindUI(contentContainer, artImg, titleTxt, printBtn, homeBtn, statusTxt, emptyTxt, qtyLabel, decBtn, incBtn, deleteBtn);
            galleryApp.BindTabs(scansTabBtn, photosTabBtn, scansTabBg, photosTabBg, scansTabTxt, photosTabTxt, topTitleText);

            TabletGalleryApp localGallery = galleryApp;
            ptTrig.SetAction("Imprimir Copia", () => localGallery?.OnPrintClicked());
            delTrig.SetAction("Eliminar Foto", () => localGallery?.OnDeletePhotoClicked());
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

            Text tabStoreLabel = CreateText("Lbl", tabStoreObj.transform, "TIENDA", 26, FontStyle.Bold, new Color(0.1f, 0.12f, 0.18f), TextAnchor.MiddleCenter);
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

            // Badge visual de dinero disponible del jugador
            GameObject moneyBadgeObj = CreateUIElement("MoneyBadge", storeHeader.transform);
            RectTransform mbRt = moneyBadgeObj.GetComponent<RectTransform>();
            mbRt.anchorMin = new Vector2(0.48f, 0.14f);
            mbRt.anchorMax = new Vector2(0.64f, 0.86f);
            mbRt.offsetMin = Vector2.zero;
            mbRt.offsetMax = Vector2.zero;

            Image mbImg = moneyBadgeObj.AddComponent<Image>();
            mbImg.color = new Color(0.93f, 0.98f, 0.94f);
            var mbOutline = moneyBadgeObj.AddComponent<Outline>();
            mbOutline.effectColor = new Color(0.18f, 0.65f, 0.32f, 0.45f);
            mbOutline.effectDistance = new Vector2(1.5f, -1.5f);

            Text moneyText = CreateText("MoneyText", moneyBadgeObj.transform, "💰 $0", 15, FontStyle.Bold, new Color(0.08f, 0.52f, 0.22f), TextAnchor.MiddleCenter);
            SetFullStretch(moneyText.gameObject);
            moneyText.raycastTarget = false;

            // BOTÓN DESTACADO EN EL HEADER PARA PUBLICAR PRODUCTO ([ ＋ PUBLICAR ])
            GameObject headerPublishObj = CreateUIElement("HeaderPublishBtn", storeHeader.transform);
            RectTransform hpRt = headerPublishObj.GetComponent<RectTransform>();
            hpRt.anchorMin = new Vector2(0.66f, 0.12f);
            hpRt.anchorMax = new Vector2(0.85f, 0.88f);
            hpRt.offsetMin = Vector2.zero;
            hpRt.offsetMax = Vector2.zero;

            Image hpImg = headerPublishObj.AddComponent<Image>();
            hpImg.color = new Color(0.12f, 0.48f, 0.95f); // Azul vibrante
            var hpOutline = headerPublishObj.AddComponent<Outline>();
            hpOutline.effectColor = new Color(0.05f, 0.25f, 0.65f, 0.6f);
            hpOutline.effectDistance = new Vector2(1.5f, -1.5f);

            Button headerPublishBtn = headerPublishObj.AddComponent<Button>();
            headerPublishBtn.targetGraphic = hpImg;
            Text hpTxt = CreateText("Lbl", headerPublishObj.transform, "＋ PUBLICAR", 18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetFullStretch(hpTxt.gameObject);
            hpTxt.raycastTarget = false;

            TabletButtonTrigger hpt = headerPublishObj.AddComponent<TabletButtonTrigger>();

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
                minusBtn, plusBtn, publishBtn, closeBtn, errorTxt, moneyText,
                headerPublishBtn
            );

            TabletStoreApp localStore = storeApp;
            hpt.SetAction("Publicar Producto", () => localStore?.OpenNewProductModal());
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
            closeObj.AddComponent<TabletButtonTrigger>();

            Text photoSub = CreateText("PhotoSub", box.transform, "1. Selecciona una ilustración o fotografía:", 15, FontStyle.Normal, new Color(0.3f, 0.3f, 0.3f), TextAnchor.MiddleLeft);
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
            prRt.localScale = new Vector3(-1f, 1f, 1f);

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
            mRt.anchorMin = new Vector2(0.68f, 0.26f);
            mRt.anchorMax = new Vector2(0.76f, 0.44f);
            mRt.offsetMin = Vector2.zero;
            mRt.offsetMax = Vector2.zero;
            minusBtn = mObj.AddComponent<Button>();
            Image mImg = mObj.AddComponent<Image>();
            mImg.color = new Color(0.90f, 0.92f, 0.96f);
            var mOut = mObj.AddComponent<Outline>();
            mOut.effectColor = new Color(0.4f, 0.5f, 0.7f, 0.5f);
            mOut.effectDistance = new Vector2(1f, -1f);
            minusBtn.targetGraphic = mImg;
            Text mTxt = CreateText("MinusLbl", mObj.transform, "－", 22, FontStyle.Bold, new Color(0.15f, 0.2f, 0.35f), TextAnchor.MiddleCenter);
            SetFullStretch(mTxt.gameObject);
            mTxt.raycastTarget = false;
            mObj.AddComponent<TabletButtonTrigger>();

            priceTxt = CreateText("PriceText", box.transform, "$2.000", 20, FontStyle.Bold, new Color(0.08f, 0.55f, 0.22f), TextAnchor.MiddleCenter);
            RectTransform ptxtRt = priceTxt.GetComponent<RectTransform>();
            ptxtRt.anchorMin = new Vector2(0.77f, 0.26f);
            ptxtRt.anchorMax = new Vector2(0.89f, 0.44f);
            ptxtRt.offsetMin = Vector2.zero;
            ptxtRt.offsetMax = Vector2.zero;
            priceTxt.raycastTarget = false;

            GameObject pObj = CreateUIElement("PlusBtn", box.transform);
            RectTransform pRt = pObj.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0.90f, 0.26f);
            pRt.anchorMax = new Vector2(0.98f, 0.44f);
            pRt.offsetMin = Vector2.zero;
            pRt.offsetMax = Vector2.zero;
            plusBtn = pObj.AddComponent<Button>();
            Image pImg = pObj.AddComponent<Image>();
            pImg.color = new Color(0.12f, 0.48f, 0.95f);
            var pOut = pObj.AddComponent<Outline>();
            pOut.effectColor = new Color(0.05f, 0.25f, 0.65f, 0.6f);
            pOut.effectDistance = new Vector2(1f, -1f);
            plusBtn.targetGraphic = pImg;
            Text pTxt = CreateText("PlusLbl", pObj.transform, "＋", 22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetFullStretch(pTxt.gameObject);
            pTxt.raycastTarget = false;
            pObj.AddComponent<TabletButtonTrigger>();

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
            pubObj.AddComponent<TabletButtonTrigger>();

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
        /// Crea un botón destacado de Volver / Menú Principal (← 🏠 INICIO) con icono limpio y color llamativo.
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
            img.color = new Color(0.18f, 0.22f, 0.30f); // Tono elegante titanio
            img.raycastTarget = true;

            Button btn = returnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(0.28f, 0.35f, 0.48f);
            cb.pressedColor = new Color(0.12f, 0.16f, 0.22f);
            btn.colors = cb;

            TabletButtonTrigger trig = returnObj.AddComponent<TabletButtonTrigger>();
            trig.SetAction("Volver al Inicio", () => TabletManager.Instance?.ShowHome());

            Sprite houseSp = Resources.Load<Sprite>("TabletUI/boton_hogarvolver");
            if (houseSp != null)
            {
                GameObject houseIconObj = CreateUIElement("HouseIcon", returnObj.transform);
                RectTransform hiRt = houseIconObj.GetComponent<RectTransform>();
                hiRt.anchorMin = new Vector2(0.08f, 0.15f);
                hiRt.anchorMax = new Vector2(0.32f, 0.85f);
                hiRt.offsetMin = Vector2.zero;
                hiRt.offsetMax = Vector2.zero;
                Image hiImg = houseIconObj.AddComponent<Image>();
                hiImg.sprite = houseSp;
                hiImg.preserveAspect = true;
                hiImg.color = Color.white;
                hiImg.raycastTarget = false;

                Text lbl = CreateText("Lbl", returnObj.transform, "INICIO", 22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
                RectTransform lRt = lbl.GetComponent<RectTransform>();
                lRt.anchorMin = new Vector2(0.34f, 0f);
                lRt.anchorMax = new Vector2(0.96f, 1f);
                lRt.offsetMin = Vector2.zero;
                lRt.offsetMax = Vector2.zero;
                lbl.raycastTarget = false;
            }
            else
            {
                Text lbl = CreateText("Lbl", returnObj.transform, "← 🏠 INICIO", 22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
                SetFullStretch(lbl.gameObject);
                lbl.raycastTarget = false;
            }

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
            homeBtn = CreateReturnButton(topBar.transform, new Vector2(0.98f, 0.5f), new Vector2(0.98f, 0.5f), new Vector2(210, 56));
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
            var tm = GetComponent<TabletManager>() != null ? GetComponent<TabletManager>() : TabletManager.Instance;
            if (tm != null)
            {
                tm.OnTabletOpenStateChanged -= OnTabletOpenChanged;
            }
        }

        public void HookEvents()
        {
            var tm = GetComponent<TabletManager>() != null ? GetComponent<TabletManager>() : TabletManager.Instance;
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
