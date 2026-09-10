using System;
using System.Collections.Generic;
using Dorado.UI.ViewModels;

namespace Dorado.UI.Navigation;

public enum NavigationDirection
{
    Forward,
    Back
}

/// <summary>
/// Immutable snapshot of an application view state within the Zune shell (PAGESTACK parity).
/// Stores both top-level hub coordinates and inner collection/media parameters.
/// </summary>
public record PageStackEntry(
    NavigationPivot Pivot,
    string HeaderTitle,
    CollectionMediaGroup MediaGroup = CollectionMediaGroup.Music,
    CollectionSubPivot SubPivot = CollectionSubPivot.Artists,
    object? DetailState = null);

/// <summary>
/// Zune 4.8 PageStack implementation (ZuneUI.PageStack parity).
/// Maintains an intra-application navigation history of up to 1024 states,
/// supporting granular Back navigation up through the media hierarchy.
/// </summary>
public class PageStack
{
    private readonly List<PageStackEntry> _entries = new();
    private uint _maximumStackSize = 1024;

    public PageStackEntry? CurrentEntry { get; private set; }

    public bool CanNavigateBack => _entries.Count > 0;

    public int Depth => _entries.Count;

    public NavigationDirection LastDirection { get; private set; } = NavigationDirection.Forward;

    public uint MaximumStackSize
    {
        get => _maximumStackSize;
        set
        {
            if (_maximumStackSize != value)
            {
                _maximumStackSize = value;
                TrimStack();
            }
        }
    }

    public event EventHandler? StackChanged;

    public void Push(PageStackEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (CurrentEntry != null)
        {
            _entries.Add(CurrentEntry);
        }

        CurrentEntry = entry;
        LastDirection = NavigationDirection.Forward;
        TrimStack();
        StackChanged?.Invoke(this, EventArgs.Empty);
    }

    public PageStackEntry? Pop()
    {
        if (_entries.Count == 0)
        {
            return null;
        }

        var previous = _entries[^1];
        _entries.RemoveAt(_entries.Count - 1);
        CurrentEntry = previous;
        LastDirection = NavigationDirection.Back;
        StackChanged?.Invoke(this, EventArgs.Empty);
        return previous;
    }

    public PageStackEntry? Peek()
    {
        return _entries.Count > 0 ? _entries[^1] : null;
    }

    public void Clear()
    {
        _entries.Clear();
        CurrentEntry = null;
        StackChanged?.Invoke(this, EventArgs.Empty);
    }

    private void TrimStack()
    {
        if (_maximumStackSize == 0 || _entries.Count <= _maximumStackSize)
        {
            return;
        }

        var overflow = _entries.Count - (int)_maximumStackSize;
        if (overflow > 0)
        {
            _entries.RemoveRange(0, overflow);
        }
    }
}
