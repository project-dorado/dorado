using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Dorado.UI.Views;

public partial class CompactMiniPlayerView : UserControl
{
    public CompactMiniPlayerView()
    {
        InitializeComponent();
    }

    private void OnMiniBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var window = TopLevel.GetTopLevel(this) as Window;
            window?.BeginMoveDrag(e);
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        var window = TopLevel.GetTopLevel(this) as Window;
        window?.Close();
    }
}
