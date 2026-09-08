using UnityEngine;

namespace ArtSimulator
{
    public static class UIFontHelper
    {
        private static Font cachedFont;

        public static Font GetDefaultFont()
        {
            if (cachedFont != null) return cachedFont;

            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cachedFont == null)
            {
                cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            if (cachedFont == null)
            {
                cachedFont = Font.CreateDynamicFontFromOSFont("Arial", 14);
            }
            return cachedFont;
        }
    }
}
