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
            if (tipTransform == null) tipTransform = transform;
            if (tipRenderer == null) tipRenderer = GetComponent<Renderer>();
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
                Vector3 tipPos = tipTransform != null ? tipTransform.position : transform.position;
                Vector3 dir = transform.up; // Eje longitudinal de la punta del lápiz

                // 1. Raycast corto hacia adelante de la punta para detectar contacto directo con la hoja
                bool hitSurface = false;
                if (Physics.Raycast(tipPos - dir * 0.015f, dir, out RaycastHit hit, 0.05f))
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
