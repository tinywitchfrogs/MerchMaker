using System;
using UnityEngine;
using UnityEngine.UI;

namespace ArtSimulator
{
    public class DrawingUIOverlay : MonoBehaviour
    {
        private static DrawingUIOverlay instance;
        public static DrawingUIOverlay Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<DrawingUIOverlay>();
                    if (instance == null)
                    {
                        GameObject obj = new GameObject("DrawingUIOverlay");
                        instance = obj.AddComponent<DrawingUIOverlay>();
                    }
                }
                return instance;
            }
        }

        private GameObject canvasObj;
        private DrawingCanvas currentCanvas;
        private Image currentColorIndicator;
        private Text brushSizeText;
        private Text zoomLabel;
        private bool isVisible = false;

        public bool IsVisible => isVisible;

        private void Awake()
        {
            if (instance == null) instance = this;
            BuildUI();
        }

        private void BuildUI()
        {
            if (canvasObj != null) return;

            canvasObj = new GameObject("DrawingModeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 980;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Panel inferior para herramientas
            GameObject toolbar = new GameObject("DrawingToolbar", typeof(RectTransform), typeof(Image));
            toolbar.transform.SetParent(canvasObj.transform, false);

            RectTransform tbRt = toolbar.GetComponent<RectTransform>();
            tbRt.anchorMin = new Vector2(0.05f, 0.03f);
            tbRt.anchorMax = new Vector2(0.95f, 0.12f);
            tbRt.offsetMin = Vector2.zero;
            tbRt.offsetMax = Vector2.zero;

            Image tbImg = toolbar.GetComponent<Image>();
            tbImg.color = new Color(0.08f, 0.11f, 0.16f, 0.95f);

            Outline tbOutline = toolbar.AddComponent<Outline>();
            tbOutline.effectColor = new Color(0.25f, 0.35f, 0.50f, 0.8f);
            tbOutline.effectDistance = new Vector2(2f, -2f);

            HorizontalLayoutGroup hlg = toolbar.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(16, 16, 8, 8);
            hlg.spacing = 8;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // Indicador de modo
            CreateLabel(toolbar.transform, "🖌️ <b>DIBUJAR</b>", 13, Color.cyan, 95);

            // Paleta de colores
            Color[] palette = new Color[]
            {
                Color.black,
                new Color(0.12f, 0.45f, 0.95f), // Azul
                new Color(0.90f, 0.18f, 0.18f), // Rojo
                new Color(0.14f, 0.68f, 0.28f), // Verde
                new Color(0.95f, 0.78f, 0.10f), // Amarillo
                new Color(0.65f, 0.20f, 0.90f), // Morado
            };

            foreach (var col in palette)
            {
                Color c = col;
                CreateColorButton(toolbar.transform, c, () => SelectColor(c));
            }

            // Botón Borrador
            CreateActionButton(toolbar.transform, "🧹 Borrador", new Color(0.35f, 0.38f, 0.45f), () =>
            {
                if (currentCanvas != null) currentCanvas.SetEraser();
                if (currentColorIndicator != null) currentColorIndicator.color = Color.white;
            }, 85);

            // Selector de grosor
            CreateActionButton(toolbar.transform, "Fino", new Color(0.20f, 0.25f, 0.35f), () => SetBrushSize(4, "4px"), 50);
            CreateActionButton(toolbar.transform, "Medio", new Color(0.20f, 0.25f, 0.35f), () => SetBrushSize(8, "8px"), 55);
            CreateActionButton(toolbar.transform, "Grueso", new Color(0.20f, 0.25f, 0.35f), () => SetBrushSize(16, "16px"), 60);

            // CONTROLES DE ZOOM DE DIBUJO
            CreateActionButton(toolbar.transform, "🔍 -", new Color(0.22f, 0.28f, 0.38f), () =>
            {
                FirstPersonPlayerController.Instance?.AdjustDrawingZoom(+6f);
            }, 45);

            zoomLabel = CreateLabel(toolbar.transform, "1.7x", 12, Color.cyan, 45);

            CreateActionButton(toolbar.transform, "🔍 +", new Color(0.22f, 0.28f, 0.38f), () =>
            {
                FirstPersonPlayerController.Instance?.AdjustDrawingZoom(-6f);
            }, 45);

            // Botón Limpiar
            CreateActionButton(toolbar.transform, "🗑️ Limpiar", new Color(0.70f, 0.18f, 0.18f), () =>
            {
                if (currentCanvas != null) currentCanvas.ClearCanvas();
            }, 80);

            // Botón Terminar / Salir
            CreateActionButton(toolbar.transform, "✓ Terminar [F / ESC]", new Color(0.14f, 0.65f, 0.35f), () =>
            {
                ExitDrawingMode();
            }, 140);

            canvasObj.SetActive(false);
        }

        private void SelectColor(Color color)
        {
            if (currentCanvas != null) currentCanvas.SetBrushColor(color);
            if (currentColorIndicator != null) currentColorIndicator.color = color;
        }

        private void SetBrushSize(int size, string label)
        {
            if (currentCanvas != null) currentCanvas.SetBrushSize(size);
            if (brushSizeText != null) brushSizeText.text = label;
        }

        private void CreateColorButton(Transform parent, Color color, Action onClick)
        {
            GameObject btnObj = new GameObject($"Color_{ColorUtility.ToHtmlStringRGB(color)}", typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.preferredWidth = 36;
            le.preferredHeight = 36;

            Image img = btnObj.GetComponent<Image>();
            img.color = color;

            Outline outline = btnObj.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());
        }

        private void CreateActionButton(Transform parent, string label, Color bgColor, Action onClick, float width)
        {
            GameObject btnObj = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = 36;

            Image img = btnObj.GetComponent<Image>();
            img.color = bgColor;

            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(btnObj.transform, false);

            RectTransform tRt = textObj.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;

            Text txt = textObj.GetComponent<Text>();
            txt.text = label;
            txt.font = UIFontHelper.GetDefaultFont();
            txt.fontSize = 12;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
        }

        private Text CreateLabel(Transform parent, string text, int fontSize, Color color, float width)
        {
            GameObject textObj = new GameObject("TitleLabel", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            textObj.transform.SetParent(parent, false);

            LayoutElement le = textObj.GetComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = 36;

            Text txt = textObj.GetComponent<Text>();
            txt.text = text;
            txt.font = UIFontHelper.GetDefaultFont();
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            return txt;
        }

        public void UpdateZoomText(string text)
        {
            if (zoomLabel != null)
            {
                zoomLabel.text = text;
            }
        }

        public void Show(DrawingCanvas canvas)
        {
            BuildUI();
            currentCanvas = canvas;
            isVisible = true;
            if (canvasObj != null) canvasObj.SetActive(true);
        }

        public void Hide()
        {
            isVisible = false;
            currentCanvas = null;
            if (canvasObj != null) canvasObj.SetActive(false);
        }

        private void ExitDrawingMode()
        {
            Hide();
            var player = FirstPersonPlayerController.Instance;
            if (player != null)
            {
                player.ExitDrawingMode();
            }
        }
    }
}
