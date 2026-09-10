namespace Dorado.Application.Interfaces;

public sealed record DialogRequest(
    string Title,
    string Message,
    string ConfirmText = "OK",
    string? CancelText = "Cancel",
    bool IsDestructive = false);

/// <summary>
/// Modal confirmation/message surface. The in-shell implementation renders a Zune-style
/// overlay; view models depend only on this contract.
/// </summary>
public interface IDialogService
{
    Task<bool> ConfirmAsync(DialogRequest request, CancellationToken cancellationToken = default);

    Task AlertAsync(string title, string message, CancellationToken cancellationToken = default);
}
