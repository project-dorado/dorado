using System;
using Avalonia.Controls;
using Avalonia.Input;
using Dorado.UI.ViewModels;

namespace Dorado.UI.Views;

public partial class PodcastsView : UserControl
{
    private readonly TypeAheadBuffer _typeAhead = new();

    public PodcastsView()
    {
        InitializeComponent();
    }

    /// <summary>A–Z jump: match the episode list first, then the series list.</summary>
    private void OnTypeAheadText(object? sender, TextInputEventArgs e)
    {
        if (e.Source is TextBox || string.IsNullOrEmpty(e.Text) || e.Text.Length != 1)
        {
            return;
        }

        var character = e.Text[0];
        if (!char.IsLetterOrDigit(character) || DataContext is not PodcastsViewModel vm)
        {
            return;
        }

        var prefix = _typeAhead.Append(character, DateTime.UtcNow);

        var episodeIndex = TypeAheadSearch.FindIndex(vm.Episodes, prefix, episode => episode.Title);
        if (episodeIndex >= 0)
        {
            EpisodesList.ContainerFromIndex(episodeIndex)?.BringIntoView();
            e.Handled = true;
            return;
        }

        var seriesIndex = TypeAheadSearch.FindIndex(vm.Podcasts, prefix, series => series.Title);
        if (seriesIndex >= 0)
        {
            vm.SelectedPodcast = vm.Podcasts[seriesIndex];
            PodcastsList.ContainerFromIndex(seriesIndex)?.BringIntoView();
        }

        e.Handled = true;
    }
}
