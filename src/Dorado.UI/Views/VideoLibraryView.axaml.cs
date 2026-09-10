using System;
using Avalonia.Controls;
using Avalonia.Input;
using Dorado.UI.ViewModels;

namespace Dorado.UI.Views;

public partial class VideoLibraryView : UserControl
{
    private readonly TypeAheadBuffer _typeAhead = new();

    public VideoLibraryView()
    {
        InitializeComponent();
    }

    private void OnTypeAheadText(object? sender, TextInputEventArgs e)
    {
        if (e.Source is TextBox || string.IsNullOrEmpty(e.Text) || e.Text.Length != 1)
        {
            return;
        }

        var character = e.Text[0];
        if (!char.IsLetterOrDigit(character) || DataContext is not VideoLibraryViewModel vm)
        {
            return;
        }

        var prefix = _typeAhead.Append(character, DateTime.UtcNow);
        var index = TypeAheadSearch.FindIndex(vm.Videos, prefix, video => video.Title);
        if (index >= 0)
        {
            VideosList.ContainerFromIndex(index)?.BringIntoView();
        }

        e.Handled = true;
    }
}
