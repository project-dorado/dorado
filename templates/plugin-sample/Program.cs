using Dorado.Plugin.Sample;
using Dorado.Plugins.Sdk;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// The host launches this executable and speaks JSON-RPC over stdin/stdout.
await PluginRuntime.RunAsync(new SamplePlugin(), new StdioLineTransport(), cts.Token);
