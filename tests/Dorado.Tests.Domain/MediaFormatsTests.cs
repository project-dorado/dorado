using Dorado.Domain.Models;
using Xunit;

namespace Dorado.Tests.Domain;

/// <summary>
/// Locks the shared media-format contract. The HD Kotlin mirror asserts the
/// same lists, so a change on one side without the other fails one of the two
/// parity tests.
/// </summary>
public sealed class MediaFormatsTests
{
    [Fact]
    public void HdPlayableSet_IsASubsetOfIngestSet()
    {
        // Everything the device plays must be something the desktop can ingest.
        foreach (var extension in MediaFormats.HdPlayableExtensions)
        {
            Assert.Contains(extension, MediaFormats.IngestExtensions);
        }
    }

    [Fact]
    public void HdPlayableExtensions_MatchTheKotlinMirror()
    {
        // Canonical list; keep in sync with dorado-hd/.../MediaFormats.kt.
        Assert.Equal(
            new[] { "mp3", "m4a", "aac", "flac", "ogg", "opus", "mp4", "m4v" },
            MediaFormats.HdPlayableExtensions);
    }

    [Fact]
    public void Wma_IsNotDevicePlayable_AndTranscodesToAac()
    {
        Assert.False(MediaFormats.IsHdPlayable("wma"));
        Assert.True(MediaFormats.NeedsTranscode("wma"));
        Assert.Equal("m4a", MediaFormats.TranscodeTargetFor("wma"));
    }

    [Theory]
    [InlineData("mp3")]
    [InlineData("m4a")]
    [InlineData("flac")]
    [InlineData(".MP3")]
    [InlineData("  ogg  ")]
    public void DevicePlayableFormats_AreCopiedVerbatim(string extension)
    {
        Assert.True(MediaFormats.IsHdPlayable(extension));
        Assert.Null(MediaFormats.TranscodeTargetFor(extension));
        Assert.False(MediaFormats.NeedsTranscode(extension));
    }

    [Fact]
    public void UnknownFormats_FallBackToAac()
    {
        Assert.Equal("m4a", MediaFormats.TranscodeTargetFor("xyz"));
    }

    [Fact]
    public void IngestExtensions_MatchTheDefaultSettingsLine()
    {
        // AppSettings.IngestExtensions default is "mp3,m4a,m4b,wma,mp4,m4v,flac,ogg,opus,aac".
        Assert.Equal(
            new[] { "mp3", "m4a", "m4b", "wma", "mp4", "m4v", "flac", "ogg", "opus", "aac" },
            MediaFormats.IngestExtensions);
    }
}
