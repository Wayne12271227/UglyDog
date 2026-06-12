using UnityEngine;

public static class UglyDogUIFont
{
    private const string NotoSansTcResourcePath = "Fonts/NotoSansTC-Regular";
    private const string BuiltInFontName = "LegacyRuntime.ttf";

    private static Font cachedFont;

    public static Font Load()
    {
        if (cachedFont != null)
        {
            return cachedFont;
        }

        cachedFont = Resources.Load<Font>(NotoSansTcResourcePath);
        if (cachedFont != null)
        {
            return cachedFont;
        }

        cachedFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Noto Sans TC", "Microsoft JhengHei", "Microsoft YaHei", "Arial Unicode MS", "Noto Sans CJK TC" },
            16);

        return cachedFont != null
            ? cachedFont
            : Resources.GetBuiltinResource<Font>(BuiltInFontName);
    }
}
