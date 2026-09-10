using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace Dorado.UI.Controls;

/// <summary>
/// Reusable Zune 4.8 A-Z alphabet jump-list (JUMPLIST / JUMPINLIST parity). Renders the
/// supplied <see cref="Letters"/> as a compact borderless strip; clicking a letter invokes
/// <see cref="JumpCommand"/> with the letter as its parameter.
/// </summary>
public partial class ZuneJumpListControl : UserControl
{
    public static readonly StyledProperty<IEnumerable?> LettersProperty =
        AvaloniaProperty.Register<ZuneJumpListControl, IEnumerable?>(nameof(Letters));

    public static readonly StyledProperty<ICommand?> JumpCommandProperty =
        AvaloniaProperty.Register<ZuneJumpListControl, ICommand?>(nameof(JumpCommand));

    public ZuneJumpListControl()
    {
        InitializeComponent();
    }

    /// <summary>The ordered set of anchor letters (e.g. "#", "A".."Z").</summary>
    public IEnumerable? Letters
    {
        get => GetValue(LettersProperty);
        set => SetValue(LettersProperty, value);
    }

    /// <summary>Invoked with the selected letter string when the user taps an anchor.</summary>
    public ICommand? JumpCommand
    {
        get => GetValue(JumpCommandProperty);
        set => SetValue(JumpCommandProperty, value);
    }
}
