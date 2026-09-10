using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Dorado.Application.Interfaces;
using Dorado.Application.Services;
using Dorado.Domain.Models;
using Dorado.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Dorado.Tests.Application.TestFakes;
using Dorado.UI.ViewModels;

namespace Dorado.Tests.Application;

public class ReputationEngineTests
{
    [Theory]
    [InlineData(9, BadgeTier.None)]
    [InlineData(10, BadgeTier.Bronze)]
    [InlineData(29, BadgeTier.Bronze)]
    [InlineData(30, BadgeTier.Silver)]
    [InlineData(74, BadgeTier.Silver)]
    [InlineData(75, BadgeTier.Gold)]
    [InlineData(5000, BadgeTier.Gold)]
    public void Album_power_listener_tiers(int plays, BadgeTier expected)
    {
        Assert.Equal(expected, ReputationEngine.AlbumPowerListener(plays).Tier);
    }

    [Fact]
    public void Tier_progress_points_at_the_next_threshold()
    {
        var bronze = ReputationEngine.ArtistPowerListener(50);
        Assert.Equal(BadgeTier.Bronze, bronze.Tier);
        Assert.Equal(75, bronze.NextThreshold);

        var locked = ReputationEngine.Milestone(0);
        Assert.Equal(BadgeTier.None, locked.Tier);
        Assert.Equal(100, locked.NextThreshold);
        Assert.False(locked.IsUnlocked);
    }

    [Fact]
    public void Reaching_a_tier_never_expires()
    {
        var early = ReputationEngine.Milestone(100);
        var later = ReputationEngine.Milestone(2000);

        Assert.Equal(BadgeTier.Bronze, early.Tier);
        Assert.Equal(BadgeTier.Silver, later.Tier);
        Assert.True(early.IsUnlocked);
        Assert.True(later.IsUnlocked);
    }

    [Fact]
    public void Reviews_and_curator_map_to_their_tiers()
    {
        Assert.Equal(BadgeTier.Bronze, ReputationEngine.Reviewer(1).Tier);
        Assert.Equal(BadgeTier.Gold, ReputationEngine.Reviewer(20).Tier);
        Assert.Equal(BadgeTier.None, ReputationEngine.Curator(2).Tier);
        Assert.Equal(BadgeTier.Silver, ReputationEngine.Curator(10).Tier);
    }
}

public class ReviewServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly SqliteReviewService _service;

    public ReviewServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dorado_reviews_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_dbPath}").Options;
        _factory = new Factory(options);
        using var ctx = _factory.CreateDbContext();
        ctx.Database.EnsureCreated();
        _service = new SqliteReviewService(_factory);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    [Fact]
    public async Task Counts_and_filters_reviews_by_album()
    {
        var albumId = Guid.NewGuid();
        await _service.AddReviewAsync(new Review { AlbumId = albumId, AlbumTitle = "Signals", ArtistName = "Rush", Body = "Timeless." });
        await _service.AddReviewAsync(new Review { AlbumId = Guid.NewGuid(), AlbumTitle = "Discovery", ArtistName = "Daft Punk", Body = "Funky." });

        Assert.Equal(2, await _service.GetReviewCountAsync());
        Assert.Single(await _service.GetReviewsForAlbumAsync(albumId));
    }

    private sealed class Factory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public Factory(DbContextOptions<AppDbContext> options) => _options = options;
        public AppDbContext CreateDbContext() => new(_options);
    }
}

public class PromptDialogTests
{
    [AvaloniaFact]
    public async Task Prompt_returns_typed_value_on_confirm_and_null_on_cancel()
    {
        var dialog = new DialogService();
        var shell = new MainShellViewModel(
            new PlaybackQueueCoordinator(),
            new FakeMediaLibraryService(),
            new FakeDeviceSyncService(),
            new SmartDJEngine(),
            dialogService: dialog);

        var confirmed = dialog.PromptAsync(new DialogRequest("Write a review", "Body", "SAVE", "CANCEL"));
        Dispatcher.UIThread.RunJobs();
        Assert.True(shell.IsDialogOpen);
        Assert.True(shell.IsDialogPrompt);

        shell.DialogInput = "A masterpiece.";
        shell.ConfirmDialogCommand.Execute(null);
        Assert.Equal("A masterpiece.", await confirmed);

        var cancelled = dialog.PromptAsync(new DialogRequest("Write a review", "Body", "SAVE", "CANCEL"));
        Dispatcher.UIThread.RunJobs();
        shell.CancelDialogCommand.Execute(null);
        Assert.Null(await cancelled);
    }
}
