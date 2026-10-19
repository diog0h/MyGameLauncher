using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameLauncher.Helpers;

public static class ImageHelper
{
    /// <summary>Carrega uma imagem do disco sem a bloquear e já reduzida ao tamanho pedido.</summary>
    public static BitmapSource? Load(string? path, int decodeWidth = 0)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        try
        {
            var bmp = new BitmapImage();
            using (var fs = File.OpenRead(path))
            {
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                if (decodeWidth > 0) bmp.DecodePixelWidth = decodeWidth;
                bmp.StreamSource = fs;
                bmp.EndInit();
            }
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    /// <summary>
    /// Escolhe a cor predominante "com cor" de uma imagem (ignora cinzentos, pretos e brancos)
    /// e ajusta-a para servir de fundo com texto branco por cima.
    /// </summary>
    public static Color? DominantColor(string? path)
    {
        var src = Load(path, 64);
        if (src is null) return null;

        var bgra = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        int w = bgra.PixelWidth, h = bgra.PixelHeight, stride = w * 4;
        var px = new byte[h * stride];
        bgra.CopyPixels(px, stride, 0);

        const int bins = 36;
        var weight = new double[bins];
        var sumR = new double[bins];
        var sumG = new double[bins];
        var sumB = new double[bins];
        double total = 0;

        for (int i = 0; i < px.Length; i += 4)
        {
            double b = px[i] / 255.0, g = px[i + 1] / 255.0, r = px[i + 2] / 255.0;
            RgbToHsv(r, g, b, out var hue, out var s, out var v);
            double wgt = s * s * v; // dá mais peso a cores vivas
            total += 1;
            if (s < 0.25 || v < 0.15) continue;
            int bin = Math.Min(bins - 1, (int)(hue / 360.0 * bins));
            weight[bin] += wgt;
            sumR[bin] += r * wgt; sumG[bin] += g * wgt; sumB[bin] += b * wgt;
        }

        int best = 0;
        for (int i = 1; i < bins; i++) if (weight[i] > weight[best]) best = i;

        // Imagem quase sem cor: devolve um cinzento-azulado escuro.
        if (total == 0 || weight[best] / total < 0.02)
            return Color.FromRgb(0x2A, 0x2B, 0x33);

        double ar = sumR[best] / weight[best], ag = sumG[best] / weight[best], ab = sumB[best] / weight[best];
        RgbToHsv(ar, ag, ab, out var hh, out var ss, out _);
        ss = Math.Clamp(ss, 0.55, 0.85);
        double vv;
        if (hh is > 40 and < 70) { hh = 36; ss = 0.85; vv = 0.58; } // amarelo → âmbar (amarelo escuro fica cor de azeitona)
        else if (hh is >= 70 and < 200) vv = 0.45;                   // verdes/cianos mais escuros para o texto branco se ler
        else vv = 0.6;                                               // vermelhos, azuis, roxos
        return HsvToColor(hh, ss, vv);
    }

    public static Color Darken(Color c, double factor) =>
        Color.FromRgb((byte)(c.R * factor), (byte)(c.G * factor), (byte)(c.B * factor));

    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static Color? FromHex(string? hex)
    {
        try { return hex is null ? null : (Color)ColorConverter.ConvertFromString(hex); }
        catch { return null; }
    }

    private static void RgbToHsv(double r, double g, double b, out double h, out double s, out double v)
    {
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        v = max;
        s = max == 0 ? 0 : d / max;
        if (d == 0) { h = 0; return; }
        if (max == r) h = 60 * (((g - b) / d) % 6);
        else if (max == g) h = 60 * ((b - r) / d + 2);
        else h = 60 * ((r - g) / d + 4);
        if (h < 0) h += 360;
    }

    private static Color HsvToColor(double h, double s, double v)
    {
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        (double r, double g, double b) = h switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }
}
