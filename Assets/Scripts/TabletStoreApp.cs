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
        [SerializeField] private Button btnHeaderPublish;

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

        [Header("Economía y Saldo")]
        [SerializeField] private Text moneyDisplayText;

        private Texture2D chosenPhoto;
        private int currentPrice = 5000;
        private string currentProductName = "Lámina Artística";
        private bool isPublishing = false;

        private void Start()
        {
            SetupTabButtons();
            SetupModalButtons();
            SetupMoneyDisplay();

            if (TabletManager.Instance != null)
            {
                TabletManager.Instance.OnProductsChanged += RefreshStoreProducts;
                TabletManager.Instance.OnOrdersChanged += RefreshOrdersList;
            }

            if (PlayerEconomy.Instance != null)
            {
                PlayerEconomy.Instance.OnMoneyChanged -= UpdateMoneyDisplay;
                PlayerEconomy.Instance.OnMoneyChanged += UpdateMoneyDisplay;
            }

            SelectStoreTab();
            RefreshStoreProducts();
            RefreshOrdersList();
        }

        private void OnDestroy()
        {
            if (TabletManager.Instance != null)
            {
                TabletManager.Instance.OnProductsChanged -= RefreshStoreProducts;
                TabletManager.Instance.OnOrdersChanged -= RefreshOrdersList;
            }

            if (PlayerEconomy.Instance != null)
            {
                PlayerEconomy.Instance.OnMoneyChanged -= UpdateMoneyDisplay;
            }
        }

        private void OnEnable()
        {
            SetupMoneyDisplay();
            RefreshStoreProducts();
            RefreshOrdersList();
        }

        public void SetupMoneyDisplay()
        {
            if (moneyDisplayText == null)
            {
                // Buscar si ya existe en la jerarquía del encabezado
                Transform header = transform.Find("StoreHeader") 
                    ?? (storePanel != null && storePanel.transform.parent != null ? storePanel.transform.parent.Find("StoreHeader") : null);

                if (header == null)
                {
                    Transform t = transform;
                    while (t != null && header == null)
                    {
                        var found = t.Find("StoreHeader");
                        if (found != null) header = found;
                        t = t.parent;
                    }
                }

                if (header != null)
                {
                    Transform existingBadge = header.Find("MoneyBadge");
                    if (existingBadge != null)
                    {
                        moneyDisplayText = existingBadge.GetComponentInChildren<Text>();
                    }
                    else
                    {
                        // Crear el badge de dinero en el header de la tienda
                        GameObject badge = new GameObject("MoneyBadge", typeof(RectTransform), typeof(Image));
                        badge.transform.SetParent(header, false);
                        RectTransform brt = badge.GetComponent<RectTransform>();
                        brt.anchorMin = new Vector2(0.48f, 0.14f);
                        brt.anchorMax = new Vector2(0.74f, 0.86f);
                        brt.offsetMin = Vector2.zero;
                        brt.offsetMax = Vector2.zero;

                        Image bImg = badge.GetComponent<Image>();
                        bImg.color = new Color(0.93f, 0.98f, 0.94f); // Fondo verde menta elegante
                        var outline = badge.AddComponent<Outline>();
                        outline.effectColor = new Color(0.18f, 0.65f, 0.32f, 0.45f);
                        outline.effectDistance = new Vector2(1.5f, -1.5f);

                        GameObject txtObj = new GameObject("MoneyText", typeof(RectTransform), typeof(Text));
                        txtObj.transform.SetParent(badge.transform, false);
                        RectTransform trt = txtObj.GetComponent<RectTransform>();
                        trt.anchorMin = Vector2.zero;
                        trt.anchorMax = Vector2.one;
                        trt.offsetMin = Vector2.zero;
                        trt.offsetMax = Vector2.zero;

                        moneyDisplayText = txtObj.GetComponent<Text>();
                        moneyDisplayText.font = UIFontHelper.GetDefaultFont();
                        moneyDisplayText.fontSize = 15;
                        moneyDisplayText.fontStyle = FontStyle.Bold;
                        moneyDisplayText.alignment = TextAnchor.MiddleCenter;
                        moneyDisplayText.color = new Color(0.08f, 0.52f, 0.22f); // Verde dinero
                        moneyDisplayText.raycastTarget = false;
                    }
                }
            }

            int currentMoney = PlayerEconomy.Instance != null ? PlayerEconomy.Instance.dinero : 0;
            UpdateMoneyDisplay(currentMoney);
        }

        public void UpdateMoneyDisplay(int currentMoney)
        {
            if (moneyDisplayText != null)
            {
                moneyDisplayText.text = $"💰 ${currentMoney:N0}";
            }
        }

        private void SetupTabButtons()
        {
            if (tabStoreButton != null)
            {
                tabStoreButton.onClick.RemoveAllListeners();
                tabStoreButton.onClick.AddListener(SelectStoreTab);
                var trig = tabStoreButton.GetComponent<TabletButtonTrigger>() ?? tabStoreButton.gameObject.AddComponent<TabletButtonTrigger>();
                trig.SetAction("Pestaña Tienda", SelectStoreTab);
            }

            if (tabOrdersButton != null)
            {
                tabOrdersButton.onClick.RemoveAllListeners();
                tabOrdersButton.onClick.AddListener(SelectOrdersTab);
                var trig = tabOrdersButton.GetComponent<TabletButtonTrigger>() ?? tabOrdersButton.gameObject.AddComponent<TabletButtonTrigger>();
                trig.SetAction("Pestaña Pedidos", SelectOrdersTab);
            }

            if (homeButton != null)
            {
                homeButton.onClick.RemoveAllListeners();
                homeButton.onClick.AddListener(() => TabletManager.Instance?.ShowHome());
                var trig = homeButton.GetComponent<TabletButtonTrigger>() ?? homeButton.gameObject.AddComponent<TabletButtonTrigger>();
                trig.SetAction("Volver a Inicio", () => TabletManager.Instance?.ShowHome());
            }

            if (btnHeaderPublish != null)
            {
                btnHeaderPublish.onClick.RemoveAllListeners();
                btnHeaderPublish.onClick.AddListener(OpenNewProductModal);
                var trig = btnHeaderPublish.GetComponent<TabletButtonTrigger>() ?? btnHeaderPublish.gameObject.AddComponent<TabletButtonTrigger>();
                trig.SetAction("Publicar Producto", OpenNewProductModal);
            }
        }

        private void SetupModalButtons()
        {
            if (btnPriceMinus != null)
            {
                btnPriceMinus.onClick.RemoveAllListeners();
                btnPriceMinus.onClick.AddListener(() => ChangePrice(-500));
                var trig = btnPriceMinus.GetComponent<TabletButtonTrigger>() ?? btnPriceMinus.gameObject.AddComponent<TabletButtonTrigger>();
                trig.SetAction("Reducir Precio", () => ChangePrice(-500));
            }

            if (btnPricePlus != null)
            {
                btnPricePlus.onClick.RemoveAllListeners();
                btnPricePlus.onClick.AddListener(() => ChangePrice(500));
                var trig = btnPricePlus.GetComponent<TabletButtonTrigger>() ?? btnPricePlus.gameObject.AddComponent<TabletButtonTrigger>();
                trig.SetAction("Aumentar Precio", () => ChangePrice(500));
            }

            if (btnPublishProduct != null)
            {
                btnPublishProduct.onClick.RemoveAllListeners();
                btnPublishProduct.onClick.AddListener(PublishNewProduct);
                var trig = btnPublishProduct.GetComponent<TabletButtonTrigger>() ?? btnPublishProduct.gameObject.AddComponent<TabletButtonTrigger>();
                trig.SetAction("Publicar Producto", PublishNewProduct);
            }

            if (btnCloseModal != null)
            {
                btnCloseModal.onClick.RemoveAllListeners();
                btnCloseModal.onClick.AddListener(CloseNewProductModal);
                var trig = btnCloseModal.GetComponent<TabletButtonTrigger>() ?? btnCloseModal.gameObject.AddComponent<TabletButtonTrigger>();
                trig.SetAction("Cerrar Ventana", CloseNewProductModal);
            }
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

            // 1. Tarjeta destacada y siempre visible: "[ ＋ PUBLICAR PRODUCTO ]" al inicio del catálogo
            GameObject addCard = CreateAddProductCard();
            addCard.transform.SetParent(storeContentContainer, false);

            // 2. Renderizar productos existentes si los hay
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
                // Tarjeta explicativa con botón de acción directo si el catálogo está vacío
                GameObject emptyCard = CreateEmptyStoreNoticeCard();
                emptyCard.transform.SetParent(storeContentContainer, false);
            }
        }

        private GameObject CreateEmptyStoreNoticeCard()
        {
            GameObject cardObj = new GameObject("EmptyStoreNotice", typeof(RectTransform), typeof(Image));
            RectTransform rt = cardObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(420, 520);

            Image cardBg = cardObj.GetComponent<Image>();
            cardBg.color = new Color(0.96f, 0.98f, 1.0f);

            var outline = cardObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.6f, 0.95f, 0.45f);
            outline.effectDistance = new Vector2(2f, -2f);

            // Icono de tienda
            GameObject iconObj = new GameObject("StoreIcon", typeof(RectTransform), typeof(Text));
            iconObj.transform.SetParent(cardObj.transform, false);
            RectTransform iRt = iconObj.GetComponent<RectTransform>();
            iRt.anchorMin = new Vector2(0.1f, 0.62f);
            iRt.anchorMax = new Vector2(0.9f, 0.90f);
            iRt.offsetMin = Vector2.zero;
            iRt.offsetMax = Vector2.zero;
            Text iconTxt = iconObj.GetComponent<Text>();
            iconTxt.text = "🏪";
            iconTxt.fontSize = 72;
            iconTxt.alignment = TextAnchor.MiddleCenter;
            iconTxt.font = UIFontHelper.GetDefaultFont();
            iconTxt.raycastTarget = false;

            // Título amigable
            GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleObj.transform.SetParent(cardObj.transform, false);
            RectTransform tRt = titleObj.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0.06f, 0.28f);
            tRt.anchorMax = new Vector2(0.94f, 0.58f);
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;

            Text t = titleObj.GetComponent<Text>();
            t.text = "¡Tu Tienda está lista!\n\nToma fotos de tus ilustraciones en el atril y publícalas aquí para recibir pedidos.";
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = 20;
            t.fontStyle = FontStyle.Bold;
            t.color = new Color(0.15f, 0.22f, 0.35f);
            t.font = UIFontHelper.GetDefaultFont();
            t.raycastTarget = false;

            // Botón interactivo dentro de la tarjeta
            GameObject btnObj = new GameObject("BtnPublicar", typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(cardObj.transform, false);
            RectTransform bRt = btnObj.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0.08f, 0.08f);
            bRt.anchorMax = new Vector2(0.92f, 0.22f);
            bRt.offsetMin = Vector2.zero;
            bRt.offsetMax = Vector2.zero;

            Image bImg = btnObj.GetComponent<Image>();
            bImg.color = new Color(0.12f, 0.48f, 0.95f); // Azul vibrante
            Button btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = bImg;
            btn.onClick.AddListener(OpenNewProductModal);

            var trig = btnObj.AddComponent<TabletButtonTrigger>();
            trig.SetAction("Publicar Producto", OpenNewProductModal);

            GameObject txtObj = new GameObject("Txt", typeof(RectTransform), typeof(Text));
            txtObj.transform.SetParent(btnObj.transform, false);
            RectTransform txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            Text btnTxt = txtObj.GetComponent<Text>();
            btnTxt.text = "＋ PUBLICAR PRODUCTO";
            btnTxt.font = UIFontHelper.GetDefaultFont();
            btnTxt.fontSize = 20;
            btnTxt.fontStyle = FontStyle.Bold;
            btnTxt.alignment = TextAnchor.MiddleCenter;
            btnTxt.color = Color.white;
            btnTxt.raycastTarget = false;

            return cardObj;
        }

        private GameObject CreateProductCard(ProductItem product)
        {
            GameObject cardObj = new GameObject($"Card_{product.title}", typeof(RectTransform), typeof(Image));
            RectTransform rt = cardObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(320, 520);

            Image cardBg = cardObj.GetComponent<Image>();
            cardBg.color = new Color(0.96f, 0.96f, 0.99f);

            var outline = cardObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.75f, 0.80f, 0.90f, 0.6f);
            outline.effectDistance = new Vector2(2f, -2f);

            // Imagen del producto
            GameObject imgObj = new GameObject("ProductPhoto", typeof(RectTransform), typeof(Image));
            imgObj.transform.SetParent(cardObj.transform, false);
            RectTransform imgRt = imgObj.GetComponent<RectTransform>();
            imgRt.anchorMin = new Vector2(0.06f, 0.44f);
            imgRt.anchorMax = new Vector2(0.94f, 0.94f);
            imgRt.offsetMin = Vector2.zero;
            imgRt.offsetMax = Vector2.zero;

            Image img = imgObj.GetComponent<Image>();
            img.sprite = product.GetSprite();
            img.preserveAspect = true;

            // Texto Nombre
            GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleObj.transform.SetParent(cardObj.transform, false);
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.04f, 0.32f);
            titleRt.anchorMax = new Vector2(0.96f, 0.42f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;

            Text titleText = titleObj.GetComponent<Text>();
            titleText.text = product.title;
            titleText.fontSize = 22;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.12f, 0.15f, 0.20f);
            titleText.font = UIFontHelper.GetDefaultFont();

            // Calidad (Estrellas) y Nivel de Demanda del Mercado
            GameObject demandObj = new GameObject("DemandBadge", typeof(RectTransform), typeof(Text));
            demandObj.transform.SetParent(cardObj.transform, false);
            RectTransform demRt = demandObj.GetComponent<RectTransform>();
            demRt.anchorMin = new Vector2(0.02f, 0.18f);
            demRt.anchorMax = new Vector2(0.98f, 0.30f);
            demRt.offsetMin = Vector2.zero;
            demRt.offsetMax = Vector2.zero;

            Text demText = demandObj.GetComponent<Text>();
            demText.text = $"{product.GetStarsString()} {product.GetDemandBadge()}";
            demText.fontSize = 19;
            demText.fontStyle = FontStyle.Bold;
            demText.alignment = TextAnchor.MiddleCenter;
            demText.color = product.GetDemandColor();
            demText.font = UIFontHelper.GetDefaultFont();

            // Contenedor Precio con botones interactivos [-] Precio [+] para editarlo en cualquier momento
            GameObject priceContainer = new GameObject("PriceRow", typeof(RectTransform));
            priceContainer.transform.SetParent(cardObj.transform, false);
            RectTransform pRowRt = priceContainer.GetComponent<RectTransform>();
            pRowRt.anchorMin = new Vector2(0.04f, 0.03f);
            pRowRt.anchorMax = new Vector2(0.96f, 0.16f);
            pRowRt.offsetMin = Vector2.zero;
            pRowRt.offsetMax = Vector2.zero;

            // Botón Reducir Precio [-]
            GameObject minusBtnObj = new GameObject("BtnMinus", typeof(RectTransform), typeof(Image), typeof(Button));
            minusBtnObj.transform.SetParent(priceContainer.transform, false);
            RectTransform mRt = minusBtnObj.GetComponent<RectTransform>();
            mRt.anchorMin = new Vector2(0f, 0.05f);
            mRt.anchorMax = new Vector2(0.24f, 0.95f);
            mRt.offsetMin = Vector2.zero;
            mRt.offsetMax = Vector2.zero;
            Image mImg = minusBtnObj.GetComponent<Image>();
            mImg.color = new Color(0.92f, 0.94f, 0.98f);
            Button mBtn = minusBtnObj.GetComponent<Button>();
            mBtn.targetGraphic = mImg;
            mBtn.onClick.AddListener(() => {
                product.UpdatePrice(Mathf.Max(500, product.price - 500));
                SoundEffectManager.Play("click");
                RefreshStoreProducts();
            });
            var mTrig = minusBtnObj.AddComponent<TabletButtonTrigger>();
            mTrig.SetAction("Bajar Precio", () => {
                product.UpdatePrice(Mathf.Max(500, product.price - 500));
                SoundEffectManager.Play("click");
                RefreshStoreProducts();
            });
            GameObject mTxtObj = new GameObject("Txt", typeof(RectTransform), typeof(Text));
            mTxtObj.transform.SetParent(minusBtnObj.transform, false);
            RectTransform mtRt = mTxtObj.GetComponent<RectTransform>();
            mtRt.anchorMin = Vector2.zero; mtRt.anchorMax = Vector2.one; mtRt.offsetMin = Vector2.zero; mtRt.offsetMax = Vector2.zero;
            Text mt = mTxtObj.GetComponent<Text>();
            mt.text = "－"; mt.fontSize = 24; mt.fontStyle = FontStyle.Bold; mt.alignment = TextAnchor.MiddleCenter; mt.color = new Color(0.2f, 0.2f, 0.2f); mt.font = UIFontHelper.GetDefaultFont(); mt.raycastTarget = false;

            // Texto Precio en el centro
            GameObject priceObj = new GameObject("Price", typeof(RectTransform), typeof(Text));
            priceObj.transform.SetParent(priceContainer.transform, false);
            RectTransform priceRt = priceObj.GetComponent<RectTransform>();
            priceRt.anchorMin = new Vector2(0.26f, 0f);
            priceRt.anchorMax = new Vector2(0.74f, 1f);
            priceRt.offsetMin = Vector2.zero;
            priceRt.offsetMax = Vector2.zero;
            Text pText = priceObj.GetComponent<Text>();
            pText.text = $"${product.price:N0}";
            pText.fontStyle = FontStyle.Bold;
            pText.fontSize = 22;
            pText.alignment = TextAnchor.MiddleCenter;
            pText.color = new Color(0.08f, 0.55f, 0.22f);
            pText.font = UIFontHelper.GetDefaultFont();

            // Botón Aumentar Precio [+]
            GameObject plusBtnObj = new GameObject("BtnPlus", typeof(RectTransform), typeof(Image), typeof(Button));
            plusBtnObj.transform.SetParent(priceContainer.transform, false);
            RectTransform pRt = plusBtnObj.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0.76f, 0.05f);
            pRt.anchorMax = new Vector2(1f, 0.95f);
            pRt.offsetMin = Vector2.zero;
            pRt.offsetMax = Vector2.zero;
            Image pImg = plusBtnObj.GetComponent<Image>();
            pImg.color = new Color(0.92f, 0.94f, 0.98f);
            Button pBtn = plusBtnObj.GetComponent<Button>();
            pBtn.targetGraphic = pImg;
            pBtn.onClick.AddListener(() => {
                product.UpdatePrice(Mathf.Min(50000, product.price + 500));
                SoundEffectManager.Play("click");
                RefreshStoreProducts();
            });
            var pTrig = plusBtnObj.AddComponent<TabletButtonTrigger>();
            pTrig.SetAction("Subir Precio", () => {
                product.UpdatePrice(Mathf.Min(50000, product.price + 500));
                SoundEffectManager.Play("click");
                RefreshStoreProducts();
            });
            GameObject pTxtObj = new GameObject("Txt", typeof(RectTransform), typeof(Text));
            pTxtObj.transform.SetParent(plusBtnObj.transform, false);
            RectTransform ptRt = pTxtObj.GetComponent<RectTransform>();
            ptRt.anchorMin = Vector2.zero; ptRt.anchorMax = Vector2.one; ptRt.offsetMin = Vector2.zero; ptRt.offsetMax = Vector2.zero;
            Text pt = pTxtObj.GetComponent<Text>();
            pt.text = "＋"; pt.fontSize = 24; pt.fontStyle = FontStyle.Bold; pt.alignment = TextAnchor.MiddleCenter; pt.color = new Color(0.2f, 0.2f, 0.2f); pt.font = UIFontHelper.GetDefaultFont(); pt.raycastTarget = false;

            return cardObj;
        }

        private GameObject CreateAddProductCard()
        {
            GameObject addCard = new GameObject("Card_AddProduct", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rt = addCard.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(320, 520);

            Image cardBg = addCard.GetComponent<Image>();
            cardBg.color = new Color(0.92f, 0.96f, 1.0f);

            var outline = addCard.AddComponent<Outline>();
            outline.effectColor = new Color(0.18f, 0.55f, 0.95f, 0.75f);
            outline.effectDistance = new Vector2(2f, -2f);

            // Icono '+' grande y visible
            GameObject plusObj = new GameObject("PlusIcon", typeof(RectTransform), typeof(Text));
            plusObj.transform.SetParent(addCard.transform, false);
            RectTransform plusRt = plusObj.GetComponent<RectTransform>();
            plusRt.anchorMin = new Vector2(0.1f, 0.42f);
            plusRt.anchorMax = new Vector2(0.9f, 0.85f);
            plusRt.offsetMin = Vector2.zero;
            plusRt.offsetMax = Vector2.zero;

            Text plusText = plusObj.GetComponent<Text>();
            plusText.text = "＋";
            plusText.fontSize = 96;
            plusText.fontStyle = FontStyle.Bold;
            plusText.alignment = TextAnchor.MiddleCenter;
            plusText.color = new Color(0.10f, 0.45f, 0.92f);
            plusText.font = UIFontHelper.GetDefaultFont();
            plusText.raycastTarget = false;

            // Texto "PUBLICAR PRODUCTO"
            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObj.transform.SetParent(addCard.transform, false);
            RectTransform labelRt = labelObj.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0.04f, 0.16f);
            labelRt.anchorMax = new Vector2(0.96f, 0.40f);
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            Text labelText = labelObj.GetComponent<Text>();
            labelText.text = "PUBLICAR\nPRODUCTO";
            labelText.fontStyle = FontStyle.Bold;
            labelText.fontSize = 24;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = new Color(0.10f, 0.35f, 0.75f);
            labelText.font = UIFontHelper.GetDefaultFont();
            labelText.raycastTarget = false;

            // Subtítulo explicativo
            GameObject subObj = new GameObject("SubLabel", typeof(RectTransform), typeof(Text));
            subObj.transform.SetParent(addCard.transform, false);
            RectTransform subRt = subObj.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.04f, 0.04f);
            subRt.anchorMax = new Vector2(0.96f, 0.15f);
            subRt.offsetMin = Vector2.zero;
            subRt.offsetMax = Vector2.zero;

            Text subText = subObj.GetComponent<Text>();
            subText.text = "Sube tu obra y fija el precio";
            subText.fontSize = 17;
            subText.alignment = TextAnchor.MiddleCenter;
            subText.color = new Color(0.4f, 0.5f, 0.65f);
            subText.font = UIFontHelper.GetDefaultFont();
            subText.raycastTarget = false;

            Button btn = addCard.GetComponent<Button>();
            btn.targetGraphic = cardBg;
            btn.onClick.AddListener(OpenNewProductModal);

            TabletButtonTrigger trig = addCard.AddComponent<TabletButtonTrigger>();
            trig.SetAction("Publicar Producto", OpenNewProductModal);

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

            // Para la tienda solo se utilizan las fotografías capturadas con la cámara
            List<ArtworkData> availableArtworks = new List<ArtworkData>();
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
                    modalErrorText.text = "¡Aún no has tomado fotografías!\nToma una foto con la app de Cámara primero para poder publicarla en la tienda.";
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
                thumbRt.localScale = new Vector3(-1f, 1f, 1f);

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

                var trig = item.AddComponent<TabletButtonTrigger>();
                trig.SetAction($"Elegir {art.title}", () => SelectArtworkForProduct(art));

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
            currentPrice = Mathf.Clamp(currentPrice + delta, 500, 50000);
            if (priceText != null)
            {
                priceText.text = $"${currentPrice:N0}";
            }
            SoundEffectManager.Play("click");
            Debug.Log($"<color=cyan>[Tienda]</color> Precio ajustado a: ${currentPrice}");
        }

        public void PublishNewProduct()
        {
            if (isPublishing) return;

            if (chosenPhoto == null)
            {
                if (modalErrorText != null)
                    modalErrorText.text = "Por favor selecciona una ilustración o fotografía de la lista.";
                return;
            }

            isPublishing = true;
            try
            {
                // Extraer la calidad de la fotografía tomada para el cálculo de mercado
                float photoQuality = 0.5f;
                if (TabletManager.Instance != null && TabletManager.Instance.PhotoArtworks != null)
                {
                    for (int i = 0; i < TabletManager.Instance.PhotoArtworks.Count; i++)
                    {
                        var pArt = TabletManager.Instance.PhotoArtworks[i];
                        if (pArt != null && pArt.texture == chosenPhoto)
                        {
                            photoQuality = pArt.qualityScore;
                            break;
                        }
                    }
                }

                if (TabletManager.Instance != null)
                {
                    TabletManager.Instance.PublishProduct(currentProductName, chosenPhoto, currentPrice, photoQuality);
                }

                CloseNewProductModal();
                RefreshStoreProducts();
            }
            finally
            {
                StartCoroutine(ResetPublishingDebounce());
            }
        }

        private IEnumerator ResetPublishingDebounce()
        {
            yield return new WaitForSeconds(0.5f);
            isPublishing = false;
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
                rt.sizeDelta = new Vector2(650, 140);

                Text t = emptyObj.GetComponent<Text>();
                t.text = "No hay pedidos pendientes.\n¡Llegarán pronto!";
                t.alignment = TextAnchor.MiddleCenter;
                t.fontSize = 26;
                t.fontStyle = FontStyle.Bold;
                t.color = new Color(0.18f, 0.22f, 0.30f);
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
            rt.sizeDelta = new Vector2(360, 520);

            Image cardBg = cardObj.GetComponent<Image>();
            cardBg.color = new Color(0.98f, 0.98f, 1.0f);

            var outline = cardObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.70f, 0.78f, 0.90f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            // 1. Encabezado / Banner superior con ID del Pedido y Estado
            GameObject headerObj = new GameObject("HeaderBar", typeof(RectTransform), typeof(Image));
            headerObj.transform.SetParent(cardObj.transform, false);
            RectTransform hRt = headerObj.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 0.89f);
            hRt.anchorMax = new Vector2(1f, 1f);
            hRt.offsetMin = Vector2.zero;
            hRt.offsetMax = Vector2.zero;
            Image hImg = headerObj.GetComponent<Image>();
            hImg.color = new Color(0.12f, 0.18f, 0.28f);

            GameObject idTextObj = new GameObject("OrderIdText", typeof(RectTransform), typeof(Text));
            idTextObj.transform.SetParent(headerObj.transform, false);
            RectTransform idRt = idTextObj.GetComponent<RectTransform>();
            idRt.anchorMin = new Vector2(0.04f, 0f);
            idRt.anchorMax = new Vector2(0.65f, 1f);
            idRt.offsetMin = Vector2.zero;
            idRt.offsetMax = Vector2.zero;
            Text idTxt = idTextObj.GetComponent<Text>();
            idTxt.text = $"📦 PEDIDO #{order.orderId}";
            idTxt.fontSize = 20;
            idTxt.fontStyle = FontStyle.Bold;
            idTxt.alignment = TextAnchor.MiddleLeft;
            idTxt.color = Color.white;
            idTxt.font = UIFontHelper.GetDefaultFont();

            GameObject badgeObj = new GameObject("StatusBadge", typeof(RectTransform), typeof(Text));
            badgeObj.transform.SetParent(headerObj.transform, false);
            RectTransform bRt = badgeObj.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0.65f, 0f);
            bRt.anchorMax = new Vector2(0.96f, 1f);
            bRt.offsetMin = Vector2.zero;
            bRt.offsetMax = Vector2.zero;
            Text bTxt = badgeObj.GetComponent<Text>();
            bTxt.text = order.isCompleted ? "COMPLETADO" : (order.unitsDelivered > 0 ? "EN PROCESO" : "NUEVO");
            bTxt.fontSize = 18;
            bTxt.fontStyle = FontStyle.Bold;
            bTxt.alignment = TextAnchor.MiddleRight;
            bTxt.color = order.isCompleted ? new Color(0.2f, 0.9f, 0.4f) : new Color(0.3f, 0.75f, 1f);
            bTxt.font = UIFontHelper.GetDefaultFont();

            // 2. Nombre del Cliente / Comprador
            GameObject custObj = new GameObject("CustomerName", typeof(RectTransform), typeof(Text));
            custObj.transform.SetParent(cardObj.transform, false);
            RectTransform custRt = custObj.GetComponent<RectTransform>();
            custRt.anchorMin = new Vector2(0.05f, 0.81f);
            custRt.anchorMax = new Vector2(0.95f, 0.88f);
            custRt.offsetMin = Vector2.zero;
            custRt.offsetMax = Vector2.zero;

            Text custText = custObj.GetComponent<Text>();
            string rawName = order.GetCustomerName();
            string custName = rawName.StartsWith("@") ? rawName : (rawName.Contains("_") || System.Text.RegularExpressions.Regex.IsMatch(rawName, @"\d") ? $"@{rawName}" : rawName);
            custText.text = $"👤 Cliente: <b>{custName}</b>";
            custText.fontSize = 21;
            custText.alignment = TextAnchor.MiddleLeft;
            custText.color = new Color(0.15f, 0.30f, 0.55f);
            custText.font = UIFontHelper.GetDefaultFont();

            // 3. Marco y Foto del Producto
            GameObject photoFrame = new GameObject("PhotoFrame", typeof(RectTransform), typeof(Image));
            photoFrame.transform.SetParent(cardObj.transform, false);
            RectTransform pfRt = photoFrame.GetComponent<RectTransform>();
            pfRt.anchorMin = new Vector2(0.06f, 0.38f);
            pfRt.anchorMax = new Vector2(0.94f, 0.80f);
            pfRt.offsetMin = Vector2.zero;
            pfRt.offsetMax = Vector2.zero;
            Image pfImg = photoFrame.GetComponent<Image>();
            pfImg.color = new Color(0.93f, 0.95f, 0.98f);

            var pfOutline = photoFrame.AddComponent<Outline>();
            pfOutline.effectColor = new Color(0.80f, 0.85f, 0.92f);
            pfOutline.effectDistance = new Vector2(1f, -1f);

            GameObject imgObj = new GameObject("ProductPhoto", typeof(RectTransform), typeof(Image));
            imgObj.transform.SetParent(photoFrame.transform, false);
            RectTransform imgRt = imgObj.GetComponent<RectTransform>();
            imgRt.anchorMin = new Vector2(0.04f, 0.04f);
            imgRt.anchorMax = new Vector2(0.96f, 0.96f);
            imgRt.offsetMin = Vector2.zero;
            imgRt.offsetMax = Vector2.zero;

            Image img = imgObj.GetComponent<Image>();
            img.sprite = order.product != null ? order.product.GetSprite() : null;
            img.preserveAspect = true;

            // 4. Nombre de la Ilustración
            GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleObj.transform.SetParent(cardObj.transform, false);
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.04f, 0.28f);
            titleRt.anchorMax = new Vector2(0.96f, 0.37f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;

            Text titleText = titleObj.GetComponent<Text>();
            titleText.text = order.product != null ? order.product.title : "Ilustración";
            titleText.fontSize = 24;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.08f, 0.12f, 0.18f);
            titleText.font = UIFontHelper.GetDefaultFont();

            // 5. Cantidad de Copias Requeridas (Insignia destacada)
            GameObject reqObj = new GameObject("UnitsBox", typeof(RectTransform), typeof(Image));
            reqObj.transform.SetParent(cardObj.transform, false);
            RectTransform reqRt = reqObj.GetComponent<RectTransform>();
            reqRt.anchorMin = new Vector2(0.05f, 0.15f);
            reqRt.anchorMax = new Vector2(0.95f, 0.26f);
            reqRt.offsetMin = Vector2.zero;
            reqRt.offsetMax = Vector2.zero;

            Image reqBg = reqObj.GetComponent<Image>();
            reqBg.color = new Color(0.99f, 0.95f, 0.88f); // Ámbar suave

            var reqOutline = reqObj.AddComponent<Outline>();
            reqOutline.effectColor = new Color(0.92f, 0.70f, 0.35f, 0.6f);
            reqOutline.effectDistance = new Vector2(1f, -1f);

            GameObject reqTxtObj = new GameObject("UnitsText", typeof(RectTransform), typeof(Text));
            reqTxtObj.transform.SetParent(reqObj.transform, false);
            RectTransform rtTxt = reqTxtObj.GetComponent<RectTransform>();
            rtTxt.anchorMin = Vector2.zero;
            rtTxt.anchorMax = Vector2.one;
            rtTxt.offsetMin = Vector2.zero;
            rtTxt.offsetMax = Vector2.zero;

            Text reqText = reqTxtObj.GetComponent<Text>();
            if (order.unitsDelivered > 0)
            {
                reqText.text = $"✓ {order.unitsDelivered}/{order.unitsRequested} COPIAS LISTAS";
                reqText.color = new Color(0.1f, 0.55f, 0.2f);
            }
            else
            {
                reqText.text = $"📄 {order.unitsRequested} {(order.unitsRequested == 1 ? "COPIA REQUERIDA" : "COPIAS REQUERIDAS")}";
                reqText.color = new Color(0.72f, 0.32f, 0.05f);
            }
            reqText.fontSize = 20;
            reqText.fontStyle = FontStyle.Bold;
            reqText.alignment = TextAnchor.MiddleCenter;
            reqText.font = UIFontHelper.GetDefaultFont();

            // 6. Monto de Pago Total al Entregar el Sobre
            GameObject payObj = new GameObject("PayBanner", typeof(RectTransform), typeof(Image));
            payObj.transform.SetParent(cardObj.transform, false);
            RectTransform payRt = payObj.GetComponent<RectTransform>();
            payRt.anchorMin = new Vector2(0.05f, 0.03f);
            payRt.anchorMax = new Vector2(0.95f, 0.13f);
            payRt.offsetMin = Vector2.zero;
            payRt.offsetMax = Vector2.zero;

            Image payBg = payObj.GetComponent<Image>();
            payBg.color = new Color(0.90f, 0.98f, 0.92f); // Verde claro agradable

            var payOutline = payObj.AddComponent<Outline>();
            payOutline.effectColor = new Color(0.40f, 0.80f, 0.50f, 0.5f);
            payOutline.effectDistance = new Vector2(1f, -1f);

            GameObject payTxtObj = new GameObject("PayText", typeof(RectTransform), typeof(Text));
            payTxtObj.transform.SetParent(payObj.transform, false);
            RectTransform ptRt = payTxtObj.GetComponent<RectTransform>();
            ptRt.anchorMin = Vector2.zero;
            ptRt.anchorMax = Vector2.one;
            ptRt.offsetMin = Vector2.zero;
            ptRt.offsetMax = Vector2.zero;

            Text payText = payTxtObj.GetComponent<Text>();
            int unitPrice = order.product != null ? order.product.price : 1000;
            int totalPay = unitPrice * order.unitsRequested;
            payText.text = $"PAGO TOTAL: <b>${totalPay:N0}</b>";
            payText.fontSize = 22;
            payText.fontStyle = FontStyle.Bold;
            payText.alignment = TextAnchor.MiddleCenter;
            payText.color = new Color(0.08f, 0.50f, 0.20f);
            payText.font = UIFontHelper.GetDefaultFont();

            return cardObj;
        }

        #endregion

        public void BindUI(
            Button tabStore, Button tabOrders, Text labelStore, Text labelOrders, Button home,
            GameObject panelStore, GameObject panelOrders, Transform containerStore, Transform containerOrders,
            GameObject modal, Transform pickerContent, Image preview, Text priceTxt, Text nameTxt,
            Button minusBtn, Button plusBtn, Button publishBtn, Button closeBtn, Text errorTxt, Text moneyTxt = null,
            Button headerPublish = null)
        {
            tabStoreButton = tabStore;
            tabOrdersButton = tabOrders;
            tabStoreLabel = labelStore;
            tabOrdersLabel = labelOrders;
            homeButton = home;
            if (headerPublish != null) btnHeaderPublish = headerPublish;

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
            if (moneyTxt != null) moneyDisplayText = moneyTxt;

            SetupTabButtons();
            SetupModalButtons();
            SetupMoneyDisplay();
        }
    }
}
