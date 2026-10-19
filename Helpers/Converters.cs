using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace GameLauncher.Helpers;

/// <summary>Caminho de ficheiro → imagem. Parâmetro opcional: largura de descodificação.</summary>
public sealed class PathToImageConverter : IValueConverter
{
    private static readonly Dictionary<string, WeakReference<ImageSource>> Cache = new();

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || path.Length == 0) return null;
        int width = 0;
        if (parameter is string p) int.TryParse(p, out width);

        var key = path + "|" + width;
        if (Cache.TryGetValue(key, out var wr) && wr.TryGetTarget(out var hit)) return hit;

        var img = ImageHelper.Load(path, width);
        if (img is not null) Cache[key] = new WeakReference<ImageSource>(img);
        return img;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>true → Visible. Com parâmetro "invert": true → Collapsed.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool v && v;
        if (parameter as string == "invert") b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>null/vazio → Visible (para mostrar alternativas quando falta uma imagem). "invert" troca.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isNull = value is null || (value is string s && s.Length == 0);
        if (parameter as string == "invert") isNull = !isNull;
        return isNull ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
