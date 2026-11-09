using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ArtSimulator
{
    /// <summary>
    /// Componente para la punta del lápiz/pincel de Realidad Virtual.
    /// Detecta contacto físico con cualquier DrawingCanvas y dibuja en 3D en tiempo real.
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
                // Si tiene un hijo (cono de la punta), usarlo automáticamente
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
            // Solo dibujar si el lápiz está siendo sostenido por el jugador
            if (grabInteractable != null && grabInteractable.isSelected)
            {
                // Posición exacta en la punta física del lápiz
                Vector3 tipPos = tipTransform != null ? tipTransform.position : transform.position;
                Vector3 dir;
                if (tipTransform != null && tipTransform != transform)
                {
                    // Eje longitudinal del lápiz hacia la punta
                    dir = (tipTransform.position - transform.position).normalized;
                }
                else
                {
                    dir = transform.up;
                }
                if (dir.sqrMagnitude < 0.001f) dir = transform.forward;

                // 1. Raycast corto y ultra-preciso directo desde la punta (sin desfase lateral)
                bool hitSurface = false;
                if (Physics.Raycast(tipPos - dir * 0.015f, dir, out RaycastHit hit, 0.045f))
                {
                    var canvas = hit.collider.GetComponent<DrawingCanvas>() ?? hit.collider.GetComponentInParent<DrawingCanvas>();
                    if (canvas != null)
                    {
                        hitSurface = true;
                        currentCanvas = canvas;
                        isTouchingCanvas = true;
                        currentCanvas.SetBrushColor(penColor);
                        currentCanvas.SetBrushSize(brushSize);
                        currentCanvas.DrawAtWorldPoint(hit.point);
                    }
                }
                else if (Physics.SphereCast(tipPos - dir * 0.015f, 0.015f, dir, out RaycastHit sHit, 0.05f))
                {
                    var canvas = sHit.collider.GetComponent<DrawingCanvas>() ?? sHit.collider.GetComponentInParent<DrawingCanvas>();
                    if (canvas != null)
                    {
                        hitSurface = true;
                        currentCanvas = canvas;
                        isTouchingCanvas = true;
                        currentCanvas.SetBrushColor(penColor);
                        currentCanvas.SetBrushSize(brushSize);
                        currentCanvas.DrawAtWorldPoint(sHit.point);
                    }
                }

                // 2. Fallback por esfera de contacto o triggers
                if (!hitSurface && isTouchingCanvas && currentCanvas != null)
                {
                    currentCanvas.SetBrushColor(penColor);
                    currentCanvas.SetBrushSize(brushSize);
                    currentCanvas.DrawAtWorldPoint(tipPos);
                }
            }
            else
            {
                if (currentCanvas != null)
                {
                    currentCanvas.StopStroke();
                    currentCanvas = null;
                }
                isTouchingCanvas = false;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var canvas = other.GetComponent<DrawingCanvas>() ?? other.GetComponentInParent<DrawingCanvas>();
            if (canvas != null)
            {
                currentCanvas = canvas;
                isTouchingCanvas = true;
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (currentCanvas == null)
            {
                currentCanvas = other.GetComponent<DrawingCanvas>() ?? other.GetComponentInParent<DrawingCanvas>();
            }
            if (currentCanvas != null)
            {
                isTouchingCanvas = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var canvas = other.GetComponent<DrawingCanvas>() ?? other.GetComponentInParent<DrawingCanvas>();
            if (canvas != null && canvas == currentCanvas)
            {
                if (currentCanvas != null)
                {
                    currentCanvas.StopStroke();
                }
                currentCanvas = null;
                isTouchingCanvas = false;
            }
        }
    }
}
