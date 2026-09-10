using Dorado.Plugins.LastFm;
using Dorado.Plugins.Sdk;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await PluginRuntime.RunAsync(new LastFmPlugin(), new StdioLineTransport(), cts.Token);
