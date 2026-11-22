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
        public float qualityScore = 0.5f;
        public float coverageRatio = 0.0f;
        public float drawingTimeSeconds = 0.0f;

        [NonSerialized]
        private Sprite cachedSprite;

        public ArtworkData(string title, Texture2D texture, float qualityScore = 0.5f, float coverageRatio = 0f, float drawingTimeSeconds = 0f)
        {
            this.id = Guid.NewGuid().ToString();
            this.title = string.IsNullOrEmpty(title) ? "Ilustración " + UnityEngine.Random.Range(100, 999) : title;
            this.texture = texture;
            this.qualityScore = Mathf.Clamp(qualityScore, 0.1f, 1.0f);
            this.coverageRatio = Mathf.Clamp01(coverageRatio);
            this.drawingTimeSeconds = Mathf.Max(0f, drawingTimeSeconds);
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
