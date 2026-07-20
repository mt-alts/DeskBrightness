using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DeskBrightness.Adb;
using DeskBrightness.Config;
using DeskBrightness.Core.Profiles;
using DeskBrightness.Core.Sensors;

namespace DeskBrightness.Services
{
    public sealed class ForwardedLuxSource : ILuxSource, IDisposable
    {
        private readonly AdbCommandRunner _adb;
        private readonly LocalizationService _localization;
        private readonly BrightnessProfile _profile;
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _headlessProcessTask;
        private string? _deviceSerial;
        private bool _disposed;

        public event EventHandler<LuxSample>? SampleReceived;

        public event EventHandler<MobileBrightnessSample>? BrightnessReceived;

        public event EventHandler<DeskBrightnessRuntimeLog>? LogReceived;

        public string? PreferredDeviceSerial { get; set; }

        public ForwardedLuxSource(AdbCommandRunner adb, LocalizationService localization, BrightnessProfile profile)
        {
            _adb = adb;
            _localization = localization;
            _profile = profile;
            _adb.DebugLogReceived += (_, log) => PublishLog(log.Level, log.Message);
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (_listener is not null)
                return;

            await ConfigureAdbReverseAsync(cancellationToken);

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _listener = new TcpListener(IPAddress.Loopback, AppConfig.Network.TcpLuxPort);
            _listener.Start();

            PublishLog("INFO", _localization.Get("DataChannelReady"));

            _ = Task.Run(() => ListenAsync(_listener, _cts.Token), CancellationToken.None);

            await TryLaunchMobileAppAsync(_cts.Token);
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            var cts = _cts;
            var serial = _deviceSerial;

            if (serial is not null)
                await StopHeadlessMobileProcessAsync(serial, cancellationToken);

            if (_cts is not null)
            {
                _cts.Cancel();
                _cts = null;
            }

            if (_headlessProcessTask is not null)
            {
                try
                {
                    await Task.WhenAny(
                        _headlessProcessTask,
                        Task.Delay(TimeSpan.FromSeconds(AppConfig.Timing.HeadlessWaitSeconds), cancellationToken)
                    );
                }
                catch
                {
                }
                finally
                {
                    _headlessProcessTask = null;
                }
            }

            cts?.Dispose();

            if (_listener is not null)
            {
                try
                {
                    _listener.Stop();
                }
                catch
                {
                }
                finally
                {
                    _listener = null;
                }
            }

            try
            {
                if (serial is not null)
                    await _adb.RemoveReverseAsync(serial, AppConfig.Network.TcpLuxPort, cancellationToken);
            }
            catch (Exception ex)
            {
                PublishLog("DEBUG", $"adb reverse remove failed: {ex.Message}");
            }

            _deviceSerial = null;
            PublishLog("INFO", _localization.Get("DataChannelClosed"));
        }

        private async Task ConfigureAdbReverseAsync(CancellationToken cancellationToken)
        {
            var devices = await _adb.GetDevicesAsync(cancellationToken);
            var device = string.IsNullOrWhiteSpace(PreferredDeviceSerial)
                ? devices.FirstOrDefault(x => x.State == AdbDeviceState.Device)
                : devices.FirstOrDefault(
                    x => x.Serial == PreferredDeviceSerial && x.State == AdbDeviceState.Device
                );

            if (device is null)
            {
                if (!string.IsNullOrWhiteSpace(PreferredDeviceSerial))
                    throw new InvalidOperationException(
                        string.Format(_localization.Get("DeviceNotReady"), PreferredDeviceSerial)
                    );

                throw new InvalidOperationException(_localization.Get("NoDeviceFound"));
            }

            _deviceSerial = device.Serial;

            await _adb.CreateReverseAsync(
                device.Serial,
                AppConfig.Network.TcpLuxPort,
                AppConfig.Network.TcpLuxPort,
                cancellationToken
            );

            PublishLog("DEBUG", $"ADB reverse ready: device 127.0.0.1:{AppConfig.Network.TcpLuxPort} -> desktop 127.0.0.1:{AppConfig.Network.TcpLuxPort}.");
        }

        private async Task TryLaunchMobileAppAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_deviceSerial is null)
                    return;

                await StopHeadlessMobileProcessAsync(_deviceSerial, cancellationToken);

                var localHeadlessPath = GetLocalHeadlessJarPath();

                await _adb.PushFileAsync(
                    _deviceSerial,
                    localHeadlessPath,
                    AppConfig.Adb.HeadlessRemotePath,
                    cancellationToken
                );

                PublishLog("DEBUG", _localization.Get("DataCollectionStarted"));

                await ProbeHeadlessRuntimeAsync(_deviceSerial, cancellationToken);

