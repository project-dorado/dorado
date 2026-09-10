using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using ManagedBass;
using ManagedBass.Mix;
using Dorado.Application.Interfaces;

namespace Dorado.Infrastructure.Audio;

/// <summary>
/// Real audio output engine built on the BASS library (via ManagedBass).
/// Owns a float mixer that sources decode streams; supports gapless chaining,
/// equal-power crossfades, seek, volume, and FFT spectrum retrieval.
/// Degrades gracefully to <see cref="IsAvailable"/> = false when no audio device or
/// native libraries are present (CI, headless, unsupported RID).
/// </summary>
public sealed class BassAudioOutputEngine : IAudioOutputEngine, IEqualizerControl
{
    private const int MixerFrequency = 44100;
    private const int MixerChannels = 2;
    private const int FftBandCount = 24;

    private static readonly string[] PluginNames = { "bassflac", "bass_aac", "bassopus" };

    private readonly object _gate = new();
    private readonly System.Timers.Timer _tickTimer;
    private readonly Stopwatch _fadeStopwatch = new();

    private int _mixer;
    private int _currentSource;
    private double _currentSourceSeconds;
    private int _preparedSource;
    private double _preparedSourceSeconds;
    private string? _preparedUri;
    private double _crossfadeSeconds;
    private double _currentBaseVolume = 1.0;
    private double _preparedBaseVolume = 1.0;
    private bool _isMuted;
    private bool _transitionFired;
    private bool _isCrossfading;
    private int _currentSyncHandle;
    private float[]? _fftScratch;
    private bool _initialized;
    private bool _disposed;

    private readonly object _eqGate = new();
    private readonly double[] _eqGains = new double[Equalizer.CenterFrequencies.Length];
    private Equalizer? _equalizer;
    private DSPProcedure? _eqDspCallback;
    private float[]? _eqScratch;
    private volatile bool _eqEnabled;
    private bool _eqDspRegistered;

    static BassAudioOutputEngine()
    {
        ConfigureNativeLibraryResolution();
    }

    public bool IsAvailable { get; private set; }

    public bool HasActiveSource
    {
        get
        {
            lock (_gate)
            {
                return ActiveSource != 0;
            }
        }
    }

    public event EventHandler? TrackEnded;
    public event EventHandler? TrackTransitioned;

    public BassAudioOutputEngine()
    {
        _tickTimer = new System.Timers.Timer(50) { AutoReset = true };
        _tickTimer.Elapsed += (_, _) => OnTick();
    }

    public void LoadAndPlay(string sourceUri)
    {
        if (!EnsureInitialized() || string.IsNullOrWhiteSpace(sourceUri))
        {
            return;
        }

        lock (_gate)
        {
            ClearPrepared();
            StopAndFreeCurrent();

            var source = CreateSource(sourceUri);
            if (source == 0)
            {
                return;
            }

            _currentSource = source;
            _currentSourceSeconds = Bass.ChannelBytes2Seconds(source, Bass.ChannelGetLength(source));
            _transitionFired = false;
            _isCrossfading = false;
            _fadeStopwatch.Reset();

            BassMix.MixerAddChannel(_mixer, source, BassFlags.MixerChanNoRampin);
            ApplySourceVolume(_currentSource, _currentBaseVolume, _isMuted);
            Bass.ChannelPlay(_mixer, Restart: true);
            ScheduleEndSync();
        }
    }

    public void Play()
    {
        if (!IsAvailable)
        {
            return;
        }

        lock (_gate)
        {
            Bass.ChannelPlay(_mixer, Restart: true);
        }
    }

    public void Pause()
    {
        if (!IsAvailable)
        {
            return;
        }

        lock (_gate)
        {
            Bass.ChannelPause(_mixer);
        }
    }

    public void Stop()
    {
        if (!IsAvailable)
        {
            return;
        }

        lock (_gate)
        {
            ClearPrepared();
            StopAndFreeCurrent();
            Bass.ChannelPause(_mixer);
            Bass.ChannelSetPosition(_mixer, 0);
        }
    }

    public void SetVolume(double volume, bool muted)
    {
        if (!IsAvailable)
        {
            return;
        }

        lock (_gate)
        {
            var clamped = Math.Clamp(volume, 0.0, 1.0);
            if (_isCrossfading)
            {
                // Fade ramps are active; remember the new base and let the ramp re-apply it.
                _currentBaseVolume = clamped;
                _preparedBaseVolume = clamped;
                _isMuted = muted;
                return;
            }

            _currentBaseVolume = clamped;
            _isMuted = muted;
            ApplySourceVolume(_currentSource, _currentBaseVolume, _isMuted);
        }
    }

