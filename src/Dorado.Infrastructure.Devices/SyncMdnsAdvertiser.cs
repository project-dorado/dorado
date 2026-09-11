using Makaretu.Dns;

namespace Dorado.Infrastructure.Devices;

/// <summary>
/// Advertises the LAN sync endpoint over multicast DNS as
/// <see cref="SyncProtocol.ServiceType"/> so the Dorado-HD Android client can
/// discover the desktop automatically (M8.2b). Best-effort: mDNS needs a
/// multicast-capable network interface, and failures never block the socket.
/// </summary>
public sealed class SyncMdnsAdvertiser : IDisposable
{
    private readonly MulticastService _mdns;
    private readonly ServiceDiscovery _discovery;
    private readonly ServiceProfile _profile;
    private bool _started;

    public SyncMdnsAdvertiser(string instanceName, int port, string serviceVersion = SyncEndpointHost.ServiceVersion)
    {
        _profile = new ServiceProfile(instanceName, SyncProtocol.ServiceType, (ushort)port);
        _profile.AddProperty("serverName", instanceName);
        _profile.AddProperty("version", serviceVersion);

        _mdns = new MulticastService();
        _discovery = new ServiceDiscovery(_mdns);
    }

    /// <summary>The fully-qualified service instance name advertised.</summary>
    public string ServiceInstanceName => _profile.FullyQualifiedName.ToString();

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _mdns.Start();
        _discovery.Advertise(_profile);
        _started = true;
    }

    public void Dispose()
    {
        if (!_started)
        {
            _mdns.Dispose();
            return;
        }

        try
        {
            _discovery.Unadvertise(_profile);
        }
        catch
        {
            // best-effort
        }

        _mdns.Dispose();
        _started = false;
    }
}
