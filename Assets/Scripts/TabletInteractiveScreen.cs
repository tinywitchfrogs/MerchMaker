using UnityEngine;

namespace ArtSimulator
{
    public class TabletInteractiveScreen : MonoBehaviour
    {
        public static TabletInteractiveScreen Instance { get; private set; }

        [Header("Referencias 3D")]
        [SerializeField] private Transform screenMeshTransform;
        [SerializeField] private BoxCollider tableCollider;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            EnsureCollidersAndVisuals();
        }

        public void SetupScreenReferences() => EnsureCollidersAndVisuals();
        public void AlignCanvasToMesh() { }

        public void EnsureCollidersAndVisuals()
        {
            // 1. Encontrar o crear BoxCollider en la tableta para clics desde el mundo
            if (tableCollider == null) tableCollider = GetComponent<BoxCollider>();
            if (tableCollider == null) tableCollider = gameObject.AddComponent<BoxCollider>();

            tableCollider.isTrigger = false;

            // Ajustar tamaño del collider a los bounds del modelo
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    b.Encapsulate(renderers[i].bounds);
                }

                tableCollider.center = transform.InverseTransformPoint(b.center);
                Vector3 localSize = transform.InverseTransformVector(b.size);
                tableCollider.size = new Vector3(
                    Mathf.Max(0.2f, Mathf.Abs(localSize.x)),
                    Mathf.Max(0.1f, Mathf.Abs(localSize.y)),
                    Mathf.Max(0.2f, Mathf.Abs(localSize.z))
                );
            }

            // 2. Darle aspecto de pantalla iluminada a la Pantalla 3D en la mesa
            if (screenMeshTransform == null)
            {
                Transform p = transform.Find("Pantalla");
                if (p != null) screenMeshTransform = p;
            }

            if (screenMeshTransform != null)
            {
                MeshRenderer mr = screenMeshTransform.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    Material mat = Application.isPlaying ? mr.material : mr.sharedMaterial;
                    Texture2D homeTex = GenerateTabletHomeTexture();
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", homeTex);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", homeTex);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                }
            }

            // 3. Garantizar que jamás tenga físicas de Rigidbody (100% estática)
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);
        }

        public void InteractWithTablet()
        {
            if (TabletManager.Instance != null)
            {
                TabletManager.Instance.OpenTablet();
            }
        }

        private static Texture2D GenerateTabletHomeTexture()
        {
            int w = 512;
            int h = 320;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[w * h];

            Color bgColor = new Color(0.93f, 0.94f, 0.96f);
            Color topBarColor = new Color(0.82f, 0.84f, 0.88f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    pixels[y * w + x] = y > h - 30 ? topBarColor : bgColor;
                }
            }

            // Dibujar los 3 iconos de apps (Imágenes, Cámara, Tienda)
            DrawAppIcon(pixels, w, h, 110, 150, 42, new Color(0.25f, 0.55f, 0.90f)); // Azul Imágenes
            DrawAppIcon(pixels, w, h, 256, 150, 42, new Color(0.22f, 0.24f, 0.27f)); // Oscuro Cámara
            DrawAppIcon(pixels, w, h, 402, 150, 42, new Color(0.92f, 0.52f, 0.20f)); // Naranja Tienda

            tex.SetPixels(pixels);
            tex.Apply();
            tex.name = "TabletHomePreviewTexture";
            return tex;
        }

        private static void DrawAppIcon(Color[] pixels, int w, int h, int cx, int cy, int radius, Color color)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int px = cx + dx;
                    int py = cy + dy;
                    if (px >= 0 && px < w && py >= 0 && py < h)
                    {
                        if (Mathf.Abs(dx) <= radius - 4 || Mathf.Abs(dy) <= radius - 4 || (dx * dx + dy * dy <= radius * radius))
                        {
                            pixels[py * w + px] = color;
                        }
                    }
                }
            }
        }
    }
}
