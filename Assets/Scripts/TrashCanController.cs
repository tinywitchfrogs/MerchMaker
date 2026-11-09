using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ArtSimulator
{
    /// <summary>
    /// Controlador para el Basurero del taller.
    /// Permite al jugador desechar y eliminar dibujos no deseados, copias fallidas o papeles,
    /// a diferencia de la caja de envíos que se usa para vender productos.
    /// </summary>
    public class TrashCanController : MonoBehaviour
    {
        [Header("Configuración del Basurero")]
        [Tooltip("Efecto de sonido de descarte")]
        [SerializeField] private string trashSoundName = "Button Pop";

        private BoxCollider triggerCollider;

        private void Awake()
        {
            SetupTrashTrigger();
        }

        public void SetupTrashTrigger()
        {
            // Asegurar que el basurero tenga un BoxCollider trigger en su boca/interior
            triggerCollider = GetComponent<BoxCollider>();
            if (triggerCollider == null)
            {
                triggerCollider = gameObject.AddComponent<BoxCollider>();
            }
            triggerCollider.isTrigger = true;
            // Cobertura ergonómica de la papelera
            triggerCollider.size = new Vector3(0.55f, 0.70f, 0.55f);
            triggerCollider.center = new Vector3(0f, 0.45f, 0f);

            var rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        private void OnTriggerStay(Collider other)
        {
            TryTrashObject(other);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryTrashObject(other);
        }

        private void TryTrashObject(Collider other)
        {
            if (other == null) return;
            GameObject target = other.gameObject;

            // Encontrar si es parte de un dibujo o producto
            ScannableDrawing drawing = target.GetComponent<ScannableDrawing>() 
                ?? target.GetComponentInParent<ScannableDrawing>() 
                ?? target.GetComponentInChildren<ScannableDrawing>();

            if (drawing != null) target = drawing.gameObject;

            // NUNCA eliminar tableta, jugador, mandos ni lápices
            if (target.GetComponentInParent<TabletManager>() != null || target.GetComponentInChildren<TabletManager>() != null) return;
            if (target.GetComponentInParent<VRPenTip>() != null || target.GetComponentInChildren<VRPenTip>() != null) return;
            if (target.CompareTag("Player") || target.name.Contains("Origin") || target.name.Contains("XR") || target.name.Contains("Controller")) return;

            // Verificar si el jugador aún lo está sosteniendo en la mano (VR o Desktop)
            var grab = target.GetComponent<XRGrabInteractable>();
            if (grab != null && grab.isSelected) return;

            if (FirstPersonPlayerController.Instance != null && FirstPersonPlayerController.Instance.IsHoldingObject(target.transform)) return;

            // Verificar si es un dibujo, copia, o papel desechable
            bool isDrawingOrPaper = drawing != null 
                || target.CompareTag("Dibujo") 
                || target.name.StartsWith("Dibujo", StringComparison.OrdinalIgnoreCase)
                || target.name.Contains("Copia") 
                || target.name.Contains("Paper");

            if (isDrawingOrPaper)
            {
                // Si el jugador de escritorio lo tenía en sus manos, liberar referencia
                if (FirstPersonPlayerController.Instance != null)
                {
                    FirstPersonPlayerController.Instance.ReleaseDeliveredObject(target.transform);
                }

                // Desactivar colliders inmediatamente para frenar eventos físicos remanentes
                var colliders = target.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;

                string objName = target.name;
                Destroy(target);

                // Feedback sonoro de reciclaje / desecho
                SoundEffectManager.Play(trashSoundName);
                Debug.Log($"<color=orange>[Basurero]</color> 🗑️ Objeto desechado y eliminado: <b>{objName}</b>");
            }
        }
    }
}

