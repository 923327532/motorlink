using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

/// <summary>
/// Genera fotografías DE DEMOSTRACIÓN (ilustraciones propias, con marca de agua "DEMO") para publicaciones de prueba.
/// No son fotos reales de vehículos y no infringen derechos de terceros.
/// </summary>
public static class PhotoGenerator
{
    public static byte[] Create(string kind, Color body, int variant, string caption)
    {
        const int W = 960, H = 720;
        using var bmp = new Bitmap(W, H);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        // Fondo según variante
        var palettes = new[] { (Color.FromArgb(14, 30, 68), Color.FromArgb(70, 120, 210)), (Color.FromArgb(40, 52, 70), Color.FromArgb(150, 170, 200)), (Color.FromArgb(20, 60, 60), Color.FromArgb(120, 190, 180)) };
        var (top, bottom) = palettes[variant % palettes.Length];
        using (var sky = new LinearGradientBrush(new Rectangle(0, 0, W, H), top, bottom, 90f)) g.FillRectangle(sky, 0, 0, W, H);
        using (var road = new SolidBrush(Color.FromArgb(28, 34, 46))) g.FillRectangle(road, 0, (int)(H * .72), W, H);
        using (var line = new Pen(Color.FromArgb(70, 255, 255, 255), 4) { DashStyle = DashStyle.Dash }) g.DrawLine(line, 0, (int)(H * .9), W, (int)(H * .9));
        using (var shadow = new SolidBrush(Color.FromArgb(90, 0, 0, 0))) g.FillEllipse(shadow, 150, (int)(H * .70), W - 300, 46);

        var scale = variant == 1 ? 1.15f : variant == 2 ? 1.45f : 1f;
        var offsetX = variant == 2 ? -170f : 0f;
        g.TranslateTransform(W / 2f + offsetX, H * .5f);
        g.ScaleTransform(scale, scale);
        g.TranslateTransform(-W / 2f, -H * .5f);
        Draw(g, kind, body, W, H);
        g.ResetTransform();

        using var font = new Font("Segoe UI", 15, FontStyle.Bold);
        using var small = new Font("Segoe UI", 13, FontStyle.Regular);
        g.FillRectangle(new SolidBrush(Color.FromArgb(150, 15, 23, 42)), 0, H - 56, W, 56);
        g.DrawString(caption, font, Brushes.White, 24, H - 48);
        var tag = "FOTO DE DEMOSTRACIÓN";
        var size = g.MeasureString(tag, small);
        g.DrawString(tag, small, new SolidBrush(Color.FromArgb(255, 251, 191, 36)), W - size.Width - 24, H - 46);

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    private static void Draw(Graphics g, string kind, Color body, int W, int H)
    {
        float cx = W / 2f, ground = H * .70f;
        using var bodyBrush = new LinearGradientBrush(new RectangleF(0, ground - 260, W, 260), Lighten(body, .25f), Darken(body, .18f), 90f);
        using var glass = new SolidBrush(Color.FromArgb(215, 25, 38, 62));
        using var dark = new SolidBrush(Color.FromArgb(255, 18, 22, 32));
        using var rim = new SolidBrush(Color.FromArgb(255, 205, 212, 224));

        void Wheel(float x, float r) { g.FillEllipse(dark, x - r, ground - r, 2 * r, 2 * r); g.FillEllipse(rim, x - r * .55f, ground - r * .55f, r * 1.1f, r * 1.1f); g.FillEllipse(dark, x - r * .18f, ground - r * .18f, r * .36f, r * .36f); }
        void Poly(Brush b, params (float x, float y)[] pts) => g.FillPolygon(b, pts.Select(p => new PointF(cx + p.x, ground + p.y)).ToArray());
        void Lights(float fx, float fy, float rx, float ry)
        {
            g.FillRectangle(new SolidBrush(Color.FromArgb(255, 253, 230, 138)), cx + fx, ground + fy, 34, 16);
            g.FillRectangle(new SolidBrush(Color.FromArgb(255, 239, 68, 68)), cx + rx, ground + ry, 26, 16);
        }

        switch (kind)
        {
            case "sedan":
                Poly(bodyBrush, (-330, -6), (-330, -80), (-290, -100), (-170, -112), (-110, -190), (90, -190), (170, -112), (290, -98), (332, -70), (332, -6));
                Poly(glass, (-100, -178), (-150, -118), (-20, -118), (-20, -178));
                Poly(glass, (0, -178), (0, -118), (130, -118), (82, -178));
                Wheel(cx - 330 + 120 - cx + cx, 52); Wheel(cx + 200, 52); Lights(296, -86, -330, -88); break;
            case "hatch":
                Poly(bodyBrush, (-300, -6), (-300, -86), (-260, -104), (-150, -116), (-96, -200), (120, -200), (180, -116), (270, -104), (304, -76), (304, -6));
                Poly(glass, (-84, -188), (-136, -122), (-8, -122), (-8, -188));
                Poly(glass, (6, -188), (6, -122), (150, -122), (110, -188));
                Wheel(cx - 190, 50); Wheel(cx + 190, 50); Lights(268, -88, -300, -92); break;
            case "suv":
                Poly(bodyBrush, (-330, -6), (-330, -96), (-290, -122), (-250, -214), (150, -214), (230, -126), (300, -112), (336, -82), (336, -6));
                Poly(glass, (-232, -200), (-262, -126), (-10, -126), (-10, -200));
                Poly(glass, (6, -200), (6, -126), (210, -126), (140, -200));
                Wheel(cx - 215, 60); Wheel(cx + 200, 60); Lights(300, -98, -330, -100); break;
            case "pickup":
                Poly(bodyBrush, (-340, -6), (-340, -100), (-300, -110), (-300, -140), (-60, -140), (-60, -190), (20, -214), (130, -214), (190, -130), (300, -116), (338, -86), (338, -6));
                Poly(glass, (-30, -198), (-50, -140), (60, -140), (110, -198));
                Poly(dark, (-300, -140), (-60, -140), (-60, -112), (-300, -112));
                Wheel(cx - 230, 60); Wheel(cx + 210, 60); Lights(302, -100, -340, -104); break;
            case "van":
                Poly(bodyBrush, (-340, -6), (-340, -236), (-310, -250), (130, -250), (210, -150), (320, -124), (344, -92), (344, -6));
                Poly(glass, (150, -236), (112, -150), (210, -150), (190, -200));
                Poly(glass, (-230, -218), (-230, -160), (-120, -160), (-120, -218));
                Wheel(cx - 220, 56); Wheel(cx + 210, 56); Lights(308, -98, -340, -110); break;
            case "truck":
                Poly(bodyBrush, (-350, -6), (-350, -250), (60, -250), (60, -6));
                Poly(new LinearGradientBrush(new RectangleF(0, ground - 240, W, 240), Darken(body, .1f), Darken(body, .3f), 90f), (80, -6), (80, -190), (150, -226), (260, -226), (320, -150), (350, -130), (350, -6));
                Poly(glass, (170, -210), (150, -150), (300, -150), (258, -210));
                Wheel(cx - 250, 58); Wheel(cx - 130, 58); Wheel(cx + 250, 58); break;
            case "bus":
                Poly(bodyBrush, (-380, -6), (-380, -250), (330, -250), (380, -150), (380, -6));
                for (var i = 0; i < 7; i++) Poly(glass, (-350 + i * 92, -226), (-350 + i * 92, -150), (-278 + i * 92, -150), (-278 + i * 92, -226));
                Wheel(cx - 250, 56); Wheel(cx + 250, 56); Lights(346, -60, -380, -60); break;
            case "moto":
                using (var frame = new Pen(Darken(body, .1f), 14) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(frame, cx - 150, ground - 20, cx - 20, ground - 120);
                    g.DrawLine(frame, cx - 20, ground - 120, cx + 120, ground - 130);
                    g.DrawLine(frame, cx + 120, ground - 130, cx + 160, ground - 22);
                    g.DrawLine(frame, cx + 120, ground - 130, cx + 150, ground - 200);
                    g.DrawLine(frame, cx + 120, ground - 200, cx + 180, ground - 205);
                }
                g.FillEllipse(bodyBrush, cx - 40, ground - 190, 170, 80);
                g.FillRectangle(dark, cx - 140, ground - 150, 130, 26);
                Wheel(cx - 150, 62); Wheel(cx + 160, 62); break;
            case "mototaxi":
                Poly(bodyBrush, (-150, -6), (-150, -190), (-120, -214), (120, -214), (150, -190), (150, -90), (260, -86), (290, -60), (290, -6));
                Poly(glass, (-110, -196), (-110, -110), (110, -110), (110, -196));
                Poly(dark, (-160, -214), (130, -214), (130, -228), (-160, -228));
                Wheel(cx - 90, 48); Wheel(cx + 100, 48); Wheel(cx + 255, 44); break;
            default: goto case "sedan";
        }
    }

    private static Color Lighten(Color c, float f) => Color.FromArgb(c.A, (int)(c.R + (255 - c.R) * f), (int)(c.G + (255 - c.G) * f), (int)(c.B + (255 - c.B) * f));
    private static Color Darken(Color c, float f) => Color.FromArgb(c.A, (int)(c.R * (1 - f)), (int)(c.G * (1 - f)), (int)(c.B * (1 - f)));
}
