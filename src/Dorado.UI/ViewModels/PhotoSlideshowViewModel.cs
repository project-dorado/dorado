using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Timers;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Dorado.Domain.Models;

namespace Dorado.UI.ViewModels;

/// <summary>
/// Full-bleed photo slideshow with slow Ken-Burns drift and transport
/// (SLIDESHOWLAND / PHOTOSLIDESHOW parity, authentic SLIDESHOW.* transport assets).
/// </summary>
public class PhotoSlideshowViewModel : ViewModelBase
{
    private static readonly Random _random = new();

    private readonly System.Timers.Timer _advanceTimer;

    public ObservableCollection<Photo> Photos { get; }

    private int _index;
    private Photo? _currentPhoto;
    public Photo? CurrentPhoto
    {
        get => _currentPhoto;
        private set
        {
            if (SetProperty(ref _currentPhoto, value))
            {
                OnPropertyChanged(nameof(CurrentTitle));
                OnPropertyChanged(nameof(PositionText));
            }
        }
    }

    private bool _isPlaying = true;
    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                OnPropertyChanged(nameof(PlayPauseIcon));
            }
        }
    }

    private double _kenBurnsScale = 1.0;
    public double KenBurnsScale
    {
        get => _kenBurnsScale;
        set => SetProperty(ref _kenBurnsScale, value);
    }

    private double _kenBurnsTranslateX;
    public double KenBurnsTranslateX
    {
        get => _kenBurnsTranslateX;
        set => SetProperty(ref _kenBurnsTranslateX, value);
    }

    private double _kenBurnsTranslateY;
    public double KenBurnsTranslateY
    {
        get => _kenBurnsTranslateY;
        set => SetProperty(ref _kenBurnsTranslateY, value);
    }

    public event EventHandler? RequestClose;

    public PhotoSlideshowViewModel(ObservableCollection<Photo> photos, int startIndex = 0)
    {
        Photos = new ObservableCollection<Photo>(photos);
        if (Photos.Count > 0)
        {
            _index = Math.Clamp(startIndex, 0, Photos.Count - 1);
            CurrentPhoto = Photos[_index];
            RandomizeKenBurns();
        }

        _advanceTimer = new System.Timers.Timer(6000) { AutoReset = true };
        _advanceTimer.Elapsed += (_, _) => Next();
        if (Photos.Count > 1)
        {
            _advanceTimer.Start();
        }

        NextCommand = new RelayCommand(Next);
        BackCommand = new RelayCommand(Back);
        PlayPauseCommand = new RelayCommand(OnPlayPause);
        CloseCommand = new RelayCommand(OnClose);
    }

    public string CurrentTitle => CurrentPhoto?.Title ?? "Slideshow";
    public string PositionText => Photos.Count == 0 ? string.Empty : $"{_index + 1} of {Photos.Count}";
    public string PlayPauseIcon => IsPlaying ? "PAUSE" : "PLAY";

    public ICommand NextCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand PlayPauseCommand { get; }
    public ICommand CloseCommand { get; }

    private void Next()
    {
        if (Photos.Count == 0)
        {
            return;
        }

        _index = (_index + 1) % Photos.Count;
        CurrentPhoto = Photos[_index];
        RandomizeKenBurns();
    }

    private void Back()
    {
        if (Photos.Count == 0)
        {
            return;
        }

        _index = (_index - 1 + Photos.Count) % Photos.Count;
        CurrentPhoto = Photos[_index];
        RandomizeKenBurns();
    }

    private void OnPlayPause()
    {
        IsPlaying = !IsPlaying;
        if (IsPlaying)
        {
            _advanceTimer.Start();
        }
        else
        {
            _advanceTimer.Stop();
        }
    }

    private void OnClose()
    {
        _advanceTimer.Stop();
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private void RandomizeKenBurns()
    {
        KenBurnsScale = 1.0 + (_random.NextDouble() * 0.12);
        KenBurnsTranslateX = (_random.NextDouble() * 40) - 20;
        KenBurnsTranslateY = (_random.NextDouble() * 30) - 15;
    }
}
