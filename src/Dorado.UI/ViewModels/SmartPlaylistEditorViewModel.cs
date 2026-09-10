using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Interfaces;
using Dorado.Domain.Models;

namespace Dorado.UI.ViewModels;

public class SmartRuleRow : ViewModelBase
{
    private static readonly string[] StringOperators = { "is", "is not", "contains", "does not contain" };
    private static readonly string[] NumberOperators = { "is", "greater than", "less than" };
    private static readonly string[] LastPlayedOperators = { "within last days" };

    public static readonly string[] FieldNames =
    {
        "Genre", "Artist", "Album", "Rating (0-2)", "Play Count", "Last Played", "Year"
    };

    public SmartRuleRow(SmartPlaylistRule rule)
    {
        _rule = rule;
        _fieldIndex = Math.Clamp((int)rule.Field, 0, FieldNames.Length - 1);
        RefreshOperators();
        _operatorIndex = Math.Clamp(AvailableOperators.ToList().IndexOf(OperatorName(rule.Operator)), 0, Math.Max(0, AvailableOperators.Count - 1));
        _value = rule.Value;
        RemoveCommand = new RelayCommand(() => RequestRemove?.Invoke(this, EventArgs.Empty));
    }

    private readonly SmartPlaylistRule _rule;
    private int _fieldIndex;
    private int _operatorIndex;
    private string _value = string.Empty;

    public event EventHandler? RequestRemove;

    public int FieldIndex
    {
        get => _fieldIndex;
        set
        {
            if (SetProperty(ref _fieldIndex, value))
            {
                RefreshOperators();
                OperatorIndex = 0;
            }
        }
    }

    public int OperatorIndex
    {
        get => _operatorIndex;
        set => SetProperty(ref _operatorIndex, value);
    }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public ObservableCollection<string> AvailableOperators { get; } = new();

    public ICommand RemoveCommand { get; }

    public SmartPlaylistRule ToRule()
    {
        return new SmartPlaylistRule
        {
            Field = (SmartRuleField)_fieldIndex,
            Operator = ParseOperator(FieldIndex, AvailableOperators.Count > _operatorIndex ? AvailableOperators[_operatorIndex] : string.Empty),
            Value = Value
        };
    }

    private void RefreshOperators()
    {
        AvailableOperators.Clear();
        var operators = ((SmartRuleField)_fieldIndex) switch
        {
            SmartRuleField.Rating or SmartRuleField.PlayCount or SmartRuleField.Year => NumberOperators,
            SmartRuleField.LastPlayed => LastPlayedOperators,
            _ => StringOperators
        };
        foreach (var op in operators)
        {
            AvailableOperators.Add(op);
        }
    }

    private static string OperatorName(SmartRuleOperator op) => op switch
    {
        SmartRuleOperator.Is => "is",
        SmartRuleOperator.IsNot => "is not",
        SmartRuleOperator.Contains => "contains",
        SmartRuleOperator.NotContains => "does not contain",
        SmartRuleOperator.GreaterThan => "greater than",
        SmartRuleOperator.LessThan => "less than",
        SmartRuleOperator.WithinLastDays => "within last days",
        _ => "is"
    };

    private static SmartRuleOperator ParseOperator(int fieldIndex, string name)
    {
        var field = (SmartRuleField)fieldIndex;
        return field switch
        {
            SmartRuleField.Rating or SmartRuleField.PlayCount or SmartRuleField.Year => name switch
            {
                "greater than" => SmartRuleOperator.GreaterThan,
                "less than" => SmartRuleOperator.LessThan,
                _ => SmartRuleOperator.Is
            },
            SmartRuleField.LastPlayed => SmartRuleOperator.WithinLastDays,
            _ => name switch
            {
                "is not" => SmartRuleOperator.IsNot,
                "contains" => SmartRuleOperator.Contains,
                "does not contain" => SmartRuleOperator.NotContains,
                _ => SmartRuleOperator.Is
            }
        };
    }
}

public class SmartPlaylistEditorViewModel : ViewModelBase
{
    private static readonly string[] SortFields = { "Title", "ArtistName", "PlayCount", "LastPlayed", "Year", "Duration" };

    private readonly ISmartPlaylistService _smartPlaylistService;
    private readonly IMediaLibraryService _libraryService;
    private readonly SmartPlaylist _playlist;
    private readonly Action<SmartPlaylist>? _onSaved;

