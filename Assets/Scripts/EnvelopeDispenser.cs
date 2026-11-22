using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ArtSimulator
{
    /// <summary>
    /// Dispensador físico de sobres en la Mesa de Envíos.
    /// Al hacer clic (PC) o agarrar/seleccionar (VR), dispensa un sobre nuevo
    /// listo para recibir copias y enviarse.
    /// </summary>
    public class EnvelopeDispenser : MonoBehaviour
    {
        private static EnvelopeDispenser instance;
        public static EnvelopeDispenser Instance
        {
            get
            {
                if (instance == null) instance = FindAnyObjectByType<EnvelopeDispenser>();
                return instance;
            }
            private set => instance = value;
        }

        [Header("Configuración del Dispensador")]
        [SerializeField] private GameObject envelopePrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private int maxActiveLooseEnvelopes = 6;

        private AudioSource audioSource;
        private float lastDispenseTime = -10f;
        private int totalDispensedCount = 0;

        private void Awake()
        {
            if (instance == null) instance = this;
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.5f;
            }

            if (envelopePrefab == null)
            {
#if UNITY_EDITOR
                envelopePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/modelos y texturas/Sobre02.blend");
#endif
            }

            if (spawnPoint == null)
            {
                Transform sp = transform.Find("SpawnPoint");
                if (sp != null) spawnPoint = sp;
                else
                {
                    GameObject spObj = new GameObject("SpawnPoint");
                    spObj.transform.SetParent(transform, false);
                    spObj.transform.position = new Vector3(3.30f, -0.065f, -2.55f);
                    spawnPoint = spObj.transform;
                }
            }

            SetupCollidersAndInteractable();
            SetupWorldStatusUI();
        }

        public void SetupCollidersAndInteractable()
        {
            // BoxCollider para clic o rayo de agarre
            var col = GetComponent<BoxCollider>();
            if (col == null) col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = false;
            col.center = new Vector3(0f, 0.05f, 0f);
            col.size = new Vector3(0.55f, 0.25f, 0.45f);

            // XRSimpleInteractable para interactuar en VR
            var simple = GetComponent<XRSimpleInteractable>();
            if (simple == null)
            {
                simple = gameObject.AddComponent<XRSimpleInteractable>();
            }
            simple.selectEntered.RemoveListener(OnVRSelect);
            simple.selectEntered.AddListener(OnVRSelect);
        }

        private void OnVRSelect(SelectEnterEventArgs args)
        {
            var newEnv = DispenseNewEnvelope();
            if (newEnv != null && args != null && args.manager != null && args.interactorObject != null)
            {
                // Solo acoplar directamente si es un agarre físico directo (XRDirectInteractor).
                // Si fue accionado por rayo a distancia (XRRayInteractor / NearFarInteractor), el sobre permanece
                // descansando en la mesa de envíos para que el jugador lo tome con calma cuando guste,
                // evitando cualquier efecto resorte o eyección física.
                if (args.interactorObject is UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor)
                {
                    var grab = newEnv.GetComponent<XRGrabInteractable>();
                    if (grab != null)
                    {
                        var interactor = args.interactorObject;
                        args.manager.SelectExit(interactor, args.interactableObject);
                        args.manager.SelectEnter(interactor, grab);
                        Debug.Log("<color=green>[Dispensador de Sobres]</color> Sobre colocado directamente en la mano VR del jugador.");
                    }
                }
            }
        }

        /// <summary>
        /// Dispensar un sobre al hacer clic desde PC (FirstPersonPlayerController).
        /// </summary>
        public void OnPlayerInteract()
        {
            DispenseNewEnvelope();
        }

        public EnvelopeController DispenseNewEnvelope()
        {
            float now = Time.realtimeSinceStartup;
            if (now - lastDispenseTime < 0.35f) return null;
            lastDispenseTime = now;

            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : (transform.position + Vector3.left * 0.30f + Vector3.up * 0.03f);
            Quaternion spawnRot = Quaternion.Euler(0f, 180f, 90f);

            // Comprobar sobres vacíos existentes en la mesa o fuera de ella
            var existing = FindObjectsByType<EnvelopeController>(FindObjectsInactive.Exclude);
            EnvelopeController recyclableEmpty = null;
            int unsealedCount = 0;
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null && !existing[i].IsSealed && existing[i].gameObject != this.gameObject)
                {
                    unsealedCount++;
                    if (existing[i].CurrentCopiesCount == 0 && recyclableEmpty == null)
                    {
                        recyclableEmpty = existing[i];
                    }
                }
            }

            // Si hay sobres vacíos sin usar que salieron disparados o quedaron lejos, reciclarlos al punto de spawn
            if (recyclableEmpty != null && (unsealedCount >= maxActiveLooseEnvelopes || Vector3.Distance(recyclableEmpty.transform.position, spawnPos) > 2.5f))
            {
                recyclableEmpty.transform.position = spawnPos;
                recyclableEmpty.transform.rotation = spawnRot;
                var rbRec = recyclableEmpty.GetComponent<Rigidbody>();
                if (rbRec != null)
                {
                    rbRec.isKinematic = true;
                }
                PlayDispenseSound();
                Debug.Log("<color=green>[Dispensador de Sobres]</color> ♻️ Sobre vacío recuperado y presentado en la mesa.");
                WorkshopTutorialManager.Instance?.NotifyEnvelopeDispensed(recyclableEmpty);
                return recyclableEmpty;
            }

            totalDispensedCount++;

            GameObject newEnvObj;
            if (envelopePrefab != null)
            {
                newEnvObj = Instantiate(envelopePrefab, spawnPos, spawnRot);
            }
            else
            {
                newEnvObj = new GameObject($"Sobre_{totalDispensedCount}");
                newEnvObj.transform.position = spawnPos;
                newEnvObj.transform.rotation = spawnRot;
            }

            newEnvObj.name = $"Sobre_{totalDispensedCount}";
            newEnvObj.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);

            // Garantizar material SobreCardboard con doble cara (_Cull = 0) para que nunca se vuelva transparente
            Material cardboardMat = null;
