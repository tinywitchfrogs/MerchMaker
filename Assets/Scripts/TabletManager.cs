using System;
using System.Collections;
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
                if (instance == null) instance = FindAnyObjectByType<TabletManager>();
                return instance;
            }
            private set => instance = value;
        }

        [Header("Datos Almacenados en la Tableta")]
        [SerializeField] private List<ArtworkData> scannedArtworks = new List<ArtworkData>();
        [SerializeField] private List<Texture2D> capturedPhotos = new List<Texture2D>();
        [SerializeField] private List<ArtworkData> photoArtworks = new List<ArtworkData>();
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
        public IReadOnlyList<ArtworkData> PhotoArtworks => photoArtworks;
        public IReadOnlyList<ProductItem> StoreProducts => storeProducts;
        public IReadOnlyList<OrderItem> CurrentOrders => currentOrders;
        public TabletScreen CurrentScreen => currentScreen;

        [Header("Visibilidad de Tableta")]
        [SerializeField] private bool isTabletOpen = false;
        public bool IsTabletOpen => isTabletOpen;
        public event Action<bool> OnTabletOpenStateChanged;

        [Header("Configuración de Pedidos (Ritmo Paulatino)")]
        [Tooltip("Tiempo de espera inicial (en segundos) antes del primer pedido")]
        [SerializeField] private float initialOrderDelay = 35f;

        [Tooltip("Intervalo mínimo base entre pedidos (en segundos)")]
        [SerializeField] private float minOrderInterval = 35f;

        [Tooltip("Intervalo máximo base entre pedidos (en segundos)")]
        [SerializeField] private float maxOrderInterval = 60f;

        [Tooltip("Tiempo adicional de espera por cada pedido activo pendiente")]
        [SerializeField] private float delayPerActiveOrder = 15f;

        [Tooltip("Cantidad máxima de pedidos pendientes en la tableta")]
        [SerializeField] private int maxActiveOrders = 3;

        [Header("Audio y Notificación")]
        [Tooltip("Clip de sonido de vibración para pedidos nuevos")]
        [SerializeField] private AudioClip vibrationSoundClip;
        private Coroutine orderRoutine;
        private AudioSource audioSource;
        private static AudioClip cachedOrderVibration;

        private void SetPhysicalTabletVisuals(bool visible)
        {
            // En Realidad Virtual la tableta física se sostiene en la mano del jugador: NUNCA se oculta
            if (XRPlayerRigManager.Instance != null && XRPlayerRigManager.Instance.IsVRActive)
            {
                visible = true;
            }

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

            bool isVR = XRPlayerRigManager.Instance != null && XRPlayerRigManager.Instance.IsVRActive;
            if (!isVR)
            {
                SetPhysicalTabletVisuals(false); // Solo en modo escritorio antiguo se ocultaba
            }
            else
            {
                SetPhysicalTabletVisuals(true); // En VR siempre visible en la mano
            }

            var ui = GetComponent<TabletUIController>() ?? FindAnyObjectByType<TabletUIController>();
            if (ui != null)
            {
                var c = ui.GetTabletCanvasObject();
                if (c != null) c.SetActive(true);
                ui.UpdateActiveScreen(currentScreen);
            }
            else
            {
                var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i].name == "TabletCanvas")
                    {
                        canvases[i].gameObject.SetActive(true);
                        break;
                    }
                }
            }
        }

        public void CloseTablet()
        {
            isTabletOpen = false;
            OnTabletOpenStateChanged?.Invoke(false);
            SetPhysicalTabletVisuals(true);
            ScannableDrawing.SetAllAurasVisible(true);

            var ui = GetComponent<TabletUIController>() ?? FindAnyObjectByType<TabletUIController>();
            if (ui != null)
            {
                var c = ui.GetTabletCanvasObject();
                bool isVR = XRPlayerRigManager.Instance != null && XRPlayerRigManager.Instance.IsVRActive;
                if (!isVR && c != null)
                {
                    c.SetActive(false);
                }
                else if (isVR)
                {
                    ui.UpdateActiveScreen(TabletScreen.Home);
                }
            }
            else
            {
                bool isVR = XRPlayerRigManager.Instance != null && XRPlayerRigManager.Instance.IsVRActive;
                if (!isVR)
                {
                    var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
                    for (int i = 0; i < canvases.Length; i++)
                    {
                        if (canvases[i].name == "TabletCanvas")
                        {
                            canvases[i].gameObject.SetActive(false);
                            break;
                        }
                    }
                }
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
                return;
            }

            isTabletOpen = false;
            currentScreen = TabletScreen.Home;
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                scannedArtworks.Clear();
                capturedPhotos.Clear();
                photoArtworks.Clear();
                photoCounter = 0;
                orderRoutine = StartCoroutine(OrderGeneratorRoutine());
            }
            SetPhysicalTabletVisuals(true);
            currentScreen = TabletScreen.Home;
            CloseTablet(); // La tableta debe iniciar cerrada sobre la mesa de trabajo
        }

        private void OnDestroy()
        {
            if (orderRoutine != null)
            {
                StopCoroutine(orderRoutine);
            }
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

            // Actualizar si ya existe una versión anterior de esta ilustración o añadirla si es nueva
            int existingIndex = scannedArtworks.FindIndex(a => a.id == artwork.id || a.title == artwork.title);
            if (existingIndex >= 0)
            {
                scannedArtworks[existingIndex] = artwork;
                Debug.Log($"<color=cyan>[Tableta]</color> Ilustración actualizada en la tableta: <b>{artwork.title}</b>");
                OnArtworksChanged?.Invoke();
            }
            else
            {
                scannedArtworks.Add(artwork);
                Debug.Log($"<color=cyan>[Tableta]</color> Nueva ilustración recibida de la impresora: <b>{artwork.title}</b>");
                OnArtworksChanged?.Invoke();
            }
        }

        [SerializeField] private int photoCounter = 0;

        public void AddCapturedPhoto(Texture2D photo)
        {
            if (photo == null) return;
            photoCounter++;
            capturedPhotos.Add(photo);

            // Registrar exclusivamente en la galería fotográfica de la tableta con ID único y número consecutivo
            ArtworkData photoArtwork = new ArtworkData($"Foto #{photoCounter}", photo);
            photoArtworks.Add(photoArtwork);

            Debug.Log($"<color=green>[Tableta]</color> Foto #{photoCounter} guardada en la galería fotográfica. Total fotos activas: {photoArtworks.Count}");
            OnPhotosChanged?.Invoke();
        }

        public bool DeleteCapturedPhoto(ArtworkData photoArtwork)
        {
            if (photoArtwork == null) return false;

            // Buscar coincidencia exacta por ID único o referencia de textura
            int idx = photoArtworks.FindIndex(p => p != null && (p.id == photoArtwork.id || (p.texture != null && photoArtwork.texture != null && p.texture == photoArtwork.texture)));
            if (idx >= 0)
            {
                ArtworkData removedArt = photoArtworks[idx];
                photoArtworks.RemoveAt(idx);

                // Mantener capturedPhotos sincronizado por referencia de textura
                if (removedArt.texture != null)
                {
                    capturedPhotos.Remove(removedArt.texture);
                }
                else if (idx < capturedPhotos.Count)
                {
                    capturedPhotos.RemoveAt(idx);
                }

                Debug.Log($"<color=orange>[Tableta]</color> Foto '{removedArt.title}' eliminada de la galería. Restantes: {photoArtworks.Count}");
                OnPhotosChanged?.Invoke();
                return true;
            }
            return false;
        }

        public bool DeleteCapturedPhoto(Texture2D photo)
        {
            if (photo == null) return false;
            int idx = photoArtworks.FindIndex(p => p != null && p.texture == photo);
            if (idx >= 0)
            {
                return DeleteCapturedPhoto(photoArtworks[idx]);
            }
            else
            {
                bool removed = capturedPhotos.Remove(photo);
                if (removed) OnPhotosChanged?.Invoke();
                return removed;
            }
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
            Debug.Log($"<color=magenta>[Pedidos]</color> 🔔 ¡Nuevo pedido entrante! #{order.orderId} de {order.product.title} (x{order.unitsRequested})");
            PlayOrderNotificationSound();
            OnOrdersChanged?.Invoke();
        }

        private IEnumerator OrderGeneratorRoutine()
        {
            // Esperar pacientemente a que el jugador publique sus primeros proyectos/productos en la tienda
            while (storeProducts == null || storeProducts.Count == 0)
            {
                yield return new WaitForSeconds(1f);
            }

            // Una vez que sale el primer proyecto en la tienda, dar un tiempo de margen paulatino antes de recibir el primer pedido
            yield return new WaitForSeconds(initialOrderDelay);

            int totalOrdersGenerated = 0;

            while (true)
            {
                // Solo si el jugador tiene proyectos a la venta y no hemos alcanzado el límite máximo de pedidos simultáneos
                if (storeProducts != null && storeProducts.Count > 0 && currentOrders.Count < maxActiveOrders)
                {
                    ProductItem chosenProduct = GetProductForNewOrder();
                    if (chosenProduct != null)
                    {
                        int requestedCount = UnityEngine.Random.Range(1, 4);
                        OrderItem newOrder = new OrderItem(chosenProduct, requestedCount);
                        AddOrder(newOrder);
                        totalOrdersGenerated++;
                    }
                }

                // Cálculo paulatino y dinámico del siguiente intervalo:
                // 1. Rango base aleatorio para variedad natural (ej. 35s - 60s)
                float baseInterval = UnityEngine.Random.Range(minOrderInterval, maxOrderInterval);

                // 2. Espera adicional si el jugador ya tiene pedidos acumulados (backpressure)
                float backpressure = currentOrders.Count * delayPerActiveOrder;

                // 3. Progresión paulatina inicial: los primeros pedidos tienen margen extra de aprendizaje
                float warmupBonus = 0f;
                if (totalOrdersGenerated < 2)
                {
                    warmupBonus = 15f;
                }
                else if (totalOrdersGenerated < 4)
                {
                    warmupBonus = 8f;
                }

                float finalWaitTime = baseInterval + backpressure + warmupBonus;
                yield return new WaitForSeconds(finalWaitTime);
            }
        }

        private ProductItem GetProductForNewOrder()
        {
            // Solo generar pedidos de productos/proyectos que el jugador haya publicado en la tienda
            if (storeProducts != null && storeProducts.Count > 0)
            {
                return storeProducts[UnityEngine.Random.Range(0, storeProducts.Count)];
            }

            // Si aún no se ha publicado ningún proyecto/producto en la tienda, no generar pedidos
            return null;
        }

        public void PlayOrderNotificationSound()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0.2f;
                }
            }

            AudioClip clipToPlay = vibrationSoundClip;
            if (clipToPlay == null)
            {
                if (cachedOrderVibration == null)
                {
                    cachedOrderVibration = GenerateOrderVibrationClip();
                }
                clipToPlay = cachedOrderVibration;
            }

            if (audioSource != null && clipToPlay != null)
            {
                audioSource.PlayOneShot(clipToPlay, 1.0f);
            }
        }

        private static AudioClip GenerateOrderVibrationClip()
        {
            int sampleRate = 44100;
            float duration = 0.50f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            float pulse1Start = 0.0f, pulse1End = 0.18f;
            float pulse2Start = 0.25f, pulse2End = 0.46f;
            float freq = 145f;

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float amp = 0f;

                if (t >= pulse1Start && t <= pulse1End)
                {
                    float pT = (t - pulse1Start) / (pulse1End - pulse1Start);
                    amp = Mathf.Sin(pT * Mathf.PI);
                }
                else if (t >= pulse2Start && t <= pulse2End)
                {
                    float pT = (t - pulse2Start) / (pulse2End - pulse2Start);
                    amp = Mathf.Sin(pT * Mathf.PI);
                }

                if (amp > 0f)
                {
                    float f1 = Mathf.Sin(2f * Mathf.PI * freq * t);
                    float f2 = Mathf.Sin(2f * Mathf.PI * (freq * 2f) * t) * 0.45f;
                    float f3 = Mathf.Sin(2f * Mathf.PI * (freq * 3f) * t) * 0.25f;
                    float f4 = Mathf.Sin(2f * Mathf.PI * (freq * 4f) * t) * 0.12f;
                    float noise = (Mathf.PerlinNoise(t * 1200f, 0f) * 2f - 1f) * 0.18f;

                    float raw = (f1 + f2 + f3 + f4 + noise) * amp;
                    samples[i] = Mathf.Clamp(raw * 1.35f, -1f, 1f) * 0.90f;
                }
                else
                {
                    samples[i] = 0f;
                }
            }

            AudioClip clip = AudioClip.Create("TabletVibrationSound", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void UpdateOrderProgress(OrderItem order)
        {
            if (order != null && currentOrders.Contains(order))
            {
                OnOrdersChanged?.Invoke();
            }
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
