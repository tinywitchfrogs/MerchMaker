using System.Collections;
using UnityEngine;

namespace ArtSimulator
{
    public class PrinterController : MonoBehaviour
    {
        private static PrinterController instance;
        public static PrinterController Instance
        {
            get
            {
                if (instance == null) instance = FindFirstObjectByType<PrinterController>();
                return instance;
            }
            private set => instance = value;
        }

        [Header("Puntos de Interacción")]
        [Tooltip("Punto físico donde salen las hojas impresas")]
        [SerializeField] private Transform paperSpawnPoint;

        [Tooltip("Prefab o plantilla de hoja de papel a instanciar")]
        [SerializeField] private GameObject paperPrefab;

        [Header("Configuración")]
        [SerializeField] private float scanCooldown = 1.5f;
        [SerializeField] private float printEjectForce = 0.20f;

        private float lastScanTime = -10f;
        private AudioSource audioSource;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.5f;
            }

            Application.runInBackground = true;
            SetupPrinterPositions();
        }

        public Vector3 GetPrinterCenter()
        {
            Transform modelChild = transform.Find("Impresora_Model");
            if (modelChild != null)
            {
                // El cristal superior del escáner se sitúa sobre el cuerpo de la impresora
                return modelChild.position + Vector3.up * 0.28f;
            }
            return transform.position;
        }

        private void SetupPrinterPositions()
        {
            Transform modelChild = transform.Find("Impresora_Model");

            // Configurar punto de salida de papel alineado con la bandeja frontal
            if (paperSpawnPoint == null)
            {
                Transform existing = transform.Find("PaperSpawnPoint");
                if (existing != null)
                {
                    paperSpawnPoint = existing;
                }
                else
                {
                    GameObject spawnObj = new GameObject("PaperSpawnPoint");
                    spawnObj.transform.SetParent(transform);
                    paperSpawnPoint = spawnObj.transform;
                }
            }

            if (modelChild != null && paperSpawnPoint != null)
            {
                paperSpawnPoint.position = modelChild.position + modelChild.forward * 0.25f + Vector3.up * 0.15f;
                paperSpawnPoint.rotation = modelChild.rotation;
            }

            // Ajustar el trigger BoxCollider exactamente sobre el cristal superior del escáner
            BoxCollider bc = GetComponent<BoxCollider>();
            if (bc != null && modelChild != null)
            {
                bc.isTrigger = true;
                bc.center = modelChild.localPosition + Vector3.up * 1.6f;
                bc.size = new Vector3(3.0f, 1.6f, 2.4f);
            }
        }

        [Header("Dibujo Insertado en Escáner")]
        [SerializeField] private ScannableDrawing currentInsertedDrawing;
        public bool HasDrawingInside => currentInsertedDrawing != null;
        public ScannableDrawing CurrentInsertedDrawing => currentInsertedDrawing;

        public Vector3 GetScannerBedPosition()
        {
            Transform modelChild = transform.Find("Impresora_Model");
            if (modelChild != null)
            {
                // Superficie superior plana del escáner
                return modelChild.position + Vector3.up * 0.28f;
            }
            return transform.position + Vector3.up * 0.28f;
        }

        /// <summary>
        /// Inserta y retiene el dibujo en la impresora/escáner, ejecutando el escaneo.
        /// </summary>
        public bool InsertDrawing(ScannableDrawing drawing)
        {
            if (drawing == null || currentInsertedDrawing != null) return false;

            currentInsertedDrawing = drawing;

            // Colocar el dibujo exactamente sobre el cristal superior del escáner
            drawing.transform.position = GetScannerBedPosition();
            drawing.transform.rotation = transform.rotation * Quaternion.Euler(0, 90, 0);

            // Asegurar que el dibujo quede estático mientras está retenido
            Rigidbody rb = drawing.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            BoxCollider bc = drawing.GetComponent<BoxCollider>();
            if (bc != null)
            {
                bc.enabled = true;
            }

            // Escanear inmediatamente y subir a la tableta
            ScanDrawing(drawing);

            Debug.Log($"<color=cyan>[Impresora]</color> Dibujo <b>'{drawing.GetArtworkData().title}'</b> introducido en el escáner.");
            return true;
        }

        /// <summary>
        /// Retira el dibujo de la impresora para que el jugador lo tome de vuelta.
        /// </summary>
        public ScannableDrawing RetrieveDrawing()
        {
            if (currentInsertedDrawing == null) return null;

            ScannableDrawing drawing = currentInsertedDrawing;
            currentInsertedDrawing = null;

            BoxCollider bc = drawing.GetComponent<BoxCollider>();
            if (bc != null) bc.enabled = true;

            Rigidbody rb = drawing.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }

            Debug.Log($"<color=cyan>[Impresora]</color> Dibujo <b>'{drawing.GetArtworkData().title}'</b> retirado de la impresora.");
            return drawing;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (currentInsertedDrawing != null) return;

            ScannableDrawing drawing = other.GetComponent<ScannableDrawing>();
            if (drawing == null)
            {
                drawing = other.GetComponentInParent<ScannableDrawing>();
            }

            if (drawing != null)
            {
                InsertDrawing(drawing);
            }
        }

        public void ScanDrawing(ScannableDrawing drawing)
        {
            if (drawing == null) return;
            if (Time.time - lastScanTime < scanCooldown) return;

            lastScanTime = Time.time;
            drawing.MarkAsScanned();

            ArtworkData data = drawing.GetArtworkData();
            if (TabletManager.Instance != null)
            {
                TabletManager.Instance.ReceiveScannedArtwork(data);
            }

            // Reproducir sonido de escaneo
            PlayBeepSound(700, 0.18f);

            Debug.Log($"<color=cyan>[Impresora]</color> ¡Dibujo <b>'{data.title}'</b> escaneado con éxito y subido a la tableta! (El dibujo sigue disponible para ser tomado).");
        }

        public void PrintArtwork(ArtworkData artwork)
        {
            PrintArtworks(artwork, 1);
        }

        public void PrintArtworks(ArtworkData artwork, int count)
        {
            if (artwork == null) return;
            count = Mathf.Clamp(count, 1, 10);
            StartCoroutine(PrintCopiesRoutine(artwork, count));
        }

        private System.Collections.IEnumerator PrintCopiesRoutine(ArtworkData artwork, int count)
        {
            for (int i = 0; i < count; i++)
            {
                SpawnPrintedCopy(artwork);
                if (count > 1 && i < count - 1)
                {
                    yield return new WaitForSecondsRealtime(0.35f);
                }
            }
        }

        private void SpawnPrintedCopy(ArtworkData artwork)
        {
            Debug.Log($"<color=orange>[Impresora]</color> Imprimiendo copia física de: <b>{artwork.title}</b>...");
            PlayBeepSound(450, 0.25f);

            // Contar copias existentes en la bandeja para apilar sutilmente en Y (evita solapamiento z-fighting)
            int existingCopies = 0;
            var currentCopies = FindObjectsByType<ScannableDrawing>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < currentCopies.Length; i++)
            {
                if (!currentCopies[i].IsOriginal && Vector3.Distance(currentCopies[i].transform.position, new Vector3(9.02f, 0.335f, -6.50f)) < 0.4f)
                {
                    existingCopies++;
                }
            }

            // Ranura frontal de salida de la impresora (reposa sobre la bandeja de la impresora)
            float stackYOffset = existingCopies * 0.005f;
            Vector3 slotPos = new Vector3(9.18f, 0.335f + stackYOffset, -6.50f);
            Vector3 trayFinalPos = new Vector3(9.02f, 0.335f + stackYOffset, -6.50f);
            Quaternion spawnRot = Quaternion.Euler(0, 90, 0);

            GameObject newPaper;
            if (paperPrefab != null)
            {
                newPaper = Instantiate(paperPrefab, trayFinalPos, spawnRot);
            }
            else
            {
                GameObject sceneDrawing = GameObject.Find("Dibujo") ?? GameObject.Find("Dibujo_1");
                if (sceneDrawing == null)
                {
                    var foundScannable = FindFirstObjectByType<ScannableDrawing>();
                    if (foundScannable != null) sceneDrawing = foundScannable.gameObject;
                }

                if (sceneDrawing != null)
                {
                    newPaper = Instantiate(sceneDrawing, trayFinalPos, spawnRot);
                }
                else
                {
                    newPaper = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    newPaper.transform.position = trayFinalPos;
                    newPaper.transform.rotation = Quaternion.Euler(90, 0, 0);
                    newPaper.transform.localScale = new Vector3(0.04f, 0.04f, 0.03f);
                }
            }

            newPaper.name = $"Dibujo_Copia_{artwork.title}";
            newPaper.transform.position = slotPos;
            newPaper.transform.rotation = spawnRot;
            GameObject origDrawing = GameObject.Find("Dibujo") ?? GameObject.Find("Dibujo_1") ?? newPaper;
            if (origDrawing != null)
            {
                newPaper.transform.localScale = origDrawing.transform.localScale;
            }

            // Si la plantilla clonada tenía DrawingCanvas, removerlo para que la copia impresa mantenga su arte
            DrawingCanvas copyCanvas = newPaper.GetComponent<DrawingCanvas>();
            if (copyCanvas != null)
            {
                Destroy(copyCanvas);
            }

            // Asegurar componente ScannableDrawing y aplicarle el Aura Amarilla de copia
            ScannableDrawing scannable = newPaper.GetComponent<ScannableDrawing>();
            if (scannable == null)
            {
                scannable = newPaper.AddComponent<ScannableDrawing>();
            }

            ArtworkData copyData = new ArtworkData($"Copia - {artwork.title}", artwork.texture);
            scannable.SetArtwork(copyData);
            scannable.SetIsOriginal(false); // AURA AMARILLA PARA LA COPIA

            // Eliminar MeshCollider cóncavo si existe para compatibilidad
            MeshCollider mc = newPaper.GetComponent<MeshCollider>();
            if (mc != null)
            {
                Destroy(mc);
            }

            // Asegurar BoxCollider generoso para que el jugador pueda apuntar y tomarlo cómodamente
            BoxCollider bc = newPaper.GetComponent<BoxCollider>();
            if (bc == null)
            {
                bc = newPaper.AddComponent<BoxCollider>();
                bc.size = new Vector3(10f, 2f, 10f);
            }
            bc.enabled = true;

            // Rigidbody cinemático para reposar perfectamente en la bandeja de la impresora sin volar
            Rigidbody rb = newPaper.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = newPaper.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.useGravity = false;

            // Deslizar suavemente desde la ranura de la impresora a la bandeja exterior
            StartCoroutine(SlidePaperToTray(newPaper.transform, slotPos, trayFinalPos, 0.45f));

            PlayBeepSound(850, 0.12f);
            Debug.Log($"<color=green>[Impresora]</color> ¡Copia física de <b>{artwork.title}</b> lista en la bandeja de la impresora!");
        }

        private IEnumerator SlidePaperToTray(Transform paperTransform, Vector3 fromPos, Vector3 toPos, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (paperTransform == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                paperTransform.position = Vector3.Lerp(fromPos, toPos, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            if (paperTransform != null)
            {
                paperTransform.position = toPos;
            }
        }

        private void PlayBeepSound(int frequency, float duration)
        {
            if (audioSource == null) return;

            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1.0f - ((float)i / sampleCount);
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * envelope * 0.25f;
            }

            AudioClip clip = AudioClip.Create("Beep", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            audioSource.PlayOneShot(clip);
        }
    }
}
