using System;

namespace Dorado.UI.Views;

/// <summary>
/// Accumulates typed letters into a jump prefix, resetting after a short idle window
/// (Zune's `JUMPINLIST.UIX` buffer semantics).
/// </summary>
public sealed class TypeAheadBuffer
{
    public static readonly TimeSpan ResetWindow = TimeSpan.FromMilliseconds(900);

    private string _prefix = string.Empty;
    private DateTime _lastInput = DateTime.MinValue;

    public string Prefix => _prefix;

    public string Append(char character, DateTime now)
    {
        if ((now - _lastInput) > ResetWindow)
        {
            _prefix = string.Empty;
        }

        _prefix += char.ToLowerInvariant(character);
        _lastInput = now;
        return _prefix;
    }

    public void Reset()
    {
        _prefix = string.Empty;
        _lastInput = DateTime.MinValue;
    }
}
