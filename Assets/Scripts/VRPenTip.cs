using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ArtSimulator
{
    /// <summary>
    /// Componente para la punta del lápiz de Realidad Virtual.
    /// Dibuja ÚNICAMENTE cuando la punta física del lápiz está tocando el papel.
    /// Si el lápiz se despega de la superficie o no se sostiene en la mano, cesa de dibujar inmediatamente.
    /// </summary>
    public class VRPenTip : MonoBehaviour
    {
        [Header("Configuración del Pincel VR")]
        [SerializeField] private Color penColor = Color.black;
        [SerializeField] private int brushSize = 8;
        [SerializeField] private Transform tipTransform;
        [SerializeField] private Renderer tipRenderer;

        private XRGrabInteractable grabInteractable;
        private DrawingCanvas currentCanvas;
        private bool isTouchingCanvas = false;

        private void Awake()
        {
            grabInteractable = GetComponentInParent<XRGrabInteractable>();
            if (tipTransform == null)
            {
                if (transform.childCount > 0)
                {
                    Transform cone = transform.Find("Cone") ?? transform.Find("Tip") ?? transform.GetChild(0);
                    if (cone != null && cone.name != "AttachPoint") tipTransform = cone;
                }
            }
            if (tipTransform == null) tipTransform = transform;
            if (tipRenderer == null && tipTransform != null) tipRenderer = tipTransform.GetComponent<Renderer>();
            if (tipRenderer == null) tipRenderer = GetComponent<Renderer>();
            UpdateTipVisualColor();
        }

        public void SetTipTransform(Transform t)
        {
            tipTransform = t;
            if (t != null)
            {
                var r = t.GetComponent<Renderer>();
                if (r != null) tipRenderer = r;
            }
            UpdateTipVisualColor();
        }

        public void SetColor(Color c)
        {
            penColor = c;
            UpdateTipVisualColor();
        }

        public void SetBrushSize(int size)
        {
            brushSize = Mathf.Clamp(size, 2, 32);
        }

        private void UpdateTipVisualColor()
        {
            if (tipRenderer != null)
            {
                Material m = Application.isPlaying ? tipRenderer.material : tipRenderer.sharedMaterial;
                if (m != null)
                {
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", penColor);
                    if (m.HasProperty("_Color")) m.SetColor("_Color", penColor);
                }
            }
        }

        private void Update()
        {
            // 1. REGLA FUNDAMENTAL: Solo puede dibujar si el lápiz está sostenido directamente en la mano del jugador
            if (grabInteractable == null || !grabInteractable.isSelected)
            {
                StopDrawing();
                return;
            }

            // Evitar que el lápiz dibuje si está a distancia (por ejemplo si fue seleccionado mediante raycast lejano)
            if (grabInteractable.interactorsSelecting != null && grabInteractable.interactorsSelecting.Count > 0)
            {
                var interactor = grabInteractable.interactorsSelecting[0];
                if (interactor != null && Vector3.Distance(transform.position, interactor.transform.position) > 0.40f)
                {
                    StopDrawing();
                    return;
                }
            }

            // Posición exacta en la punta física del lápiz
            Vector3 tipPos = tipTransform != null ? tipTransform.position : transform.position;
            Vector3 dir = (tipTransform != null && tipTransform != transform)
                ? (tipTransform.position - transform.position).normalized
                : transform.forward;
            if (dir.sqrMagnitude < 0.001f) dir = transform.forward;

            // 2. DETECCIÓN FÍSICA ESTRICTA: El lápiz SOLO dibuja si la punta está en contacto real con el papel
            DrawingCanvas contactedCanvas = null;
            Vector3 contactPoint = tipPos;

            // Comprobación A: Raycast ultra corto (máximo 8 mm) desde la punta para detectar contacto superficial
            if (Physics.Raycast(tipPos - dir * 0.004f, dir, out RaycastHit hit, 0.012f, ~0, QueryTriggerInteraction.Ignore))
            {
                var canvas = hit.collider.GetComponent<DrawingCanvas>() ?? hit.collider.GetComponentInParent<DrawingCanvas>();
                if (canvas != null)
                {
                    contactedCanvas = canvas;
                    contactPoint = hit.point;
                }
            }

            // Comprobación B: Esfera diminuta de 8 mm en el ápice de la punta si la punta ya está tocando físicamente
            if (contactedCanvas == null)
            {
                Collider[] hits = Physics.OverlapSphere(tipPos, 0.008f, ~0, QueryTriggerInteraction.Collide);
                for (int i = 0; i < hits.Length; i++)
                {
                    var canvas = hits[i].GetComponent<DrawingCanvas>() ?? hits[i].GetComponentInParent<DrawingCanvas>();
                    if (canvas != null)
                    {
                        contactedCanvas = canvas;
                        contactPoint = tipPos;
                        break;
                    }
                }
            }

            // 3. APLICAR DIBUJO O DETENERLO EN TIEMPO REAL
            if (contactedCanvas != null)
            {
                if (currentCanvas != null && currentCanvas != contactedCanvas)
                {
                    currentCanvas.StopStroke();
                }

                currentCanvas = contactedCanvas;
                isTouchingCanvas = true;
                currentCanvas.SetBrushColor(penColor);
                currentCanvas.SetBrushSize(brushSize);
                currentCanvas.DrawAtWorldPoint(contactPoint);
            }
            else
            {
                // Si la punta se separó del papel aunque sea 1 milímetro, suspender el trazo inmediatamente
                StopDrawing();
            }
        }

        private void StopDrawing()
        {
            if (currentCanvas != null)
            {
                currentCanvas.StopStroke();
                currentCanvas = null;
            }
            isTouchingCanvas = false;
        }

        private void OnDisable()
        {
            StopDrawing();
        }
    }
}
