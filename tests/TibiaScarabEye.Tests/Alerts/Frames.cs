using System.Drawing;
using TibiaScarabEye.Alerts;

namespace TibiaScarabEye.Tests.Alerts;

// Quadros de pixels sinteticos: um fundo de slot escuro com o sprite de um item desenhado numa posicao.
internal static class Frames
{
    public static readonly Color SlotBackground = Color.FromArgb(30, 30, 28);

    public static PixelBuffer Empty(int width = 200, int height = 150) => With(width, height);

    public static PixelBuffer With(int width, int height, params (PlasmaItem item, Point at)[] sprites)
    {
        using var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(SlotBackground);
            foreach (var (item, at) in sprites)
            {
                using var sprite = SpriteTemplate.LoadBitmap(PlasmaInfo.SpriteResource(item));
                g.DrawImageUnscaled(sprite, at);
            }
        }
        return PixelBuffer.FromBitmap(bitmap);
    }

    public static SpriteTemplate Template(PlasmaItem item) => SpriteTemplate.FromResource(PlasmaInfo.SpriteResource(item));
}
