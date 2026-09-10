using Dorado.Application.Interfaces;

namespace Dorado.Application.Services;

/// <summary>
/// Default <see cref="IDialogService"/>: delegates to a handler installed by the shell
/// host. With no handler (headless/tests) confirmations resolve to <c>false</c> and
/// alerts complete without blocking.
/// </summary>
public sealed class DialogService : IDialogService
{
    public Func<DialogRequest, CancellationToken, Task<bool>>? ConfirmHandler { get; set; }

    public Task<bool> ConfirmAsync(DialogRequest request, CancellationToken cancellationToken = default)
        => ConfirmHandler?.Invoke(request, cancellationToken) ?? Task.FromResult(false);

    public async Task AlertAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        await ConfirmAsync(new DialogRequest(title, message, "OK", null), cancellationToken).ConfigureAwait(false);
    }
}
