using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArtSimulator
{
    public enum GalleryTab
    {
        Scans,
        Photos
    }

    public class TabletGalleryApp : MonoBehaviour
    {
        [Header("Elementos UI")]
        [SerializeField] private Transform galleryContentContainer;
        [SerializeField] private Image selectedArtworkPreview;
        [SerializeField] private Text selectedArtworkTitleText;
        [SerializeField] private Button printButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private Text statusMessageText;
        [SerializeField] private Text emptyStateText;

        [Header("Pestañas de Galería")]
        [SerializeField] private GalleryTab currentTab = GalleryTab.Scans;
        [SerializeField] private Button scansTabButton;
        [SerializeField] private Button photosTabButton;
        [SerializeField] private Image scansTabBg;
        [SerializeField] private Image photosTabBg;
        [SerializeField] private Text scansTabText;
        [SerializeField] private Text photosTabText;
        [SerializeField] private Text topBarTitleText;

        [Header("Selector de Cantidad de Copias (Mín 1, Máx 10)")]
        [SerializeField] private int printQuantity = 1;
        [SerializeField] private Text quantityText;
        [SerializeField] private Button decreaseQtyButton;
        [SerializeField] private Button increaseQtyButton;

        [Header("Eliminar Foto")]
        [SerializeField] private Button deletePhotoButton;

        private ArtworkData currentSelectedArtwork;
        private float lastPrintTime = -1f;
        private float lastDeleteTime = -1f;
        private readonly List<GameObject> activeCards = new List<GameObject>();
        private readonly List<ArtworkData> activeCardArtworks = new List<ArtworkData>();

        private void OnEnable()
        {
            if (TabletManager.Instance != null)
            {
                TabletManager.Instance.OnArtworksChanged -= OnScannedArtworksUpdated;
                TabletManager.Instance.OnArtworksChanged += OnScannedArtworksUpdated;
                TabletManager.Instance.OnPhotosChanged -= OnPhotosUpdated;
                TabletManager.Instance.OnPhotosChanged += OnPhotosUpdated;
            }
            UpdateQuantityUI();
            UpdateTabVisuals();
            RefreshGallery();
        }

        private void Start()
        {
            if (homeButton != null)
            {
                homeButton.onClick.RemoveAllListeners();
                homeButton.onClick.AddListener(() =>
                {
                    if (TabletManager.Instance != null)
                        TabletManager.Instance.ShowHome();
                });
                TabletButtonTrigger ht = homeButton.GetComponent<TabletButtonTrigger>() ?? homeButton.gameObject.AddComponent<TabletButtonTrigger>();
                ht.SetAction("Inicio", () => TabletManager.Instance?.ShowHome());
            }

            if (printButton != null)
            {
                printButton.onClick.RemoveAllListeners();
                printButton.onClick.AddListener(OnPrintClicked);
                TabletButtonTrigger pt = printButton.GetComponent<TabletButtonTrigger>() ?? printButton.gameObject.AddComponent<TabletButtonTrigger>();
                pt.SetAction("Imprimir Copia", OnPrintClicked);
            }

            if (decreaseQtyButton != null)
            {
                decreaseQtyButton.onClick.RemoveAllListeners();
                decreaseQtyButton.onClick.AddListener(DecreaseQuantity);
                TabletButtonTrigger dt = decreaseQtyButton.GetComponent<TabletButtonTrigger>() ?? decreaseQtyButton.gameObject.AddComponent<TabletButtonTrigger>();
                dt.SetAction("Menos Copias", DecreaseQuantity);
            }

            if (increaseQtyButton != null)
            {
                increaseQtyButton.onClick.RemoveAllListeners();
                increaseQtyButton.onClick.AddListener(IncreaseQuantity);
                TabletButtonTrigger it = increaseQtyButton.GetComponent<TabletButtonTrigger>() ?? increaseQtyButton.gameObject.AddComponent<TabletButtonTrigger>();
                it.SetAction("Más Copias", IncreaseQuantity);
            }

            if (deletePhotoButton != null)
            {
                TabletButtonTrigger dt = deletePhotoButton.GetComponent<TabletButtonTrigger>() ?? deletePhotoButton.gameObject.AddComponent<TabletButtonTrigger>();
                dt.SetAction("Eliminar Foto", OnDeletePhotoClicked);
                deletePhotoButton.onClick.RemoveAllListeners();
                deletePhotoButton.onClick.AddListener(() => dt.TriggerClick());
            }

            if (scansTabButton != null)
            {
                TabletButtonTrigger st = scansTabButton.GetComponent<TabletButtonTrigger>() ?? scansTabButton.gameObject.AddComponent<TabletButtonTrigger>();
                st.SetAction("Ver Escaneos", () => SwitchTab(GalleryTab.Scans));
            }

            if (photosTabButton != null)
            {
                TabletButtonTrigger pt = photosTabButton.GetComponent<TabletButtonTrigger>() ?? photosTabButton.gameObject.AddComponent<TabletButtonTrigger>();
                pt.SetAction("Ver Fotos", () => SwitchTab(GalleryTab.Photos));
            }

            UpdateQuantityUI();
            UpdateTabVisuals();

            if (TabletManager.Instance != null)
            {
                TabletManager.Instance.OnArtworksChanged -= OnScannedArtworksUpdated;
                TabletManager.Instance.OnArtworksChanged += OnScannedArtworksUpdated;
                TabletManager.Instance.OnPhotosChanged -= OnPhotosUpdated;
                TabletManager.Instance.OnPhotosChanged += OnPhotosUpdated;
            }

            RefreshGallery();
        }

        private void OnDestroy()
        {
            if (TabletManager.Instance != null)
            {
                TabletManager.Instance.OnArtworksChanged -= OnScannedArtworksUpdated;
                TabletManager.Instance.OnPhotosChanged -= OnPhotosUpdated;
            }
        }

        private void OnScannedArtworksUpdated()
        {
            currentTab = GalleryTab.Scans;
            UpdateTabVisuals();
            RefreshGallery();
        }

        private void OnPhotosUpdated()
        {
            currentTab = GalleryTab.Photos;
            UpdateTabVisuals();
            RefreshGallery();
        }

        public void SwitchTab(GalleryTab tab)
        {
            currentTab = tab;
            UpdateTabVisuals();
            RefreshGallery();
        }

        public void UpdateTabVisuals()
        {
            Color activeBg = new Color(0.12f, 0.48f, 0.95f);
            Color inactiveBg = new Color(0.85f, 0.88f, 0.93f);
            Color activeText = Color.white;
            Color inactiveText = new Color(0.20f, 0.25f, 0.35f);

            if (scansTabBg != null) scansTabBg.color = (currentTab == GalleryTab.Scans) ? activeBg : inactiveBg;
            if (photosTabBg != null) photosTabBg.color = (currentTab == GalleryTab.Photos) ? activeBg : inactiveBg;

            if (scansTabText != null) scansTabText.color = (currentTab == GalleryTab.Scans) ? activeText : inactiveText;
            if (photosTabText != null) photosTabText.color = (currentTab == GalleryTab.Photos) ? activeText : inactiveText;

            if (topBarTitleText != null)
            {
                topBarTitleText.text = (currentTab == GalleryTab.Scans) 
                    ? "🖨️ DIBUJOS ESCANEADOS" 
                    : "📷 FOTOS DE CÁMARA";
            }

            if (deletePhotoButton != null)
            {
                deletePhotoButton.gameObject.SetActive(currentTab == GalleryTab.Photos);
            }
        }

        public void BindTabs(Button scansBtn, Button photosBtn, Image sBg, Image pBg, Text sTxt, Text pTxt, Text topTitle)
        {
            scansTabButton = scansBtn;
            photosTabButton = photosBtn;
            scansTabBg = sBg;
            photosTabBg = pBg;
            scansTabText = sTxt;
            photosTabText = pTxt;
            topBarTitleText = topTitle;

            if (scansTabButton != null)
            {
                scansTabButton.onClick.RemoveAllListeners();
                scansTabButton.onClick.AddListener(() => SwitchTab(GalleryTab.Scans));
                var t = scansTabButton.GetComponent<TabletButtonTrigger>() ?? scansTabButton.gameObject.AddComponent<TabletButtonTrigger>();
                t.SetAction("Ver Escaneos", () => SwitchTab(GalleryTab.Scans));
            }

            if (photosTabButton != null)
            {
                photosTabButton.onClick.RemoveAllListeners();
                photosTabButton.onClick.AddListener(() => SwitchTab(GalleryTab.Photos));
                var t = photosTabButton.GetComponent<TabletButtonTrigger>() ?? photosTabButton.gameObject.AddComponent<TabletButtonTrigger>();
                t.SetAction("Ver Fotos", () => SwitchTab(GalleryTab.Photos));
            }

            UpdateTabVisuals();
        }

        public void BindUI(Transform contentContainer, Image previewImage, Text titleText, Button printBtn, Button homeBtn, Text statusText, Text emptyText, Text qtyText = null, Button decBtn = null, Button incBtn = null, Button deleteBtn = null)
        {
            galleryContentContainer = contentContainer;
            selectedArtworkPreview = previewImage;
            selectedArtworkTitleText = titleText;
            printButton = printBtn;
            homeButton = homeBtn;
            statusMessageText = statusText;
            emptyStateText = emptyText;
            quantityText = qtyText;
            decreaseQtyButton = decBtn;
            increaseQtyButton = incBtn;
            deletePhotoButton = deleteBtn;

            if (homeButton != null)
            {
                homeButton.onClick.RemoveAllListeners();
                homeButton.onClick.AddListener(() =>
                {
                    if (TabletManager.Instance != null)
                        TabletManager.Instance.ShowHome();
                });
                TabletButtonTrigger ht = homeButton.GetComponent<TabletButtonTrigger>() ?? homeButton.gameObject.AddComponent<TabletButtonTrigger>();
                ht.SetAction("Inicio", () => TabletManager.Instance?.ShowHome());
            }

            if (printButton != null)
            {
                printButton.onClick.RemoveAllListeners();
                printButton.onClick.AddListener(OnPrintClicked);
                TabletButtonTrigger pt = printButton.GetComponent<TabletButtonTrigger>() ?? printButton.gameObject.AddComponent<TabletButtonTrigger>();
                pt.SetAction("Imprimir Copia", OnPrintClicked);
            }

            if (deletePhotoButton != null)
            {
                TabletButtonTrigger dt = deletePhotoButton.GetComponent<TabletButtonTrigger>() ?? deletePhotoButton.gameObject.AddComponent<TabletButtonTrigger>();
                dt.SetAction("Eliminar Foto", OnDeletePhotoClicked);
                deletePhotoButton.onClick.RemoveAllListeners();
                deletePhotoButton.onClick.AddListener(() => dt.TriggerClick());
            }

            if (decreaseQtyButton != null)
            {
                decreaseQtyButton.onClick.RemoveAllListeners();
                decreaseQtyButton.onClick.AddListener(DecreaseQuantity);
                TabletButtonTrigger dt = decreaseQtyButton.GetComponent<TabletButtonTrigger>() ?? decreaseQtyButton.gameObject.AddComponent<TabletButtonTrigger>();
                dt.SetAction("Menos Copias", DecreaseQuantity);
            }

            if (increaseQtyButton != null)
            {
                increaseQtyButton.onClick.RemoveAllListeners();
                increaseQtyButton.onClick.AddListener(IncreaseQuantity);
                TabletButtonTrigger it = increaseQtyButton.GetComponent<TabletButtonTrigger>() ?? increaseQtyButton.gameObject.AddComponent<TabletButtonTrigger>();
                it.SetAction("Más Copias", IncreaseQuantity);
            }

            UpdateQuantityUI();
            UpdateTabVisuals();
        }

        private float lastQtyChangeTime = -1f;

        public void IncreaseQuantity()
        {
            float now = Time.unscaledTime;
            if (now - lastQtyChangeTime < 0.20f) return;
            lastQtyChangeTime = now;

            if (printQuantity < 10)
            {
                printQuantity++;
                UpdateQuantityUI();
            }
        }

        public void DecreaseQuantity()
        {
            float now = Time.unscaledTime;
            if (now - lastQtyChangeTime < 0.20f) return;
            lastQtyChangeTime = now;

            if (printQuantity > 1)
            {
                printQuantity--;
                UpdateQuantityUI();
            }
        }

        public void UpdateQuantityUI()
        {
            printQuantity = Mathf.Clamp(printQuantity, 1, 10);
            if (quantityText != null)
            {
                quantityText.text = printQuantity == 1 ? "1 copia" : $"{printQuantity} copias";
            }
            if (decreaseQtyButton != null)
            {
                decreaseQtyButton.interactable = printQuantity > 1;
            }
            if (increaseQtyButton != null)
            {
                increaseQtyButton.interactable = printQuantity < 10;
            }
        }

        public void RefreshGallery()
        {
            if (TabletManager.Instance == null || galleryContentContainer == null) return;

            IReadOnlyList<ArtworkData> artworks = (currentTab == GalleryTab.Scans)
                ? TabletManager.Instance.ScannedArtworks
                : TabletManager.Instance.PhotoArtworks;

            activeCards.Clear();
            activeCardArtworks.Clear();

            // Limpiar lista anterior de forma inmediata para evitar tarjetas residuales
            foreach (Transform child in galleryContentContainer)
            {
                child.gameObject.SetActive(false);
                SafeDestroy(child.gameObject);
            }

            bool hasArtworks = artworks != null && artworks.Count > 0;
            if (emptyStateText != null)
            {
                emptyStateText.gameObject.SetActive(!hasArtworks);
                emptyStateText.text = (currentTab == GalleryTab.Scans)
                    ? "🖨️ Sin Dibujos Escaneados\nColoca un dibujo sobre la impresora para escanearlo y verlo aquí."
                    : "📷 Galería Fotográfica Vacía\nToma fotos con la app de Cámara para verlas aquí.";
            }

            if (selectedArtworkPreview != null)
                selectedArtworkPreview.gameObject.SetActive(hasArtworks);
            if (selectedArtworkTitleText != null)
                selectedArtworkTitleText.gameObject.SetActive(hasArtworks);
            if (printButton != null)
                printButton.interactable = hasArtworks;
            if (deletePhotoButton != null)
                deletePhotoButton.gameObject.SetActive(currentTab == GalleryTab.Photos && hasArtworks);

            if (!hasArtworks)
            {
                currentSelectedArtwork = null;
                if (deletePhotoButton != null)
                    deletePhotoButton.gameObject.SetActive(false);
                if (statusMessageText != null)
                {
                    statusMessageText.text = (currentTab == GalleryTab.Scans)
                        ? "Coloca un dibujo en la impresora para escanear."
                        : "No hay fotografías capturadas.";
                }
                return;
            }

            // Crear tarjetas de miniaturas en la cuadrícula horizontal
            for (int i = 0; i < artworks.Count; i++)
            {
                ArtworkData art = artworks[i];
                GameObject card = CreateThumbnailCard(art);
                activeCards.Add(card);
                activeCardArtworks.Add(art);
            }

            // Re-vincular la obra seleccionada para reflejar posibles actualizaciones de textura
            ArtworkData targetArt = null;
            if (currentSelectedArtwork != null)
            {
                for (int i = 0; i < artworks.Count; i++)
                {
                    if (artworks[i].id == currentSelectedArtwork.id || artworks[i].title == currentSelectedArtwork.title)
                    {
                        targetArt = artworks[i];
                        break;
                    }
                }
            }

            if (targetArt != null)
            {
                SelectArtwork(targetArt);
            }
            else if (artworks.Count > 0)
            {
                SelectArtwork(artworks[0]);
            }
        }

        private GameObject CreateThumbnailCard(ArtworkData art)
        {
            GameObject cardObj = new GameObject($"Card_{art.title}", typeof(RectTransform), typeof(Image), typeof(Button));
            cardObj.transform.SetParent(galleryContentContainer, false);

            RectTransform rt = cardObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160, 200);

            LayoutElement le = cardObj.AddComponent<LayoutElement>();
            le.minWidth = 160;
            le.minHeight = 200;
            le.preferredWidth = 160;
            le.preferredHeight = 200;

            Image bg = cardObj.GetComponent<Image>();
            bg.color = Color.white;
            bg.raycastTarget = true;

            // Miniatura
            GameObject imgObj = new GameObject("Thumbnail", typeof(RectTransform), typeof(Image));
            imgObj.transform.SetParent(cardObj.transform, false);
            RectTransform imgRt = imgObj.GetComponent<RectTransform>();
            imgRt.anchorMin = new Vector2(0.08f, 0.25f);
            imgRt.anchorMax = new Vector2(0.92f, 0.95f);
            imgRt.offsetMin = Vector2.zero;
            imgRt.offsetMax = Vector2.zero;

            Image thumb = imgObj.GetComponent<Image>();
            thumb.sprite = art.GetSprite();
            thumb.preserveAspect = true;
            thumb.raycastTarget = false;

            // Texto título
            GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(cardObj.transform, false);
            RectTransform txtRt = textObj.GetComponent<RectTransform>();
            txtRt.anchorMin = new Vector2(0.05f, 0f);
            txtRt.anchorMax = new Vector2(0.95f, 0.25f);
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            Text txt = textObj.GetComponent<Text>();
            txt.text = art.title;
            txt.fontSize = 13;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.1f, 0.15f, 0.2f);
            txt.font = UIFontHelper.GetDefaultFont();
            txt.raycastTarget = false;

            Button btn = cardObj.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => SelectArtwork(art));

            TabletButtonTrigger trig = cardObj.AddComponent<TabletButtonTrigger>();
            trig.SetAction($"Ver {art.title}", () => SelectArtwork(art));

            return cardObj;
        }

        public void SelectArtwork(ArtworkData art)
        {
            currentSelectedArtwork = art;
            if (currentSelectedArtwork == null) return;

            if (selectedArtworkPreview != null)
            {
                selectedArtworkPreview.sprite = currentSelectedArtwork.GetSprite();
                selectedArtworkPreview.preserveAspect = true;
            }

            if (selectedArtworkTitleText != null)
            {
                selectedArtworkTitleText.text = currentSelectedArtwork.title;
            }

            if (statusMessageText != null)
            {
                statusMessageText.text = "Ilustración lista para imprimir";
            }

            // Resaltar visualmente la tarjeta seleccionada en la cuadrícula
            for (int i = 0; i < activeCards.Count; i++)
            {
                var card = activeCards[i];
                if (card == null) continue;
                var cardArt = (i < activeCardArtworks.Count) ? activeCardArtworks[i] : null;
                bool isSelected = cardArt != null && (cardArt.id == currentSelectedArtwork.id || cardArt == currentSelectedArtwork);

                Image bg = card.GetComponent<Image>();
                if (bg != null)
                {
                    bg.color = isSelected ? new Color(0.12f, 0.48f, 0.95f) : Color.white;
                }

                Text lbl = card.transform.Find("Label")?.GetComponent<Text>();
                if (lbl != null)
                {
                    lbl.color = isSelected ? Color.white : new Color(0.1f, 0.15f, 0.2f);
                }
            }
        }

        public void OnPrintClicked()
        {
            // Debounce para evitar doble ejecución accidental
            if (Time.unscaledTime - lastPrintTime < 0.35f) return;
            lastPrintTime = Time.unscaledTime;

            if (currentSelectedArtwork == null && TabletManager.Instance != null)
            {
                var list = (currentTab == GalleryTab.Scans) ? TabletManager.Instance.ScannedArtworks : TabletManager.Instance.PhotoArtworks;
                if (list != null && list.Count > 0)
                {
                    SelectArtwork(list[0]);
                }
                else
                {
                    // Intentar con la otra lista si la actual está vacía
                    var fallbackList = (currentTab == GalleryTab.Scans) ? TabletManager.Instance.PhotoArtworks : TabletManager.Instance.ScannedArtworks;
                    if (fallbackList != null && fallbackList.Count > 0)
                    {
                        SelectArtwork(fallbackList[0]);
                    }
                }
            }

            if (currentSelectedArtwork == null)
            {
                Debug.LogWarning("[TabletGalleryApp] No hay ninguna obra seleccionada para imprimir.");
                if (statusMessageText != null)
                {
                    statusMessageText.text = (currentTab == GalleryTab.Scans)
                        ? "Primero coloca y escanea un dibujo en la impresora física."
                        : "Primero toma una fotografía con la app de Cámara.";
                }
                return;
            }

            var printer = PrinterController.Instance != null ? PrinterController.Instance : FindAnyObjectByType<PrinterController>();
            if (printer != null)
            {
                int qty = Mathf.Clamp(printQuantity, 1, 10);
                Debug.Log($"<color=green>[TabletGalleryApp]</color> Imprimiendo {qty} copias de: <b>{currentSelectedArtwork.title}</b>");
                printer.PrintArtworks(currentSelectedArtwork, qty);
                if (statusMessageText != null)
                {
                    string plural = qty == 1 ? "copia" : "copias";
                    statusMessageText.text = $"✓ ¡Imprimiendo {qty} {plural} de '{currentSelectedArtwork.title}'! Revisa la bandeja de la impresora.";
                }
            }
            else
            {
                Debug.LogWarning("[TabletGalleryApp] No se encontró PrinterController en la escena.");
                if (statusMessageText != null)
                {
                    statusMessageText.text = "⚠️ No se encontró la impresora.";
                }
            }
        }

        public void OnDeletePhotoClicked()
        {
            // Debounce para evitar ejecuciones duplicadas en el mismo clic
            float now = Time.unscaledTime;
            if (now - lastDeleteTime < 0.40f) return;
            lastDeleteTime = now;

            if (currentSelectedArtwork == null || currentTab != GalleryTab.Photos) return;
            if (TabletManager.Instance == null) return;

            ArtworkData toDelete = currentSelectedArtwork;
            string title = toDelete.title;

            // Determinar índice antes de eliminar para seleccionar la foto adyacente
            var photos = TabletManager.Instance.PhotoArtworks;
            int currentIndex = -1;
            for (int i = 0; i < photos.Count; i++)
            {
                if (photos[i].id == toDelete.id || photos[i].title == toDelete.title)
                {
                    currentIndex = i;
                    break;
                }
            }

            // Desvincular temporalmente antes de invocar DeleteCapturedPhoto para evitar que eventos intermedios la re-seleccionen
            currentSelectedArtwork = null;

            bool deleted = TabletManager.Instance.DeleteCapturedPhoto(toDelete);
            if (deleted)
            {
                if (statusMessageText != null)
                {
                    statusMessageText.text = $"🗑️ Foto '{title}' eliminada de la galería.";
                }

                var remaining = TabletManager.Instance.PhotoArtworks;
                if (remaining != null && remaining.Count > 0)
                {
                    int nextIdx = Mathf.Clamp(currentIndex, 0, remaining.Count - 1);
                    currentSelectedArtwork = remaining[nextIdx];
                }

                RefreshGallery();
            }
        }

        private void SafeDestroy(GameObject obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }
    }
}