    public void Seek(TimeSpan position)
    {
        if (!IsAvailable)
        {
            return;
        }

        lock (_gate)
        {
            var source = ActiveSource;
            if (source == 0)
            {
                return;
            }

            var seconds = Math.Clamp(position.TotalSeconds, 0, Math.Max(0, ActiveSourceSeconds - 0.5));
            var bytes = Bass.ChannelSeconds2Bytes(source, seconds);
            BassMix.ChannelSetPosition(source, bytes);
        }
    }

    public TimeSpan GetPosition()
    {
        if (!IsAvailable)
        {
            return TimeSpan.Zero;
        }

        lock (_gate)
        {
            var source = ActiveSource;
            if (source == 0)
            {
                return TimeSpan.Zero;
            }

            var seconds = Bass.ChannelBytes2Seconds(source, Bass.ChannelGetPosition(source));
            return TimeSpan.FromSeconds(Math.Max(0, seconds));
        }
    }

    public TimeSpan GetDuration()
    {
        if (!IsAvailable)
        {
            return TimeSpan.Zero;
        }

        lock (_gate)
        {
            var source = ActiveSource;
            return source == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(Math.Max(0, ActiveSourceSeconds));
        }
    }

    public float[] GetFftData()
    {
        if (!IsAvailable || _mixer == 0)
        {
            return Array.Empty<float>();
        }

        lock (_gate)
        {
            _fftScratch ??= new float[256];
            var read = Bass.ChannelGetData(_mixer, _fftScratch, (int)DataFlags.FFT512);
            if (read <= 0)
            {
                return Array.Empty<float>();
            }

            var bands = new float[FftBandCount];
            // Aggregate the 256 FFT bins logarithmically into 24 perceptual bands.
            double lowBin = 1;
            for (var band = 0; band < FftBandCount; band++)
            {
                var highBin = Math.Min(255, Math.Floor(1.9 * lowBin * Math.Pow(1.16, band + 1)));
                if (highBin <= lowBin)
                {
                    highBin = lowBin + 1;
                }

                var sum = 0.0;
                var count = 0;
                for (var bin = (int)lowBin; bin <= (int)highBin && bin < 256; bin++)
                {
                    sum += _fftScratch[bin];
                    count++;
                }

                var magnitude = count > 0 ? sum / count : 0;
                // Perceptual scale with a -60 dB floor.
                var db = magnitude > 0.0000001 ? 20 * Math.Log10(magnitude) : -120;
                bands[band] = (float)Math.Clamp((db + 60) / 60, 0, 1);
                lowBin = highBin + 1;
            }

            return bands;
        }
    }

    public void SetAutoTransition(string? nextSourceUri, double crossfadeSeconds)
    {
        if (!IsAvailable)
        {
            return;
        }

        lock (_gate)
        {
            _preparedUri = string.IsNullOrWhiteSpace(nextSourceUri) ? null : nextSourceUri;
            _crossfadeSeconds = Math.Clamp(crossfadeSeconds, 0, 10);
            if (_preparedUri == null)
            {
                ClearPrepared();
            }
        }
    }

    public void ApplyEqualizer(bool enabled, IReadOnlyList<double> bandGainsDb, double preampDb)
    {
        lock (_eqGate)
        {
            _eqEnabled = enabled;
            for (var i = 0; i < _eqGains.Length; i++)
            {
                _eqGains[i] = i < bandGainsDb.Count ? bandGainsDb[i] : 0;
            }

            _equalizer ??= new Equalizer(MixerFrequency, MixerChannels);
            _equalizer.SetGains(_eqGains, preampDb);
            EnsureEqDsp();
        }
    }

    private void EnsureEqDsp()
    {
        if (!IsAvailable || _mixer == 0 || _eqDspRegistered)
        {
            return;
        }

        try
        {
            _eqDspCallback ??= OnEqDsp;
            _eqDspRegistered = Bass.ChannelSetDSP(_mixer, _eqDspCallback, IntPtr.Zero, 0) != 0;
        }
        catch
        {
            _eqDspRegistered = false;
        }
    }

