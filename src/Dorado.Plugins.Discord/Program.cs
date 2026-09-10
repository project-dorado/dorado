using Dorado.Plugins.Discord;
using Dorado.Plugins.Sdk;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await PluginRuntime.RunAsync(new DiscordPlugin(), new StdioLineTransport(), cts.Token);
