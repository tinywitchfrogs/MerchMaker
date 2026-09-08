using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArtSimulator
{
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

        [Header("Selector de Cantidad de Copias (Mín 1, Máx 10)")]
        [SerializeField] private int printQuantity = 1;
        [SerializeField] private Text quantityText;
        [SerializeField] private Button decreaseQtyButton;
        [SerializeField] private Button increaseQtyButton;

        private ArtworkData currentSelectedArtwork;
        private float lastPrintTime = -1f;

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

            UpdateQuantityUI();

            if (TabletManager.Instance != null)
            {
                TabletManager.Instance.OnArtworksChanged -= RefreshGallery;
                TabletManager.Instance.OnArtworksChanged += RefreshGallery;
            }

            RefreshGallery();
        }

        private void OnDestroy()
        {
            if (TabletManager.Instance != null)
            {
                TabletManager.Instance.OnArtworksChanged -= RefreshGallery;
            }
        }

        private void OnEnable()
        {
            RefreshGallery();
        }

        public void BindUI(Transform contentContainer, Image previewImage, Text titleText, Button printBtn, Button homeBtn, Text statusText, Text emptyText, Text qtyText = null, Button decBtn = null, Button incBtn = null)
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

            UpdateQuantityUI();
        }

        public void IncreaseQuantity()
        {
            if (printQuantity < 10)
            {
                printQuantity++;
                UpdateQuantityUI();
            }
        }

        public void DecreaseQuantity()
        {
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

            IReadOnlyList<ArtworkData> artworks = TabletManager.Instance.ScannedArtworks;

            // Limpiar lista anterior
            foreach (Transform child in galleryContentContainer)
            {
                Destroy(child.gameObject);
            }

            bool hasArtworks = artworks != null && artworks.Count > 0;
            if (emptyStateText != null)
            {
                emptyStateText.gameObject.SetActive(!hasArtworks);
                emptyStateText.text = "📭 Galería Vacía\nEscanea tu dibujo en la impresora física para verlo e imprimir copias aquí.";
            }

            if (selectedArtworkPreview != null)
                selectedArtworkPreview.gameObject.SetActive(hasArtworks);
            if (selectedArtworkTitleText != null)
                selectedArtworkTitleText.gameObject.SetActive(hasArtworks);
            if (printButton != null)
                printButton.interactable = hasArtworks;

            if (!hasArtworks)
            {
                currentSelectedArtwork = null;
                if (statusMessageText != null) statusMessageText.text = "No hay ilustraciones escaneadas.";
                return;
            }

            // Crear tarjetas de miniaturas en la cuadrícula horizontal
            for (int i = 0; i < artworks.Count; i++)
            {
                ArtworkData art = artworks[i];
                CreateThumbnailCard(art);
            }

            // Seleccionar automáticamente la primera si no hay ninguna seleccionada
            if (currentSelectedArtwork == null && artworks.Count > 0)
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
        }

        public void OnPrintClicked()
        {
            // Debounce para evitar doble ejecución accidental
            if (Time.unscaledTime - lastPrintTime < 0.6f) return;
            lastPrintTime = Time.unscaledTime;

            if (currentSelectedArtwork == null && TabletManager.Instance != null && TabletManager.Instance.ScannedArtworks.Count > 0)
            {
                SelectArtwork(TabletManager.Instance.ScannedArtworks[0]);
            }

            if (currentSelectedArtwork == null)
            {
                if (statusMessageText != null) statusMessageText.text = "Primero escanea el dibujo en la impresora física.";
                return;
            }

            if (PrinterController.Instance != null)
            {
                int qty = Mathf.Clamp(printQuantity, 1, 10);
                PrinterController.Instance.PrintArtworks(currentSelectedArtwork, qty);
                if (statusMessageText != null)
                {
                    string plural = qty == 1 ? "copia" : "copias";
                    statusMessageText.text = $"✓ ¡Imprimiendo {qty} {plural} de '{currentSelectedArtwork.title}'! Revisa la bandeja.";
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
    }
}
