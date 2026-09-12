using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dorado.Application.Interfaces;
using Dorado.Domain.Models;
using Tmds.DBus.Protocol;

namespace Dorado.Infrastructure.Audio;

/// <summary>
/// Linux MPRIS2 server (<c>org.mpris.MediaPlayer2.dorado</c>) exposing now-playing
/// state to the desktop shell and routing media-key commands back to the player.
/// Capability-guarded: if there is no session bus the instance reports
/// <see cref="IsAvailable"/> = false and does nothing.
///
/// Built on Tmds.DBus.Protocol 0.95.x (the patched line for GHSA-xrw6-gwf8-vvr9).
/// </summary>
public sealed class MprisMediaControls : ISystemMediaControls, IDisposable
{
    private const string ServiceName = "org.mpris.MediaPlayer2.dorado";
    private const string RootPath = "/org/mpris/MediaPlayer2";
    private const string RootInterface = "org.mpris.MediaPlayer2";
    private const string PlayerInterface = "org.mpris.MediaPlayer2.Player";
    private const string PropertiesInterface = "org.freedesktop.DBus.Properties";

    private readonly DBusConnection? _connection;
    private readonly object _gate = new();

    private Track? _track;
    private bool _playing;
    private TimeSpan _position;
    private TimeSpan _duration;
    private double _volume = 1.0;
    private string _playbackStatus = "Stopped";

    public bool IsAvailable { get; }

    /// <summary>Diagnostic: why construction failed, when it did.</summary>
    public Exception? InitializationError { get; private set; }

    /// <summary>True once the bus name has been claimed and handlers registered.</summary>
    public bool IsRegistered { get; private set; }

    public MprisMediaControls(DBusConnection? connection = null)
    {
        try
        {
            _connection = connection ?? new DBusConnection(DBusAddress.Session);
            IsAvailable = true;
            _ = InitializeAsync();
        }
        catch (Exception ex)
        {
            InitializationError = ex;
            _connection = null;
            IsAvailable = false;
        }
    }

    public void Dispose()
    {
        try { _connection?.Dispose(); } catch { /* best effort */ }
    }

    private async Task InitializeAsync()
    {
        if (_connection is null) return;
        try
        {
            // 0.95.x requires the connection to be established before handlers can
            // be registered and a well-known name claimed.
            await _connection.ConnectAsync();
            _connection.AddMethodHandler(new MprisMethodHandler(this, RootPath));
            await _connection.RequestNameAsync(ServiceName);
            IsRegistered = true;
        }
        catch (Exception ex)
        {
            // The bus is best-effort; the app must never fail because of it.
            InitializationError = ex;
        }
    }

    public void Update(Track? track, bool isPlaying, TimeSpan position, TimeSpan duration)
    {
        if (!IsAvailable) return;

        bool metadataChanged;
        lock (_gate)
        {
            metadataChanged = !ReferenceEquals(_track, track);
            _track = track;
            _playing = isPlaying;
            _position = position;
            _duration = duration;
            _playbackStatus = isPlaying ? "Playing" : (track is null ? "Stopped" : "Paused");
        }

        EmitPropertiesChanged(metadataChanged);
    }

    private void EmitPropertiesChanged(bool metadataChanged)
    {
        if (_connection is null) return;
        try
        {
            using var writer = _connection.GetMessageWriter();
            writer.WriteSignalHeader(null, RootPath, PropertiesInterface, "PropertiesChanged", "sa{sv}as");
            writer.WriteString(PlayerInterface);

            var changed = new Dict<string, VariantValue>();
            lock (_gate)
            {
                changed["PlaybackStatus"] = VariantValue.String(_playbackStatus);
                changed["Position"] = VariantValue.Int64(ToMicros(_position));
                if (metadataChanged) changed["Metadata"] = BuildMetadata().AsVariantValue();
            }
            writer.WriteDictionary(changed);
            writer.WriteArray(Array.Empty<string>());

            _connection.TrySendMessage(writer.CreateMessage());
        }
        catch
        {
            // ignore signal failures
        }
    }

    private Dict<string, VariantValue> BuildMetadata()
    {
        var meta = new Dict<string, VariantValue>();
        lock (_gate)
        {
            var track = _track;
            if (track is null) return meta;

            meta["mpris:trackid"] = VariantValue.ObjectPath(new ObjectPath("/org/dorado/track/" + track.Id.ToString("N")));
            meta["xesam:title"] = VariantValue.String(track.Title ?? string.Empty);
            meta["xesam:artist"] = new Array<string>(new[] { track.ArtistName ?? string.Empty }).AsVariantValue();
            meta["xesam:album"] = VariantValue.String(track.AlbumTitle ?? string.Empty);
            meta["mpris:length"] = VariantValue.Int64(ToMicros(_duration));
            if (!string.IsNullOrWhiteSpace(track.ArtworkUri)) meta["mpris:artUrl"] = VariantValue.String(track.ArtworkUri);
        }
        return meta;
    }

