using System;
using UnityEngine;

namespace ArtSimulator
{
    [Serializable]
    public class ProductItem
    {
        public string id;
        public string title;
        public Texture2D photoTexture;
        public int price;
        public int totalUnitsSold;
        public float qualityScore = 0.5f;
        public int fairPrice = 3000;
        public float marketDemandMultiplier = 1.0f;

        [NonSerialized]
        private Sprite cachedSprite;

        public ProductItem(string title, Texture2D photoTexture, int price, float qualityScore = 0.5f)
        {
            this.id = Guid.NewGuid().ToString();
            this.title = string.IsNullOrEmpty(title) ? "Producto " + UnityEngine.Random.Range(1, 100) : title;
            this.photoTexture = photoTexture;
            this.price = Mathf.Max(100, price);
            this.totalUnitsSold = 0;
            this.qualityScore = Mathf.Clamp(qualityScore, 0.10f, 1.0f);

            // Precio justo de mercado calculado a partir de la calidad y esfuerzo invertido
            // Calidad mínima (0.10) => ~$1,500
            // Calidad media (0.50) => ~$3,500
            // Calidad máxima (1.00) => ~$6,000
            this.fairPrice = 1000 + Mathf.RoundToInt(this.qualityScore * 5000f);

            CalculateMarketDemand();
        }

        public void UpdatePrice(int newPrice)
        {
            this.price = Mathf.Max(100, newPrice);
            CalculateMarketDemand();
        }

        public void CalculateMarketDemand()
        {
            // Ratio de valor percibido por el comprador = Precio Justo / Precio Fijado
            // - Dibujo rápido y descuidado a precio caro: ratio < 0.5 => demanda muy baja
            // - Dibujo detallado y con esfuerzo a precio accesible: ratio > 2.0 => demanda altísima
            // - Dibujo detallado a precio alto justo: ratio ~ 1.0 => ventas estables de alto margen
            float ratio = (float)fairPrice / Mathf.Max(100, price);

            // Curva no lineal para respuesta del mercado
            if (ratio >= 2.5f)
            {
                marketDemandMultiplier = Mathf.Clamp(ratio * 1.3f, 2.0f, 4.0f);
            }
            else if (ratio >= 1.0f)
            {
                marketDemandMultiplier = Mathf.Clamp(ratio, 1.0f, 2.5f);
            }
            else
            {
                // Penalización por sobreprecio: cae rápidamente
                marketDemandMultiplier = Mathf.Clamp(Mathf.Pow(ratio, 1.5f), 0.15f, 0.95f);
            }
        }

        public string GetStarsString()
        {
            int stars = Mathf.Clamp(Mathf.RoundToInt(qualityScore * 5f), 1, 5);
            switch (stars)
            {
                case 1: return "★☆☆☆☆";
                case 2: return "★★☆☆☆";
                case 3: return "★★★☆☆";
                case 4: return "★★★★☆";
                default: return "★★★★★";
            }
        }

        public string GetDemandBadge()
        {
            if (marketDemandMultiplier >= 2.2f) return "Demanda: Muy Alta 🔥";
            if (marketDemandMultiplier >= 1.2f) return "Demanda: Alta ⭐";
            if (marketDemandMultiplier >= 0.8f) return "Demanda: Normal";
            return "Demanda: Baja ⚠️";
        }

        public Color GetDemandColor()
        {
            if (marketDemandMultiplier >= 2.2f) return new Color(0.85f, 0.20f, 0.10f); // Rojo fuego
            if (marketDemandMultiplier >= 1.2f) return new Color(0.12f, 0.60f, 0.25f); // Verde alto
            if (marketDemandMultiplier >= 0.8f) return new Color(0.20f, 0.45f, 0.75f); // Azul normal
            return new Color(0.65f, 0.40f, 0.10f); // Ámbar baja
        }

        public Sprite GetSprite()
        {
            if (cachedSprite == null && photoTexture != null)
            {
                cachedSprite = Sprite.Create(
                    photoTexture,
                    new Rect(0, 0, photoTexture.width, photoTexture.height),
                    new Vector2(0.5f, 0.5f)
                );
            }
            return cachedSprite;
        }
    }
}
