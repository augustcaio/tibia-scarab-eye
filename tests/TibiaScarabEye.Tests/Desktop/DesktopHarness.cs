using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace TibiaScarabEye.Tests.Desktop;

internal static class DesktopHarness
{
    // Janelas de teste ficam fora da area visivel para nao atrapalhar o desktop de quem roda os testes.
    public static readonly Point Offscreen = new Point(-20000, -20000);

    private const BindingFlags NonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

    public static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, NonPublic).GetValue(instance);

    public static void SetField(object instance, string name, object value) => instance.GetType().GetField(name, NonPublic).SetValue(instance, value);

    public static object Call(object instance, string name, params object[] args) => instance.GetType().GetMethod(name, NonPublic).Invoke(instance, args);

    public static void ShowOffscreen(Form form)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = Offscreen;
        form.Show();
        Application.DoEvents();
    }

    public static void Down(Control c, MouseButtons button, int x, int y) => Raise(c, "OnMouseDown", button, 1, x, y, 0);

    public static void Move(Control c, MouseButtons button, int x, int y) => Raise(c, "OnMouseMove", button, 0, x, y, 0);

    public static void Up(Control c, MouseButtons button, int x, int y) => Raise(c, "OnMouseUp", button, 1, x, y, 0);

    public static void Wheel(Control c, int x, int y, int delta) => Raise(c, "OnMouseWheel", MouseButtons.None, 0, x, y, delta);

    public static void Leave(Control c) => typeof(Control).GetMethod("OnMouseLeave", NonPublic).Invoke(c, new object[] { EventArgs.Empty });

    public static void Drag(Control c, MouseButtons button, Point from, Point to)
    {
        Down(c, button, from.X, from.Y);
        Move(c, button, to.X, to.Y);
        Up(c, button, to.X, to.Y);
    }

    public static void Click(Control parent, string buttonText)
    {
        foreach (Control control in parent.Controls)
            if (control is Button && control.Text == buttonText) ((Button)control).PerformClick();
    }

    // DrawToBitmap ignora o Region da janela; aplica o recorte para a imagem mostrar o formato real (fora dele fica transparente).
    public static Bitmap RenderShaped(Form form)
    {
        using var raw = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(raw, new Rectangle(Point.Empty, form.Size));
        var shaped = new Bitmap(form.Width, form.Height);
        using var g = Graphics.FromImage(shaped);
        if (form.Region != null) g.SetClip(form.Region, System.Drawing.Drawing2D.CombineMode.Replace);
        g.DrawImageUnscaled(raw, Point.Empty);
        return shaped;
    }

    public static void SaveArtifact(Bitmap bitmap, string fileName)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "artifacts");
        Directory.CreateDirectory(directory);
        bitmap.Save(Path.Combine(directory, fileName));
    }

    private static void Raise(Control c, string handler, MouseButtons button, int clicks, int x, int y, int delta) =>
        typeof(Control).GetMethod(handler, NonPublic).Invoke(c, new object[] { new MouseEventArgs(button, clicks, x, y, delta) });
}