    private void OnEqDsp(int handle, int channel, IntPtr buffer, int length, IntPtr user)
    {
        if (!_eqEnabled || _equalizer is null || buffer == IntPtr.Zero || length <= 0)
        {
            return;
        }

        try
        {
            var count = length / sizeof(float);
            var scratch = _eqScratch;
            if (scratch is null || scratch.Length < count)
            {
                scratch = new float[count];
                _eqScratch = scratch;
            }

            Marshal.Copy(buffer, scratch, 0, count);
            _equalizer.Process(scratch, count);
            Marshal.Copy(scratch, 0, buffer, count);
        }
        catch
        {
            // Never let a DSP glitch crash the audio thread.
        }
    }

    private int ActiveSource => _isCrossfading && _preparedSource != 0 ? _preparedSource : _currentSource;
    private double ActiveSourceSeconds => _isCrossfading && _preparedSource != 0 ? _preparedSourceSeconds : _currentSourceSeconds;

    private void OnTick()
    {
        if (!IsAvailable)
        {
            return;
        }

        try
        {
            lock (_gate)
            {
                if (_currentSource == 0 || _transitionFired)
                {
                    return;
                }

                // Drive crossfade scheduling when a prepared source exists (overlap intended).
                if (!_isCrossfading && _preparedUri != null && _crossfadeSeconds > 0)
                {
                    var remaining = _currentSourceSeconds - Bass.ChannelBytes2Seconds(_currentSource, Bass.ChannelGetPosition(_currentSource));
                    if (remaining <= _crossfadeSeconds)
                    {
                        BeginCrossfade();
                    }
                }

                // Advance active fade ramps.
                if (_isCrossfading)
                {
                    ApplyFadeRamp();
                }
            }
        }
        catch
        {
            // Never let an audio glitch crash the tick loop.
        }
    }

    private void BeginCrossfade()
    {
        var prepared = CreateSource(_preparedUri!);
        if (prepared == 0)
        {
            _preparedUri = null;
            return;
        }

        _preparedSource = prepared;
        _preparedSourceSeconds = Bass.ChannelBytes2Seconds(prepared, Bass.ChannelGetLength(prepared));
        BassMix.MixerAddChannel(_mixer, prepared, BassFlags.MixerChanNoRampin);
        ApplySourceVolume(prepared, 0.0001, _isMuted);

        _isCrossfading = true;
        _fadeStopwatch.Restart();
        FireTransitioned();
    }

    private void ApplyFadeRamp()
    {
        var elapsed = _fadeStopwatch.Elapsed.TotalSeconds;
        var progress = _crossfadeSeconds <= 0 ? 1.0 : Math.Min(1.0, elapsed / _crossfadeSeconds);

        // Equal-power crossfade curves.
        var outgoing = _currentBaseVolume * Math.Cos(progress * Math.PI / 2);
        var incoming = _preparedBaseVolume * Math.Sin(progress * Math.PI / 2);
        ApplySourceVolume(_currentSource, Math.Max(0, outgoing), _isMuted);
        ApplySourceVolume(_preparedSource, Math.Max(0, incoming), _isMuted);

        if (progress >= 1.0)
        {
            _isCrossfading = false;
            _fadeStopwatch.Reset();
            PromotePreparedToCurrent();
        }
    }

    private void PromotePreparedToCurrent()
    {
        var oldSource = _currentSource;
        _currentSource = _preparedSource;
        _currentSourceSeconds = _preparedSourceSeconds;
        _currentBaseVolume = _preparedBaseVolume;
        _preparedSource = 0;
        _preparedSourceSeconds = 0;
        _preparedUri = null;
        _transitionFired = false;
        _isCrossfading = false;
        _fadeStopwatch.Reset();

        if (oldSource != 0 && oldSource != _currentSource)
        {
            BassMix.MixerRemoveChannel(oldSource);
            Bass.ChannelStop(oldSource);
            Bass.StreamFree(oldSource);
        }

        ApplySourceVolume(_currentSource, _currentBaseVolume, _isMuted);
        ScheduleEndSync();
    }

    private void FireTransitioned()
    {
        if (_transitionFired)
        {
            return;
        }

        _transitionFired = true;
        TrackTransitioned?.Invoke(this, EventArgs.Empty);
    }

    private void ScheduleEndSync()
    {
        if (_currentSyncHandle != 0 && _currentSource != 0)
        {
            Bass.ChannelRemoveSync(_currentSource, _currentSyncHandle);
        }

        _currentSyncHandle = 0;

        if (_currentSource == 0)
        {
            return;
        }

        _currentSyncHandle = Bass.ChannelSetSync(
            _currentSource,
            SyncFlags.End,
            0,
            (_, _, _, _) => OnCurrentSourceEnd());
    }

