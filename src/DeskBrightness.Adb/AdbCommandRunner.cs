using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;

namespace DeskBrightness.Adb
{
    public sealed class AdbCommandRunner : IDisposable
    {
        private readonly string _adbPath;
        private readonly IAdbClient _client;
        private readonly Func<string, string> _t;
        private bool _startedByUs;
        private bool _skipDispose;

        public event EventHandler<AdbDebugLog>? DebugLogReceived;

        public void SkipDisposeOnExit() => _skipDispose = true;

        public AdbCommandRunner(string adbPath = "adb", Func<string, string>? localize = null)
        {
            if (string.IsNullOrWhiteSpace(adbPath))
                throw new ArgumentException(
                    localize is not null ? localize("AdbPathEmpty") : "ADB path cannot be empty.",
                    nameof(adbPath)
                );

            _adbPath = adbPath;
            _t = localize ?? (key => key);
            _client = AdbClient.Instance;
        }

        public async Task<bool> EnsureServerAsync(CancellationToken cancellationToken = default)
        {
            if (await IsServerRunningAsync(cancellationToken))
                return true;

            PublishDebug("ADB server not found, starting...");
            var result = await RunAsync("start-server", cancellationToken);
            var started = result.ExitCode == 0;

            if (started)
            {
                _startedByUs = true;
                PublishDebug("ADB server started successfully");
            }

            return started;
        }

        public async Task ShutdownAsync(bool force = false, CancellationToken cancellationToken = default)
        {
            if (!force && !_startedByUs)
                return;

            PublishDebug("Shutting down ADB server (started by us)");
            await RunAsync("kill-server", cancellationToken);
            _startedByUs = false;
        }

        private async Task<bool> IsServerRunningAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var tcp = new System.Net.Sockets.TcpClient();
                var task = tcp.ConnectAsync(System.Net.IPAddress.Loopback, 5037);
                if (await Task.WhenAny(task, Task.Delay(500, cancellationToken)) != task)
                    return false;
                if (task.IsFaulted) return false;
                return tcp.Connected;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_skipDispose)
                return;

