using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Dorado.Application.Interfaces;
using Dorado.Application.Models;
using Dorado.UI.ViewModels;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Editable Zune Card profile parity (2026-09-11 audit M-6 / Top-15 #12):
/// ZuneTag, status message and avatar must be user-editable and persisted.
/// </summary>
public class ZuneCardProfileEditTests
{
    private sealed class MemorySettingsStore : ISettingsStore
    {
        public AppSettings Current { get; set; } = new();
        public AppSettings Load() => Current;
        public void Save(AppSettings settings) => Current = settings;
    }

    private sealed class StubFolderPicker : IFolderPickerService
    {
        public string? FileToReturn { get; set; }
        public Task<string?> PickFolderAsync(string title = "Select Music Collection Folder") => Task.FromResult<string?>(null);
        public Task<string?> PickFileAsync(string title = "Select File", string extension = "*.*") => Task.FromResult(FileToReturn);
    }

    [Fact]
    public async Task SaveProfile_PersistsTagStatusAndAvatar()
    {
        var store = new MemorySettingsStore();
        var vm = new ZuneCardViewModel(new StubUserStats(), null, null, store);
        await vm.LoadStatsAsync();

        vm.BeginEditProfileCommand.Execute(null);
        Assert.True(vm.IsEditingProfile);

        vm.EditZuneTag = "NeonRunner";
        vm.EditStatusMessage = "Listening to Boards of Canada";
        vm.EditAvatarUri = "/tmp/a.png";
        vm.SaveProfileCommand.Execute(null);

        Assert.False(vm.IsEditingProfile);
        Assert.Equal("NeonRunner", vm.ZuneTag);
        Assert.Equal("Listening to Boards of Canada", vm.StatusMessage);
        Assert.True(vm.HasAvatar);
        Assert.Equal("NeonRunner", store.Current.ZuneTag);
        Assert.Equal("Listening to Boards of Canada", store.Current.ZuneStatusMessage);
        Assert.Equal("/tmp/a.png", store.Current.ZuneAvatarUri);
    }

    [Fact]
    public async Task LoadStats_AppliesPersistedProfile()
    {
        var store = new MemorySettingsStore
        {
            Current = new AppSettings
            {
                ZuneTag = "SavedTag",
                ZuneStatusMessage = "SavedStatus",
                ZuneAvatarUri = "/x.png"
            }
        };
        var vm = new ZuneCardViewModel(new StubUserStats(), null, null, store);
        await vm.LoadStatsAsync();

        Assert.Equal("SavedTag", vm.ZuneTag);
        Assert.Equal("SavedStatus", vm.StatusMessage);
        Assert.True(vm.HasAvatar);
    }

    [Fact]
    public async Task PickAvatar_SetsEditUri()
    {
        var picker = new StubFolderPicker { FileToReturn = "/pic/avatar.png" };
        var vm = new ZuneCardViewModel(new StubUserStats(), null, null, null, picker);

        await ((AsyncRelayCommand)vm.PickAvatarCommand).ExecuteAsync(null);

        Assert.Equal("/pic/avatar.png", vm.EditAvatarUri);
    }

    [Fact]
    public async Task CancelEdit_DoesNotPersist()
    {
        var store = new MemorySettingsStore();
        var vm = new ZuneCardViewModel(new StubUserStats(), null, null, store);
        await vm.LoadStatsAsync();
        var originalTag = vm.ZuneTag;

        vm.BeginEditProfileCommand.Execute(null);
        vm.EditZuneTag = "Discarded";
        vm.CancelEditProfileCommand.Execute(null);

        Assert.False(vm.IsEditingProfile);
        Assert.Equal(originalTag, vm.ZuneTag);
        Assert.NotEqual("Discarded", store.Current.ZuneTag);
    }
}