    private Dict<string, VariantValue> BuildAllProperties()
    {
        var d = new Dict<string, VariantValue>
        {
            ["Identity"] = VariantValue.String("Dorado"),
            ["DesktopEntry"] = VariantValue.String("dorado"),
            ["CanQuit"] = VariantValue.Bool(false),
            ["CanRaise"] = VariantValue.Bool(false),
            ["HasTrackList"] = VariantValue.Bool(false),
            ["SupportedUriSchemes"] = new Array<string>().AsVariantValue(),
            ["SupportedMimeTypes"] = new Array<string>().AsVariantValue(),
        };

        lock (_gate)
        {
            d["PlaybackStatus"] = VariantValue.String(_playbackStatus);
            d["LoopStatus"] = VariantValue.String("None");
            d["Rate"] = VariantValue.Double(1.0);
            d["Shuffle"] = VariantValue.Bool(false);
            d["Metadata"] = BuildMetadata().AsVariantValue();
            d["Volume"] = VariantValue.Double(_volume);
            d["Position"] = VariantValue.Int64(ToMicros(_position));
            d["MinimumRate"] = VariantValue.Double(1.0);
            d["MaximumRate"] = VariantValue.Double(1.0);
        }
        d["CanGoNext"] = VariantValue.Bool(true);
        d["CanGoPrevious"] = VariantValue.Bool(true);
        d["CanPlay"] = VariantValue.Bool(true);
        d["CanPause"] = VariantValue.Bool(true);
        d["CanSeek"] = VariantValue.Bool(true);
        d["CanControl"] = VariantValue.Bool(true);
        return d;
    }

    private static long ToMicros(TimeSpan value) => (long)(value.TotalMilliseconds * 1000);

    public event EventHandler? PlayPauseRequested;
    public event EventHandler? NextRequested;
    public event EventHandler? PreviousRequested;
    public event EventHandler? StopRequested;
    public event EventHandler<TimeSpan>? SeekRequested;

    private void RaisePlayPause() => PlayPauseRequested?.Invoke(this, EventArgs.Empty);
    private void RaiseNext() => NextRequested?.Invoke(this, EventArgs.Empty);
    private void RaisePrevious() => PreviousRequested?.Invoke(this, EventArgs.Empty);
    private void RaiseStop() => StopRequested?.Invoke(this, EventArgs.Empty);

    private void RaiseSeek(TimeSpan position)
    {
        lock (_gate) _position = position;
        SeekRequested?.Invoke(this, position);
        EmitPropertiesChanged(metadataChanged: false);
    }

    private sealed class MprisMethodHandler : IPathMethodHandler
    {
        private readonly MprisMediaControls _owner;

        public MprisMethodHandler(MprisMediaControls owner, string path)
        {
            _owner = owner;
            Path = path;
        }

        public string Path { get; }

        public bool HandlesChildPaths => false;

        public ValueTask HandleMethodAsync(MethodContext context)
        {
            try
            {
                var iface = context.Request.InterfaceAsString;
                var member = context.Request.MemberAsString;

                if (iface == "org.freedesktop.DBus.Introspectable" && member == "Introspect")
                {
                    using var w = context.CreateReplyWriter("s");
                    w.WriteString(IntrospectionXml);
                    context.Reply(w.CreateMessage());
                    return default;
                }

                if (iface == PropertiesInterface)
                {
                    HandleProperties(context, member);
                    return default;
                }

                if (iface == PlayerInterface || iface == RootInterface)
                {
                    switch (member)
                    {
                        case "PlayPause":
                        case "Play":
                        case "Pause":
                            _owner.RaisePlayPause();
                            break;
                        case "Stop":
                            _owner.RaiseStop();
                            break;
                        case "Next":
                            _owner.RaiseNext();
                            break;
                        case "Previous":
                            _owner.RaisePrevious();
                            break;
                        case "Seek":
                            {
                                var reader = context.Request.GetBodyReader();
                                long offsetMicros = reader.ReadInt64();
                                TimeSpan current;
                                lock (_owner._gate) current = _owner._position;
                                _owner.RaiseSeek(current + TimeSpan.FromTicks(offsetMicros * 10));
                                break;
                            }
                        case "SetPosition":
                            {
                                var reader = context.Request.GetBodyReader();
                                reader.ReadObjectPathAsString();
                                long posMicros = reader.ReadInt64();
                                _owner.RaiseSeek(TimeSpan.FromTicks(posMicros * 10));
                                break;
                            }
                        case "OpenUri":
                        case "Raise":
                        case "Quit":
                            break;
                    }
                    ReplyEmpty(context);
                    return default;
                }

                context.ReplyError("org.freedesktop.DBus.Error.UnknownMethod", $"Unknown {iface}.{member}");
            }
            catch (Exception ex)
            {
                try { context.ReplyError("org.freedesktop.DBus.Error.Failed", ex.Message); } catch { }
            }
            return default;
        }

