using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using DeskBrightness.Core.Brightness;

namespace DeskBrightness.Win
{
    public sealed class DdcCiBrightnessController : IBrightnessController, IDisposable
    {
        private readonly List<PhysicalMonitor> _monitors = new();
        private bool _disposed;

        public DdcCiBrightnessController(MonitorDetector detector)
        {
            foreach (var hMonitor in detector.GetDisplayMonitorHandles())
            {
                if (!Dxva2.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out var count))
                    continue;

                if (count == 0)
                    continue;

                var physicalMonitors = new PhysicalMonitor[count];

                if (!Dxva2.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, physicalMonitors))
                    continue;

                _monitors.AddRange(physicalMonitors);
            }
        }

        public Task SetBrightnessAsync(byte percent, CancellationToken cancellationToken = default)
        {
            percent = Clamp(percent);

            return Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var readableCount = 0;
                    var appliedCount = 0;
                    var lastError = 0;

                    foreach (var monitor in _monitors)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (
                            Dxva2.GetMonitorBrightness(
                                monitor.Handle,
                                out var minimum,
                                out _,
                                out var maximum
                            )
                        )
                        {
                            readableCount++;
                            var target = minimum + ((maximum - minimum) * percent / 100);

                            if (Dxva2.SetMonitorBrightness(monitor.Handle, target))
                                appliedCount++;
                            else
                                lastError = Marshal.GetLastWin32Error();
                        }
                        else
                        {
                            lastError = Marshal.GetLastWin32Error();
                        }
                    }

                    if (_monitors.Count == 0)
                        throw new InvalidOperationException("No physical monitors were detected.");

                    if (readableCount == 0)
                        throw new InvalidOperationException(
                            $"No detected monitor reported DDC/CI brightness support. Last Win32 error: {lastError}."
                        );

                    if (appliedCount == 0)
                        throw new InvalidOperationException(
                            $"DDC/CI SetMonitorBrightness failed for all readable monitors. Last Win32 error: {lastError}."
                        );
                },
                cancellationToken
            );
        }

        public bool IsSupported()
        {
            return _monitors.Count > 0;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            foreach (var monitor in _monitors)
            {
                Dxva2.DestroyPhysicalMonitor(monitor.Handle);
            }

            _monitors.Clear();
            _disposed = true;
        }

        private static byte Clamp(byte value)
        {
            if (value < 1)
                return 1;

            if (value > 100)
                return 100;

            return value;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct PhysicalMonitor
    {
        public IntPtr Handle;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
    }

    internal static class Dxva2
    {
        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor,
            out uint numberOfPhysicalMonitors
        );

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor,
            uint physicalMonitorArraySize,
            [Out] PhysicalMonitor[] physicalMonitorArray
        );

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetMonitorBrightness(
            IntPtr hMonitor,
            out uint minimumBrightness,
            out uint currentBrightness,
            out uint maximumBrightness
        );

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool SetMonitorBrightness(IntPtr hMonitor, uint newBrightness);
    }
}
