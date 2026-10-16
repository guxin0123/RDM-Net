namespace RdmNet.Utils;

/// <summary>
/// 用 Windows 自带的 Segoe MDL2 Assets 字体字形生成工具栏/菜单图标位图。
/// 矢量字形按 2 倍尺寸渲染，交给 ToolStrip 按 DPI 缩放，保证清晰。
/// </summary>
internal static class ToolbarIcons
{
    public const char Add = '\uE710';
    public const char Edit = '\uE70F';
    public const char Delete = '\uE74D';
    public const char Refresh = '\uE72C';
    public const char Download = '\uE896';
    public const char Upload = '\uE898';
    public const char Folder = '\uE8B7';
    public const char Server = '\uE968';
    public const char Monitor = '\uE7F4';

    private const string IconFontName = "Segoe MDL2 Assets";
    private const int DefaultBitmapSize = 32;
    private const float GlyphToBitmapRatio = 22f / 32f;

    private static readonly Dictionary<(char Glyph, int Size), Bitmap> Cache = new();
    private static readonly object SyncRoot = new();

    private static readonly bool FontAvailable =
        Array.Exists(FontFamily.Families, f => f.Name == IconFontName);

    /// <summary>获取指定字形的 32px 位图（按字形缓存，进程生命周期内复用）；字体不存在时返回 null。</summary>
    public static Bitmap? Get(char glyph) => Get(glyph, DefaultBitmapSize);

    /// <summary>获取指定字形、指定边长（像素）的位图并按字形与尺寸缓存；字体不存在时返回 null。</summary>
    public static Bitmap? Get(char glyph, int pixelSize)
    {
        if (!FontAvailable || pixelSize <= 0)
        {
            return null;
        }

        lock (SyncRoot)
        {
            if (Cache.TryGetValue((glyph, pixelSize), out Bitmap? cached))
            {
                return cached;
            }

            var bitmap = new Bitmap(pixelSize, pixelSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using var font = new Font(IconFontName, pixelSize * GlyphToBitmapRatio, FontStyle.Regular, GraphicsUnit.Pixel);
                TextRenderer.DrawText(
                    graphics,
                    glyph.ToString(),
                    font,
                    new Rectangle(0, 0, pixelSize, pixelSize),
                    SystemColors.ControlText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            Cache[(glyph, pixelSize)] = bitmap;
            return bitmap;
        }
    }
}
