using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ArtSimulator
{
    /// <summary>
    /// Efecto visual emergente del símbolo de dinero ('money up') al completar un envío.
    /// Flota hacia arriba sobre la caja de envíos, muestra la ganancia obtenida y se desvanece suavemente.
    /// </summary>
    public class FloatingMoneyEffect : MonoBehaviour
    {
        public static void Spawn(Vector3 worldPos, int amount)
        {
            if (!Application.isPlaying) return; // NUNCA instanciar en Modo Edición para evitar que quede congelado en la escena

            GameObject effectObj = new GameObject("FloatingMoneyEffect");
            effectObj.transform.position = worldPos;
            var effect = effectObj.AddComponent<FloatingMoneyEffect>();
            effect.Initialize(amount);
        }

        private SpriteRenderer spriteRenderer;
        private TextMesh textMesh;
        private float duration = 1.6f;

        public void Initialize(int amount)
        {
            // 1. Sprite de la flecha de dinero verde hacia arriba (money_up.png)
            Sprite moneySprite = Resources.Load<Sprite>("money_up");
            if (moneySprite != null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = moneySprite;
                spriteRenderer.sortingOrder = 900;
                // Escala proporcional y discreta sobre la caja (0.055f ≈ 30cm)
                transform.localScale = Vector3.one * 0.055f;
            }

            // 2. Texto con el monto ganado ubicado debajo de la flecha
            GameObject textObj = new GameObject("AmountText");
            textObj.transform.SetParent(transform, false);
            textObj.transform.localPosition = new Vector3(0f, -2.8f, 0f);

            textMesh = textObj.AddComponent<TextMesh>();
            textMesh.text = $"+${amount:N0}";
            textMesh.fontSize = 44;
            textMesh.characterSize = 0.22f;
            textMesh.fontStyle = FontStyle.Bold;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = new Color(0.12f, 0.90f, 0.25f, 1f); // Verde brillante

            StartCoroutine(AnimateRoutine());
        }

        private void LateUpdate()
        {
            // Orientar siempre de frente a la cámara activa (jugador en VR o PC)
            Camera activeCam = Camera.main;
            if (activeCam != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - activeCam.transform.position);
            }
        }

        private IEnumerator AnimateRoutine()
        {
            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + Vector3.up * 0.55f;
            Vector3 baseScale = transform.localScale;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Movimiento ascendente suave
                transform.position = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, t));

                // Efecto pop de escala al inicio
                float scaleMul = t < 0.25f 
                    ? Mathf.Lerp(0.3f, 1.15f, t / 0.25f) 
                    : Mathf.Lerp(1.15f, 1.0f, (t - 0.25f) / 0.75f);
                transform.localScale = baseScale * scaleMul;

                // Desvanecimiento gradual hacia el final
                if (t > 0.60f)
                {
                    float alpha = Mathf.Lerp(1f, 0f, (t - 0.60f) / 0.40f);
                    if (spriteRenderer != null)
                    {
                        Color c = spriteRenderer.color;
                        c.a = alpha;
                        spriteRenderer.color = c;
                    }
                    if (textMesh != null)
                    {
                        Color tc = textMesh.color;
                        tc.a = alpha;
                        textMesh.color = tc;
                    }
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