            if (_startedByUs)
            {
                try
                {
                    using var proc = CreateProcess("kill-server");
                    proc.Start();
                    proc.WaitForExit(3000);
                }
                catch { }
                _startedByUs = false;
            }
        }

        public async Task<IReadOnlyList<AdbDevice>> GetDevicesAsync(
            CancellationToken cancellationToken = default
        )
        {
            PublishDebug("AdvancedSharpAdbClient GetDevicesAsync");

            var devices = await _client.GetDevicesAsync(cancellationToken);

            var result = new List<AdbDevice>();

            foreach (var device in devices)
            {
                PublishDebug($"adb device: {device.Serial} {device.State}");
                result.Add(await BuildDeviceAsync(device, cancellationToken));
            }

            return result.ToArray();
        }

        public async Task<AdbDevice?> GetFirstReadyDeviceAsync(
            CancellationToken cancellationToken = default
        )
        {
            var devices = await GetDevicesAsync(cancellationToken);
            return devices.FirstOrDefault(x => x.State == AdbDeviceState.Device);
        }

        public async Task<AdbCommandResult> ConnectTcpAsync(
            string host,
            int port,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException(_t("AdbHostEmpty"), nameof(host));

            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), _t("AdbPortRange"));

            return await RunAsync($"connect {host.Trim()}:{port}", cancellationToken);
        }

        public async Task<AdbCommandResult> PairTcpAsync(
            string host,
            int port,
            string pairingCode,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException(_t("AdbHostEmpty"), nameof(host));

            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), _t("AdbPortRange"));

            if (string.IsNullOrWhiteSpace(pairingCode))
                throw new ArgumentException(_t("AdbPairingCodeEmpty"), nameof(pairingCode));

            return await RunAsync(
                $"pair {host.Trim()}:{port} {pairingCode.Trim()}",
                cancellationToken
            );
        }

        public async Task CreateReverseAsync(
            string serial,
            int devicePort,
            int hostPort,
            CancellationToken cancellationToken = default
        )
        {
            var device = await FindDeviceAsync(serial, cancellationToken);
            var remote = new ForwardSpec($"tcp:{devicePort}");
            var local = new ForwardSpec($"tcp:{hostPort}");

            PublishDebug($"AdvancedSharpAdbClient reverse {remote} {local}");

            await _client.CreateReverseForwardAsync(
                device,
                remote,
                local,
                true,
                cancellationToken
            );
        }

        public async Task RemoveReverseAsync(
            string serial,
            int devicePort,
            CancellationToken cancellationToken = default
        )
        {
            var device = await FindDeviceAsync(serial, cancellationToken);
            var remote = new ForwardSpec($"tcp:{devicePort}");

            PublishDebug($"AdvancedSharpAdbClient reverse --remove {remote}");

            await _client.RemoveReverseForwardAsync(device, remote, cancellationToken);
        }

        public async Task<string> ExecuteShellAsync(
            string serial,
            string command,
            CancellationToken cancellationToken = default
        )
        {
            var device = await FindDeviceAsync(serial, cancellationToken);

            PublishDebug($"AdvancedSharpAdbClient shell {command}");

            var outputBuilder = new StringBuilder();

            await _client.ExecuteRemoteCommandAsync(
                command,
                device,
                line =>
                {
                    outputBuilder.AppendLine(line);
                    return true;
                },
                cancellationToken
            );

            var output = outputBuilder.ToString();

            PublishMultilineDebug("adb shell stdout", output);

            return output;
        }

        public async Task ExecuteShellStreamingAsync(
            string serial,
            string command,
            Action<string> onLine,
            CancellationToken cancellationToken = default
        )
        {
            var device = await FindDeviceAsync(serial, cancellationToken);

            PublishDebug($"AdvancedSharpAdbClient shell stream {command}");

            await _client.ExecuteRemoteCommandAsync(
                command,
                device,
                line =>
                {
                    onLine(line);
                    return !cancellationToken.IsCancellationRequested;
                },
                cancellationToken
            );
        }

        public async Task PushFileAsync(
            string serial,
            string localPath,
            string remotePath,
            CancellationToken cancellationToken = default
        )
        {
            var device = await FindDeviceAsync(serial, cancellationToken);

            PublishDebug($"AdvancedSharpAdbClient push {localPath} {remotePath}");

            await Task.Run(
                () =>
                {
                    using var service = new SyncService(device);
                    using var stream = File.OpenRead(localPath);

                    service.Push(
                        stream,
                        remotePath,
                        UnixFileStatus.DefaultFileMode,
                        DateTimeOffset.Now,
                        null
                    );
                },
                cancellationToken
            );
        }

        private async Task<DeviceData> FindDeviceAsync(
            string serial,
            CancellationToken cancellationToken
        )
        {
            var devices = await _client.GetDevicesAsync(cancellationToken);
            var device = devices.FirstOrDefault(x => x.Serial == serial);

            if (device is null)
                throw new InvalidOperationException($"{_t("AdbDeviceNotFound")}: {serial}");

            return device;
        }

        private async Task<AdbDevice> BuildDeviceAsync(
            DeviceData device,
            CancellationToken cancellationToken
        )
        {
            var state = MapState(device.State);

            if (state != AdbDeviceState.Device)
                return new AdbDevice(
                    device.Serial,
                    state,
                    isEmulator: IsLikelyEmulator(device.Serial, qemu: null, hardware: null)
                );

            try
            {
                var properties = await ReadDeviceIdentityAsync(device, cancellationToken);
                var modelName = BuildModelName(properties);
                var isEmulator = IsLikelyEmulator(
                    device.Serial,
                    properties.Qemu,
                    properties.Hardware
                );

                PublishDebug(
                    $"adb identity: {device.Serial} model='{modelName ?? "<empty>"}' emulator={isEmulator}"
                );

                return new AdbDevice(device.Serial, state, modelName, isEmulator);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                PublishDebug($"adb identity failed for {device.Serial}: {ex.Message}");

                return new AdbDevice(
                    device.Serial,
                    state,
                    isEmulator: IsLikelyEmulator(device.Serial, qemu: null, hardware: null)
                );
            }
        }

        private async Task<DeviceIdentityProperties> ReadDeviceIdentityAsync(
            DeviceData device,
            CancellationToken cancellationToken
        )
        {
            const string command =
                "getprop ro.product.marketname; "
                + "getprop ro.product.vendor.marketname; "
                + "getprop ro.product.system.marketname; "
                + "getprop ro.product.odm.marketname; "
                + "getprop ro.product.manufacturer; "
                + "getprop ro.product.model; "
                + "getprop ro.product.device; "
                + "getprop ro.kernel.qemu; "
                + "getprop ro.hardware";

            var lines = new List<string>();

            await _client.ExecuteRemoteCommandAsync(
                command,
                device,
                line =>
                {
                    lines.Add(line?.Trim() ?? string.Empty);
                    return true;
                },
                cancellationToken
            );

            return new DeviceIdentityProperties(
                GetLine(lines, 0),
                GetLine(lines, 1),
                GetLine(lines, 2),
                GetLine(lines, 3),
                GetLine(lines, 4),
                GetLine(lines, 5),
                GetLine(lines, 6),
                GetLine(lines, 7),
                GetLine(lines, 8)
            );
        }

        private static string? BuildModelName(DeviceIdentityProperties properties)
        {
            var marketName = FirstNonEmpty(
                properties.MarketName,
                properties.VendorMarketName,
                properties.SystemMarketName,
                properties.OdmMarketName
            );

            var manufacturer = NormalizeManufacturer(properties.Manufacturer);

            if (!string.IsNullOrWhiteSpace(marketName))
                return CombineManufacturerAndModel(manufacturer, marketName);

            return CombineManufacturerAndModel(
                manufacturer,
                FirstNonEmpty(properties.Model, properties.Device)
            );
        }

        private static string? CombineManufacturerAndModel(
            string? manufacturer,
            string? model
        )
        {
            if (string.IsNullOrWhiteSpace(model))
                return manufacturer;

            if (string.IsNullOrWhiteSpace(manufacturer))
                return model;

            if (model.StartsWith(manufacturer, StringComparison.OrdinalIgnoreCase))
                return model;

            return $"{manufacturer} {model}";
        }

        private static string? NormalizeManufacturer(string? manufacturer)
        {
            if (string.IsNullOrWhiteSpace(manufacturer))
                return null;

            var trimmed = manufacturer.Trim();
            var english = CultureInfo.GetCultureInfo("en-US");

            return english.TextInfo.ToTitleCase(trimmed.ToLower(english));
        }

        private static bool IsLikelyEmulator(
            string serial,
            string? qemu,
            string? hardware
        )
        {
            if (serial.StartsWith("emulator-", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(qemu?.Trim(), "1", StringComparison.Ordinal))
                return true;

            var hardwareValue = hardware?.Trim();

            return string.Equals(hardwareValue, "ranchu", StringComparison.OrdinalIgnoreCase)
                || string.Equals(hardwareValue, "goldfish", StringComparison.OrdinalIgnoreCase);
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return null;
        }

        private static string? GetLine(IReadOnlyList<string> lines, int index)
        {
            if (index < 0 || index >= lines.Count)
                return null;

            return string.IsNullOrWhiteSpace(lines[index]) ? null : lines[index].Trim();
        }

        public async Task<AdbCommandResult> RunAsync(
            string arguments,
            CancellationToken cancellationToken = default
        )
        {
            using var process = CreateProcess(arguments);

            PublishDebug($"adb {arguments}");

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                    outputBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                    errorBuilder.AppendLine(e.Data);
            };

            try
            {
                process.Start();
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException(
                    _t("AdbNotFound"),
                    ex
                );
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await Task.Run(() => process.WaitForExit(), cancellationToken);

            var standardOutput = outputBuilder.ToString();
            var standardError = errorBuilder.ToString();

            PublishDebug($"adb exited with code {process.ExitCode}");
            PublishMultilineDebug("adb stdout", standardOutput);
            PublishMultilineDebug("adb stderr", standardError);

            return new AdbCommandResult(
                process.ExitCode,
                standardOutput,
                standardError
            );
        }

        public Process StartStreaming(string arguments)
        {
            var process = CreateProcess(arguments);

            PublishDebug($"adb {arguments}");
            try
            {
                process.Start();
            }
            catch (Win32Exception ex)
            {
                process.Dispose();

                throw new InvalidOperationException(
                    _t("AdbNotFound"),
                    ex
                );
            }

            return process;
        }

        private void PublishDebug(string message)
        {
            DebugLogReceived?.Invoke(this, new AdbDebugLog("DEBUG", message, DateTimeOffset.Now));
        }

        private void PublishMultilineDebug(string label, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                PublishDebug($"{label}: <empty>");
                return;
            }

            using var reader = new System.IO.StringReader(text);

            while (reader.ReadLine() is { } line)
                PublishDebug($"{label}: {line}");
        }

        private Process CreateProcess(string arguments)
        {
            return new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _adbPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = false,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true,
            };
        }

        private static AdbDeviceState MapState(DeviceState state)
        {
            return state switch
            {
                DeviceState.Online => AdbDeviceState.Device,
                DeviceState.Offline => AdbDeviceState.Offline,
                DeviceState.Unauthorized => AdbDeviceState.Unauthorized,
                _ => AdbDeviceState.Unknown,
            };
        }
    }

    public sealed class AdbDebugLog
    {
        public AdbDebugLog(string level, string message, DateTimeOffset timestamp)
        {
            Level = level;
            Message = message;
            Timestamp = timestamp;
        }

        public string Level { get; }

        public string Message { get; }

        public DateTimeOffset Timestamp { get; }
    }

    internal sealed class DeviceIdentityProperties
    {
        public DeviceIdentityProperties(
            string? marketName,
            string? vendorMarketName,
            string? systemMarketName,
            string? odmMarketName,
            string? manufacturer,
            string? model,
            string? device,
            string? qemu,
            string? hardware
        )
        {
            MarketName = marketName;
            VendorMarketName = vendorMarketName;
            SystemMarketName = systemMarketName;
            OdmMarketName = odmMarketName;
            Manufacturer = manufacturer;
            Model = model;
            Device = device;
            Qemu = qemu;
            Hardware = hardware;
        }

        public string? MarketName { get; }
        public string? VendorMarketName { get; }
        public string? SystemMarketName { get; }
        public string? OdmMarketName { get; }
        public string? Manufacturer { get; }
        public string? Model { get; }
        public string? Device { get; }
        public string? Qemu { get; }
        public string? Hardware { get; }
    }
}
