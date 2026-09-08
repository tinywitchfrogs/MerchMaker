using System;
using UnityEngine;

namespace ArtSimulator
{
    [Serializable]
    public class ArtworkData
    {
        public string id;
        public string title;
        public Texture2D texture;

        [NonSerialized]
        private Sprite cachedSprite;

        public ArtworkData(string title, Texture2D texture)
        {
            this.id = Guid.NewGuid().ToString();
            this.title = string.IsNullOrEmpty(title) ? "Ilustraci\u00f3n " + UnityEngine.Random.Range(100, 999) : title;
            this.texture = texture;
        }

        public Sprite GetSprite()
        {
            if (cachedSprite == null && texture != null)
            {
                cachedSprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f)
                );
            }
            return cachedSprite;
        }
    }
}
