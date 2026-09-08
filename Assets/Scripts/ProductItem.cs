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

        [NonSerialized]
        private Sprite cachedSprite;

        public ProductItem(string title, Texture2D photoTexture, int price)
        {
            this.id = Guid.NewGuid().ToString();
            this.title = string.IsNullOrEmpty(title) ? "Producto " + UnityEngine.Random.Range(1, 100) : title;
            this.photoTexture = photoTexture;
            this.price = price;
            this.totalUnitsSold = 0;
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