#if UNITY_EDITOR
            cardboardMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/modelos y texturas/SobreCardboard.mat");
#endif
            if (cardboardMat != null)
            {
                var rends = newEnvObj.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < rends.Length; r++)
                {
                    if (rends[r].name.Contains("Cube"))
                    {
                        rends[r].sharedMaterial = cardboardMat;
                    }
                }
            }

            var ctrl = newEnvObj.GetComponent<EnvelopeController>();
            if (ctrl == null) ctrl = newEnvObj.AddComponent<EnvelopeController>();
            ctrl.SetupPhysicsAndGrab();
            ctrl.SetupWorldStatusUI();
            ctrl.UpdateVisuals();

            // Ponerlo listo y firme
            var rb = newEnvObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }

            PlayDispenseSound();
            Debug.Log($"<color=green>[Dispensador de Sobres]</color> ✨ Sobre nuevo #{totalDispensedCount} dispensado en la mesa.");

            // Notificar al tutorial si está escuchando
            WorkshopTutorialManager.Instance?.NotifyEnvelopeDispensed(ctrl);

            return ctrl;
        }

        public void SetupWorldStatusUI()
        {
            Transform existing = transform.Find("DispenserCanvas");
            if (existing != null)
            {
                existing.localPosition = new Vector3(0.26f, 1.68f, 0f);
                existing.localRotation = Quaternion.Euler(0f, 90f, 90f);
                existing.localScale = new Vector3(-0.0022f, 0.0022f, 0.0022f);
                if (existing.GetComponent<GraphicRaycaster>() == null) existing.gameObject.AddComponent<GraphicRaycaster>();
                if (existing.GetComponent<TrackedDeviceGraphicRaycaster>() == null) existing.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
                return;
            }

            GameObject canvasObj = new GameObject("DispenserCanvas");
            canvasObj.transform.SetParent(transform, false);
            canvasObj.transform.localPosition = new Vector3(0.26f, 1.68f, 0f);
            canvasObj.transform.localRotation = Quaternion.Euler(0f, 90f, 90f);
            canvasObj.transform.localScale = new Vector3(-0.0022f, 0.0022f, 0.0022f);

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 5;
            canvasObj.AddComponent<GraphicRaycaster>();
            canvasObj.AddComponent<TrackedDeviceGraphicRaycaster>();

            var rt = canvasObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(320, 160);

            // Botón interactivo sobre el dispensador
            GameObject btnObj = new GameObject("DispenserButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(canvasObj.transform, false);
            var bRt = btnObj.GetComponent<RectTransform>();
            bRt.anchorMin = Vector2.zero;
            bRt.anchorMax = Vector2.one;
            bRt.offsetMin = Vector2.zero;
            bRt.offsetMax = Vector2.zero;

            var bgImg = btnObj.GetComponent<Image>();
            bgImg.color = new Color(0.18f, 0.22f, 0.32f, 0.90f);

            var btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = bgImg;
            btn.onClick.AddListener(OnPlayerInteract);

            var trig = btnObj.AddComponent<TabletButtonTrigger>();
            trig.SetAction("Dispensar Sobre", OnPlayerInteract);

            // Texto
            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            var txtRt = txtObj.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
            var txt = txtObj.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 24;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.text = "📬 PILA DE SOBRES\nToca o haz clic para\ntomar un sobre";
            txt.raycastTarget = false;
        }

        private void PlayDispenseSound()
        {
            if (audioSource == null) return;
            int sampleRate = 44100;
            float duration = 0.16f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float env = Mathf.Sin(Mathf.PI * t);
                samples[i] = (Random.value * 2f - 1f) * env * 0.22f;
            }

            AudioClip clip = AudioClip.Create("DispenseEnvelope", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            audioSource.PlayOneShot(clip);
        }
    }
}
