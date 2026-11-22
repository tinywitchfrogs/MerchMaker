using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ArtSimulator
{
    [RequireComponent(typeof(Collider))]
    public class ScannableDrawing : MonoBehaviour
    {
        [Header("Datos de la Ilustración")]
        [SerializeField] private string artworkTitle = "Ilustración Artística";
        [SerializeField] private Texture2D artworkTexture;
        [SerializeField] public bool isOriginal = true;
        [SerializeField] private float qualityScore = 0.5f;

        public bool IsOriginal => isOriginal;
        public float QualityScore
        {
            get => qualityScore;
            set
            {
                qualityScore = Mathf.Clamp(value, 0.1f, 1.0f);
                if (artworkData != null) artworkData.qualityScore = qualityScore;
            }
        }

        private Renderer meshRenderer;
        private ArtworkData artworkData;
        private bool isScanned = false;
        private XRGrabInteractable grabInteractable;
        private GameObject starIndicatorObj;

        public bool IsScanned => isScanned;

        public static void SetAllAurasVisible(bool visible)
        {
            // Mantenido por retrocompatibilidad con llamadas externas
        }

        private void Awake()
        {
            meshRenderer = GetComponent<Renderer>();
            grabInteractable = GetComponent<XRGrabInteractable>();

            // Limpiar cualquier aura residual previa
            Transform oldAura = transform.Find("AuraEffect");
            if (oldAura != null)
            {
                if (Application.isPlaying) Destroy(oldAura.gameObject);
                else DestroyImmediate(oldAura.gameObject);
            }

            if (string.IsNullOrEmpty(artworkTitle))
            {
                artworkTitle = "Ilustración Artística";
            }

            if (artworkTexture == null)
            {
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

            SetupStarIndicator();
        }

        private void SetupStarIndicator()
        {
            Transform existingStar = transform.Find("OriginalStarIndicator");
            if (existingStar != null)
            {
                starIndicatorObj = existingStar.gameObject;
            }

            if (!isOriginal)
            {
                if (starIndicatorObj != null) starIndicatorObj.SetActive(false);
                return;
            }

            if (starIndicatorObj == null)
            {
                starIndicatorObj = new GameObject("OriginalStarIndicator");
                starIndicatorObj.transform.SetParent(transform, false);

                // Ubicar en la esquina superior derecha del plano de la hoja
                starIndicatorObj.transform.localPosition = new Vector3(3.6f, 0.006f, 3.6f);
                starIndicatorObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                starIndicatorObj.transform.localScale = new Vector3(0.003f, 0.003f, 0.003f);

                Canvas starCanvas = starIndicatorObj.AddComponent<Canvas>();
                starCanvas.renderMode = RenderMode.WorldSpace;
                var rt = starIndicatorObj.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(100, 100);

                GameObject textObj = new GameObject("StarText");
                textObj.transform.SetParent(starIndicatorObj.transform, false);
                var tRt = textObj.AddComponent<RectTransform>();
                tRt.anchorMin = Vector2.zero;
                tRt.anchorMax = Vector2.one;
                tRt.offsetMin = Vector2.zero;
                tRt.offsetMax = Vector2.zero;

                Text starTxt = textObj.AddComponent<Text>();
                starTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                starTxt.fontSize = 72;
                starTxt.fontStyle = FontStyle.Bold;
                starTxt.alignment = TextAnchor.MiddleCenter;
                starTxt.color = new Color(1.0f, 0.84f, 0.0f, 0.95f); // Dorado brillante
                starTxt.text = "★";
                starTxt.raycastTarget = false;
            }

            // Iniciar oculto (solo visible al tomar la hoja)
            starIndicatorObj.SetActive(false);
        }

        private void Update()
        {
            if (grabInteractable == null)
            {
                grabInteractable = GetComponent<XRGrabInteractable>();
            }

            // Solo los dibujos originales muestran la estrella en la esquina al ser sostenidos
            if (isOriginal && starIndicatorObj != null)
            {
                bool isHeld = (grabInteractable != null && grabInteractable.isSelected) ||
                              (FirstPersonPlayerController.Instance != null && FirstPersonPlayerController.Instance.IsHoldingObject(transform));

                if (starIndicatorObj.activeSelf != isHeld)
                {
                    starIndicatorObj.SetActive(isHeld);
                }
            }
            else if (starIndicatorObj != null && starIndicatorObj.activeSelf)
            {
                starIndicatorObj.SetActive(false);
            }
        }

        public void SetIsOriginal(bool original)
        {
            isOriginal = original;
            SetupStarIndicator();
        }

        public ArtworkData GetArtworkData()
        {
            if (string.IsNullOrEmpty(artworkTitle))
            {
                artworkTitle = "Ilustración Artística";
            }

            Texture2D currentVisualTex = null;

            DrawingCanvas canvas = GetComponent<DrawingCanvas>();
            if (canvas != null)
            {
                currentVisualTex = canvas.GetTexture();
            }

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

            if (currentVisualTex == null && artworkTexture != null)
            {
                currentVisualTex = artworkTexture;
            }

            if (currentVisualTex == null)
            {
                currentVisualTex = GenerateSampleArtwork(artworkTitle);
                artworkTexture = currentVisualTex;
                ApplyTextureToMaterial();
            }

            artworkTexture = currentVisualTex;

            if (artworkData == null || artworkData.texture != currentVisualTex)
            {
                artworkData = new ArtworkData(artworkTitle, currentVisualTex, qualityScore);
            }

            return artworkData;
        }

        public void SetArtwork(ArtworkData data)
        {
            if (data == null) return;
            artworkData = data;
            artworkTitle = data.title;
            artworkTexture = data.texture;
            qualityScore = data.qualityScore;
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

            Color paperColor = Color.white;
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
