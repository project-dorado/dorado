using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.Domain.Models;

namespace Dorado.UI.ViewModels;

public class TrackMatchRow : ViewModelBase
{
    private bool _accepted = true;

    public TrackMatchRow(TrackMatchCandidate candidate, Track originalTrack)
    {
        Candidate = candidate;
        OriginalTrack = originalTrack;
    }

    public TrackMatchCandidate Candidate { get; }
    public Track OriginalTrack { get; }

    public bool Accepted
    {
        get => _accepted;
        set => SetProperty(ref _accepted, value);
    }

    public string ScoreText => $"{Candidate.Score}%";
    public string DurationText => Candidate.MatchedDurationMs.HasValue
        ? TimeSpan.FromMilliseconds(Candidate.MatchedDurationMs.Value).ToString(@"m\:ss")
        : "—";
}

public class TrackMatchReviewViewModel : ViewModelBase
{
    private readonly IMediaLibraryService _libraryService;
    private string? _statusText;
    private bool _isApplying;

    public event EventHandler? RequestClose;

    public TrackMatchReviewViewModel(
        string albumTitle,
        string artistName,
        IReadOnlyList<TrackMatchCandidate> candidates,
        IReadOnlyList<Track> originalTracks,
        IMediaLibraryService libraryService)
    {
        AlbumTitle = albumTitle;
        ArtistName = artistName;
        _libraryService = libraryService;

        var tracksById = originalTracks.ToDictionary(t => t.Id);
        Rows = new ObservableCollection<TrackMatchRow>(
            candidates
                .Where(c => tracksById.ContainsKey(c.TrackId))
                .Select(c => new TrackMatchRow(c, tracksById[c.TrackId])));

        ApplyCommand = new AsyncRelayCommand(OnApplyAsync);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));
    }

    public string AlbumTitle { get; }
    public string ArtistName { get; }

    public ObservableCollection<TrackMatchRow> Rows { get; }

    public bool HasRows => Rows.Count > 0;

    public string? StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsApplying
    {
        get => _isApplying;
        set => SetProperty(ref _isApplying, value);
    }

    public ICommand ApplyCommand { get; }
    public ICommand CancelCommand { get; }

    private async Task OnApplyAsync()
    {
        var accepted = Rows.Where(r => r.Accepted).ToList();
        if (accepted.Count == 0)
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
            return;
        }

        IsApplying = true;
        StatusText = $"Updating {accepted.Count} track{(accepted.Count == 1 ? string.Empty : "s")}...";
        var updated = 0;
        foreach (var row in accepted)
        {
            var track = row.OriginalTrack;
            var matchedTitle = row.Candidate.MatchedTitle;
            if (string.IsNullOrWhiteSpace(matchedTitle) || matchedTitle.Equals(track.Title, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                await _libraryService.UpdateTrackMetadataAsync(
                    track.Id,
                    matchedTitle,
                    track.ArtistName,
                    track.AlbumTitle,
                    track.Year,
                    track.Genre,
                    track.TrackNumber,
                    track.DiscNumber);
                updated++;
            }
            catch (Exception)
            {
                // Per-track update failures should not abort the batch.
            }
        }

        IsApplying = false;
        StatusText = $"Updated {updated} track{(updated == 1 ? string.Empty : "s")} from MusicBrainz.";
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
