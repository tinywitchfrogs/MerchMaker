using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ArtSimulator
{
    /// <summary>
    /// Controlador del Atril en la mesa de dibujo.
    /// Permite encajar hojas de papel (originales, en blanco o copias impresas)
    /// de manera firme y orientada hacia el jugador para pintar o fotografiar con la tableta.
    /// </summary>
    public class AtrilController : MonoBehaviour
    {
        private static AtrilController instance;
        public static AtrilController Instance
        {
            get
            {
                if (instance == null) instance = FindAnyObjectByType<AtrilController>();
                return instance;
            }
            private set => instance = value;
        }

        [Header("Punto de Montaje del Papel")]
        [SerializeField] private Transform paperSnapPoint;

        [Header("Dibujo Actualmente Montado")]
        [SerializeField] private ScannableDrawing currentMountedDrawing;
        public ScannableDrawing CurrentMountedDrawing => currentMountedDrawing;
        public bool HasDrawingMounted => currentMountedDrawing != null;

        [Header("Configuración de Detección")]
        [SerializeField] private float snapDistanceHeld = 0.28f;
        [SerializeField] private float snapDistanceReleased = 0.42f;

        private AudioSource audioSource;
        private float lastDetachTime = -10f;
        private ScannableDrawing lastDetachedDrawing = null;

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

            SetupSnapPoint();
            SetupColliders();
        }

        public void SetupSnapPoint()
        {
            if (paperSnapPoint == null)
            {
                Transform existing = transform.Find("PaperSnapPoint");
                if (existing != null)
                {
                    paperSnapPoint = existing;
                }
                else
                {
                    GameObject snapObj = new GameObject("PaperSnapPoint");
                    snapObj.transform.SetParent(transform, false);
                    paperSnapPoint = snapObj.transform;
                }
            }

            // Posición y rotación de reposo exactas en el frente del atril
            // Ajustado hacia el frente (Z = -0.720) y ángulo 81° para que el papel descanse
            // de forma natural en la repisa inferior sin que el enganche superior atraviese la hoja.
            paperSnapPoint.position = new Vector3(5.60f, 0.285f, -0.720f);
            paperSnapPoint.rotation = Quaternion.Euler(81.0f, 180f, 0f);
        }

        public void SetupColliders()
        {
            // Remover cualquier BoxCollider sólido en el atril para evitar que bloquee o colisione con el jugador
            BoxCollider solidCol = GetComponent<BoxCollider>();
            if (solidCol != null)
            {
                Destroy(solidCol);
            }

            // Trigger frontal para capturar hojas al acercarlas al punto de anclaje
            BoxCollider triggerCol = transform.Find("SnapTrigger")?.GetComponent<BoxCollider>();
            if (triggerCol == null)
            {
                GameObject trigObj = new GameObject("SnapTrigger");
                trigObj.transform.SetParent(transform, false);
                trigObj.transform.position = paperSnapPoint != null ? paperSnapPoint.position : transform.position + Vector3.up * 0.28f;
                triggerCol = trigObj.AddComponent<BoxCollider>();
            }
            else if (paperSnapPoint != null)
            {
                triggerCol.transform.position = paperSnapPoint.position;
            }
            triggerCol.isTrigger = true;
            triggerCol.size = new Vector3(0.60f, 0.60f, 0.40f);
        }

        private void Update()
        {
            if (currentMountedDrawing == null)
            {
                CheckForNearbyDrawingToMount();
            }
        }

        private void CheckForNearbyDrawingToMount()
        {
            if (paperSnapPoint == null) SetupSnapPoint();
            Vector3 targetPos = paperSnapPoint.position;

            var allDrawings = FindObjectsByType<ScannableDrawing>(FindObjectsInactive.Exclude);
            for (int i = 0; i < allDrawings.Length; i++)
            {
                ScannableDrawing drawing = allDrawings[i];
                if (drawing == null) continue;

                // Evitar re-enganchar inmediatamente la hoja que el jugador acaba de retirar
                if (drawing == lastDetachedDrawing && Time.time - lastDetachTime < 2.0f) continue;

                var grab = drawing.GetComponent<XRGrabInteractable>();
                bool isBeingHeld = grab != null && grab.isSelected;

                float dist = Vector3.Distance(drawing.transform.position, targetPos);
                bool shouldMount = isBeingHeld ? (dist < snapDistanceHeld) : (dist < snapDistanceReleased);

                if (shouldMount)
                {
                    // Si el jugador lo tiene en su mano en VR, soltarlo
                    if (grab != null && grab.isSelected && grab.interactionManager != null)
                    {
                        var interactor = grab.firstInteractorSelecting;
                        if (interactor != null)
                        {
                            grab.interactionManager.SelectExit(interactor, grab);
                        }
                    }

                    // Si el jugador lo tiene en la mano de PC, soltarlo
                    if (FirstPersonPlayerController.Instance != null && FirstPersonPlayerController.Instance.IsHoldingObject(drawing.transform))
                    {
                        FirstPersonPlayerController.Instance.ReleaseDeliveredObject(drawing.transform);
                    }

                    MountDrawing(drawing);
                    break;
                }
            }
        }

        public bool MountDrawing(ScannableDrawing drawing)
        {
            if (drawing == null || currentMountedDrawing != null) return false;

            currentMountedDrawing = drawing;
            if (paperSnapPoint == null) SetupSnapPoint();

            // Bloquear firmemente en el punto de apoyo del atril
            drawing.transform.position = paperSnapPoint.position;
            drawing.transform.rotation = paperSnapPoint.rotation;

            Rigidbody rb = drawing.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            BoxCollider bc = drawing.GetComponent<BoxCollider>();
            if (bc != null)
            {
                bc.enabled = true;
            }

            // Escuchar evento de agarre para desenganchar cuando el jugador lo retire
            var grab = drawing.GetComponent<XRGrabInteractable>();
            if (grab != null)
            {
                grab.selectEntered.RemoveListener(OnDrawingGrabbed);
                grab.selectEntered.AddListener(OnDrawingGrabbed);
            }

            if (XRPlayerRigManager.Instance != null)
            {
                XRPlayerRigManager.Instance.ConfigureDrawingForVR(drawing.gameObject);
            }

            PlaySnapSound();
            Debug.Log($"<color=green>[Atril]</color> Hoja <b>'{drawing.GetArtworkData().title}'</b> montada firmemente en el atril para dibujar o fotografiar.");
            return true;
        }

        private void OnDrawingGrabbed(SelectEnterEventArgs args)
        {
            if (currentMountedDrawing != null && args.interactableObject.transform == currentMountedDrawing.transform)
            {
                DetachDrawing();
            }
        }

        public ScannableDrawing DetachDrawing()
        {
            if (currentMountedDrawing == null) return null;

            ScannableDrawing drawing = currentMountedDrawing;
            currentMountedDrawing = null;
            lastDetachedDrawing = drawing;
            lastDetachTime = Time.time;

            var grab = drawing.GetComponent<XRGrabInteractable>();
            if (grab != null)
            {
                grab.selectEntered.RemoveListener(OnDrawingGrabbed);
            }

            Rigidbody rb = drawing.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }

            Debug.Log($"<color=cyan>[Atril]</color> Hoja <b>'{drawing.GetArtworkData().title}'</b> retirada del atril.");
            return drawing;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (currentMountedDrawing != null) return;
            var drawing = other.GetComponent<ScannableDrawing>() ?? other.GetComponentInParent<ScannableDrawing>();
            if (drawing != null)
            {
                MountDrawing(drawing);
            }
        }

        private void PlaySnapSound()
        {
            if (audioSource == null) return;
            int sampleRate = 44100;
            float duration = 0.08f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1.0f - ((float)i / sampleCount);
                samples[i] = Mathf.Sin(2 * Mathf.PI * 520 * t) * env * 0.2f;
            }

            AudioClip clip = AudioClip.Create("SnapSound", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            audioSource.PlayOneShot(clip);
        }
    }
}
