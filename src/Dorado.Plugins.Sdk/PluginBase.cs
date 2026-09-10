using Dorado.Plugins.Protocol.Dto;
using Dorado.Plugins.Protocol.Messages;

namespace Dorado.Plugins.Sdk;

public interface IPluginLogger
{
    void LogInformation(string message, params object[] args);
    void LogWarning(string message, params object[] args);
    void LogError(string message, Exception? exception, params object[] args);
}

public interface IPluginHostContext
{
    IPluginLogger Logger { get; }
    Task<string?> GetSecureStorageAsync(string key);
    Task SetSecureStorageAsync(string key, string value);
    Task ShowToastAsync(string title, string message);
}

public interface IPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    string Author { get; }
    string Description { get; }

    Task InitializeAsync(IPluginHostContext context, CancellationToken cancellationToken);
    Task ShutdownAsync(CancellationToken cancellationToken);
    Task HandleEventAsync(string eventName, object? eventData);
}

public abstract class PluginBase : IPlugin
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Version { get; }
    public abstract string Author { get; }
    public abstract string Description { get; }

    protected IPluginHostContext Context { get; private set; } = null!;
    protected IPluginLogger Logger => Context.Logger;

    private readonly Dictionary<string, List<Func<object?, Task>>> _handlers = new();

    public virtual async Task InitializeAsync(IPluginHostContext context, CancellationToken cancellationToken)
    {
        Context = context;
        await OnStartAsync(cancellationToken);
    }

    public virtual async Task ShutdownAsync(CancellationToken cancellationToken)
    {
        await OnStopAsync(cancellationToken);
    }

    public virtual async Task HandleEventAsync(string eventName, object? eventData)
    {
        if (_handlers.TryGetValue(eventName, out var list))
        {
            foreach (var handler in list)
            {
                await handler(eventData);
            }
        }
    }

    protected void Subscribe<TEvent>(string eventName, Func<TEvent, Task> handler) where TEvent : class
    {
        if (!_handlers.ContainsKey(eventName))
        {
            _handlers[eventName] = new List<Func<object?, Task>>();
        }

        _handlers[eventName].Add(obj =>
        {
            if (obj is TEvent typedEvent)
            {
                return handler(typedEvent);
            }
            return Task.CompletedTask;
        });
    }

    protected virtual Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
