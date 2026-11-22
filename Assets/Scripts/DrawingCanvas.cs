using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ArtSimulator
{

    [RequireComponent(typeof(Collider))]
    public class DrawingCanvas : MonoBehaviour
    {
        [Header("Dimensiones del Lienzo")]
        [SerializeField] private int textureWidth = 512;
        [SerializeField] private int textureHeight = 512;

        [Header("Propiedades del Pincel")]
        [SerializeField] private Color currentBrushColor = Color.black;
        [SerializeField] private int currentBrushSize = 8;
        [SerializeField] private bool useInterpolation = true;

        private Texture2D drawingTexture;
        private Color[] colorMap;
        private Renderer meshRenderer;
        private ScannableDrawing scannableDrawing;

        private bool pressedLastFrame = false;
        private int lastX = 0;
        private int lastY = 0;
        private bool hasBeenDrawnOn = false;

        [Header("Métricas de Esfuerzo y Calidad")]
        private float activeDrawingTime = 0f;
        private HashSet<Color32> usedColors = new HashSet<Color32>();
        private float currentQualityScore = 0.5f;

        public bool HasBeenDrawnOn => hasBeenDrawnOn;
        public Color CurrentBrushColor => currentBrushColor;
        public int CurrentBrushSize => currentBrushSize;
        public float ActiveDrawingTime => activeDrawingTime;
        public float CurrentQualityScore => currentQualityScore;

        private void Awake()
        {
            meshRenderer = GetComponent<Renderer>();
            scannableDrawing = GetComponent<ScannableDrawing>();
            InitializeCanvas();
        }

        public void InitializeCanvas()
        {
            if (drawingTexture != null) return;

            // Si este objeto es una copia impresa, NO debe inicializar un lienzo blanco ni sobrescribir su textura
            if (gameObject.name.Contains("Copia") || (scannableDrawing != null && !scannableDrawing.IsOriginal))
            {
                return;
            }

            colorMap = new Color[textureWidth * textureHeight];
            for (int i = 0; i < colorMap.Length; i++)
            {
                colorMap[i] = Color.white;
            }

            drawingTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            drawingTexture.filterMode = FilterMode.Bilinear;
            drawingTexture.wrapMode = TextureWrapMode.Clamp;
            drawingTexture.name = $"Lienzo_{gameObject.name}";
            drawingTexture.SetPixels(colorMap);
            drawingTexture.Apply(false);

            // Instanciar material independiente para no afectar otras hojas
            if (meshRenderer != null)
            {
                Material instancedMat = meshRenderer.sharedMaterial != null ? new Material(meshRenderer.sharedMaterial) : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                if (instancedMat.HasProperty("_BaseColor")) instancedMat.SetColor("_BaseColor", Color.white);
                if (instancedMat.HasProperty("_Color")) instancedMat.SetColor("_Color", Color.white);
                if (instancedMat.HasProperty("_BaseMap")) instancedMat.SetTexture("_BaseMap", drawingTexture);
                if (instancedMat.HasProperty("_MainTex")) instancedMat.SetTexture("_MainTex", drawingTexture);
                meshRenderer.material = instancedMat;
            }

            // Sincronizar con ScannableDrawing si existe
            if (scannableDrawing != null)
            {
                string title = string.IsNullOrEmpty(scannableDrawing.GetArtworkData().title) 
                    ? $"Dibujo {gameObject.name}" 
                    : scannableDrawing.GetArtworkData().title;
                scannableDrawing.SetArtwork(new ArtworkData(title, drawingTexture));
            }
        }

        public void ResetToFreshCanvas(string title = null)
        {
            if (meshRenderer == null) meshRenderer = GetComponent<Renderer>();
            if (scannableDrawing == null) scannableDrawing = GetComponent<ScannableDrawing>();

            colorMap = new Color[textureWidth * textureHeight];
            for (int i = 0; i < colorMap.Length; i++)
            {
                colorMap[i] = Color.white;
            }

            drawingTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            drawingTexture.filterMode = FilterMode.Bilinear;
            drawingTexture.wrapMode = TextureWrapMode.Clamp;
            drawingTexture.name = $"Lienzo_{gameObject.name}";
            drawingTexture.SetPixels(colorMap);
            drawingTexture.Apply(false);
            hasBeenDrawnOn = false;
            activeDrawingTime = 0f;
            usedColors.Clear();
            currentQualityScore = 0.1f;

            if (meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                Material instancedMat = meshRenderer.sharedMaterial != null ? new Material(meshRenderer.sharedMaterial) : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                if (instancedMat.HasProperty("_BaseColor")) instancedMat.SetColor("_BaseColor", Color.white);
                if (instancedMat.HasProperty("_Color")) instancedMat.SetColor("_Color", Color.white);
                if (instancedMat.HasProperty("_BaseMap")) instancedMat.SetTexture("_BaseMap", drawingTexture);
                if (instancedMat.HasProperty("_MainTex")) instancedMat.SetTexture("_MainTex", drawingTexture);
                meshRenderer.material = instancedMat;
            }

            if (scannableDrawing != null)
            {
                string finalTitle = string.IsNullOrEmpty(title) ? gameObject.name : title;
                scannableDrawing.SetArtwork(new ArtworkData(finalTitle, drawingTexture, 0.1f, 0f, 0f));
                scannableDrawing.QualityScore = 0.1f;
                scannableDrawing.SetIsOriginal(true);
            }
        }

        public void SetBrushColor(Color color)
        {
            currentBrushColor = color;
        }

        public void SetBrushSize(int size)
        {
            currentBrushSize = Mathf.Clamp(size, 2, 32);
        }

        public void SetEraser()
        {
            currentBrushColor = Color.white;
        }

        public void ClearCanvas()
        {
            if (colorMap == null || drawingTexture == null) return;

            for (int i = 0; i < colorMap.Length; i++)
            {
                colorMap[i] = Color.white;
            }

            drawingTexture.SetPixels(colorMap);
            drawingTexture.Apply(false);
            hasBeenDrawnOn = false;

            if (scannableDrawing != null)
            {
                scannableDrawing.SetArtwork(new ArtworkData(scannableDrawing.GetArtworkData().title, drawingTexture));
            }

            Debug.Log($"<color=cyan>[DrawingCanvas]</color> Lienzo '{gameObject.name}' reiniciado a blanco.");
        }

        public bool DrawAtWorldPoint(Vector3 worldPoint)
        {
            if (colorMap == null || drawingTexture == null)
            {
                InitializeCanvas();
            }

            // Calcular coordenadas locales relativas al plano de Dibujo (extensión -5 a +5)
            Vector3 localPos = transform.InverseTransformPoint(worldPoint);

            // COMPROBACIÓN FÍSICA ESTRICTA: El lápiz debe estar tocando físicamente la superficie del papel
            // El grosor local del papel es mínimo; si el lápiz está a más de 1.8 cm en el aire (eje Y local), NO dibuja
            float surfaceDistance = Mathf.Abs(localPos.y) * transform.lossyScale.y;
            if (surfaceDistance > 0.018f)
            {
                pressedLastFrame = false;
                return false;
            }

            float u = Mathf.InverseLerp(5.0f, -5.0f, localPos.x);
            float v = Mathf.InverseLerp(5.0f, -5.0f, localPos.z);

            // Fuera de los límites del papel
            if (u < 0f || u > 1f || v < 0f || v > 1f)
            {
                pressedLastFrame = false;
                return false;
            }

            int px = Mathf.Clamp((int)(u * textureWidth), 0, textureWidth - 1);
            int py = Mathf.Clamp((int)(v * textureHeight), 0, textureHeight - 1);

            // Interpolación de trazo continuo entre cuadros
            if (useInterpolation && pressedLastFrame && (lastX != px || lastY != py))
            {
                int dist = (int)Mathf.Sqrt((px - lastX) * (px - lastX) + (py - lastY) * (py - lastY));
                for (int i = 1; i <= dist; i++)
                {
                    int stepX = (i * px + (dist - i) * lastX) / dist;
                    int stepY = (i * py + (dist - i) * lastY) / dist;
                    PaintBrushCircle(stepX, stepY);
                }
            }
            else
            {
                PaintBrushCircle(px, py);
            }

            pressedLastFrame = true;
            lastX = px;
            lastY = py;
            hasBeenDrawnOn = true;
            activeDrawingTime += Time.deltaTime;

            if (currentBrushColor.r < 0.96f || currentBrushColor.g < 0.96f || currentBrushColor.b < 0.96f)
            {
                usedColors.Add((Color32)currentBrushColor);
            }

            drawingTexture.SetPixels(colorMap);
            drawingTexture.Apply(false);
            return true;
        }

        public void StopStroke()
        {
            pressedLastFrame = false;
            float quality = CalculateQualityMetrics(out float coverage, out float timeSpent, out int colorsCount);
            currentQualityScore = quality;

            if (scannableDrawing != null && drawingTexture != null)
            {
                string title = scannableDrawing.GetArtworkData() != null ? scannableDrawing.GetArtworkData().title : gameObject.name;
                scannableDrawing.SetArtwork(new ArtworkData(title, drawingTexture, quality, coverage, timeSpent));
                scannableDrawing.QualityScore = quality;
            }
        }

        /// <summary>
        /// Calcula el índice de calidad y esfuerzo del dibujo:
        /// - Cobertura del lienzo (área pintada vs fondo blanco): 45% peso
        /// - Tiempo activo invertido dibujando: 35% peso
        /// - Variedad de colores utilizados: 20% peso
        /// </summary>
        public float CalculateQualityMetrics(out float coverage, out float timeSpent, out int colorsCount)
        {
            timeSpent = activeDrawingTime;
            colorsCount = usedColors.Count;

            if (colorMap == null || colorMap.Length == 0)
            {
                coverage = 0f;
                return 0.10f;
            }

            // Muestreo optimizado de píxeles modificados respecto al blanco original
            int step = 4; // Muestrear cada 4 píxeles para cálculo ultra-rápido
            int sampledTotal = 0;
            int modifiedCount = 0;

            for (int i = 0; i < colorMap.Length; i += step)
            {
                sampledTotal++;
                Color c = colorMap[i];
                // Si el píxel no es blanco puro / borrador
                if (c.r < 0.92f || c.g < 0.92f || c.b < 0.92f)
                {
                    modifiedCount++;
                }
            }

            coverage = sampledTotal > 0 ? (float)modifiedCount / sampledTotal : 0f;

            // Factores normalizados 0..1
            // 1. Cobertura: Si pintó el fondo o gran parte del lienzo (ej. > 40%), llega a 1.0
            float coverageFactor = Mathf.Clamp01(coverage / 0.40f);

            // 2. Tiempo invertido: 45 segundos o más otorga puntaje completo de dedicación
            float timeFactor = Mathf.Clamp01(activeDrawingTime / 40.0f);

            // 3. Variedad cromática: 3 colores o más otorga puntaje completo
            float colorFactor = Mathf.Clamp01(colorsCount / 3.0f);

            // Calidad final combinada (rango 0.10 a 1.00)
            float score = (coverageFactor * 0.45f) + (timeFactor * 0.35f) + (colorFactor * 0.20f);
            return Mathf.Clamp(score, 0.10f, 1.0f);
        }

        private void PaintBrushCircle(int cx, int cy)
        {
            int r = currentBrushSize;
            int rSq = r * r;

            int minX = Mathf.Max(0, cx - r + 1);
            int maxX = Mathf.Min(textureWidth - 1, cx + r - 1);
            int minY = Mathf.Max(0, cy - r + 1);
            int maxY = Mathf.Min(textureHeight - 1, cy + r - 1);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= rSq)
                    {
                        colorMap[y * textureWidth + x] = currentBrushColor;
                    }
                }
            }
        }

        public Texture2D GetTexture()
        {
            if (drawingTexture == null) InitializeCanvas();
            return drawingTexture;
        }
    }
}
