using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Covers the Dorado Cloud section of the Settings screen: status text, the
/// sign-in/sign-out commands, and — critically — that saving unrelated settings
/// does not wipe the token owned by the sign-in service.
/// </summary>
public sealed class SettingsCloudTests
{
    [Fact]
    public void CloudStatus_ReflectsDisabledSignedOutSignedIn()
    {
        Assert.Equal("cloud disabled", NewVm(new AppSettings { CloudEnabled = false }).CloudStatusText);
        Assert.Equal("not signed in", NewVm(new AppSettings { CloudEnabled = true }).CloudStatusText);
        Assert.Equal("signed in", NewVm(new AppSettings { CloudEnabled = true, CloudAccessToken = "tok" }).CloudStatusText);
    }

    [Fact]
    public async Task CloudSignInCommand_InvokesServiceAndEnablesCloud()
    {
        var store = new FakeSettingsStore(new AppSettings
        {
            CloudEnabled = false,
            CloudBaseUrl = "https://cloud.dorado.example/",
        });
        var signIn = new FakeSignInService { Result = true };
        var vm = new SettingsViewModel(settingsStore: store, cloudSignIn: signIn);

        await ((IAsyncRelayCommand)vm.CloudSignInCommand).ExecuteAsync(null);

        Assert.Equal(1, signIn.SignInCalls);
        Assert.True(vm.CloudEnabled);
        Assert.Equal("signed in", vm.CloudStatusText);
    }

    [Fact]
    public void CloudSignOutCommand_InvokesService()
    {
        var signIn = new FakeSignInService();
        var vm = new SettingsViewModel(settingsStore: new FakeSettingsStore(new AppSettings()), cloudSignIn: signIn);

        vm.CloudSignOutCommand.Execute(null);

        Assert.Equal(1, signIn.SignOutCalls);
    }

    [Fact]
    public void SavingUnrelatedSettings_PreservesCloudToken()
    {
        var store = new FakeSettingsStore(new AppSettings
        {
            CloudEnabled = true,
            CloudBaseUrl = "https://cloud.dorado.example/",
            CloudAccessToken = "secret-token",
            CloudAccessTokenExpiresAtUtc = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LanSyncEnabled = true,
            LanSyncPort = 9999,
            EmulatorCliPath = "/opt/dorado/emu",
        });
        var vm = new SettingsViewModel(settingsStore: store);

        vm.CloudHandle = "jane"; // triggers SaveCurrentSettings

        Assert.Equal("secret-token", store.Current.CloudAccessToken);
        Assert.Equal(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc), store.Current.CloudAccessTokenExpiresAtUtc);
        Assert.True(store.Current.LanSyncEnabled);
        Assert.Equal(9999, store.Current.LanSyncPort);
        Assert.Equal("/opt/dorado/emu", store.Current.EmulatorCliPath);
        Assert.Equal("jane", store.Current.CloudHandle);
    }

    private static SettingsViewModel NewVm(AppSettings settings) =>
        new(settingsStore: new FakeSettingsStore(settings));

    private sealed class FakeSignInService : ICloudSignInService
    {
        public bool Result { get; set; } = true;
        public int SignInCalls { get; private set; }
        public int SignOutCalls { get; private set; }

        public Task<bool> SignInAsync(CancellationToken cancellationToken = default)
        {
            SignInCalls++;
            return Task.FromResult(Result);
        }

        public void SignOut() => SignOutCalls++;
    }
}
