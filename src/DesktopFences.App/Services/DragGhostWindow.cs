using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using DesktopFences.Native;
using Image = System.Windows.Controls.Image;
using Point = System.Windows.Point;
using Brushes = System.Windows.Media.Brushes;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace DesktopFences.App.Services;

/// <summary>
/// Mini janela que segue o cursor no arraste (para fora ou para reordenar).
/// Clique atravessa (WS_EX_TRANSPARENT); o hook de mouse decide o drop.
/// </summary>
internal sealed class DragGhostWindow : Window
{
    private readonly Image _icon;
    private readonly TextBlock _label;
    private readonly TextBlock _badge;

    public DragGhostWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = false;
        IsHitTestVisible = false;
        Width = 40;
        Height = 54;
        ResizeMode = ResizeMode.NoResize;
        Focusable = false;

        _icon = new Image
        {
            Width = 32,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Left,
            Opacity = 0.95
        };
        RenderOptions.SetBitmapScalingMode(_icon, BitmapScalingMode.HighQuality);

        _label = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 11,
            TextAlignment = TextAlignment.Left,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            Margin = new Thickness(0, 2, 0, 0),
            MaxWidth = 40
        };

        _badge = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right,
            Visibility = Visibility.Collapsed
        };

        var panel = new StackPanel();
        panel.Children.Add(_icon);
        panel.Children.Add(_label);
        var root = new Grid();
        root.Children.Add(panel);
        root.Children.Add(_badge);
        Content = root;

        SourceInitialized += (_, _) =>
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            InvisibleOleWindow.MakeClickThrough(hwnd);
            DesktopWindowAnchor.HideFromTaskSwitchers(hwnd);
        };
    }

    public void ShowItems(ImageSource? icon, string name, int extraCount)
    {
        _icon.Source = icon;
        _label.Text = name;
        if (extraCount > 0)
        {
            _badge.Text = $"+{extraCount}";
            _badge.Visibility = Visibility.Visible;
        }
        else
            _badge.Visibility = Visibility.Collapsed;

        if (!IsVisible)
            Show();
    }

    public void FollowScreen(int screenX, int screenY, Visual dpiReference)
    {
        PresentationSource? source = PresentationSource.FromVisual(this)
                                     ?? PresentationSource.FromVisual(dpiReference);
        Point raw = new(screenX, screenY);
        Point dip = source?.CompositionTarget is not null
            ? source.CompositionTarget.TransformFromDevice.Transform(raw)
            : raw;
        Left = dip.X + 2;
        Top = dip.Y + 2;
    }

    public void HideGhost()
    {
        if (IsVisible)
            Hide();
    }
}
