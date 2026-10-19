using UnityEngine;

namespace ArtSimulator
{
    [RequireComponent(typeof(Collider))]
    public class ScannableDrawing : MonoBehaviour
    {
        [Header("Datos de la Ilustración")]
        [SerializeField] private string artworkTitle = "Ilustración Artística";
        [SerializeField] private Texture2D artworkTexture;
        [SerializeField] public bool isOriginal = true;

        public bool IsOriginal => isOriginal;

        private Renderer meshRenderer;
        private ArtworkData artworkData;
        private bool isScanned = false;

        public bool IsScanned => isScanned;

        private static readonly System.Collections.Generic.HashSet<ScannableDrawing> allDrawings = new System.Collections.Generic.HashSet<ScannableDrawing>();
        private static bool globalAurasVisible = true;

        
        /// <summary>
        /// Oculta o muestra todas las auras de dibujos (originales y copias), útil al tomar fotos con la cámara.
        /// </summary>
        public static void SetAllAurasVisible(bool visible)
        {
            globalAurasVisible = visible;
            foreach (var drawing in allDrawings)
            {
                if (drawing != null)
                {
                    drawing.SetAuraVisible(visible);
                }
            }
        }

        private void OnEnable()
        {
            allDrawings.Add(this);
            SetAuraVisible(globalAurasVisible);
        }

        private void OnDisable()
        {
            allDrawings.Remove(this);
        }

        private void OnDestroy()
        {
            allDrawings.Remove(this);
        }

        public void SetAuraVisible(bool visible)
        {
            Transform existingAura = transform.Find("AuraEffect");
            if (existingAura != null)
            {
                existingAura.gameObject.SetActive(visible);
            }
        }

        private void Awake()
        {
            meshRenderer = GetComponent<Renderer>();

            if (string.IsNullOrEmpty(artworkTitle))
            {
                artworkTitle = "Ilustración Artística";
            }

            if (artworkTexture == null)
            {
                // Intentar recuperar del material existente si ya tiene textura
                if (meshRenderer != null && meshRenderer.sharedMaterial != null)
                {
                    Texture matTex = meshRenderer.sharedMaterial.HasProperty("_BaseMap")
                        ? meshRenderer.sharedMaterial.GetTexture("_BaseMap")
                        : meshRenderer.sharedMaterial.mainTexture;
                    if (matTex is Texture2D t2d)
                    {
                        artworkTexture = t2d;
                    }
                }

                // Si es un dibujo original y aún no tiene textura, generar la de muestra
                if (artworkTexture == null && isOriginal)
                {
                    artworkTexture = GenerateSampleArtwork(artworkTitle);
                }
            }

            if (artworkTexture != null)
            {
                ApplyTextureToMaterial();
                artworkData = new ArtworkData(artworkTitle, artworkTexture);
            }

            UpdateAuraVisuals();
        }

        public void SetIsOriginal(bool original)
        {
            isOriginal = original;
            UpdateAuraVisuals();
        }

