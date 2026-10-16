using System.Drawing;
using TibiaScarabEye.Alerts;

namespace TibiaScarabEye.Tests.Alerts;

// Quadros de pixels sinteticos: um fundo de slot escuro com o sprite de um item desenhado numa posicao e, opcionalmente, o tempo
// restante que o Tibia escreve embaixo dele ("1m28").
internal static class Frames
{
    public static readonly Color SlotBackground = Color.FromArgb(30, 30, 28);
    public static readonly Color TextColor = Color.FromArgb(205, 208, 205);

    public static PixelBuffer Empty(int width = 200, int height = 150) => With(width, height);

    public static PixelBuffer With(int width, int height, params (PlasmaItem item, Point at)[] sprites)
    {
        var items = new (PlasmaItem item, Point at, string text)[sprites.Length];
        for (int i = 0; i < sprites.Length; i++) items[i] = (sprites[i].item, sprites[i].at, null);
        return WithTimers(width, height, items);
    }

    // Cada item com o texto do tempo embaixo (null: sem texto).
    public static PixelBuffer WithTimers(int width, int height, params (PlasmaItem item, Point at, string text)[] sprites)
    {
        using var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(SlotBackground);
            foreach (var (item, at, text) in sprites)
            {
                using var sprite = SpriteTemplate.LoadBitmap(PlasmaInfo.SpriteResource(item));
                g.DrawImageUnscaled(sprite, at);
            }
        }
        foreach (var (item, at, text) in sprites) if (text != null) DrawText(bitmap, at, text);
        return PixelBuffer.FromBitmap(bitmap);
    }

    // Desenha o texto como o jogo: caracteres de 8 linhas, uma coluna de folga entre eles, 22 linhas abaixo do topo do sprite.
    private static void DrawText(Bitmap bitmap, Point sprite, string text)
    {
        int x = sprite.X + 1;
        foreach (char ch in text)
        {
            var rows = TimerReader.Rows(ch) ?? throw new System.ArgumentException("sem desenho para '" + ch + "'");
            for (int y = 0; y < rows.Length; y++)
                for (int c = 0; c < rows[y].Length; c++)
                    if (rows[y][c] == '#' && x + c < bitmap.Width && sprite.Y + 22 + y < bitmap.Height) bitmap.SetPixel(x + c, sprite.Y + 22 + y, TextColor);
            x += rows[0].Length + 1;
        }
    }

    public static SpriteTemplate Template(PlasmaItem item) => SpriteTemplate.FromResource(PlasmaInfo.SpriteResource(item));
}
