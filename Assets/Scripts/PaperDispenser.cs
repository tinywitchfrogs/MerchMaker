using System.Collections.Generic;
using UnityEngine;

namespace ArtSimulator
{
    public class PaperDispenser : MonoBehaviour
    {
        private static PaperDispenser instance;
        public static PaperDispenser Instance
        {
            get
            {
                if (instance == null) instance = FindAnyObjectByType<PaperDispenser>();
                return instance;
            }
            private set => instance = value;
        }

        [Header("Configuración del Dispensador")]
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private GameObject paperTemplate;
        [SerializeField] private int spawnedPaperCount = 3; // Dibujo_1, 2, 3 ya existen al inicio

        private AudioSource audioSource;
        private float lastDispenseTime = -1f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.5f;
            }

            if (spawnPoint == null)
            {
                Transform sp = transform.Find("PaperSpawnPoint");
                if (sp != null) spawnPoint = sp;
                else
                {
                    GameObject spObj = new GameObject("PaperSpawnPoint");
                    spObj.transform.SetParent(transform);
                    spObj.transform.localPosition = new Vector3(0, 0.08f, 0);
                    spObj.transform.localRotation = Quaternion.identity;
                    spawnPoint = spObj.transform;
                }
            }

            CountExistingDrawings();
        }

        private void CountExistingDrawings()
        {
            var drawings = FindObjectsByType<ScannableDrawing>(FindObjectsInactive.Include);
            int maxId = 3;
            for (int i = 0; i < drawings.Length; i++)
            {
                if (drawings[i] != null && drawings[i].IsOriginal && drawings[i].name.StartsWith("Dibujo_"))
                {
                    string numPart = drawings[i].name.Substring("Dibujo_".Length);
                    if (int.TryParse(numPart, out int num))
                    {
                        if (num > maxId) maxId = num;
                    }
                }
            }
            spawnedPaperCount = maxId;
        }

        public ScannableDrawing DispenseNewPaper()
        {
            // Debounce para no generar múltiples hojas por un solo clic
            if (Time.unscaledTime - lastDispenseTime < 0.4f) return null;
            lastDispenseTime = Time.unscaledTime;

            spawnedPaperCount++;
            string paperName = $"Dibujo_{spawnedPaperCount}";
            string paperTitle = $"Dibujo {spawnedPaperCount}";

            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : (transform.position + Vector3.up * 0.08f);
            Quaternion spawnRot = transform.rotation;

            GameObject template = paperTemplate;
            if (template == null)
            {
                template = GameObject.Find("Dibujo_1") ?? GameObject.Find("Dibujo");
            }

            GameObject newPaper;
            if (template != null)
            {
                newPaper = Instantiate(template, spawnPos, spawnRot);
            }
            else
            {
                newPaper = GameObject.CreatePrimitive(PrimitiveType.Plane);
                newPaper.transform.position = spawnPos;
                newPaper.transform.rotation = spawnRot;
                newPaper.transform.localScale = new Vector3(0.04f, 0.04f, 0.03f);
            }

            newPaper.name = paperName;
            newPaper.SetActive(true);

            // Eliminar aura duplicada heredada para que se regenere limpiamente
            Transform oldAura = newPaper.transform.Find("AuraEffect");
            if (oldAura != null) DestroyImmediate(oldAura.gameObject);

            // Configurar ScannableDrawing
            ScannableDrawing scannable = newPaper.GetComponent<ScannableDrawing>();
            if (scannable == null) scannable = newPaper.AddComponent<ScannableDrawing>();
            scannable.SetIsOriginal(true);

            // Configurar DrawingCanvas y reiniciar textura blanca limpia
            DrawingCanvas canvas = newPaper.GetComponent<DrawingCanvas>();
            if (canvas == null) canvas = newPaper.AddComponent<DrawingCanvas>();
            canvas.ResetToFreshCanvas(paperTitle);

            // Configurar BoxCollider
            BoxCollider bc = newPaper.GetComponent<BoxCollider>();
            if (bc == null) bc = newPaper.AddComponent<BoxCollider>();
            bc.size = new Vector3(10f, 0.5f, 10f);
            bc.center = Vector3.zero;
            bc.enabled = true;

            // Configurar Rigidbody
            Rigidbody rb = newPaper.GetComponent<Rigidbody>();
            if (rb == null) rb = newPaper.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;

            // Reproducir sonido de hoja de papel
            PlayPaperSound();

            Debug.Log($"<color=cyan>[PaperDispenser]</color> ¡Nueva hoja original blanca dispensada con éxito: <b>{paperName}</b>!");
            return scannable;
        }

        private void PlayPaperSound()
        {
            if (audioSource == null) return;
            int sampleRate = 44100;
            float duration = 0.12f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 25f);
                float noise = (Random.value * 2f - 1f) * 0.25f;
                float tone = Mathf.Sin(2 * Mathf.PI * 400 * t) * 0.15f;
                samples[i] = (noise + tone) * envelope;
            }
            AudioClip clip = AudioClip.Create("PaperRustle", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            audioSource.PlayOneShot(clip, 0.5f);
        }
    }
}