    private string _name = string.Empty;
    private bool _matchAll = true;
    private int _sortFieldIndex;
    private bool _sortDescending;
    private int _trackLimit = 50;
    private string? _previewText;
    private string? _errorMessage;

    public event EventHandler? RequestClose;

    public SmartPlaylistEditorViewModel(
        SmartPlaylist playlist,
        ISmartPlaylistService smartPlaylistService,
        IMediaLibraryService libraryService,
        Action<SmartPlaylist>? onSaved = null)
    {
        _playlist = playlist;
        _smartPlaylistService = smartPlaylistService;
        _libraryService = libraryService;
        _onSaved = onSaved;

        _name = playlist.Name;
        _matchAll = playlist.Match == SmartPlaylistMatch.All;
        _sortFieldIndex = Math.Max(0, SortFields.ToList().IndexOf(playlist.SortField));
        _sortDescending = playlist.SortDescending;
        _trackLimit = playlist.TrackLimit;

        Rules = new ObservableCollection<SmartRuleRow>(playlist.Rules.Select(r => new SmartRuleRow(r)));
        foreach (var row in Rules)
        {
            row.RequestRemove += OnRuleRemove;
        }

        AddRuleCommand = new RelayCommand(OnAddRule);
        RemoveRuleCommand = new RelayCommand<SmartRuleRow>(OnRuleRemove);
        PreviewCommand = new AsyncRelayCommand(OnPreviewAsync);
        SaveCommand = new AsyncRelayCommand(OnSaveAsync);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));
    }

    public string Title => _playlist.Name.Length == 0 ? "NEW SMART PLAYLIST" : "EDIT SMART PLAYLIST";

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool MatchAll
    {
        get => _matchAll;
        set => SetProperty(ref _matchAll, value);
    }

    public bool MatchAny => !MatchAll;

    public ObservableCollection<SmartRuleRow> Rules { get; }

    public ObservableCollection<string> SortFieldOptions { get; } = new(SortFields);

    public int SortFieldIndex
    {
        get => _sortFieldIndex;
        set => SetProperty(ref _sortFieldIndex, value);
    }

    public bool SortDescending
    {
        get => _sortDescending;
        set => SetProperty(ref _sortDescending, value);
    }

    public int TrackLimit
    {
        get => _trackLimit;
        set => SetProperty(ref _trackLimit, value);
    }

    public string? PreviewText
    {
        get => _previewText;
        set => SetProperty(ref _previewText, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public ICommand AddRuleCommand { get; }
    public ICommand RemoveRuleCommand { get; }
    public ICommand PreviewCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    private void OnAddRule()
    {
        var row = new SmartRuleRow(new SmartPlaylistRule());
        row.RequestRemove += OnRuleRemove;
        Rules.Add(row);
    }

    private void OnRuleRemove(object? sender, EventArgs e)
    {
        if (sender is SmartRuleRow row)
        {
            Rules.Remove(row);
        }
    }

    private void OnRuleRemove(SmartRuleRow? row)
    {
        if (row != null)
        {
            Rules.Remove(row);
        }
    }

    private async Task OnPreviewAsync()
    {
        ErrorMessage = null;
        var tracks = await _libraryService.GetAllTracksAsync();
        var matches = _smartPlaylistService.Evaluate(BuildPlaylist(), tracks);
        PreviewText = $"{matches.Count} song{(matches.Count == 1 ? string.Empty : "s")} match";
    }

    private async Task OnSaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Give the smart playlist a name.";
            return;
        }

        if (Rules.Count == 0)
        {
            ErrorMessage = "Add at least one rule.";
            return;
        }

        var playlist = BuildPlaylist();
        try
        {
            await _smartPlaylistService.SaveAsync(playlist);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Save failed: {ex.Message}";
            return;
        }

        _onSaved?.Invoke(playlist);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private SmartPlaylist BuildPlaylist()
    {
        _playlist.Name = Name.Trim();
        _playlist.Match = MatchAll ? SmartPlaylistMatch.All : SmartPlaylistMatch.Any;
        _playlist.SortField = SortFieldOptions.Count > SortFieldIndex ? SortFieldOptions[SortFieldIndex] : "Title";
        _playlist.SortDescending = SortDescending;
        _playlist.TrackLimit = Math.Max(0, TrackLimit);
        _playlist.Rules = Rules.Select(r => r.ToRule()).ToList();
        _playlist.UpdatedAtUtc = DateTime.UtcNow;
        return _playlist;
    }
}