        public void UpdateAuraVisuals()
        {
            Transform existingAura = transform.Find("AuraEffect");
            GameObject auraObj;
            if (existingAura == null)
            {
                auraObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                auraObj.name = "AuraEffect";
                auraObj.transform.SetParent(transform, false);
                Collider col = auraObj.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }
            else
            {
                auraObj = existingAura.gameObject;
            }

            // Alinear plano del aura detrás del dibujo
            auraObj.transform.localPosition = new Vector3(0, -0.05f, 0);
            auraObj.transform.localRotation = Quaternion.Euler(90, 0, 0);
            auraObj.transform.localScale = new Vector3(12.5f, 12.5f, 1.0f);

            Renderer r = auraObj.GetComponent<Renderer>();
            if (r != null)
            {
                Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
                Material auraMat = new Material(unlitShader);
                auraMat.SetFloat("_Cull", 0f);

                Color auraColor = isOriginal 
                    ? new Color(0.08f, 0.65f, 1.0f, 0.90f)    // Aura Azul eléctrica (Original)
                    : new Color(1.0f, 0.88f, 0.05f, 0.90f);   // Aura Amarilla dorada (Copia)

                if (auraMat.HasProperty("_BaseColor")) auraMat.SetColor("_BaseColor", auraColor);
                if (auraMat.HasProperty("_Color")) auraMat.SetColor("_Color", auraColor);
                r.material = auraMat;
            }

            Light auraLight = auraObj.GetComponent<Light>();
            if (auraLight == null) auraLight = auraObj.AddComponent<Light>();
            auraLight.type = LightType.Point;
            auraLight.range = 0.65f;
            auraLight.intensity = 1.2f;
            auraLight.color = isOriginal ? new Color(0.15f, 0.65f, 1f) : new Color(1f, 0.85f, 0.15f);

            // Respetar estado global de visibilidad (oculto en modo cámara)
            auraObj.SetActive(globalAurasVisible);
        }

        public ArtworkData GetArtworkData()
        {
            if (string.IsNullOrEmpty(artworkTitle))
            {
                artworkTitle = "Ilustración Artística";
            }

            Texture2D currentVisualTex = null;

            // 1. Si el dibujo tiene DrawingCanvas, obtener la textura real que el jugador pintó
            DrawingCanvas canvas = GetComponent<DrawingCanvas>();
            if (canvas != null)
            {
                currentVisualTex = canvas.GetTexture();
            }

            // 2. Si no hay canvas, buscar en el material del Renderer
            if (currentVisualTex == null)
            {
                if (meshRenderer == null) meshRenderer = GetComponent<Renderer>();
                if (meshRenderer != null && meshRenderer.material != null)
                {
                    Texture matTex = meshRenderer.material.HasProperty("_BaseMap")
                        ? meshRenderer.material.GetTexture("_BaseMap")
                        : meshRenderer.material.mainTexture;
                    if (matTex is Texture2D t2d)
                    {
                        currentVisualTex = t2d;
                    }
                }
            }

            // 3. Si sigue nula, usar artworkTexture
            if (currentVisualTex == null && artworkTexture != null)
            {
                currentVisualTex = artworkTexture;
            }

            // 4. Si aún no hay textura, generar muestra
            if (currentVisualTex == null)
            {
                currentVisualTex = GenerateSampleArtwork(artworkTitle);
                artworkTexture = currentVisualTex;
                ApplyTextureToMaterial();
            }

            artworkTexture = currentVisualTex;

            // Mantener artworkData sincronizado con la textura real
            if (artworkData == null || artworkData.texture != currentVisualTex)
            {
                artworkData = new ArtworkData(artworkTitle, currentVisualTex);
            }

            return artworkData;
        }

        public void SetArtwork(ArtworkData data)
        {
            if (data == null) return;
            artworkData = data;
            artworkTitle = data.title;
            artworkTexture = data.texture;
            ApplyTextureToMaterial();
        }

        public void MarkAsScanned()
        {
            isScanned = true;
        }

        public void ApplyTextureToMaterial()
        {
            if (meshRenderer == null)
                meshRenderer = GetComponent<Renderer>();

            if (meshRenderer != null && artworkTexture != null)
            {
                // Crear instancia del material para no alterar otros objetos
                Material mat = meshRenderer.material;
                if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", artworkTexture);
                }
                if (mat.HasProperty("_MainTex"))
                {
                    mat.SetTexture("_MainTex", artworkTexture);
                }
            }
        }

        public static Texture2D GenerateSampleArtwork(string title)
        {
            int width = 512;
            int height = 512;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

            Color paperColor = Color.white; // Hoja de papel limpia en blanco
            Color[] pixels = new Color[width * height];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = paperColor;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            tex.name = title;
            return tex;
        }
    }
}
