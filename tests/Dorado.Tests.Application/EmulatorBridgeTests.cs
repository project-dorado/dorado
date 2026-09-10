using System.Text.Json;
using Dorado.Infrastructure.Emulator;
using Dorado.Plugins.Protocol.Rpc;
using Xunit;

namespace Dorado.Tests.Application;

/// <summary>
/// Exercises the desktop-side emulator bridge against an in-memory fake that
/// mirrors the wire shape of <c>dorado-emu/src/Dorado.Cli.Ipc/EmulatorRpcServer</c>.
/// No sub-process is spawned, so these tests do not depend on the emulator
/// repo's build output — the JSON-RPC framing and payload parsing are what is
/// under test.
/// </summary>
public sealed class EmulatorBridgeTests
{
    [Fact]
    public async Task InspectAsync_MapsPackageInfo()
    {
        await WithBridgeAsync(async bridge =>
        {
            var info = await bridge.InspectAsync("/pkgs/Pong.zcp");

            Assert.Equal("Ccgame", info.Kind);
            Assert.Equal("Pong", info.Title);
            Assert.Equal("ZunePong.exe", info.StartupAssembly);
            Assert.Equal("Zune.v3.1", info.RuntimeProfile);
            Assert.False(info.Encrypted);
            Assert.Equal(2, info.EntryCount);
            Assert.Equal(2, info.FileCount);
            Assert.Equal(2, info.Files.Count);
            Assert.Equal("ZunePong.exe", info.Files[1].Path);
        });
    }

    [Fact]
    public async Task UnpackAsync_ReportsWrittenCount()
    {
        await WithBridgeAsync(async bridge =>
        {
            var result = await bridge.UnpackAsync("/pkgs/Pong.zcp", "/tmp/out");

            Assert.Equal(7, result.Written);
            Assert.Null(result.Error);
        });
    }

    [Fact]
    public async Task RefsAsync_MapsAssemblyReport()
    {
        await WithBridgeAsync(async bridge =>
        {
            var report = await bridge.RefsAsync("/tmp/app.exe");

            Assert.Contains("Microsoft.Xna.Framework, Version=3.1.0.0", report.AssemblyReferences);
            Assert.Contains("Microsoft.Xna.Framework.Game", report.TypeReferences);
            Assert.Single(report.XnaDerivedTypes);
            Assert.True(report.MemberReferences.ContainsKey("Microsoft.Xna.Framework.Game"));
        });
    }

    [Fact]
    public async Task RunAsync_ReturnsFrameHash()
    {
        await WithBridgeAsync(async bridge =>
        {
            var result = await bridge.RunAsync("/pkgs/Pong.zcp", frames: 20, hash: true);

            Assert.Equal(20, result.FramesRendered);
            Assert.Equal("ZunePong.exe::Main", result.EntryPoint);
            Assert.Equal(64, result.FrameSha256!.Length);
        });
    }

    [Fact]
    public async Task RunAsync_WhenEmulatorReturnsError_ThrowsBridgeException()
    {
        await WithBridgeAsync(async bridge =>
        {
            var ex = await Assert.ThrowsAsync<EmulatorBridgeException>(() => bridge.RunAsync("/missing.zcp"));
            Assert.Contains("package not found", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static async Task WithBridgeAsync(Func<JsonRpcEmulatorBridge, Task> body)
    {
        var (host, emulator) = InMemoryLineTransport.CreatePair();
        await using var fake = new FakeEmulator(emulator);
        await using var bridge = new JsonRpcEmulatorBridge(host, TimeSpan.FromSeconds(10));
        await bridge.StartAsync();
        await body(bridge);
    }
}

/// <summary>Minimal JSON-RPC responder that emulates the emulator CLI's replies.</summary>
internal sealed class FakeEmulator : IAsyncDisposable
{
    private readonly ILineTransport _transport;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;

    public FakeEmulator(ILineTransport transport)
    {
        _transport = transport;
        _loop = Task.Run(RespondAsync);
    }

    private async Task RespondAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var line = await _transport.ReadLineAsync(_cts.Token).ConfigureAwait(false);
                if (line is null)
                {
                    return;
                }

                using var doc = JsonDocument.Parse(line);
                if (!doc.RootElement.TryGetProperty("id", out var id) ||
                    !doc.RootElement.TryGetProperty("method", out var methodElement))
                {
                    continue;
                }

                var method = methodElement.GetString();
                // The error case is signaled by the package path.
                if (string.Equals(method, "run", StringComparison.Ordinal) &&
                    line.Contains("missing.zcp", StringComparison.Ordinal))
                {
                    await _transport.WriteLineAsync(
                        $"{{\"jsonrpc\":\"2.0\",\"id\":{id.GetRawText()},\"error\":{{\"code\":-32603,\"message\":\"package not found\"}}}}",
                        _cts.Token).ConfigureAwait(false);
                    continue;
                }

                var result = method switch
                {
                    "inspect" => InspectJson,
                    "unpack" => "{\"written\":7,\"outputDir\":\"/tmp/out\"}",
                    "refs" => RefsJson,
                    "run" => "{\"entryPoint\":\"ZunePong.exe::Main\",\"framesRendered\":20,\"frameSha256\":\"" + new string('a', 64) + "\"}",
                    _ => "{}",
                };
                await _transport.WriteLineAsync(
                    $"{{\"jsonrpc\":\"2.0\",\"id\":{id.GetRawText()},\"result\":{result}}}",
                    _cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    private const string InspectJson =
        "{\"kind\":\"Ccgame\",\"title\":\"Pong\",\"description\":\"Pong on you Zune.\"," +
        "\"executable\":\"ZunePong.exe\",\"startupAssembly\":\"ZunePong.exe\",\"platform\":\"Zune\"," +
        "\"guid\":\"\",\"runtimeProfile\":\"Zune.v3.1\",\"ccgameVersion\":\"\",\"encrypted\":false," +
        "\"entryCount\":2,\"fileCount\":2,\"files\":[" +
        "{\"path\":\"ZuneLib.dll\",\"container\":\"0\",\"size\":8192}," +
        "{\"path\":\"ZunePong.exe\",\"container\":\"1\",\"size\":17920}]}";

    private const string RefsJson =
        "{\"assemblyReferences\":[\"mscorlib, Version=3.5.0.0\",\"Microsoft.Xna.Framework, Version=3.1.0.0\"]," +
        "\"typeReferences\":[\"Microsoft.Xna.Framework.Game\",\"Microsoft.Xna.Framework.Graphics.SpriteBatch\"]," +
        "\"xnaDerivedTypes\":[\"PongGame.PongGame\"]," +
        "\"memberReferences\":{\"Microsoft.Xna.Framework.Game\":[\"Run\",\"Content\"],\"PongGame.PongGame\":[\"LoadContent\"]}}";

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { await _loop.WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
        _cts.Dispose();
    }

    public async Task StopAsync()
    {
        _cts.Cancel();
        try { await _loop.WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
    }
}
