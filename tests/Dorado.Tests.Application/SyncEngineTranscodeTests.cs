using Dorado.Application.Interfaces;
using Dorado.Application.Services;
using Dorado.Domain.Models;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Verifies the transcode step inside <see cref="SyncEngine.ApplyPlanAsync"/>:
/// device-playable sources copy verbatim, unsupported ones are transcoded first,
/// and the temporary transcode is cleaned up.
/// </summary>
public sealed class SyncEngineTranscodeTests
{
    [Fact]
    public async Task ApplyPlan_TranscodesUnsupportedSource()
    {
        var transcode = new FakeTranscodeService();
        var engine = new SyncEngine(transcode: transcode);
        var transport = new RecordingTransport();

        await engine.ApplyPlanAsync(Plan("/music/track.wma"), transport);

        Assert.Equal("m4a", transcode.LastTarget);
        Assert.Single(transport.Copies);
        Assert.EndsWith(".m4a", transport.Copies[0].SourcePath);
        // The temporary transcode is removed after the copy.
        Assert.False(File.Exists(transcode.OutputPath));
    }

    [Fact]
    public async Task ApplyPlan_CopiesPlayableSourceVerbatim()
    {
        var transcode = new FakeTranscodeService();
        var engine = new SyncEngine(transcode: transcode);
        var transport = new RecordingTransport();

        await engine.ApplyPlanAsync(Plan("/music/track.mp3"), transport);

        Assert.Null(transcode.LastTarget);
        Assert.Single(transport.Copies);
        Assert.Equal("/music/track.mp3", transport.Copies[0].SourcePath);
    }

    [Fact]
    public async Task ApplyPlan_CopiesVerbatimWhenTranscoderUnavailable()
    {
        var transcode = new FakeTranscodeService { Available = false };
        var engine = new SyncEngine(transcode: transcode);
        var transport = new RecordingTransport();

        await engine.ApplyPlanAsync(Plan("/music/track.wma"), transport);

        Assert.Null(transcode.LastTarget);
        Assert.Equal("/music/track.wma", transport.Copies[0].SourcePath);
    }

    private static SyncPlan Plan(string sourcePath) => new()
    {
        DeviceSerialNumber = "SERIAL-1",
        Items = new List<TransferItem>
        {
            new()
            {
                Action = TransferAction.Add,
                Category = SyncCategoryType.Music,
                EntityId = Guid.NewGuid(),
                Title = "Track",
                SourcePath = sourcePath,
                SizeBytes = 1024,
            },
        },
    };

    private sealed class FakeTranscodeService : ITranscodeService
    {
        public bool Available { get; set; } = true;
        public bool IsAvailable => Available;
        public string? LastTarget { get; private set; }
        public string OutputPath { get; private set; } = string.Empty;

        public Task<TranscodeResult> TranscodeAsync(
            string sourcePath, string targetContainer, string outputDirectory,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            LastTarget = targetContainer;
            Directory.CreateDirectory(outputDirectory);
            OutputPath = Path.Combine(outputDirectory, $"transcoded-{Guid.NewGuid():N}.{targetContainer}");
            File.WriteAllBytes(OutputPath, new byte[] { 1, 2, 3, 4 });
            return Task.FromResult(new TranscodeResult(true, OutputPath, null));
        }
    }

    private sealed class RecordingTransport : IDeviceTransport
    {
        public List<TransferItem> Copies { get; } = new();
        public string DeviceSerialNumber => "SERIAL-1";
        public string DeviceName => "Device";
        public long TotalCapacityBytes => 1_000_000;
        public long SystemBytes => 0;
        public long UsedBytes => 0;
        public long FreeBytes => TotalCapacityBytes;

        public IReadOnlyList<DeviceContentItem> GetContents() => Array.Empty<DeviceContentItem>();
        public bool TryGetItem(Guid entityId, out DeviceContentItem item) { item = null!; return false; }
        public void CopyToDevice(TransferItem item) => Copies.Add(item);
        public void RemoveFromDevice(DeviceContentItem item) { }
    }
}
