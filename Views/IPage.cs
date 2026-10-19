using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GameLauncher.Models;

namespace GameLauncher.Views;

public interface IPage
{
    /// <summary>Título na barra de cima.</summary>
    string Title { get; }

    /// <summary>Jogo "da página" (para os botões X/Y do comando quando o foco não está num jogo).</summary>
    Game? ContextGame => null;

    /// <summary>Chamado quando se volta a esta página (Voltar) ou depois de atualizar a biblioteca.</summary>
    void OnReturned() { }
}

public static class ScrollHelper
{
    /// <summary>
    /// Filas horizontais: a roda do rato continua a deslocar a página para baixo;
    /// com Shift (ou num touchpad horizontal) desloca a fila para o lado.
    /// </summary>
    public static void ForwardWheelToParent(ScrollViewer row)
    {
        row.PreviewMouseWheel += (s, e) =>
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                row.ScrollToHorizontalOffset(row.HorizontalOffset - e.Delta);
                e.Handled = true;
                return;
            }
            e.Handled = true;
            var parent = FindParent<ScrollViewer>(row);
            parent?.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = row,
            });
        };
    }

    /// <summary>Desliza uma fila horizontal suavemente até à posição pedida.</summary>
    public static void AnimateHorizontal(ScrollViewer sv, double target)
    {
        target = Math.Clamp(target, 0, sv.ScrollableWidth);
        var anim = new System.Windows.Media.Animation.DoubleAnimation(sv.HorizontalOffset, target, TimeSpan.FromMilliseconds(320))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut },
        };
        sv.BeginAnimation(HorizontalOffsetProperty, anim);
    }

    /// <summary>Propriedade auxiliar: o ScrollViewer não deixa animar o deslocamento diretamente.</summary>
    public static readonly DependencyProperty HorizontalOffsetProperty = DependencyProperty.RegisterAttached(
        "HorizontalOffset", typeof(double), typeof(ScrollHelper),
        new PropertyMetadata(0.0, (d, e) => (d as ScrollViewer)?.ScrollToHorizontalOffset((double)e.NewValue)));

    public static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var p = System.Windows.Media.VisualTreeHelper.GetParent(child);
        while (p is not null and not T) p = System.Windows.Media.VisualTreeHelper.GetParent(p);
        return p as T;
    }
}
