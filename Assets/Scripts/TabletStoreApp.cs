using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArtSimulator
{
    public class TabletStoreApp : MonoBehaviour
    {
        [Header("Pestañas y Navegación")]
        [SerializeField] private Button tabStoreButton;
        [SerializeField] private Button tabOrdersButton;
        [SerializeField] private Text tabStoreLabel;
        [SerializeField] private Text tabOrdersLabel;
        [SerializeField] private Button homeButton;

        [Header("Paneles Principales")]
        [SerializeField] private GameObject storePanel;
        [SerializeField] private GameObject ordersPanel;
        [SerializeField] private Transform storeContentContainer;
        [SerializeField] private Transform ordersContentContainer;

        [Header("Modal Crear Producto")]
        [SerializeField] private GameObject newProductModal;
        [SerializeField] private Transform photoPickerContent;
        [SerializeField] private Image selectedPhotoPreview;
        [SerializeField] private Text priceText;
        [SerializeField] private Text productNameInputText;
        [SerializeField] private Button btnPriceMinus;
        [SerializeField] private Button btnPricePlus;
        [SerializeField] private Button btnPublishProduct;
        [SerializeField] private Button btnCloseModal;
        [SerializeField] private Text modalErrorText;

        [Header("Configuración de Pedidos Automáticos")]
        [SerializeField] private float orderCheckInterval = 15f;
        [SerializeField] private int maxActiveOrders = 6;

        private Texture2D chosenPhoto;
        private int currentPrice = 5000;
        private string currentProductName = "Lámina Artística";
        private Coroutine ordersRoutine;

        private void Start()
        {
            SetupTabButtons();
            SetupModalButtons();

            if (TabletManager.Instance != null)
            {
                TabletManager.Instance.OnProductsChanged += RefreshStoreProducts;
                TabletManager.Instance.OnOrdersChanged += RefreshOrdersList;
            }

            SelectStoreTab();
            RefreshStoreProducts();
            RefreshOrdersList();

            ordersRoutine = StartCoroutine(AutoOrderGeneratorRoutine());
        }

        private void OnDestroy()
        {
            if (TabletManager.Instance != null)
            {
                TabletManager.Instance.OnProductsChanged -= RefreshStoreProducts;
                TabletManager.Instance.OnOrdersChanged -= RefreshOrdersList;
            }

            if (ordersRoutine != null)
            {
                StopCoroutine(ordersRoutine);
            }
        }

        private void OnEnable()
        {
            RefreshStoreProducts();
            RefreshOrdersList();
        }

        private void SetupTabButtons()
        {
            if (tabStoreButton != null)
                tabStoreButton.onClick.AddListener(SelectStoreTab);

            if (tabOrdersButton != null)
                tabOrdersButton.onClick.AddListener(SelectOrdersTab);

            if (homeButton != null)
                homeButton.onClick.AddListener(() =>
                {
                    if (TabletManager.Instance != null)
                        TabletManager.Instance.ShowHome();
                });
        }

        private void SetupModalButtons()
        {
            if (btnPriceMinus != null)
                btnPriceMinus.onClick.AddListener(() => ChangePrice(-500));

            if (btnPricePlus != null)
                btnPricePlus.onClick.AddListener(() => ChangePrice(500));

            if (btnPublishProduct != null)
                btnPublishProduct.onClick.AddListener(PublishNewProduct);

            if (btnCloseModal != null)
                btnCloseModal.onClick.AddListener(CloseNewProductModal);
        }

        public void SelectStoreTab()
        {
            if (storePanel != null) storePanel.SetActive(true);
            if (ordersPanel != null) ordersPanel.SetActive(false);
            if (newProductModal != null) newProductModal.SetActive(false);

            HighlightTab(true);
            RefreshStoreProducts();
        }

        public void SelectOrdersTab()
        {
            if (storePanel != null) storePanel.SetActive(false);
            if (ordersPanel != null) ordersPanel.SetActive(true);
            if (newProductModal != null) newProductModal.SetActive(false);

            HighlightTab(false);
            RefreshOrdersList();
        }

        private void HighlightTab(bool isStore)
        {
            if (tabStoreLabel != null)
            {
                tabStoreLabel.fontStyle = isStore ? FontStyle.Bold : FontStyle.Normal;
                tabStoreLabel.color = isStore ? Color.black : new Color(0.4f, 0.4f, 0.4f);
            }
            if (tabOrdersLabel != null)
            {
                tabOrdersLabel.fontStyle = !isStore ? FontStyle.Bold : FontStyle.Normal;
                tabOrdersLabel.color = !isStore ? Color.black : new Color(0.4f, 0.4f, 0.4f);
            }
        }

        #region Tienda (Productos)

        private void SafeDestroy(GameObject obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }

        public void RefreshStoreProducts()
        {
            if (TabletManager.Instance == null || storeContentContainer == null) return;

            var toDestroy = new List<GameObject>();
            foreach (Transform child in storeContentContainer)
            {
                toDestroy.Add(child.gameObject);
            }
            for (int i = 0; i < toDestroy.Count; i++)
            {
                SafeDestroy(toDestroy[i]);
            }

            IReadOnlyList<ProductItem> products = TabletManager.Instance.StoreProducts;

            // Renderizar tarjetas de productos existentes
            if (products != null && products.Count > 0)
            {
                for (int i = 0; i < products.Count; i++)
                {
                    ProductItem prod = products[i];
                    GameObject card = CreateProductCard(prod);
                    card.transform.SetParent(storeContentContainer, false);
                }
            }
            else
            {
                // Aviso informativo cuando la tienda aún no tiene productos
                GameObject emptyCard = new GameObject("EmptyStoreNotice", typeof(RectTransform), typeof(Text));
                emptyCard.transform.SetParent(storeContentContainer, false);
                RectTransform ert = emptyCard.GetComponent<RectTransform>();
                ert.sizeDelta = new Vector2(250, 100);

                Text et = emptyCard.GetComponent<Text>();
                et.text = "¡No hay productos a la venta todavía!\n\nToca el botón '+' a la derecha para elegir tu ilustración o foto, fijar el precio y publicarla.";
                et.alignment = TextAnchor.MiddleCenter;
                et.fontSize = 12;
                et.color = new Color(0.42f, 0.46f, 0.54f);
                et.font = UIFontHelper.GetDefaultFont();
            }

            // Renderizar tarjeta de botón "[+] MÁS"
            GameObject addCard = CreateAddProductCard();
            addCard.transform.SetParent(storeContentContainer, false);
        }

        private GameObject CreateProductCard(ProductItem product)
        {
            GameObject cardObj = new GameObject($"Card_{product.title}", typeof(RectTransform), typeof(Image));
            RectTransform rt = cardObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(110, 140);

            Image cardBg = cardObj.GetComponent<Image>();
            cardBg.color = new Color(0.95f, 0.95f, 0.98f);

            // Imagen del producto
            GameObject imgObj = new GameObject("ProductPhoto", typeof(RectTransform), typeof(Image));
            imgObj.transform.SetParent(cardObj.transform, false);
            RectTransform imgRt = imgObj.GetComponent<RectTransform>();
            imgRt.anchorMin = new Vector2(0.08f, 0.35f);
            imgRt.anchorMax = new Vector2(0.92f, 0.95f);
            imgRt.offsetMin = Vector2.zero;
            imgRt.offsetMax = Vector2.zero;

            Image img = imgObj.GetComponent<Image>();
            img.sprite = product.GetSprite();
            img.preserveAspect = true;

            // Texto Nombre
            GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleObj.transform.SetParent(cardObj.transform, false);
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.05f, 0.18f);
            titleRt.anchorMax = new Vector2(0.95f, 0.35f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;

            Text titleText = titleObj.GetComponent<Text>();
            titleText.text = product.title;
            titleText.fontSize = 11;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.black;
            titleText.font = UIFontHelper.GetDefaultFont();

            // Texto Precio
            GameObject priceObj = new GameObject("Price", typeof(RectTransform), typeof(Text));
            priceObj.transform.SetParent(cardObj.transform, false);
            RectTransform priceRt = priceObj.GetComponent<RectTransform>();
            priceRt.anchorMin = new Vector2(0.05f, 0.02f);
            priceRt.anchorMax = new Vector2(0.95f, 0.18f);
            priceRt.offsetMin = Vector2.zero;
            priceRt.offsetMax = Vector2.zero;

            Text pText = priceObj.GetComponent<Text>();
            pText.text = $"${product.price}";
            pText.fontStyle = FontStyle.Bold;
            pText.fontSize = 12;
            pText.alignment = TextAnchor.MiddleCenter;
            pText.color = new Color(0.1f, 0.5f, 0.2f);
            pText.font = UIFontHelper.GetDefaultFont();

            return cardObj;
        }

        private GameObject CreateAddProductCard()
        {
            GameObject addCard = new GameObject("Card_AddProduct", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rt = addCard.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(110, 140);

            Image cardBg = addCard.GetComponent<Image>();
            cardBg.color = new Color(0.9f, 0.9f, 0.92f);

            // Icono '+'
            GameObject plusObj = new GameObject("PlusIcon", typeof(RectTransform), typeof(Text));
            plusObj.transform.SetParent(addCard.transform, false);
            RectTransform plusRt = plusObj.GetComponent<RectTransform>();
            plusRt.anchorMin = new Vector2(0.1f, 0.35f);
            plusRt.anchorMax = new Vector2(0.9f, 0.9f);
            plusRt.offsetMin = Vector2.zero;
            plusRt.offsetMax = Vector2.zero;

            Text plusText = plusObj.GetComponent<Text>();
            plusText.text = "+";
            plusText.fontSize = 50;
            plusText.alignment = TextAnchor.MiddleCenter;
            plusText.color = new Color(0.3f, 0.3f, 0.35f);
            plusText.font = UIFontHelper.GetDefaultFont();

            // Texto "MÁS"
            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObj.transform.SetParent(addCard.transform, false);
            RectTransform labelRt = labelObj.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0.05f, 0.08f);
            labelRt.anchorMax = new Vector2(0.95f, 0.35f);
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            Text labelText = labelObj.GetComponent<Text>();
            labelText.text = "MÁS";
            labelText.fontStyle = FontStyle.Bold;
            labelText.fontSize = 13;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = new Color(0.3f, 0.3f, 0.35f);
            labelText.font = UIFontHelper.GetDefaultFont();

            Button btn = addCard.GetComponent<Button>();
            btn.onClick.AddListener(OpenNewProductModal);

            return addCard;
        }

        #endregion

        #region Modal Agregar Producto

        public void OpenNewProductModal()
        {
            if (newProductModal != null)
                newProductModal.SetActive(true);

            currentPrice = 2000;
            currentProductName = "Ilustración";

            if (priceText != null) priceText.text = $"${currentPrice}";
            if (productNameInputText != null) productNameInputText.text = $"Nombre: {currentProductName}";
            if (modalErrorText != null) modalErrorText.text = "";

            chosenPhoto = null;
            if (selectedPhotoPreview != null)
            {
                selectedPhotoPreview.sprite = null;
                selectedPhotoPreview.color = new Color(0.8f, 0.8f, 0.8f, 0.5f);
            }

            PopulatePhotoPicker();
        }

        public void CloseNewProductModal()
        {
            if (newProductModal != null)
                newProductModal.SetActive(false);
        }

        private void PopulatePhotoPicker()
        {
            if (photoPickerContent == null || TabletManager.Instance == null) return;

            var toDestroy = new List<GameObject>();
            foreach (Transform child in photoPickerContent)
            {
                toDestroy.Add(child.gameObject);
            }
            for (int i = 0; i < toDestroy.Count; i++)
            {
                SafeDestroy(toDestroy[i]);
            }

            // Recopilar todas las obras disponibles (dibujos escaneados y fotos de cámara)
            List<ArtworkData> availableArtworks = new List<ArtworkData>();
            if (TabletManager.Instance.ScannedArtworks != null)
            {
                for (int i = 0; i < TabletManager.Instance.ScannedArtworks.Count; i++)
                {
                    ArtworkData art = TabletManager.Instance.ScannedArtworks[i];
                    if (art != null && art.texture != null)
                        availableArtworks.Add(art);
                }
            }
            if (TabletManager.Instance.PhotoArtworks != null)
            {
                for (int i = 0; i < TabletManager.Instance.PhotoArtworks.Count; i++)
                {
                    ArtworkData pArt = TabletManager.Instance.PhotoArtworks[i];
                    if (pArt != null && pArt.texture != null)
                        availableArtworks.Add(pArt);
                }
            }

            if (availableArtworks.Count == 0)
            {
                if (modalErrorText != null)
                    modalErrorText.text = "¡Aún no has escaneado ningún dibujo ni tomado fotos!\nEscanea un dibujo en la impresora o toma una foto primero.";
                return;
            }

            for (int i = 0; i < availableArtworks.Count; i++)
            {
                ArtworkData art = availableArtworks[i];
                Sprite spr = art.GetSprite();

                GameObject item = new GameObject($"ArtItem_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                RectTransform rt = item.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(75, 75);

                Image bg = item.GetComponent<Image>();
                bg.color = new Color(0.92f, 0.94f, 0.98f);

                // Miniatura dentro del item
                GameObject thumbObj = new GameObject("Thumbnail", typeof(RectTransform), typeof(Image));
                thumbObj.transform.SetParent(item.transform, false);
                RectTransform thumbRt = thumbObj.GetComponent<RectTransform>();
                thumbRt.anchorMin = new Vector2(0.08f, 0.22f);
                thumbRt.anchorMax = new Vector2(0.92f, 0.95f);
                thumbRt.offsetMin = Vector2.zero;
                thumbRt.offsetMax = Vector2.zero;

                Image thumbImg = thumbObj.GetComponent<Image>();
                thumbImg.sprite = spr;
                thumbImg.preserveAspect = true;
                thumbImg.raycastTarget = false;

                // Etiqueta del título
                GameObject lblObj = new GameObject("TitleLbl", typeof(RectTransform), typeof(Text));
                lblObj.transform.SetParent(item.transform, false);
                RectTransform lblRt = lblObj.GetComponent<RectTransform>();
                lblRt.anchorMin = new Vector2(0.04f, 0.02f);
                lblRt.anchorMax = new Vector2(0.96f, 0.22f);
                lblRt.offsetMin = Vector2.zero;
                lblRt.offsetMax = Vector2.zero;

                Text lblTxt = lblObj.GetComponent<Text>();
                lblTxt.text = art.title;
                lblTxt.fontSize = 9;
                lblTxt.alignment = TextAnchor.MiddleCenter;
                lblTxt.color = new Color(0.15f, 0.15f, 0.2f);
                lblTxt.font = UIFontHelper.GetDefaultFont();
                lblTxt.raycastTarget = false;

                Button btn = item.GetComponent<Button>();
                btn.onClick.AddListener(() => SelectArtworkForProduct(art));

                item.transform.SetParent(photoPickerContent, false);
            }

            // Seleccionar por defecto la obra más reciente
            ArtworkData latest = availableArtworks[availableArtworks.Count - 1];
            SelectArtworkForProduct(latest);
        }

        private void SelectArtworkForProduct(ArtworkData art)
        {
            if (art == null || art.texture == null) return;

            chosenPhoto = art.texture;
            currentProductName = !string.IsNullOrEmpty(art.title) ? art.title : "Ilustración";

            if (productNameInputText != null)
            {
                productNameInputText.text = $"Nombre: {currentProductName}";
            }

            if (selectedPhotoPreview != null)
            {
                selectedPhotoPreview.sprite = art.GetSprite();
                selectedPhotoPreview.color = Color.white;
                selectedPhotoPreview.preserveAspect = true;
            }

            if (modalErrorText != null) modalErrorText.text = "";
        }

        private void ChangePrice(int delta)
        {
            currentPrice = Mathf.Max(500, currentPrice + delta);
            if (priceText != null)
            {
                priceText.text = $"${currentPrice}";
            }
        }

        public void PublishNewProduct()
        {
            if (chosenPhoto == null)
            {
                if (modalErrorText != null)
                    modalErrorText.text = "Por favor selecciona una ilustración o fotografía de la lista.";
                return;
            }

            if (TabletManager.Instance != null)
            {
                TabletManager.Instance.PublishProduct(currentProductName, chosenPhoto, currentPrice);
            }

            CloseNewProductModal();
            RefreshStoreProducts();
        }

        #endregion

        #region Pedidos

        public void RefreshOrdersList()
        {
            if (TabletManager.Instance == null || ordersContentContainer == null) return;

            var toDestroy = new List<GameObject>();
            foreach (Transform child in ordersContentContainer)
            {
                toDestroy.Add(child.gameObject);
            }
            for (int i = 0; i < toDestroy.Count; i++)
            {
                SafeDestroy(toDestroy[i]);
            }

            IReadOnlyList<OrderItem> orders = TabletManager.Instance.CurrentOrders;
            if (orders == null || orders.Count == 0)
            {
                // Mensaje si no hay pedidos
                GameObject emptyObj = new GameObject("EmptyOrdersMsg", typeof(RectTransform), typeof(Text));
                emptyObj.transform.SetParent(ordersContentContainer, false);
                RectTransform rt = emptyObj.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(300, 60);

                Text t = emptyObj.GetComponent<Text>();
                t.text = "No hay pedidos pendientes.\n¡Llegarán pronto!";
                t.alignment = TextAnchor.MiddleCenter;
                t.fontSize = 13;
                t.color = new Color(0.5f, 0.5f, 0.5f);
                t.font = UIFontHelper.GetDefaultFont();
                return;
            }

            for (int i = 0; i < orders.Count; i++)
            {
                OrderItem ord = orders[i];
                GameObject card = CreateOrderCard(ord);
                card.transform.SetParent(ordersContentContainer, false);
            }
        }

        private GameObject CreateOrderCard(OrderItem order)
        {
            GameObject cardObj = new GameObject($"Order_{order.orderId}", typeof(RectTransform), typeof(Image));
            RectTransform rt = cardObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(110, 140);

            Image cardBg = cardObj.GetComponent<Image>();
            cardBg.color = new Color(0.98f, 0.95f, 0.92f);

            // Foto del producto
            GameObject imgObj = new GameObject("ProductPhoto", typeof(RectTransform), typeof(Image));
            imgObj.transform.SetParent(cardObj.transform, false);
            RectTransform imgRt = imgObj.GetComponent<RectTransform>();
            imgRt.anchorMin = new Vector2(0.08f, 0.35f);
            imgRt.anchorMax = new Vector2(0.92f, 0.95f);
            imgRt.offsetMin = Vector2.zero;
            imgRt.offsetMax = Vector2.zero;

            Image img = imgObj.GetComponent<Image>();
            img.sprite = order.product.GetSprite();
            img.preserveAspect = true;

            // Nombre Producto
            GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleObj.transform.SetParent(cardObj.transform, false);
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.05f, 0.18f);
            titleRt.anchorMax = new Vector2(0.95f, 0.35f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;

            Text titleText = titleObj.GetComponent<Text>();
            titleText.text = order.product.title;
            titleText.fontSize = 11;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.black;
            titleText.font = UIFontHelper.GetDefaultFont();

            // Cantidad pedida (según boceto: "UNIDADES PEDIDAS")
            GameObject reqObj = new GameObject("UnitsText", typeof(RectTransform), typeof(Text));
            reqObj.transform.SetParent(cardObj.transform, false);
            RectTransform reqRt = reqObj.GetComponent<RectTransform>();
            reqRt.anchorMin = new Vector2(0.02f, 0.02f);
            reqRt.anchorMax = new Vector2(0.98f, 0.18f);
            reqRt.offsetMin = Vector2.zero;
            reqRt.offsetMax = Vector2.zero;

            Text reqText = reqObj.GetComponent<Text>();
            if (order.unitsDelivered > 0)
            {
                reqText.text = $"{order.unitsDelivered}/{order.unitsRequested} LISTAS";
                reqText.color = new Color(0.1f, 0.55f, 0.2f);
            }
            else
            {
                reqText.text = $"{order.unitsRequested} PEDIDAS";
                reqText.color = new Color(0.7f, 0.2f, 0.1f);
            }
            reqText.fontSize = 10;
            reqText.fontStyle = FontStyle.Bold;
            reqText.alignment = TextAnchor.MiddleCenter;
            reqText.font = UIFontHelper.GetDefaultFont();

            return cardObj;
        }

        private IEnumerator AutoOrderGeneratorRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(orderCheckInterval);

                if (TabletManager.Instance != null)
                {
                    IReadOnlyList<ProductItem> prods = TabletManager.Instance.StoreProducts;
                    if (prods != null && prods.Count > 0 && TabletManager.Instance.CurrentOrders.Count < maxActiveOrders)
                    {
                        // Elegir un producto al azar
                        ProductItem chosen = prods[Random.Range(0, prods.Count)];
                        int requestedCount = Random.Range(1, 4);

                        OrderItem newOrder = new OrderItem(chosen, requestedCount);
                        TabletManager.Instance.AddOrder(newOrder);
                    }
                }
            }
        }

        #endregion

        public void BindUI(
            Button tabStore, Button tabOrders, Text labelStore, Text labelOrders, Button home,
            GameObject panelStore, GameObject panelOrders, Transform containerStore, Transform containerOrders,
            GameObject modal, Transform pickerContent, Image preview, Text priceTxt, Text nameTxt,
            Button minusBtn, Button plusBtn, Button publishBtn, Button closeBtn, Text errorTxt)
        {
            tabStoreButton = tabStore;
            tabOrdersButton = tabOrders;
            tabStoreLabel = labelStore;
            tabOrdersLabel = labelOrders;
            homeButton = home;

            storePanel = panelStore;
            ordersPanel = panelOrders;
            storeContentContainer = containerStore;
            ordersContentContainer = containerOrders;

            newProductModal = modal;
            photoPickerContent = pickerContent;
            selectedPhotoPreview = preview;
            priceText = priceTxt;
            productNameInputText = nameTxt;
            btnPriceMinus = minusBtn;
            btnPricePlus = plusBtn;
            btnPublishProduct = publishBtn;
            btnCloseModal = closeBtn;
            modalErrorText = errorTxt;

            SetupTabButtons();
            SetupModalButtons();
        }
    }
}
