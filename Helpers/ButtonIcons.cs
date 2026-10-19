using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Path = System.Windows.Shapes.Path;

namespace GameLauncher.Helpers;

/// <summary>
/// Ícones de botões (Xbox Series, DualSense e teclado) desenhados em vetor, num só tom:
/// formas brancas com letras e símbolos "recortados", que deixam ver a cor de fundo da app.
/// Uso: <see cref="Row"/>("xb:A"), Row("key:Ctrl + key:F"), Row("ps:L1 / ps:R1").
/// Tokens:
///   xb:A|B|X|Y|LB|RB|dpad|menu|view|share
///   ps:cross|circle|square|triangle|L1|R1|dpad|options|create|home
///   stick:L|R   keys:arrows   key:Texto
///   "/" e "+" são separadores; "—" = sem atalho.
/// </summary>
public static class ButtonIcons
{
    private static readonly Brush Fill = Frozen("#FFF4F5F8");
    private static readonly Brush Faint = Frozen("#99FFFFFF");
    private static readonly Typeface Font = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    private static Brush Frozen(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    /// <summary>Uma linha de ícones a partir de uma descrição como "key:Esc / key:Backspace".</summary>
    public static FrameworkElement Row(string spec)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var token in spec.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token is "/" or "+" or "—")
            {
                panel.Children.Add(new TextBlock
                {
                    Text = token, Foreground = Faint, FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = token == "—" ? new Thickness(0) : new Thickness(6, 0, 6, 0),
                });
                continue;
            }
            var icon = Create(token);
            if (panel.Children.Count > 0 && panel.Children[^1] is not TextBlock)
                icon.Margin = new Thickness(4, 0, 0, 0); // dois ícones seguidos (ex.: LB RB)
            panel.Children.Add(icon);
        }
        return panel;
    }

    public static FrameworkElement Create(string token)
    {
        var (kind, arg) = token.IndexOf(':') is var i and >= 0 ? (token[..i], token[(i + 1)..]) : (token, "");
        return kind switch
        {
            "xb" => arg switch
            {
                "A" or "B" or "X" or "Y" => Icon(28, 28, Cut(Disc(14, 14, 13), Text(arg, 15.5, 14, 14))),
                "LB" or "RB" => Icon(38, 24, Cut(Geo("M3,7 Q4,2 10,2 H28 Q34,2 35,7 L36,18 Q36,22 32,22 H6 Q2,22 2,18 Z"), Text(arg, 10.5, 19, 12.5))),
                "dpad" => Icon(26, 26, XboxDpad()),
                "menu" => Icon(28, 28, Cut(Disc(14, 14, 13), Line("M8.5,10 H19.5 M8.5,14 H19.5 M8.5,18 H19.5", 1.9))),
                "view" => Icon(28, 28, Cut(Disc(14, 14, 13), Line("M8,12 H16 V19 H8 Z M11.5,12 V8.5 H20 V15.5 H16", 1.6))),
                "share" => Icon(38, 24, Cut(Geo("M12,2 H26 A10,10 0 0 1 26,22 H12 A10,10 0 0 1 12,2 Z"), Line("M13.5,13.5 V17 H24.5 V13.5 M19,15 V7.5 M16,10.5 L19,7.5 L22,10.5", 1.6))),
                _ => Key(arg),
            },
            "ps" => arg switch
            {
                "cross" => PsFace(Line("M10.6,10.6 L17.4,17.4 M17.4,10.6 L10.6,17.4", 2)),
                "circle" => PsFace(Widen(new EllipseGeometry(new Point(14, 14), 4.3, 4.3), 1.8)),
                "square" => PsFace(Widen(new RectangleGeometry(new Rect(10, 10, 8, 8)), 1.8)),
                "triangle" => PsFace(Line("M14,9.3 L18.8,17.6 H9.2 Z", 1.8)),
                "L1" or "R1" => Icon(38, 24, Cut(Geo("M2,22 V16 Q2,3 19,3 Q36,3 36,16 V22 Z"), Text(arg, 10.5, 19, 14))),
                "dpad" => Icon(26, 26, PlayStationDpad()),
                "options" => Icon(14, 28, Union(Pill(3, 9, 8, 18), Line("M3,2.5 H11 M3,5 H11 M3,7.5 H11", 1.2))),
                "create" => Icon(14, 28, Union(Pill(3, 9, 8, 18), Line("M3.5,6.5 L2,3.5 M7,5.5 V1.5 M10.5,6.5 L12,3.5", 1.5))),
                "home" => Icon(28, 28, Cut(Disc(14, 14, 13), Text("PS", 10, 14, 14))),
                _ => Key(arg),
            },
            "stick" => Icon(28, 28, Cut(Disc(14, 14, 13), Widen(new EllipseGeometry(new Point(14, 14), 9.5, 9.5), 1.5), Text(arg, 11, 14, 14))),
            "keys" when arg == "arrows" => Arrows(),
            "key" => Key(arg),
            _ => Key(token),
        };
    }

    // ------------------------------------------------------------ peças de cada comando

    /// <summary>Botão frontal PlayStation: anel fino por fora e disco com o símbolo recortado.</summary>
    private static FrameworkElement PsFace(Geometry symbol)
    {
        var ring = new CombinedGeometry(GeometryCombineMode.Exclude, Disc(14, 14, 13.5), Disc(14, 14, 12));
        var face = Cut(Disc(14, 14, 10.6), symbol);
        return Icon(28, 28, Union(ring, face));
    }

    private static Geometry XboxDpad() => new CombinedGeometry(GeometryCombineMode.Union,
        new RectangleGeometry(new Rect(9, 1, 8, 24), 2.2, 2.2),
        new RectangleGeometry(new Rect(1, 9, 24, 8), 2.2, 2.2));

    /// <summary>Quatro botões separados, cada um com a ponta virada para o centro.</summary>
    private static Geometry PlayStationDpad()
    {
        var up = Geo("M10,1 H16 Q17.5,1 17.5,2.5 V8 L13,12 L8.5,8 V2.5 Q8.5,1 10,1 Z");
        var group = new GeometryGroup();
        foreach (var angle in new[] { 0, 90, 180, 270 })
        {
            var copy = up.Clone();
            copy.Transform = new RotateTransform(angle, 13, 13);
            group.Children.Add(copy);
        }
        return group;
    }

    // ------------------------------------------------------------ teclado

    /// <summary>Tecla: retângulo arredondado com o texto recortado.</summary>
    private static FrameworkElement Key(string text)
    {
        var label = Text(text, 11.5, 0, 0);
        double w = Math.Max(26, label.Bounds.Width + 16);
        label = Text(text, 11.5, w / 2, 13);
        return Icon(w, 26, Cut(new RectangleGeometry(new Rect(0, 0, w, 26), 5, 5), label));
    }

    /// <summary>As quatro setas em forma de T invertido.</summary>
    private static FrameworkElement Arrows()
    {
        Geometry Small(double x, double y, double angle)
        {
            var arrow = Line("M-2.6,1.3 L0,-1.3 L2.6,1.3", 1.5);
            arrow.Transform = new TransformGroup
            {
                Children = { new RotateTransform(angle), new TranslateTransform(x + 7, y + 6) },
            };
            return Cut(new RectangleGeometry(new Rect(x, y, 14, 12), 2.5, 2.5), arrow);
        }
        return Icon(46, 26, Union(Small(16, 0, 0), Small(0, 14, 270), Small(16, 14, 180), Small(32, 14, 90)));
    }

    // ------------------------------------------------------------ geometria

    private static FrameworkElement Icon(double w, double h, Geometry g)
    {
        g.Freeze();
        return new Path
        {
            Data = g, Fill = Fill, Width = w, Height = h, Stretch = Stretch.None,
            VerticalAlignment = VerticalAlignment.Center, SnapsToDevicePixels = true,
        };
    }

    private static Geometry Geo(string data) => Geometry.Parse(data);

    private static Geometry Disc(double cx, double cy, double r) => new EllipseGeometry(new Point(cx, cy), r, r);

    private static Geometry Pill(double x, double y, double w, double h) =>
        new RectangleGeometry(new Rect(x, y, w, h), w / 2, w / 2);

    /// <summary>Forma "base" com os buracos recortados.</summary>
    private static Geometry Cut(Geometry shape, params Geometry[] holes)
    {
        Geometry result = shape;
        foreach (var h in holes) result = new CombinedGeometry(GeometryCombineMode.Exclude, result, h);
        return result;
    }

    private static Geometry Union(params Geometry[] parts)
    {
        Geometry result = parts[0];
        foreach (var p in parts.Skip(1)) result = new CombinedGeometry(GeometryCombineMode.Union, result, p);
        return result;
    }

    /// <summary>Traço (linhas) transformado em forma, com pontas redondas.</summary>
    private static Geometry Line(string data, double thickness) => Widen(Geo(data), thickness);

    private static Geometry Widen(Geometry g, double thickness)
    {
        var pen = new Pen(Brushes.Black, thickness)
        {
            StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round,
        };
        return g.GetWidenedPathGeometry(pen);
    }

    /// <summary>Texto transformado em forma e centrado em (cx, cy).</summary>
    private static Geometry Text(string text, double size, double cx, double cy)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, size, Brushes.Black, 1.0);
        var g = ft.BuildGeometry(new Point(0, 0));
        var b = g.Bounds;
        if (b.IsEmpty) return Geometry.Empty;
        var moved = g.Clone();
        moved.Transform = new TranslateTransform(cx - (b.Left + b.Width / 2), cy - (b.Top + b.Height / 2));
        return moved;
    }
}
