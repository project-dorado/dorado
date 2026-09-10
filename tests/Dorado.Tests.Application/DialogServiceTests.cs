using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Dorado.Application.Interfaces;
using Dorado.Application.Services;
using Dorado.Tests.Application.TestFakes;
using Dorado.UI.ViewModels;

namespace Dorado.Tests.Application;

public class DialogServiceTests
{
    [Fact]
    public async Task Without_a_handler_confirm_declines_and_alert_completes()
    {
        var dialog = new DialogService();

        Assert.False(await dialog.ConfirmAsync(new DialogRequest("T", "M")));
        await dialog.AlertAsync("T", "M"); // must not throw
    }

    [Fact]
    public async Task Handler_receives_requests_and_controls_the_result()
    {
        var dialog = new DialogService();
        DialogRequest? captured = null;
        dialog.ConfirmHandler = (request, _) =>
        {
            captured = request;
            return Task.FromResult(true);
        };

        Assert.True(await dialog.ConfirmAsync(new DialogRequest("Title", "Message", "YES", "NO", true)));
        Assert.NotNull(captured);
        Assert.Equal("Title", captured!.Title);
        Assert.True(captured.IsDestructive);

        // Alerts are confirmations with no cancel button.
        await dialog.AlertAsync("Alert", "Body");
        Assert.Equal("Alert", captured!.Title);
        Assert.Null(captured.CancelText);
    }

    [AvaloniaFact]
    public async Task Shell_renders_confirmation_and_resolves_on_button()
    {
        var dialog = new DialogService();
        var shell = new MainShellViewModel(
            new PlaybackQueueCoordinator(),
            new FakeMediaLibraryService(),
            new FakeDeviceSyncService(),
            new SmartDJEngine(),
            dialogService: dialog);

        Assert.NotNull(dialog.ConfirmHandler);

        var task = dialog.ConfirmAsync(new DialogRequest("Delete playlist", "Sure?", "DELETE", "CANCEL", true));
        Dispatcher.UIThread.RunJobs();

        Assert.True(shell.IsDialogOpen);
        Assert.Equal("Delete playlist", shell.DialogTitle);
        Assert.True(shell.HasDialogCancel);

        shell.ConfirmDialogCommand.Execute(null);
        Assert.True(await task);
        Assert.False(shell.IsDialogOpen);
    }

    [AvaloniaFact]
    public async Task Shell_cancel_resolves_false()
    {
        var dialog = new DialogService();
        var shell = new MainShellViewModel(
            new PlaybackQueueCoordinator(),
            new FakeMediaLibraryService(),
            new FakeDeviceSyncService(),
            new SmartDJEngine(),
            dialogService: dialog);

        var task = dialog.ConfirmAsync(new DialogRequest("T", "M"));
        Dispatcher.UIThread.RunJobs();
        shell.CancelDialogCommand.Execute(null);

        Assert.False(await task);
        Assert.False(shell.IsDialogOpen);
    }
}
