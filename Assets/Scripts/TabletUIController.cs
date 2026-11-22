using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArtSimulator
{
    public class TabletUIController : MonoBehaviour
    {
        [Header("Referencias de Paneles de la Tableta")]
        [SerializeField] private GameObject homeScreenPanel;
        [SerializeField] private GameObject imagesScreenPanel;
        [SerializeField] private GameObject cameraScreenPanel;
        [SerializeField] private GameObject storeScreenPanel;
        [SerializeField] private GameObject bottomDockBar;

        [Header("Apps")]
        [SerializeField] private TabletGalleryApp galleryApp;
        [SerializeField] private TabletCameraApp cameraApp;
        [SerializeField] private TabletStoreApp storeApp;

        private void Awake()
        {
            EnsureReferences();
            WireAllButtons();
        }

        private void Start()
        {
            EnsureReferences();
            WireAllButtons();

            var tm = GetComponent<TabletManager>() != null ? GetComponent<TabletManager>() : TabletManager.Instance;
            if (tm != null)
            {
                tm.OnScreenChanged -= UpdateActiveScreen;
                tm.OnScreenChanged += UpdateActiveScreen;
                tm.OnTabletOpenStateChanged -= OnTabletOpenChanged;
                tm.OnTabletOpenStateChanged += OnTabletOpenChanged;
                UpdateActiveScreen(tm.CurrentScreen);
            }
        }

        private void OnEnable()
        {
            EnsureReferences();
            WireAllButtons();

            var tm = GetComponent<TabletManager>() != null ? GetComponent<TabletManager>() : TabletManager.Instance;
            if (tm != null)
            {
                tm.OnScreenChanged -= UpdateActiveScreen;
                tm.OnScreenChanged += UpdateActiveScreen;
                tm.OnTabletOpenStateChanged -= OnTabletOpenChanged;
                tm.OnTabletOpenStateChanged += OnTabletOpenChanged;
                UpdateActiveScreen(tm.CurrentScreen);
            }
        }

        private void OnDestroy()
        {
            var tm = GetComponent<TabletManager>() != null ? GetComponent<TabletManager>() : TabletManager.Instance;
            if (tm != null)
            {
                tm.OnScreenChanged -= UpdateActiveScreen;
                tm.OnTabletOpenStateChanged -= OnTabletOpenChanged;
            }
        }

        private void OnTabletOpenChanged(bool isOpen)
        {
            if (isOpen)
            {
                EnsureReferences();
                WireAllButtons();
                var tm = GetComponent<TabletManager>() != null ? GetComponent<TabletManager>() : TabletManager.Instance;
                if (tm != null)
                {
                    UpdateActiveScreen(tm.CurrentScreen);
                }
            }
        }

        public void UpdateActiveScreen(TabletScreen screen)
        {
            EnsureReferences();
            WireAllButtons();

            Debug.Log($"<color=cyan>[TabletUIController]</color> Mostrando pantalla: <b>{screen}</b>");
            if (homeScreenPanel != null) homeScreenPanel.SetActive(screen == TabletScreen.Home);
            if (imagesScreenPanel != null) imagesScreenPanel.SetActive(screen == TabletScreen.Images);
            if (cameraScreenPanel != null) cameraScreenPanel.SetActive(screen == TabletScreen.Camera);
            if (storeScreenPanel != null) storeScreenPanel.SetActive(screen == TabletScreen.Store);

            // El botón de casa del dock inferior no es necesario en el menú principal (ya estamos en él),
            // pero se mantiene activo en el resto de aplicaciones (Cámara, Galería, Tienda) para poder volver al inicio.
            if (bottomDockBar != null) bottomDockBar.SetActive(screen != TabletScreen.Home);
        }

        public void EnsureReferences()
        {
            if (homeScreenPanel == null || imagesScreenPanel == null || cameraScreenPanel == null || storeScreenPanel == null || bottomDockBar == null)
            {
                GameObject canvasObj = GetTabletCanvasObject();
                if (canvasObj != null)
                {
                    Transform screenArea = canvasObj.transform.Find("TabletDeviceFrame/ScreenArea");
                    if (screenArea != null)
                    {
                        Transform home = screenArea.Find("HomeScreen");
                        Transform images = screenArea.Find("ImagesScreen");
                        Transform camera = screenArea.Find("CameraScreen");
                        Transform store = screenArea.Find("StoreScreen");
                        Transform dock = screenArea.Find("BottomDockBar");

                        if (home != null) homeScreenPanel = home.gameObject;
                        if (images != null)
                        {
                            imagesScreenPanel = images.gameObject;
                            galleryApp = images.GetComponent<TabletGalleryApp>();
                        }
                        if (camera != null)
                        {
                            cameraScreenPanel = camera.gameObject;
                            cameraApp = camera.GetComponent<TabletCameraApp>();
                        }
                        if (store != null)
                        {
                            storeScreenPanel = store.gameObject;
                            storeApp = store.GetComponent<TabletStoreApp>();
                        }
                        if (dock != null) bottomDockBar = dock.gameObject;
                    }
                }
            }
        }

        public GameObject GetTabletCanvasObject()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].name == "TabletCanvas")
                {
                    return canvases[i].gameObject;
                }
            }
            return null;
        }

        /// <summary>
        /// Reconecta de forma robusta e infalible todos los botones de la UI de la tableta,
        /// asignando tanto el delegado Button.onClick como el componente TabletButtonTrigger (IPointerClickHandler).
        /// </summary>
        public void WireAllButtons()
        {
            GameObject canvasObj = GetTabletCanvasObject();
            if (canvasObj == null) return;

            // 1. Botón Cerrar Tableta (Marco superior)
            SetupButton(canvasObj.transform, "TabletDeviceFrame/TopFrameBar/CloseTabletButton", "Cerrar Tableta", () =>
            {
                if (TabletManager.Instance != null) TabletManager.Instance.CloseTablet();
            });

            // 2. Apps del Menú Principal
            SetupButton(canvasObj.transform, "TabletDeviceFrame/ScreenArea/HomeScreen/AppsGrid/App_Galería de Fotos", "Galería de Fotos", () =>
            {
                if (TabletManager.Instance != null) TabletManager.Instance.ShowImages();
            });

            SetupButton(canvasObj.transform, "TabletDeviceFrame/ScreenArea/HomeScreen/AppsGrid/App_Galería de Arte", "Galería de Arte", () =>
            {
                if (TabletManager.Instance != null) TabletManager.Instance.ShowImages();
            });

            SetupButton(canvasObj.transform, "TabletDeviceFrame/ScreenArea/HomeScreen/AppsGrid/App_Cámara", "Cámara", () =>
            {
                if (TabletManager.Instance != null) TabletManager.Instance.ShowCamera();
            });

            SetupButton(canvasObj.transform, "TabletDeviceFrame/ScreenArea/HomeScreen/AppsGrid/App_Tienda de Arte", "Tienda de Arte", () =>
            {
                if (TabletManager.Instance != null) TabletManager.Instance.ShowStore();
            });

            // Respaldo de búsqueda en AppsGrid para enlazar cualquier variante
            Transform appsGrid = canvasObj.transform.Find("TabletDeviceFrame/ScreenArea/HomeScreen/AppsGrid");
            if (appsGrid != null)
            {
                foreach (Transform child in appsGrid)
                {
                    Button childBtn = child.GetComponent<Button>();
                    if (childBtn == null) continue;

                    string lower = child.name.ToLower();
                    if (lower.Contains("galer") || lower.Contains("foto") || lower.Contains("imag"))
                    {
                        SetupSingleButton(childBtn, "Galería de Fotos", () => TabletManager.Instance?.ShowImages());
                    }
                    else if (lower.Contains("cam"))
                    {
                        SetupSingleButton(childBtn, "Cámara", () => TabletManager.Instance?.ShowCamera());
                    }
                    else if (lower.Contains("tienda") || lower.Contains("store"))
                    {
                        SetupSingleButton(childBtn, "Tienda de Arte", () => TabletManager.Instance?.ShowStore());
                    }
                }
            }

            // 3. Botones Home (Inicio) y Dock Home en todas las pantallas
            SetupButton(canvasObj.transform, "TabletDeviceFrame/ScreenArea/BottomDockBar/DockHomeButton", "Volver al Inicio", () =>
            {
                if (TabletManager.Instance != null) TabletManager.Instance.ShowHome();
            });

            var allButtons = canvasObj.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < allButtons.Length; i++)
            {
                Button btn = allButtons[i];
                string bName = btn.name.ToLower();
                if (bName.Contains("home") || bName.Contains("hogar") || bName.Contains("volver"))
                {
                    SetupSingleButton(btn, "Volver al Inicio", () =>
                    {
                        if (TabletManager.Instance != null) TabletManager.Instance.ShowHome();
                    });
                }
            }

            // 4. Botón Imprimir en Pantalla de Galería
            SetupButton(canvasObj.transform, "TabletDeviceFrame/ScreenArea/ImagesScreen/PreviewPanel/PrintButton", "Imprimir Copia", () =>
            {
                if (galleryApp != null) galleryApp.OnPrintClicked();
                else if (imagesScreenPanel != null)
                {
                    var g = imagesScreenPanel.GetComponent<TabletGalleryApp>();
                    if (g != null) g.OnPrintClicked();
                }
            });

            SetupButton(canvasObj.transform, "TabletDeviceFrame/ScreenArea/ImagesScreen/PreviewPanel/DeletePhotoButton", "Eliminar Foto", () =>
            {
                if (galleryApp != null) galleryApp.OnDeletePhotoClicked();
                else if (imagesScreenPanel != null)
                {
                    var g = imagesScreenPanel.GetComponent<TabletGalleryApp>();
                    if (g != null) g.OnDeletePhotoClicked();
                }
            });

            // Pestañas de Galería (Escaneos vs Fotos)
            SetupButton(canvasObj.transform, "TabletDeviceFrame/ScreenArea/ImagesScreen/GalleryTabBar/ScansTab", "Ver Escaneos", () =>
            {
                if (galleryApp != null) galleryApp.SwitchTab(GalleryTab.Scans);
                else if (imagesScreenPanel != null) imagesScreenPanel.GetComponent<TabletGalleryApp>()?.SwitchTab(GalleryTab.Scans);
            });

            SetupButton(canvasObj.transform, "TabletDeviceFrame/ScreenArea/ImagesScreen/GalleryTabBar/PhotosTab", "Ver Fotos", () =>
            {
                if (galleryApp != null) galleryApp.SwitchTab(GalleryTab.Photos);
                else if (imagesScreenPanel != null) imagesScreenPanel.GetComponent<TabletGalleryApp>()?.SwitchTab(GalleryTab.Photos);
            });

            // 5. Botones de Cámara (Disparador y Zoom)
            SetupButton(canvasObj.transform, "TabletDeviceFrame/ScreenArea/CameraScreen/CameraControls/ShutterButton", "Capturar Foto", () =>
            {
                if (cameraApp != null) cameraApp.TakePhoto();
                else if (cameraScreenPanel != null)
                {
                    var c = cameraScreenPanel.GetComponent<TabletCameraApp>();
                    if (c != null) c.TakePhoto();
                }
            });

            SetupButton(canvasObj.transform, "TabletDeviceFrame/ScreenArea/CameraScreen/CameraControls/ZoomControls/ZoomInBtn", "Zoom +", () =>
            {
                if (cameraApp != null) cameraApp.ZoomIn();
                else if (cameraScreenPanel != null)
                {
                    var c = cameraScreenPanel.GetComponent<TabletCameraApp>();
                    if (c != null) c.ZoomIn();
                }
            });

            SetupButton(canvasObj.transform, "TabletDeviceFrame/ScreenArea/CameraScreen/CameraControls/ZoomControls/ZoomOutBtn", "Zoom -", () =>
            {
                if (cameraApp != null) cameraApp.ZoomOut();
                else if (cameraScreenPanel != null)
                {
                    var c = cameraScreenPanel.GetComponent<TabletCameraApp>();
                    if (c != null) c.ZoomOut();
                }
            });

            // 6. Pestañas de la Tienda
            SetupButton(canvasObj.transform, "TabletDeviceFrame/ScreenArea/StoreScreen/StoreHeader/TabStore", "Pestaña Tienda", () =>
            {
                if (storeApp != null) storeApp.SelectStoreTab();
                else if (storeScreenPanel != null)
                {
                    var s = storeScreenPanel.GetComponent<TabletStoreApp>();
                    if (s != null) s.SelectStoreTab();
                }
            });

            SetupButton(canvasObj.transform, "TabletDeviceFrame/ScreenArea/StoreScreen/StoreHeader/TabOrders", "Pestaña Pedidos", () =>
            {
                if (storeApp != null) storeApp.SelectOrdersTab();
                else if (storeScreenPanel != null)
                {
                    var s = storeScreenPanel.GetComponent<TabletStoreApp>();
                    if (s != null) s.SelectOrdersTab();
                }
            });
        }

        private void SetupButton(Transform root, string relativePath, string actionName, Action callback)
        {
            Transform target = root.Find(relativePath);
            if (target == null) return;

            Button btn = target.GetComponent<Button>();
            if (btn == null) btn = target.gameObject.AddComponent<Button>();

            SetupSingleButton(btn, actionName, callback);
        }

        private void SetupSingleButton(Button btn, string actionName, Action callback)
        {
            if (btn == null) return;

            // Asegurar Graphic interactivo
            Image img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.raycastTarget = true;
                btn.targetGraphic = img;
            }

            // Componente directo IPointerClickHandler para inmunidad ante pérdida de estado
            TabletButtonTrigger trigger = btn.GetComponent<TabletButtonTrigger>();
            if (trigger == null) trigger = btn.gameObject.AddComponent<TabletButtonTrigger>();
            trigger.SetAction(actionName, callback);

            // Delegado Button.onClick nativo canalizado a través del trigger para deduplicación
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => trigger.TriggerClick());
        }

        public void SetReferences(
            GameObject homePanel, GameObject imagesPanel, GameObject cameraPanel, GameObject storePanel,
            TabletGalleryApp gallery, TabletCameraApp camera, TabletStoreApp store)
        {
            homeScreenPanel = homePanel;
            imagesScreenPanel = imagesPanel;
            cameraScreenPanel = cameraPanel;
            storeScreenPanel = storePanel;
            galleryApp = gallery;
            cameraApp = camera;
            storeApp = store;
            WireAllButtons();
        }
    }
}