    private void OnCurrentSourceEnd()
    {
        // Raised on a BASS thread when the current source's data is exhausted.
        if (_isCrossfading)
        {
            return;
        }

        if (_preparedUri != null)
        {
            BeginGaplessChainFromSync();
        }
        else
        {
            TrackEnded?.Invoke(this, EventArgs.Empty);
        }
    }

    private void BeginGaplessChainFromSync()
    {
        // The previous source has fully rendered; plugging now yields sample-boundary continuity.
        lock (_gate)
        {
            if (_preparedUri == null || _preparedSource != 0 || _disposed)
            {
                return;
            }

            var prepared = CreateSource(_preparedUri);
            if (prepared == 0)
            {
                _preparedUri = null;
                TrackEnded?.Invoke(this, EventArgs.Empty);
                return;
            }

            _preparedSource = prepared;
            _preparedSourceSeconds = Bass.ChannelBytes2Seconds(prepared, Bass.ChannelGetLength(prepared));
            BassMix.MixerAddChannel(_mixer, prepared, BassFlags.MixerChanNoRampin);
            ApplySourceVolume(prepared, _currentBaseVolume, _isMuted);

            FireTransitioned();
            PromotePreparedToCurrent();
        }
    }

    private void ClearPrepared()
    {
        if (_preparedSource != 0)
        {
            BassMix.MixerRemoveChannel(_preparedSource);
            Bass.ChannelStop(_preparedSource);
            Bass.StreamFree(_preparedSource);
            _preparedSource = 0;
        }

        _preparedSourceSeconds = 0;
        _preparedUri = null;
        _isCrossfading = false;
        _fadeStopwatch.Reset();
    }

    private void StopAndFreeCurrent()
    {
        if (_currentSource != 0)
        {
            if (_currentSyncHandle != 0)
            {
                Bass.ChannelRemoveSync(_currentSource, _currentSyncHandle);
                _currentSyncHandle = 0;
            }

            BassMix.MixerRemoveChannel(_currentSource);
            Bass.ChannelStop(_currentSource);
            Bass.StreamFree(_currentSource);
            _currentSource = 0;
            _currentSourceSeconds = 0;
        }

        _isCrossfading = false;
        _transitionFired = false;
        _fadeStopwatch.Reset();
    }

    private static void ApplySourceVolume(int source, double volume, bool muted)
    {
        if (source == 0)
        {
            return;
        }

        Bass.ChannelSetAttribute(source, ChannelAttribute.Volume, muted ? 0 : Math.Clamp(volume, 0, 1));
    }

    private int CreateSource(string uri)
    {
        var isUrl = uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        var flags = BassFlags.Decode | BassFlags.Float;
        var handle = isUrl
            ? Bass.CreateStream(uri, 0, flags | BassFlags.AsyncFile, null, default)
            : Bass.CreateStream(uri, 0, 0, flags);

        if (handle == 0)
        {
            // Retry once without Float (some add-on decoders produce 16-bit only).
            handle = isUrl
                ? Bass.CreateStream(uri, 0, BassFlags.Decode | BassFlags.AsyncFile, null, default)
                : Bass.CreateStream(uri, 0, 0, BassFlags.Decode);
        }

        return handle;
    }

    private bool EnsureInitialized()
    {
        if (_initialized)
        {
            return IsAvailable;
        }

        _initialized = true;
        try
        {
            LoadFormatPlugins();

            IsAvailable = TryInitializeOutputDevice();
            if (IsAvailable)
            {
                Bass.Start();
                _mixer = BassMix.CreateMixerStream(MixerFrequency, MixerChannels, BassFlags.Float | BassFlags.MixerEnd);
                if (_mixer != 0)
                {
                    Bass.ChannelPlay(_mixer);
                    EnsureEqDsp();
                    _tickTimer.Start();
                }
                else
                {
                    IsAvailable = false;
                }
            }
        }
        catch (Exception)
        {
            IsAvailable = false;
        }

        return IsAvailable;
    }