                StartHeadlessStream(_deviceSerial, cancellationToken);
            }
            catch (Exception ex)
            {
                PublishLog("DEBUG", $"Headless mobile launch failed: {ex.Message}");
            }
        }

        private void StartHeadlessStream(string serial, CancellationToken cancellationToken)
        {
            var command = BuildHeadlessStreamingCommand();

            _headlessProcessTask = Task.Run(
                async () =>
                {
                    try
                    {
                        await _adb.ExecuteShellStreamingAsync(
                            serial,
                            command,
                            line => PublishLog("DEBUG", $"headless stream: {line}"),
                            cancellationToken
                        );

                        if (!cancellationToken.IsCancellationRequested)
                        {
                            PublishLog("ERROR", _localization.Get("BackgroundStreamStoppedUnexpectedly"));
                            await PublishHeadlessDiagnosticsAsync(serial, CancellationToken.None);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        PublishLog("DEBUG", "Headless mobile stream cancelled.");
                    }
                    catch (Exception ex)
                    {
                        if (!cancellationToken.IsCancellationRequested)
                            PublishLog("ERROR", $"{_localization.Get("BackgroundStreamFailed")}: {ex.Message}");
                    }
                },
                CancellationToken.None
            );

            PublishLog("DEBUG", _localization.Get("BackgroundStreamStarted"));
        }

        private async Task ProbeHeadlessRuntimeAsync(
            string serial,
            CancellationToken cancellationToken
        )
        {
            await RunHeadlessProbeAsync(serial, "--probe-basic", cancellationToken);
            var probeOutput = await RunHeadlessProbeAsync(serial, "--probe", cancellationToken);

            foreach (var line in ReadNonEmptyLines(probeOutput))
            {
                var clean = line.Replace("DeskBrightnessHeadless: ", "");
                if (clean.Contains("maxRange"))
                {
                    PublishLog("INFO", clean);

                    var rangeStr = ExtractMaxRange(clean);
                    if (rangeStr is not null && double.TryParse(rangeStr,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var maxLux) && maxLux > 0)
                    {
                        _profile.SensorMaxLux = maxLux;
                        PublishLog("INFO", $"Sensor max range: {maxLux} lux");
                    }
                }
            }
        }

        private static string? ExtractMaxRange(string line)
        {
            var idx = line.IndexOf("maxRange=", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var val = line[(idx + 9)..].Trim();
            var end = val.IndexOfAny([' ', ',', ';', '/']);
            return end > 0 ? val[..end] : val;
        }

        private async Task<string> RunHeadlessProbeAsync(
            string serial,
            string probeArgument,
            CancellationToken cancellationToken
        )
        {
            var command =
                "sh -c 'CLASSPATH="
                + AppConfig.Adb.HeadlessRemotePath
                + " app_process /system/bin "
                + AppConfig.Adb.HeadlessBridgeClass
                + " "
                + probeArgument
                + " 2>&1'";

            var output = await _adb.ExecuteShellAsync(serial, command, cancellationToken);

            foreach (var line in ReadNonEmptyLines(output))
            {
                var clean = line.Replace("DeskBrightnessHeadless: ", "");
                if (clean.Contains("light sensor") || clean.Contains("maxRange"))
                    PublishLog("INFO", clean);
            }

            return output;
        }

        private async Task StopHeadlessMobileProcessAsync(
            string serial,
            CancellationToken cancellationToken
        )
        {
            try
            {
                await _adb.ExecuteShellAsync(
                    serial,
                    "sh -c 'pkill -TERM -f AppConfig.Adb.HeadlessBridgeClass 2>/dev/null || true; sleep 0.2; pkill -KILL -f AppConfig.Adb.HeadlessBridgeClass 2>/dev/null || true'",
                    cancellationToken
                );

                PublishLog("DEBUG", "Android headless process stop requested.");
            }
            catch (Exception ex)
            {
                PublishLog("DEBUG", $"Headless mobile stop failed: {ex.Message}");
            }
        }

        private async Task PublishHeadlessDiagnosticsAsync(
            string serial,
            CancellationToken cancellationToken
        )
        {
            try
            {
                var processOutput = await _adb.ExecuteShellAsync(
                    serial,
                    "sh -c 'pidof app_process || ps -A | grep app_process || true'",
                    cancellationToken
                );

                PublishLog("DEBUG", $"headless process: {NormalizeSingleLine(processOutput)}");

                var logOutput = await _adb.ExecuteShellAsync(
                    serial,
                    $"sh -c 'tail -n {AppConfig.Adb.HeadlessLogTailLines} {AppConfig.Adb.HeadlessLogPath} 2>/dev/null || true'",
                    cancellationToken
                );

                foreach (var line in ReadNonEmptyLines(logOutput))
                    PublishLog("DEBUG", $"headless log: {line}");

                var internalLogOutput = await _adb.ExecuteShellAsync(
                    serial,
                    $"sh -c 'tail -n {AppConfig.Adb.HeadlessInternalLogTailLines} {AppConfig.Adb.HeadlessInternalLogPath} 2>/dev/null || true'",
                    cancellationToken
                );

                foreach (var line in ReadNonEmptyLines(internalLogOutput))
                    PublishLog("DEBUG", $"headless internal: {line}");
            }
            catch (Exception ex)
            {
                PublishLog("DEBUG", $"Headless diagnostics failed: {ex.Message}");
            }
        }

        private string GetLocalHeadlessJarPath()
        {
            var path = Path.GetFullPath(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "deskbrightness-headless.jar"
                )
            );

            if (File.Exists(path))
                return path;

            path = Path.GetFullPath(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "..", "..", "..", "..",
                    "DeskBrightness.Mobile",
                    "app", "build", "outputs", "headless",
                    "deskbrightness-headless.jar"
                )
            );

            if (File.Exists(path))
                return path;

            throw new FileNotFoundException(
                _localization.Get("RequiredBackgroundFileMissing"),
                path
            );

            return path;
        }

        private static string BuildHeadlessStreamingCommand()
        {
            return string.Join(
                " ",
                "sh -c",
                "'rm -f " + AppConfig.Adb.HeadlessLogPath + " " + AppConfig.Adb.HeadlessInternalLogPath + ";",
                "CLASSPATH=" + AppConfig.Adb.HeadlessRemotePath + " exec app_process /system/bin " + AppConfig.Adb.HeadlessBridgeClass + " --mode lightSensor 2>&1'"
            );
        }

        private async Task ListenAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;

                try
                {
                    client = await listener.AcceptTcpClientAsync();
                client.NoDelay = true;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (InvalidOperationException)
                {
                    break;
                }

                _ = Task.Run(() => HandleConnectionAsync(client), CancellationToken.None);
            }
        }

        private async Task HandleConnectionAsync(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    while (true)
                    {
                        var lengthBuffer = new byte[4];
                        await ReadExactlyAsync(stream, lengthBuffer);

                        var length = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);

                        if (length <= 0 || length > AppConfig.Adb.MaxTcpFrameLength)
                        {
                            PublishLog("ERROR", $"{_localization.Get("TcpInvalidFrameLength")}: {length}");
                            stream.WriteByte(1);
                            await stream.FlushAsync();
                            break;
                        }

                        var jsonBuffer = new byte[length];
                        await ReadExactlyAsync(stream, jsonBuffer);

                        var body = Encoding.UTF8.GetString(jsonBuffer);
                        PublishLog("DEBUG", $"TCP frame body: {body}");

                        var sample = ParseMobileSample(body);

                        if (sample.Brightness is not null)
                        {
                            BrightnessReceived?.Invoke(
                                this,
                                new MobileBrightnessSample(sample.Brightness.Value, DateTimeOffset.Now)
                            );
                        }
                        else if (sample.Lux is not null)
                        {
                            SampleReceived?.Invoke(this, new LuxSample(sample.Lux.Value, DateTimeOffset.Now));
                        }
                        else
                        {
                            throw new FormatException(_localization.Get("TcpParseError"));
                        }

                        stream.WriteByte(0);
                        await stream.FlushAsync();
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                PublishLog("ERROR", $"{_localization.Get("TcpConnectionError")}: {ex.Message}");
            }
        }

        private static async Task ReadExactlyAsync(NetworkStream stream, byte[] buffer)
        {
            int offset = 0;

            while (offset < buffer.Length)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(offset));

                if (read == 0)
                    throw new EndOfStreamException("Client disconnected");

                offset += read;
            }
        }

        private MobileSamplePayload ParseMobileSample(string body)
        {
            if (
                double.TryParse(
                    body,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var plainLux
                )
            )
                return new MobileSamplePayload(plainLux, null);

            using var document = JsonDocument.Parse(body);

            if (document.RootElement.TryGetProperty("lux", out var luxElement))
                return new MobileSamplePayload(luxElement.GetDouble(), null);

            if (document.RootElement.TryGetProperty("brightness", out var brightnessElement))
                return new MobileSamplePayload(null, ClampPercent(brightnessElement.GetInt32()));

            throw new FormatException(_localization.Get("TcpInvalidBody"));
        }

        private static byte ClampPercent(int value)
        {
            if (value < 0)
                return 0;

            if (value > 100)
                return 100;

            return (byte)value;
        }

        private static IEnumerable<string> ReadNonEmptyLines(string value)
        {
            using var reader = new StringReader(value);

            while (reader.ReadLine() is { } line)
            {
                line = line.Trim();

                if (line.Length > 0)
                    yield return line;
            }
        }

        private static string NormalizeSingleLine(string value)
        {
            var lines = ReadNonEmptyLines(value).ToArray();
            return lines.Length == 0 ? "<empty>" : string.Join(", ", lines);
        }

        private void PublishLog(string level, string message)
        {
            LogReceived?.Invoke(
                this,
                new DeskBrightnessRuntimeLog(level, message, DateTimeOffset.Now)
            );
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ForwardedLuxSource));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            StopAsync().GetAwaiter().GetResult();
            _disposed = true;
        }
    }

    public sealed record MobileBrightnessSample(byte Percent, DateTimeOffset Timestamp);

    internal sealed record MobileSamplePayload(double? Lux, byte? Brightness);
}
