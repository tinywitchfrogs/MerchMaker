using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArtSimulator
{
    /// <summary>
    /// Componente que garantiza la ejecución del clic de ratón directamente vía IPointerClickHandler.
    /// No depende de la serialización de Button.onClick y sobrevive recargas de dominio y cambios de escena.
    /// </summary>
    public class TabletButtonTrigger : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private string actionName = "ButtonAction";
        
        private Action onClickCallback;
        private Vector3 originalScale;
        private Image targetImage;
        private Color originalColor;
        private bool isHovered = false;

        private void Awake()
        {
            originalScale = transform.localScale;
            targetImage = GetComponent<Image>();
            if (targetImage != null)
            {
                originalColor = targetImage.color;
            }
        }

        private float lastTriggerTime = -1f;

        public void SetAction(string name, Action callback)
        {
            actionName = name;
            onClickCallback = callback;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                TriggerClick();
            }
        }

        public void TriggerClick()
        {
            // Evitar doble activación accidental en el mismo clic o frame
            if (Time.unscaledTime - lastTriggerTime < 0.25f) return;
            lastTriggerTime = Time.unscaledTime;

            Debug.Log($"<color=yellow>[TabletButtonTrigger]</color> Botón presionado: <b>{actionName}</b> ({gameObject.name})");
            onClickCallback?.Invoke();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            transform.localScale = originalScale * 1.05f;
            if (targetImage != null)
            {
                targetImage.color = Color.Lerp(originalColor, Color.white, 0.2f);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            transform.localScale = originalScale;
            if (targetImage != null)
            {
                targetImage.color = originalColor;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            transform.localScale = originalScale * 0.95f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            transform.localScale = isHovered ? originalScale * 1.05f : originalScale;
        }

        private void OnDisable()
        {
            transform.localScale = originalScale;
            if (targetImage != null)
            {
                targetImage.color = originalColor;
            }
        }
    }
}