        private void HandleProperties(MethodContext context, string? member)
        {
            var reader = context.Request.GetBodyReader();
            switch (member)
            {
                case "Get":
                    {
                        reader.ReadString(); // interface
                        var property = reader.ReadString();
                        var all = _owner.BuildAllProperties();
                        var value = all.TryGetValue(property, out var v) ? v : VariantValue.String(string.Empty);
                        using var w = context.CreateReplyWriter("v");
                        w.WriteVariant(value);
                        context.Reply(w.CreateMessage());
                        break;
                    }
                case "GetAll":
                    {
                        reader.ReadString(); // interface
                        using var w = context.CreateReplyWriter("a{sv}");
                        w.WriteDictionary(_owner.BuildAllProperties());
                        context.Reply(w.CreateMessage());
                        break;
                    }
                case "Set":
                    {
                        reader.ReadString(); // interface
                        reader.ReadString(); // property
                        reader.ReadVariantValue(); // value (Volume writes accepted as no-ops)
                        ReplyEmpty(context);
                        break;
                    }
                default:
                    context.ReplyError("org.freedesktop.DBus.Error.UnknownMethod", $"Unknown Properties.{member}");
                    break;
            }
        }

        private static void ReplyEmpty(MethodContext context)
        {
            using var w = context.CreateReplyWriter(null);
            context.Reply(w.CreateMessage());
        }

        private const string IntrospectionXml = """
            <!DOCTYPE node PUBLIC "-//freedesktop//DTD D-BUS Object Introspection 1.0//EN"
             "http://www.freedesktop.org/standards/dbus/1.0/introspect.dtd">
            <node>
              <interface name="org.mpris.MediaPlayer2">
                <method name="Raise"/>
                <method name="Quit"/>
                <property name="CanQuit" type="b" access="read"/>
                <property name="CanRaise" type="b" access="read"/>
                <property name="HasTrackList" type="b" access="read"/>
                <property name="Identity" type="s" access="read"/>
                <property name="DesktopEntry" type="s" access="read"/>
                <property name="SupportedUriSchemes" type="as" access="read"/>
                <property name="SupportedMimeTypes" type="as" access="read"/>
              </interface>
              <interface name="org.mpris.MediaPlayer2.Player">
                <method name="Next"/>
                <method name="Previous"/>
                <method name="Pause"/>
                <method name="PlayPause"/>
                <method name="Stop"/>
                <method name="Play"/>
                <method name="Seek"><arg name="Offset" type="x" direction="in"/></method>
                <method name="SetPosition">
                  <arg name="TrackId" type="o" direction="in"/>
                  <arg name="Position" type="x" direction="in"/>
                </method>
                <method name="OpenUri"><arg name="Uri" type="s" direction="in"/></method>
                <property name="PlaybackStatus" type="s" access="read"/>
                <property name="Metadata" type="a{sv}" access="read"/>
                <property name="Position" type="x" access="read"/>
                <property name="Volume" type="d" access="readwrite"/>
                <property name="CanGoNext" type="b" access="read"/>
                <property name="CanGoPrevious" type="b" access="read"/>
                <property name="CanPlay" type="b" access="read"/>
                <property name="CanPause" type="b" access="read"/>
                <property name="CanSeek" type="b" access="read"/>
                <property name="CanControl" type="b" access="read"/>
              </interface>
              <interface name="org.freedesktop.DBus.Properties">
                <method name="Get"><arg name="Interface" type="s" direction="in"/><arg name="Property" type="s" direction="in"/><arg name="Value" type="v" direction="out"/></method>
                <method name="GetAll"><arg name="Interface" type="s" direction="in"/><arg name="Properties" type="a{sv}" direction="out"/></method>
                <method name="Set"><arg name="Interface" type="s" direction="in"/><arg name="Property" type="s" direction="in"/><arg name="Value" type="v" direction="in"/></method>
              </interface>
            </node>
            """;
    }
}
