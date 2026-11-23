using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ArtSimulator
{
    /// <summary>
    /// Dispensador físico de sobres en la Mesa de Envíos.
    /// Mantiene siempre un sobre físico listo para agarrar en la parte superior de la pila.
    /// Al tomar el sobre superior (con VR Grip/Trigger o con clic/E en PC), el sobre se desprende
    /// a la mano del jugador y el dispensador genera de inmediato el siguiente sobre en la pila.
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

        private AudioSource audioSource;
        private float lastDispenseTime = -10f;
        private int totalDispensedCount = 0;
        private EnvelopeController currentTopEnvelope;
        private bool isRespawningNext = false;

        public EnvelopeController CurrentTopEnvelope => currentTopEnvelope;

        public Vector3 GetStackTopPosition()
        {
            if (spawnPoint != null) return spawnPoint.position;
            return new Vector3(3.65f, -0.045f, -2.85f);
        }

        public Quaternion GetStackTopRotation()
        {
            if (spawnPoint != null) return spawnPoint.rotation;
            return Quaternion.Euler(0f, 180f, 90f);
        }

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
                envelopePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SobrePrefab.prefab");
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
                    spObj.transform.position = new Vector3(3.65f, -0.045f, -2.85f);
                    spawnPoint = spObj.transform;
                }
            }

            // Eliminar canvas 2D residual en el dispensador si existiera (no se usan botones flotantes en la pila)
            Transform oldCanvas = transform.Find("DispenserCanvas");
            if (oldCanvas != null)
            {
                if (Application.isPlaying) Destroy(oldCanvas.gameObject);
                else DestroyImmediate(oldCanvas.gameObject);
            }

            // Eliminar XRSimpleInteractable del dispensador base para que NUNCA bloquee el agarre del sobre superior
            var simple = GetComponent<XRSimpleInteractable>();
            if (simple != null)
            {
                if (Application.isPlaying) Destroy(simple);
                else DestroyImmediate(simple);
            }

            SetupColliders();
        }

        private void Start()
        {
            // Solo al iniciar el juego: si ya existe un sobre en la escena descansando sobre la pila, adoptarlo como primer sobre
            if (currentTopEnvelope == null)
            {
                var existing = FindObjectsByType<EnvelopeController>(FindObjectsInactive.Exclude);
                for (int i = 0; i < existing.Length; i++)
                {
                    if (existing[i] != null && Vector3.Distance(existing[i].transform.position, GetStackTopPosition()) <= 0.15f)
                    {
                        MountEnvelopeOnStack(existing[i]);
                        break;
                    }
                }
            }

            EnsureTopEnvelopeReady();
        }

        private void Update()
        {
            // Detectar si el sobre superior fue retirado de la pila por agarre (VR o PC) o por desplazamiento
            if (currentTopEnvelope != null)
            {
                bool isTaken = false;
                var grab = currentTopEnvelope.GetComponent<XRGrabInteractable>();
                if (grab != null && grab.isSelected) isTaken = true;
                if (Vector3.Distance(currentTopEnvelope.transform.position, GetStackTopPosition()) > 0.18f) isTaken = true;

                if (isTaken)
                {
                    var taken = currentTopEnvelope;
                    currentTopEnvelope = null;

                    PlayDispenseSound();
                    WorkshopTutorialManager.Instance?.NotifyEnvelopeDispensed(taken);
                    Debug.Log("<color=green>[Dispensador de Sobres]</color> 📬 Sobre retirado de la pila. Preparando siguiente sobre...");
                    StartCoroutine(RespawnNextOnStackDelayed(0.35f));
                }
            }
            else
            {
                if (!isRespawningNext)
                {
                    StartCoroutine(RespawnNextOnStackDelayed(0.35f));
                }
            }
        }

        private IEnumerator RespawnNextOnStackDelayed(float delay)
        {
            if (isRespawningNext) yield break;
            isRespawningNext = true;
            yield return new WaitForSeconds(delay);
            EnsureTopEnvelopeReady();
            isRespawningNext = false;
        }

        public void EnsureTopEnvelopeReady()
        {
            // 1. Si ya hay un sobre en la cima de la pila listo para tomar y no está agarrado, mantenerlo
            if (currentTopEnvelope != null && currentTopEnvelope.gameObject.activeInHierarchy)
            {
                var grab = currentTopEnvelope.GetComponent<XRGrabInteractable>();
                bool isGrabbed = grab != null && grab.isSelected;
                if (!isGrabbed && Vector3.Distance(currentTopEnvelope.transform.position, GetStackTopPosition()) <= 0.15f)
                {
                    return;
                }
            }

            // 2. Si el sobre anterior ya fue tomado por el jugador o desplazado de la pila:
            // Desacoplarlo definitivamente de la pila (jamás teletransportar sobres que el jugador ya sacó).
            currentTopEnvelope = null;

            // 3. Crear un sobre nuevo independiente en la cima de la pila listo para ser retirado
            SpawnEnvelopeOnStack();
        }

        public void MountEnvelopeOnStack(EnvelopeController env)
        {
            if (env == null) return;
            currentTopEnvelope = env;
            env.transform.position = GetStackTopPosition();
            env.transform.rotation = GetStackTopRotation();
            env.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);

            var rb = env.GetComponent<Rigidbody>();
            if (rb != null)
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
            }

            // Ignorar colisiones entre la base del dispensador y el sobre
            var baseCol = GetComponent<BoxCollider>();
            var envCol = env.GetComponent<BoxCollider>();
            if (baseCol != null && envCol != null)
            {
                Physics.IgnoreCollision(baseCol, envCol, true);
            }

            var grab = env.GetComponent<XRGrabInteractable>();
            if (grab != null)
            {
                grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
                grab.throwOnDetach = false;
                grab.selectEntered.RemoveListener(OnTopEnvelopeGrabbed);
                grab.selectEntered.AddListener(OnTopEnvelopeGrabbed);
            }

            env.UpdateVisuals();
        }

        private void OnTopEnvelopeGrabbed(SelectEnterEventArgs args)
        {
            if (currentTopEnvelope != null && args != null && args.interactableObject != null &&
                args.interactableObject.transform == currentTopEnvelope.transform)
            {
                var taken = currentTopEnvelope;
                currentTopEnvelope = null;

                PlayDispenseSound();
                WorkshopTutorialManager.Instance?.NotifyEnvelopeDispensed(taken);
                Debug.Log("<color=green>[Dispensador de Sobres]</color> 📬 Sobre tomado directamente de la pila en VR.");
                StartCoroutine(RespawnNextOnStackDelayed(0.35f));
            }
        }

        public void SetupColliders()
        {
            // El BoxCollider del dispensador base debe ser pequeño y estar abajo en la mesa
            // para NUNCA interferir ni bloquear el sobre que descansa encima de la pila.
            var col = GetComponent<BoxCollider>();
            if (col == null) col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = false;
            col.center = new Vector3(0f, 0.5f, 0f);
            col.size = new Vector3(1.20f, 0.80f, 1.20f);

            Transform cubeT = transform.Find("Cube");
            if (cubeT != null)
            {
                var cCol = cubeT.GetComponent<BoxCollider>();
                if (cCol != null)
                {
                    if (Application.isPlaying) Destroy(cCol);
                    else DestroyImmediate(cCol);
                }
            }
        }

        /// <summary>
        /// Compatibilidad: dispensar un sobre al hacer clic desde PC (FirstPersonPlayerController).
        /// </summary>
        public void OnPlayerInteract()
        {
            DispenseNewEnvelope();
        }

        public EnvelopeController DispenseNewEnvelope()
        {
            float now = Time.realtimeSinceStartup;
            if (now - lastDispenseTime < 0.35f && currentTopEnvelope != null) return currentTopEnvelope;
            lastDispenseTime = now;

            EnsureTopEnvelopeReady();
            if (currentTopEnvelope != null)
            {
                var env = currentTopEnvelope;
                currentTopEnvelope = null;

                PlayDispenseSound();
                WorkshopTutorialManager.Instance?.NotifyEnvelopeDispensed(env);
                StartCoroutine(RespawnNextOnStackDelayed(0.35f));
                return env;
            }

            return SpawnEnvelopeOnStack();
        }

        private EnvelopeController SpawnEnvelopeOnStack()
        {
            totalDispensedCount++;
            Vector3 spawnPos = GetStackTopPosition();
            Quaternion spawnRot = GetStackTopRotation();

            GameObject newEnvObj = null;
            if (envelopePrefab != null)
            {
                newEnvObj = Instantiate(envelopePrefab, spawnPos, spawnRot);
            }
            else
            {
#if UNITY_EDITOR
                var loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SobrePrefab.prefab");
                if (loaded != null)
                {
                    envelopePrefab = loaded;
                    newEnvObj = Instantiate(loaded, spawnPos, spawnRot);
                }
#endif
                if (newEnvObj == null)
                {
                    var template = GameObject.Find("Sobre");
                    if (template != null)
                    {
                        newEnvObj = Instantiate(template, spawnPos, spawnRot);
                    }
                }
            }

            if (newEnvObj == null) return null;

            newEnvObj.name = $"Sobre_{totalDispensedCount}";
            newEnvObj.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);

            var ctrl = newEnvObj.GetComponent<EnvelopeController>();
            if (ctrl == null) ctrl = newEnvObj.AddComponent<EnvelopeController>();
            ctrl.SetupPhysicsAndGrab();
            ctrl.SetupWorldStatusUI();
            ctrl.UpdateVisuals();

            MountEnvelopeOnStack(ctrl);
            return ctrl;
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