    /// <summary>
    /// Opens a real playback device. The system default is tried first; if that fails we
    /// probe physical devices (preferring the flagged default). BASS device 0 is the silent
    /// "No sound" device and is deliberately never used as a fallback — opening it reported
    /// <see cref="IsAvailable"/> = true while producing no audio. When no real device opens
    /// we leave the engine unavailable so the coordinator runs its simulated clock instead.
    /// </summary>
    private static bool TryInitializeOutputDevice()
    {
        if (Bass.Init(-1, MixerFrequency))
        {
            return true;
        }

        var candidates = new List<(int Index, bool IsDefault)>();
        for (var i = 1; i < 64; i++)
        {
            if (!Bass.GetDeviceInfo(i, out var info))
            {
                if (i > 8)
                {
                    break;
                }

                continue;
            }

            if (info.IsEnabled)
            {
                candidates.Add((i, info.IsDefault));
            }
        }

        foreach (var candidate in candidates.OrderByDescending(c => c.IsDefault))
        {
            if (Bass.Init(candidate.Index, MixerFrequency))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Registers a DllImport resolver so ManagedBass (and ManagedBass.Mix) load the BASS
    /// natives from <c>Native/&lt;rid&gt;/</c>, and — on Linux — preloads the system ALSA
    /// library before BASS uses it. Without the resolver the Mix add-on cannot be found and
    /// the engine silently degrades; without the ALSA preload a shadowing ALSA build (for
    /// example Homebrew's) fails to load PipeWire/Pulse plugin modules and BASS opens the
    /// silent "No sound" device.
    /// </summary>
    private static void ConfigureNativeLibraryResolution()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                PreloadSystemAlsa();
            }

            var nativeDir = Path.Combine(AppContext.BaseDirectory, "Native", ResolveRuntimeIdentifier());
            var isWindows = OperatingSystem.IsWindows();
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["bass"] = isWindows ? "bass.dll" : "libbass.so",
                ["bassmix"] = isWindows ? "bassmix.dll" : "libbassmix.so",
            };

            RegisterNativeResolver(typeof(Bass).Assembly, nativeDir, map);
            RegisterNativeResolver(typeof(BassMix).Assembly, nativeDir, map);
        }
        catch
        {
            // Default probing still applies; unavailable native libs degrade via IsAvailable.
        }
    }

    private static void RegisterNativeResolver(Assembly assembly, string nativeDir, IReadOnlyDictionary<string, string> map)
    {
        try
        {
            NativeLibrary.SetDllImportResolver(assembly, (name, _, _) =>
            {
                if (map.TryGetValue(name, out var file))
                {
                    var path = Path.Combine(nativeDir, file);
                    if (File.Exists(path))
                    {
                        return NativeLibrary.Load(path);
                    }
                }

                return IntPtr.Zero;
            });
        }
        catch
        {
            // A resolver may already be registered for this assembly by the host; ignore.
        }
    }

    private static void PreloadSystemAlsa()
    {
        if (IsLibraryLoaded("libasound"))
        {
            return;
        }

        var candidates = new[]
        {
            "/usr/lib64/libasound.so.2",
            "/usr/lib/libasound.so.2",
            "/lib64/libasound.so.2",
            "/lib/libasound.so.2",
            "/usr/lib/x86_64-linux-gnu/libasound.so.2",
            "/usr/lib/aarch64-linux-gnu/libasound.so.2",
            "/usr/lib/arm-linux-gnueabihf/libasound.so.2",
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                NativeLibrary.TryLoad(path, out _);
                return;
            }
        }
    }

    private static bool IsLibraryLoaded(string fragment)
    {
        try
        {
            foreach (var line in File.ReadLines("/proc/self/maps"))
            {
                if (line.Contains(fragment, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    private static void LoadFormatPlugins()
    {
        var isWindows = OperatingSystem.IsWindows();
        var rid = ResolveRuntimeIdentifier();

        foreach (var plugin in PluginNames)
        {
            var name = isWindows ? plugin + ".dll" : "lib" + plugin + ".so";
            var path = Path.Combine(AppContext.BaseDirectory, "Native", rid, name);
            if (File.Exists(path))
            {
                Bass.PluginLoad(path);
            }
        }
    }

    private static string ResolveRuntimeIdentifier()
    {
        if (OperatingSystem.IsWindows())
        {
            return "win-x64";
        }

        return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _tickTimer.Stop();
        _tickTimer.Dispose();
        try
        {
            lock (_gate)
            {
                ClearPrepared();
                StopAndFreeCurrent();
                if (_mixer != 0)
                {
                    Bass.StreamFree(_mixer);
                    _mixer = 0;
                }

                if (IsAvailable)
                {
                    Bass.Free();
                }
            }
        }
        catch
        {
        }
    }
}
