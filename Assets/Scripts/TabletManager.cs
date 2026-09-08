using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArtSimulator
{
    public enum TabletScreen
    {
        Home,
        Images,
        Camera,
        Store
    }

    public class TabletManager : MonoBehaviour
    {
        private static TabletManager instance;
        public static TabletManager Instance
        {
            get
            {
                if (instance == null) instance = FindFirstObjectByType<TabletManager>();
                return instance;
            }
            private set => instance = value;
        }

        [Header("Datos Almacenados en la Tableta")]
        [SerializeField] private List<ArtworkData> scannedArtworks = new List<ArtworkData>();
        [SerializeField] private List<Texture2D> capturedPhotos = new List<Texture2D>();
        [SerializeField] private List<ProductItem> storeProducts = new List<ProductItem>();
        [SerializeField] private List<OrderItem> currentOrders = new List<OrderItem>();

        [Header("Estado de Pantalla")]
        [SerializeField] private TabletScreen currentScreen = TabletScreen.Home;

        // Eventos para actualizar la interfaz
        public event Action OnArtworksChanged;
        public event Action OnPhotosChanged;
        public event Action OnProductsChanged;
        public event Action OnOrdersChanged;
        public event Action<TabletScreen> OnScreenChanged;

        public IReadOnlyList<ArtworkData> ScannedArtworks => scannedArtworks;
        public IReadOnlyList<Texture2D> CapturedPhotos => capturedPhotos;
        public IReadOnlyList<ProductItem> StoreProducts => storeProducts;
        public IReadOnlyList<OrderItem> CurrentOrders => currentOrders;
        public TabletScreen CurrentScreen => currentScreen;

        [Header("Visibilidad de Tableta")]
        [SerializeField] private bool isTabletOpen = false;
        public bool IsTabletOpen => isTabletOpen;
        public event Action<bool> OnTabletOpenStateChanged;

        private void SetPhysicalTabletVisuals(bool visible)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = visible;
            }
        }

        public void OpenTablet()
        {
            isTabletOpen = true;
            OnTabletOpenStateChanged?.Invoke(true);
            SetPhysicalTabletVisuals(false); // Ocultar modelo en la mesa mientras se usa

            var ui = GetComponent<TabletUIController>();
            if (ui != null)
            {
                var c = ui.GetTabletCanvasObject();
                if (c != null) c.SetActive(true);
                ui.UpdateActiveScreen(currentScreen);
            }
        }

        public void CloseTablet()
        {
            isTabletOpen = false;
            OnTabletOpenStateChanged?.Invoke(false);
            SetPhysicalTabletVisuals(true); // Reaparecer en la mesa al guardarla
            ScannableDrawing.SetAllAurasVisible(true); // Restaurar auras al guardar la tableta

            var ui = GetComponent<TabletUIController>();
            if (ui != null)
            {
                var c = ui.GetTabletCanvasObject();
                if (c != null) c.SetActive(false);
            }
        }

        public void ToggleTablet()
        {
            if (isTabletOpen) CloseTablet();
            else OpenTablet();
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this); // Solo destruir el componente duplicado, NO el GameObject entero
            }
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                scannedArtworks.Clear();
                capturedPhotos.Clear();
            }
            SetPhysicalTabletVisuals(true);
            ShowScreen(TabletScreen.Home);
        }

        public void ShowScreen(TabletScreen screen)
        {
            currentScreen = screen;
            OnScreenChanged?.Invoke(currentScreen);

            // En modo cámara las auras se ocultan para no arruinar el encuadre fotográfico
            if (currentScreen == TabletScreen.Camera)
            {
                ScannableDrawing.SetAllAurasVisible(false);
            }
            else
            {
                ScannableDrawing.SetAllAurasVisible(true);
            }

            var ui = GetComponent<TabletUIController>();
            if (ui != null)
            {
                ui.UpdateActiveScreen(currentScreen);
            }
        }

        public void ShowHome() => ShowScreen(TabletScreen.Home);
        public void ShowImages() => ShowScreen(TabletScreen.Images);
        public void ShowCamera() => ShowScreen(TabletScreen.Camera);
        public void ShowStore() => ShowScreen(TabletScreen.Store);

        public void ReceiveScannedArtwork(ArtworkData artwork)
        {
            if (artwork == null || string.IsNullOrEmpty(artwork.title)) return;

            // Evitar duplicar exactamente la misma referencia si ya está
            if (!scannedArtworks.Exists(a => a.id == artwork.id))
            {
                scannedArtworks.Add(artwork);
                Debug.Log($"<color=cyan>[Tableta]</color> Nueva ilustración recibida de la impresora: <b>{artwork.title}</b>");
                OnArtworksChanged?.Invoke();
            }
        }

        public void AddCapturedPhoto(Texture2D photo)
        {
            if (photo == null) return;
            capturedPhotos.Add(photo);
            Debug.Log($"<color=green>[Tableta]</color> Foto guardada en la galería. Total fotos: {capturedPhotos.Count}");
            OnPhotosChanged?.Invoke();
        }

        public ProductItem PublishProduct(string title, Texture2D photo, int price)
        {
            ProductItem newProduct = new ProductItem(title, photo, price);
            storeProducts.Add(newProduct);
            Debug.Log($"<color=yellow>[Tienda]</color> Nuevo producto publicado: {newProduct.title} a ${newProduct.price}");
            OnProductsChanged?.Invoke();
            return newProduct;
        }

        public void AddOrder(OrderItem order)
        {
            if (order == null) return;
            currentOrders.Insert(0, order);
            Debug.Log($"<color=magenta>[Pedidos]</color> ¡Nuevo pedido entrante! #{order.orderId} de {order.product.title} (x{order.unitsRequested})");
            OnOrdersChanged?.Invoke();
        }

        public void CompleteOrder(OrderItem order)
        {
            if (order != null && currentOrders.Contains(order))
            {
                order.isCompleted = true;
                currentOrders.Remove(order);
                OnOrdersChanged?.Invoke();
            }
        }
    }
}
